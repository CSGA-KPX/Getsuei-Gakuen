// HIP 专有格式读写库（Arc System Works 纹理容器）
// 移植自 external-src/ArcSysLib-master（HIPFileInfo.cs / SEGSCompression.cs），逻辑逐行对应。
// 纯字节层：不依赖 ImageSharp；PNG 互转见 hip-tool.fsx。
//
// 像素格式（头 param[0]）：0x1=8bpp 索引 / 0x4=16bpp 灰度 / 0x10=32bpp(BGRA 字节序) / 0x40=16bpp RGB565
// 像素编码（头 param[1]）：0x8=Raw / 0x1=RawRepeat / 0x2=Key / 0x10=RawSignRepeat / 0x20=RawCanvas
// 验证状态见 docs/formats/hip.md（全量解码 + roundtrip ✅）

module HipLib

open System
open System.IO
open System.IO.Compression

type HipFormat =
    | Indexed8 | Gray16 | Argb32 | Rgb565 | FormatUnknown of byte
    member x.Bpp =
        match x with
        | Indexed8 -> 1 | Gray16 -> 2 | Argb32 -> 4 | Rgb565 -> 2 | FormatUnknown _ -> 0
    member x.FormatByte =
        match x with
        | Indexed8 -> 0x1uy | Gray16 -> 0x4uy | Argb32 -> 0x10uy | Rgb565 -> 0x40uy
        | FormatUnknown b -> b

type HipEncoding =
    | EncRaw | EncRawRepeat | EncKey | EncRawSignRepeat | EncRawCanvas | EncUnknown of byte
    member x.EncodingByte =
        match x with
        | EncRaw -> 0x8uy | EncRawRepeat -> 0x1uy | EncKey -> 0x2uy
        | EncRawSignRepeat -> 0x10uy | EncRawCanvas -> 0x20uy | EncUnknown b -> b

/// 解码后的 HIP 图像。
/// Pixels 为文件字节序（Argb32 = BGRA 排列、Gray16/Rgb565 = LE u16），
/// RawCanvas 编码时为画布尺寸，其余为图像尺寸。
type HipImage = {
    Format: HipFormat
    Encoding: HipEncoding
    CanvasWidth: int; CanvasHeight: int
    ImageWidth: int; ImageHeight: int
    OffsetX: int; OffsetY: int
    Layered: bool
    /// 索引格式的调色板，每项 4 字节 BGRA（与文件一致）
    Palette: byte[][] option
    Pixels: byte[]
}

exception HipException of string

/// 判断是否为 HIP 专有格式（前 4 字节 "HIP\0"）
let isHip (bytes: byte[]) =
    bytes.Length >= 4 && bytes[0] = 0x48uy && bytes[1] = 0x49uy && bytes[2] = 0x50uy && bytes[3] = 0x00uy

// ---------- SEGS 解压（SEGSCompression.DecompressStream 移植） ----------

/// 从 data[offset..]（"segs" 魔数处）解压。
let segsDecompress (data: byte[]) (offset: int) : byte[] =
    use ms = new MemoryStream()
    // 头（大端读法，与源实现一致）：4B magic + i16 Flags + i16 ChunkCount + u32 FullSize + u32 CompressedSize
    let chunkCount = (int data[offset+6] <<< 8) ||| int data[offset+7]
    let tablePos = offset + 12
    // 块数据基址 = beginPos + ChunkCount * 8
    let chunkBase = offset + chunkCount * 8
    let mutable workAround = 0
    for i in 0 .. chunkCount - 1 do
        let p = tablePos + i * 8
        let zsize = (int data[p] <<< 8) ||| int data[p+1]
        let mutable size = (int data[p+2] <<< 8) ||| int data[p+3]
        let mutable coff = ((int data[p+4] <<< 24) ||| (int data[p+5] <<< 16) ||| (int data[p+6] <<< 8) ||| int data[p+7]) - 1
        if i = 0 && coff = 0 then workAround <- 1
        if workAround <> 0 then coff <- coff + chunkBase
        if size = 0 then size <- 0x10000
        if size = zsize then
            ms.Write(data, coff, size)
        else
            use input = new MemoryStream(data, coff, zsize, false)
            use ds = new DeflateStream(input, CompressionMode.Decompress)
            ds.CopyTo(ms)
    ms.ToArray()

// ---------- 解码 ----------

