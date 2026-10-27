// PAC 打包文件格式读取库（docs/formats/pac.md）
// 被 pac-unpack.fsx / pac-search.fsx 以 #load 引用。
module PacLib

open System
open System.IO
open System.Text

// .NET 默认不含 CP932（Shift-JIS）等代码页编码，pac 条目名会用到 → 必须先注册
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

[<Flags>]
type PacStyle =
    | Normal = 0x0
    | AutoLenFilename = 0x1
    | IdOnly = 0x2
    | PathCut = 0x10
    | LongName = 0x20000000
    | HashSort = 0x40000000
    | Version2 = 0x80000000

type PacHeader =
    { FourCC: string
      DataStart: int32 // 数据区起始偏移 = HeaderSize + 元数据大小
      TotalSize: int32 // pac 文件总大小
      FileCount: int32
      PacStyle: PacStyle
      StringSize: int32 // 每个文件名字段的字节长度（由 Header 指定，不能硬编码）
      Padding: uint64 }

type PacEntry =
    { Filename: string
      /// 文件名在 pac 里的**原始字节**（定长 StringSize，含尾部 0x00）。
      /// ⚠️ 必须保留：pac 内条目名可以是 **Shift-JIS**（例如 options_screen.pac 里的
      /// `window_frame_Ｌ_light_01.hip`，全角Ｌ = 0x82 0x6B），用 UTF-8 解码再写回会把它变成
      /// U+FFFD 三个字节，名字就变了 → 引擎按名字/哈希查不到 → 闪退（2026-10-20 实测）。
      FileNameBytes: byte[]
      FileId: int32
      OffsetStart: int32 // 相对 DataStart 的偏移
      Length: int32
      FileNameHash: byte[] } // 4 字节，见 hashFileName

type PacFile =
    { Header: PacHeader
      Entries: PacEntry[]
      /// 整个 pac 的原始字节（供 extract 使用）
      Bytes: byte[] }

/// 文件名哈希算法（重打包重建 FileNameHash 时使用）
let hashFileName (fileName: string) : byte[] =
    let hash =
        Encoding.UTF8.GetBytes(fileName)
        |> Array.fold (fun (h: uint32) c -> uint32 c + 137u * h) 0u

    BitConverter.GetBytes(hash)

/// 文件名解码：先按严格 UTF-8（非法字节抛异常），失败则按 Shift-JIS (CP932)。
/// pac 内条目名混用两种编码（绝大多数 ASCII，个别 SJIS 全角字符），必须两条都试。
let private encoders =
    [| UTF8Encoding(false, true) :> Encoding
       try Encoding.GetEncoding(932) with _ -> Encoding.UTF8 |]
    |> Array.map (fun e -> e)

let private decodeName (raw: byte[]) =
    let trimmed =
        let i = Array.IndexOf(raw, 0uy)
        if i >= 0 then raw.[.. i - 1] else raw
    let rec tryOne i =
        if i >= encoders.Length then Encoding.UTF8.GetString trimmed   // 两条都失败：退回宽松 UTF-8
        else
            try encoders.[i].GetString trimmed
            with _ -> tryOne (i + 1)
    tryOne 0

let readBytes (bytes: byte[]) : PacFile =
    use ms = new MemoryStream(bytes, writable = false)
    use br = new BinaryReader(ms)

    let header =
        // ⚠️ 文件名表必须按**字节**读再解码，不能用 BinaryReader.ReadChars(n)：
        // ReadChars 的 n 是「字符数」不是字节数，一条含多字节的文件名会多消耗字节，
        // 导致后续条目起点漂移、最后一条抛 "output char buffer is too small"（2026-10-20 修）。
        { FourCC = br.ReadBytes(4) |> Encoding.ASCII.GetString
          DataStart = br.ReadInt32()
          TotalSize = br.ReadInt32()
          FileCount = br.ReadInt32()
          PacStyle = enum<PacStyle> (br.ReadInt32())
          StringSize = br.ReadInt32()
          Padding = br.ReadUInt64() }

    let entries =
        [| for _ in 1..header.FileCount ->
               let nameRaw = br.ReadBytes header.StringSize
               { Filename = decodeName nameRaw
                 FileNameBytes = nameRaw
                 FileId = br.ReadInt32()
                 OffsetStart = br.ReadInt32()
                 Length = br.ReadInt32()
                 FileNameHash = br.ReadBytes(4) } |]

    { Header = header; Entries = entries; Bytes = bytes }

