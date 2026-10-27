// TGA type9 (RLE colormapped, 8bit 索引) 编解码库
// 由 decode-tga.fsx / encode-tga.fsx 以 #load 引用。
// 格式（scripts/decode-tga.fsx 已验证可解本游戏 image 目录全部文字图）：
//   头 18B + 可选 ID 段 + 调色板 cmapLen×4B（BGRA）+ RLE 索引数据
//   RLE 包：header byte >= 0x80 → 重复包（count = hb-0x7F，后跟 1 字节索引）
//            header byte < 0x80 → 原始包（count = hb+1，后跟 count 字节索引）
// 注意：消费脚本需先 #r "nuget: SixLabors.ImageSharp" 再 #load 本文件
module TgaLib

open System
open System.IO
open System.Text
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

type TgaImage =
    { Id: byte[]
      CmapStart: int
      Palette: Rgba32[] // 按文件顺序（从 cmapStart 起连续 cmapLen 项）
      Width: int
      Height: int
      Desc: int
      /// 原点（头 u16×2）：子图在原始画布中的位置（原版工具裁剪图时保留），新图为 0
      XOrigin: int
      YOrigin: int
      /// 索引，按文件存储顺序（desc bit5=0 时首行是图像底行）
      Indices: byte[]
      /// RLE 数据在 w*h 像素之后的残留字节（部分文件多一个 00 00 终止包，语义未知，
      /// 疑似另一版工具的终止符；roundtrip 原样保留，新图为空）
      RleTrailing: byte[]
      /// 页脚中部 4 字节（扩展区偏移=0 之后、签名之前）。
      /// 实测 = 画布原始尺寸（可能被裁剪）的宽高向上取 2 的幂，u16 LE 各一。
      /// 游戏几乎不读此处，但 roundtrip 需原样保留。
      FooterDim: byte[] }

let decode (data: byte[]) : TgaImage =
    let idLen = int data[0]
    let cmapType = int data[1]
    let imgType = int data[2]
    let cmapStart = BitConverter.ToUInt16(data, 3) |> int
    let cmapLen = BitConverter.ToUInt16(data, 5) |> int
    let cmapBits = int data[7]
    let xOrigin = BitConverter.ToUInt16(data, 8) |> int
    let yOrigin = BitConverter.ToUInt16(data, 10) |> int
    let w = BitConverter.ToUInt16(data, 12) |> int
    let h = BitConverter.ToUInt16(data, 14) |> int
    let depth = int data[16]
    let desc = int data[17]

    if imgType <> 9 || cmapType <> 1 || depth <> 8 || cmapBits <> 32 then
        failwithf "仅支持 type9/32bit 调色板/8bit 索引（实际 type=%d cmapBits=%d depth=%d）" imgType cmapBits depth

    let palette =
        Array.init cmapLen (fun i ->
            let off = 18 + idLen + (cmapStart + i) * 4
            Rgba32(data[off + 2], data[off + 1], data[off], data[off + 3]))

    let id = if idLen > 0 then data[18 .. 18 + idLen - 1] else Array.empty

    let indices = Array.zeroCreate<byte> (w * h)
    let mutable pos = 18 + idLen + cmapLen * 4
    let mutable outPos = 0
    while outPos < indices.Length && pos < data.Length do
        let hb = int data[pos]
        pos <- pos + 1
        if hb >= 0x80 then
            let count = hb - 0x7F
            let idx = data[pos]
            pos <- pos + 1
            for _ in 1..count do
                if outPos < indices.Length then
                    indices[outPos] <- idx
                    outPos <- outPos + 1
        else
            let count = hb + 1
            for _ in 1..count do
                if outPos < indices.Length then
                    indices[outPos] <- data[pos]
                    pos <- pos + 1
                    outPos <- outPos + 1

    if outPos < indices.Length then
        failwithf "RLE 数据不足: %d/%d 像素" outPos indices.Length

    // 页脚（26B）：4B 扩展区偏移 + 4B 尺寸字段 + "TRUEVISION-XFILE." + NUL
    let hasFooter =
        data.Length >= 26
        && Encoding.ASCII.GetString(data, data.Length - 18, 17) = "TRUEVISION-XFILE."

    let rleEnd = if hasFooter then data.Length - 26 else data.Length
    if pos > rleEnd then
        failwithf "RLE 数据溢出页脚: pos=0x%X rleEnd=0x%X" pos rleEnd
    let rleTrailing = data[pos .. rleEnd - 1]

    let footerDim =
        if hasFooter then
            data[data.Length - 22 .. data.Length - 19]
        else
            [| 0uy; 0uy; 0uy; 0uy |]

    { Id = id; CmapStart = cmapStart; Palette = palette
      Width = w; Height = h; Desc = desc
      XOrigin = xOrigin; YOrigin = yOrigin
      Indices = indices
      RleTrailing = rleTrailing
      FooterDim = footerDim }