type private Reader(bytes: byte[], pos0: int, be0: bool) =
    let mutable pos = pos0
    let mutable bigEndian = be0
    member _.Length = bytes.Length
    member _.Pos with get() = pos and set(v) = pos <- v
    member _.BigEndian with get() = bigEndian and set(v) = bigEndian <- v
    member _.ReadByte() = let b = bytes[pos] in pos <- pos + 1; b
    member _.Skip(n: int) = pos <- pos + n
    member _.ReadBytes(n: int) =
        if pos + n > bytes.Length then raise (EndOfStreamException())
        let r = Array.sub bytes pos n in pos <- pos + n; r
    member r.ReadI32() =
        let b = r.ReadBytes(4)
        if bigEndian then
            (int b[0] <<< 24) ||| (int b[1] <<< 16) ||| (int b[2] <<< 8) ||| int b[3]
        else
            (int b[3] <<< 24) ||| (int b[2] <<< 16) ||| (int b[1] <<< 8) ||| int b[0]

let private formatOf (b: byte) =
    match b with
    | 0x1uy -> Indexed8 | 0x4uy -> Gray16 | 0x10uy -> Argb32 | 0x40uy -> Rgb565
    | x -> FormatUnknown x

let private encodingOf (b: byte) =
    match b with
    | 0x8uy -> EncRaw | 0x1uy -> EncRawRepeat | 0x2uy -> EncKey
    | 0x10uy -> EncRawSignRepeat | 0x20uy -> EncRawCanvas | x -> EncUnknown x