let read (path: string) : PacFile = readBytes (File.ReadAllBytes(path))

/// 提取某个条目的文件数据（Length = 0 时为空文件）
let extract (pac: PacFile) (entry: PacEntry) : byte[] =
    if entry.Length = 0 then
        Array.empty
    else
        let dataStart = pac.Header.DataStart + entry.OffsetStart
        pac.Bytes[dataStart .. dataStart + entry.Length - 1]

/// hip 数据的子格式判别：前 3 字节 "HIP" 为专有格式（hip-tool.fsx 转 PNG），否则是 TGA
let hipExtension (data: byte[]) : string =
    if data.Length >= 3 && Encoding.UTF8.GetString(data, 0, 3) = "HIP" then ".hip" else ".tga"

// ─── 重打包（docs/formats/pac.md §3，2026-10-18 全量实测验证）───

let private align16 (n: int) = (n + 15) / 16 * 16

/// 重打包核心：按 orig 的条目顺序/元数据（FileId/StringSize/PacStyle/FourCC）+ 新文件内容序列化 pac 字节。
/// 约定：数据区每条目起始偏移对齐 16 字节（补 0），TotalSize 对齐 16；
/// FileNameHash = hashFileName(文件名小写)。
let build (orig: PacFile) (contents: byte[][]) : byte[] =
    let h = orig.Header
    let dataStart = 0x20 + h.FileCount * (16 + h.StringSize)

    // 预计算每条目对齐后的 OffsetStart
    let offsets = ResizeArray<int>()
    let mutable pos = 0
    for c in contents do
        offsets.Add(pos)
        pos <- align16 (pos + c.Length)

    let totalSize = align16 (dataStart + pos)

    use ms = new MemoryStream(totalSize)
    use bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen = true)

    bw.Write(Encoding.ASCII.GetBytes(h.FourCC.PadRight(4, '\000').Substring(0, 4)))
    bw.Write(dataStart)
    bw.Write(totalSize)
    bw.Write(h.FileCount)
    bw.Write(int h.PacStyle)
    bw.Write(h.StringSize)
    bw.Write(0UL)

    for i in 0 .. orig.Entries.Length - 1 do
        let e = orig.Entries[i]
        // 文件名：**原样回写原始字节**（不重新编码）。pac 内条目名可能是 Shift-JIS
        // （`window_frame_Ｌ_light_*` 的全角Ｌ = 0x82 0x6B），UTF-8 编码会把它毁掉（见 PacEntry 注释）。
        let nameBytes = e.FileNameBytes
        if nameBytes.Length <> h.StringSize then
            failwithf "文件名原始字节长度(%d) 与 StringSize(%d) 不符: %s" nameBytes.Length h.StringSize e.Filename
        bw.Write(nameBytes)
        bw.Write(e.FileId)
        bw.Write(offsets[i])
        bw.Write(contents[i].Length)
        // FileNameHash：**沿用原条目的值，不重算**（2026-10-20 实测教训）。
        // 对非 ASCII 名字，按当前名字重算得到的哈希与引擎索引值不同 ⇒ 查不到 ⇒ 闪退。
        // 对纯 ASCII 名字，重算与原值本就相同（2201 pac roundtrip 逐字节一致），所以沿用等价且更安全。
        bw.Write(e.FileNameHash)

    // 数据区（条目顺序 == 偏移顺序，已验证）
    for i in 0 .. orig.Entries.Length - 1 do
        bw.Write(contents[i])
        let padded = align16 contents[i].Length
        bw.Write(Array.zeroCreate<byte> (padded - contents[i].Length))

    ms.ToArray()