/// RLE 编码（复刻原工具算法，2026-10-18 由 00_0-01_01_001.hip / ed_c2_image02.hip 包序列逆向）：
/// - 包起点处下一像素与当前相同（run >= 2）→ 发重复包（上限 128）
/// - 否则累积原始包，条件：下一像素 != 包内最后一个像素（即在 run 的第二个像素前截断，
///   run 首像素并入原始包）或满 128
/// - endRunAsRaw：另一版工具的边界行为——末尾 run 恰好延伸到数据末尾时不发重复包，
///   拆成单像素原始包（ed_c2_image02 末尾 250,250 → W1 W1 实测）。新图用 false。
let encodeRleVariant (endRunAsRaw: bool) (indices: byte[]) : byte[] =
    use ms = new MemoryStream()
    let n = indices.Length
    let runLenAt i =
        let mutable r = 1
        while i + r < n && indices[i + r] = indices[i] && r < 128 do
            r <- r + 1
        r
    let mutable i = 0
    while i < n do
        let run = runLenAt i
        if run >= 2 && (not endRunAsRaw || i + run < n) then
            ms.WriteByte(byte (0x80 + run - 1))
            ms.WriteByte(indices[i])
            i <- i + run
        else
            let start = i
            let mutable count = 1
            i <- i + 1
            while i < n && count < 128 && indices[i] <> indices[i - 1] do
                count <- count + 1
                i <- i + 1
            ms.WriteByte(byte (count - 1))
            ms.Write(indices, start, count)
    ms.ToArray()

/// 默认变体（00_* 文字图工具链行为）
let encodeRle = encodeRleVariant false

/// 序列化为 TGA type9 字节（调色板按 BGRA 写出，末尾带 TRUEVISION-XFILE 签名块）
/// endRunAsRaw：见 encodeRleVariant，roundtrip 另一版工具产物时试 true
let encodeWith (endRunAsRaw: bool) (img: TgaImage) : byte[] =
    let cmapLen = img.Palette.Length
    let idLen = img.Id.Length
    let rle = encodeRleVariant endRunAsRaw img.Indices

    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms)

    bw.Write(byte idLen)
    bw.Write(byte 1) // cmapType
    bw.Write(byte 9) // imgType
    bw.Write(uint16 img.CmapStart)
    bw.Write(uint16 cmapLen)
    bw.Write(byte 32) // cmapBits
    bw.Write(uint16 img.XOrigin)
    bw.Write(uint16 img.YOrigin)
    bw.Write(uint16 img.Width)
    bw.Write(uint16 img.Height)
    bw.Write(byte 8) // depth
    bw.Write(byte img.Desc)
    if idLen > 0 then bw.Write(img.Id)
    for c in img.Palette do
        bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A)
    bw.Write(rle)
    bw.Write(img.RleTrailing)
    // TGA 2.0 页脚（26B）：4B 扩展区偏移=0 + 4B 尺寸字段（decode 时原样保留）+ 签名 18B
    bw.Write([| 0uy; 0uy; 0uy; 0uy |])
    bw.Write(img.FooterDim)
    bw.Write("TRUEVISION-XFILE."B)
    bw.Write(0uy)
    ms.ToArray()

/// 默认变体（新图用）
let encode = encodeWith false