/// 解码 HIP 专有格式。失败抛 HipException（42B 空壳等占位文件会走这里）。
let decode (bytes: byte[]) : HipImage =
    if not (isHip bytes) then raise (HipException "not a HIP file")
    if bytes.Length < 32 then raise (HipException "file too small (<32)")

    let r = Reader(bytes, 0, false)
    r.Pos <- 4
    // CheckEndianness：offset 4 首字节为 0 → 大端
    if bytes[4] = 0uy then r.BigEndian <- true
    r.ReadI32() |> ignore // 0x125
    let fileLength = min (r.ReadI32()) bytes.Length
    let colorRange = r.ReadI32()
    let canvasW = r.ReadI32()
    let canvasH = r.ReadI32()
    let fmt = formatOf (r.ReadByte())
    let enc = encodingOf (r.ReadByte())
    let layeredImages = r.ReadByte()
    let extraParams = r.ReadByte()
    let renderableLayers = (extraParams &&& 0x20uy) <> 0uy
    let layerHeaderSize = r.ReadI32()
    let headerSize = 0x20 + layerHeaderSize

    let imageW, imageH, offsetX, offsetY =
        if renderableLayers && layeredImages <> 0uy then
            r.ReadI32(), r.ReadI32(), r.ReadI32(), r.ReadI32()
        else canvasW, canvasH, 0, 0

    if headerSize > bytes.Length then raise (HipException "header size exceeds file length")

    let bpp = fmt.Bpp
    if bpp = 0 then raise (HipException(sprintf "unknown pixel format 0x%02X" fmt.FormatByte))

    // 调色板（在 HeaderSize 处；GetImage 的 palette 分支移植）
    let palette : byte[][] option =
        if fmt = Indexed8 then
            if colorRange = 0 then
                // MissingPalette：色 0 透明、其余黑（源实现 alpha=0/255）
                Some (Array.init 256 (fun i -> if i = 0 then [| 0uy; 0uy; 0uy; 0uy |] else [| 0uy; 0uy; 0uy; 255uy |]))
            elif headerSize + colorRange * 4 > bytes.Length then
                raise (HipException "palette out of range")
            else
                Some (Array.init colorRange (fun i -> Array.sub bytes (headerSize + i * 4) 4)) // BGRA 原样
        else None

    // 像素数据定位 + SEGS 检测（HIPFileInfo.GetImage 的 savpos 逻辑移植）
    let mutable dataReader = r
    let mutable pixelPos = headerSize + (if fmt = Indexed8 then colorRange * 4 else 0)
    let trySegsAt (p: int) =
        p + 4 <= bytes.Length
        && bytes[p] = 0x73uy && bytes[p+1] = 0x65uy && bytes[p+2] = 0x67uy && bytes[p+3] = 0x73uy // "segs"
    if trySegsAt pixelPos then
        let dec = segsDecompress bytes pixelPos
        dataReader <- Reader(dec, 0, true) // 源实现：SEGS 后 Endianness = BigEndian
        pixelPos <- 0
    else
        let after12 = pixelPos + 12
        if after12 <> bytes.Length && trySegsAt after12 then
            let dec = segsDecompress bytes after12
            dataReader <- Reader(dec, 0, true)
            pixelPos <- 0
        // 否则像素从 pixelPos 开始（after12 = EOF 的尾零情形同样回到 pixelPos）
    dataReader.Pos <- pixelPos

    // 像素解码（GetPixelsFromRawColors 的 renderCanvas 条件：extraParams==0 || RawCanvas）
    let renderCanvas = extraParams = 0uy || (enc = EncRawCanvas)
    let decW, decH =
        match enc with
        | EncKey | EncRawSignRepeat -> imageW, imageH // 这两个解码器只用图像尺寸（C# 原文）
        | _ -> (if renderCanvas then canvasW else imageW), (if renderCanvas then canvasH else imageH)
    let width = decW
    let height = decH
    let total = width * height * bpp
    let pixels = Array.zeroCreate total
    let dataBound = fileLength - headerSize // Key / RawSignRepeat 的读取上界

    match enc with
    | EncUnknown 0x04uy -> raise (HipException "encoding 0x4 unsupported")
    | EncKey ->
        // GetPixelsWithKey：该函数内游标强制小端
        dataReader.BigEndian <- false
        let position = dataReader.Pos
        let key = dataReader.ReadByte()
        dataReader.Skip(1) // 源实现注释掉了 colorSize 读取，固定用 Bpp
        let colorSize = bpp
        let mutable pos = 0
        let mutable stop = false
        while not stop
              && dataReader.Pos - position < dataBound
              && dataReader.Pos <> dataReader.Length
              && pos < total do
            try
                let bytesRead = dataReader.ReadBytes(colorSize)
                if bytesRead[0] = key then
                    if bytesRead[0] <> bytesRead[1] then
                        let mutable back = if bytesRead[1] = 0xFFuy then key else bytesRead[1]
                        back <- back + 1uy
                        for _ in 1 .. int bytesRead[2] do
                            if pos + colorSize <= total then
                                Buffer.BlockCopy(pixels, pos - int back * colorSize, pixels, pos, colorSize)
                                pos <- pos + colorSize
                        dataReader.Skip(-1)
                    else
                        dataReader.Skip(-3)
                        let bs = dataReader.ReadBytes(colorSize)
                        if dataReader.BigEndian then Array.Reverse bs
                        Buffer.BlockCopy(bs, 0, pixels, pos, colorSize)
                        pos <- pos + colorSize
                else
                    if dataReader.BigEndian then Array.Reverse bytesRead
                    Buffer.BlockCopy(bytesRead, 0, pixels, pos, colorSize)
                    pos <- pos + colorSize
            with :? EndOfStreamException | :? ArgumentException ->
                stop <- true
    | EncRawSignRepeat ->
        // GetPixelsRawSignRepeat
        let position = dataReader.Pos
        let mutable pos = 0
        let mutable stop = false
        while not stop
              && dataReader.Pos - position < dataBound
              && dataReader.Pos <> dataReader.Length
              && pos < total do
            try
                let mutable v = dataReader.ReadI32()
                if v < 0 then
                    v <- v &&& 0x7FFFFFFF
                    for _ in 1 .. v do
                        if pos + bpp <= total then
                            let bs = dataReader.ReadBytes(bpp)
                            Buffer.BlockCopy(bs, 0, pixels, pos, bpp)
                            pos <- pos + bpp
                else
                    let bs = dataReader.ReadBytes(bpp)
                    for _ in 1 .. v do
                        if pos + bpp <= total then
                            Buffer.BlockCopy(bs, 0, pixels, pos, bpp)
                            pos <- pos + bpp
            with :? EndOfStreamException | :? ArgumentException ->
                stop <- true
    | _ ->
        // GetPixelsFromRawColors：Raw / RawRepeat / RawCanvas
        let repeat = (enc = EncRawRepeat)
        let mutable index = 0
        let position = dataReader.Pos
        while dataReader.Pos - position < total
              && dataReader.Pos <> dataReader.Length
              && index < total do
            let bs = dataReader.ReadBytes(bpp)
            let repeatCount = if repeat then int (dataReader.ReadByte()) else 1
            for _ in 1 .. repeatCount do
                if index + bpp <= total then
                    Buffer.BlockCopy(bs, 0, pixels, index, bpp)
                    index <- index + bpp

    // 输出尺寸对齐 GetImage：仅 RawCanvas 编码输出画布，其余输出图像尺寸；
    // pixels 与输出尺寸不一致时（layered 且 extraParams==0）按 C# Clone 行为从左上角裁剪
    let outW = if enc = EncRawCanvas then canvasW else imageW
    let outH = if enc = EncRawCanvas then canvasH else imageH
    let pixelsOut =
        if outW = width && outH = height then pixels
        else
            let crop = Array.zeroCreate (outW * outH * bpp)
            for y in 0 .. outH - 1 do
                Buffer.BlockCopy(pixels, y * width * bpp, crop, y * outW * bpp, outW * bpp)
            crop

    {
        Format = fmt
        Encoding = enc
        CanvasWidth = canvasW; CanvasHeight = canvasH
        ImageWidth = outW; ImageHeight = outH
        OffsetX = offsetX; OffsetY = offsetY
        Layered = renderableLayers && layeredImages <> 0uy
        Palette = palette
        Pixels = pixelsOut
    }

// ---------- 编码（HIPFileInfo.CreateHIP 移植；只支持 Raw / RawRepeat，与 CLI 能力对齐） ----------

/// 编码为 HIP 专有格式。Pixels 长度须 = 图幅 × Bpp（RawCanvas 时画布尺寸）。
/// Palette 仅在 Indexed8 时需要。layered = true 时写入图层头（图幅/画布/偏移）。
let encode (img: HipImage) : byte[] =
    let bpp = img.Format.Bpp
    if bpp = 0 then raise (HipException "unknown pixel format")
    let layered = img.Layered
    let palette = img.Palette
    if img.Format = Indexed8 && (palette.IsNone || palette.Value.Length = 0) then
        raise (HipException "indexed format requires palette")
    let colorRange = if img.Format = Indexed8 then palette.Value.Length else 0
    let pixels = img.Pixels
    if pixels.Length <> img.ImageWidth * img.ImageHeight * bpp && not (img.Encoding = EncRawCanvas) then
        raise (HipException(sprintf "pixel buffer size %d != %d*%d*%d"
                    pixels.Length img.ImageWidth img.ImageHeight bpp))

    use ms = new MemoryStream()
    use w = new BinaryWriter(ms)
    w.Write([| 0x48uy; 0x49uy; 0x50uy; 0x00uy |]) // "HIP\0"
    w.Write(0x125)
    w.Write(0) // FileLength，末尾回填
    w.Write(colorRange)
    w.Write(img.CanvasWidth)
    w.Write(img.CanvasHeight)
    w.Write(img.Format.FormatByte)
    w.Write(img.Encoding.EncodingByte)
    w.Write(if layered then 1uy else 0uy)
    w.Write(if layered then 0x20uy else 0x00uy)
    if layered then
        w.Write(0x20) // layerHeaderSize = 0x20（含 16B 尾零）
        w.Write(img.ImageWidth)
        w.Write(img.ImageHeight)
        w.Write(img.OffsetX)
        w.Write(img.OffsetY)
        w.Write(Array.zeroCreate<byte> 16)
    else
        w.Write(0)

    if img.Format = Indexed8 then
        for c in palette.Value do w.Write(c) // BGRA，与 Color.ToArgb() 的 LE 字节一致

    match img.Encoding with
    | EncRaw ->
        w.Write(pixels)
    | EncRawRepeat ->
        // 源实现的 run 打包（含 255 上限 quirk）逐行移植
        let mutable repeatCount = 1
        let mutable i = 0
        while i < pixels.Length do
            let colorBytes = Array.sub pixels i bpp
            let mutable same = false
            if i < pixels.Length - bpp then
                let mutable j = 0
                while j < bpp && pixels[i + bpp + j] = colorBytes[j] do
                    j <- j + 1
                same <- (j = bpp)
                if same then
                    repeatCount <- repeatCount + 1
                    if repeatCount = 256 then
                        repeatCount <- repeatCount - 1
                        same <- false
            if not same then
                w.Write(colorBytes)
                w.Write(byte repeatCount)
                repeatCount <- 1
            i <- i + bpp
    | e ->
        raise (HipException(sprintf "encoding %A not supported by encoder" e))

    // 回填 FileLength（offset 8）
    let arr = ms.ToArray()
    let len = arr.Length
    arr[8] <- byte len
    arr[9] <- byte (len >>> 8)
    arr[10] <- byte (len >>> 16)
    arr[11] <- byte (len >>> 24)
    arr
