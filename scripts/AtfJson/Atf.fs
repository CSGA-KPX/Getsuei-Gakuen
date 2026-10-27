namespace AtfJson

open System
open System.IO
open System.Text

/// 一条文本记录：JSON 中实际存储的核心内容
type TextRecord =
    { Key: string
      Value: string
      /// 固定 6 个 uint32；本文件中观测到的均为 0
      TextParam: uint32[] }

/// ATF 文件的逻辑模型。TextNum / ParamHeader / StringHeader / 字符串池等
/// 全部在编译（build）时由 Records 重建，不进入 JSON。
type AtfFile =
    { /// 文件魔数（原始 uint32，小端字节即 "ATF\0"）
      Type: uint32
      /// 文件末尾未被任何 TextHeader 引用的空 StringHeader 数量
      /// （string.atf 中为 2，是原始工具留下的尾部填充）
      TrailingEmptyStringHeaders: int
      Records: TextRecord[] }

module AtfBinary =

    let private typeToString (typ: uint32) : string =
        BitConverter.GetBytes(typ)
        |> Array.takeWhile ((<>) 0uy)
        |> Encoding.ASCII.GetString

    let private stringToType (s: string) : uint32 =
        let b = Encoding.ASCII.GetBytes(s)
        if b.Length > 4 then invalidArg "s" "type 魔数最多 4 个 ASCII 字符"
        BitConverter.ToUInt32(Array.append b (Array.zeroCreate (4 - b.Length)), 0)

    let typeName (typ: uint32) = typeToString typ
    let typeValue (name: string) = stringToType name

    // ---------------- Parse ----------------

    let parseBytes (bytes: byte[]) : AtfFile =
        use ms = new MemoryStream(bytes, writable = false)
        use br = new BinaryReader(ms)

        let typ = br.ReadUInt32()
        let textNum = br.ReadUInt32()
        br.BaseStream.Seek(8L, SeekOrigin.Current) |> ignore // FileHeader padding

        // 4 个 ParamHeader：TextHeaders / StringHeaders / StringPool / WStringPool
        let paramHeaders =
            [| for _ in 1..4 ->
                   let offset = br.ReadUInt32()
                   let num = br.ReadUInt32()
                   let size = br.ReadUInt32()
                   br.BaseStream.Seek(4L, SeekOrigin.Current) |> ignore // padding
                   struct (offset, num, size) |]

        let struct (thOff, thNum, _) = paramHeaders[0]
        let struct (shOff, shNum, _) = paramHeaders[1]
        let struct (sOff, _, _) = paramHeaders[2]
        let struct (wOff, _, _) = paramHeaders[3]

        if thNum <> textNum then
            failwithf "TextNum(%d) 与 TextHeader 数量(%d) 不一致" textNum thNum

        // TextHeader：NameIdx, TextIdx, TextParam[6], 16 字节 padding
        br.BaseStream.Seek(int64 thOff, SeekOrigin.Begin) |> ignore
        let textHeaders =
            [| for _ in 1 .. int thNum ->
                   let nameIdx = br.ReadUInt32()
                   let textIdx = br.ReadUInt32()
                   let ps = [| for _ in 1..6 -> br.ReadUInt32() |]
                   br.BaseStream.Seek(16L, SeekOrigin.Current) |> ignore
                   (nameIdx, textIdx, ps) |]

        // StringHeader：Top, Len, 8 字节 padding
        br.BaseStream.Seek(int64 shOff, SeekOrigin.Begin) |> ignore
        let stringHeaders =
            [| for _ in 1 .. int shNum ->
                   let top = br.ReadUInt32()
                   let len = br.ReadUInt32()
                   br.BaseStream.Seek(8L, SeekOrigin.Current) |> ignore
                   struct (top, len) |]

        let readName idx =
            let struct (top, len) = stringHeaders[idx]
            if len = 0u then ""
            else
                br.BaseStream.Seek(int64 sOff + int64 top, SeekOrigin.Begin) |> ignore
                Encoding.ASCII.GetString(br.ReadBytes(int len - 1))

        let readValue idx =
            let struct (top, len) = stringHeaders[idx]
            if len = 0u then ""
            else
                // wstring 的 Top 以 UTF-16 码元为单位
                br.BaseStream.Seek(int64 wOff + int64 top * 2L, SeekOrigin.Begin) |> ignore
                Encoding.Unicode.GetString(br.ReadBytes(int len * 2))

        // 校验简单模型：NameIdx = 2i, TextIdx = 2i+1
        textHeaders
        |> Array.iteri (fun i (nameIdx, textIdx, _) ->
            if nameIdx <> uint32 (i * 2) || textIdx <> uint32 (i * 2 + 1) then
                failwithf
                    "TextHeader[%d] 的字符串索引 (NameIdx=%d, TextIdx=%d) 不是顺序配对 (2i, 2i+1)，该文件使用了当前工具不支持的布局（如字符串去重/共享）"
                    i nameIdx textIdx)

        // 末尾未被引用的 StringHeader 必须全为空（Top=0, Len=0）
        let referenced = int thNum * 2
        let trailing = int shNum - referenced
        if trailing < 0 then
            failwithf "StringHeader 数量(%d) 少于 TextHeader 引用数量(%d)" shNum referenced
        for i in referenced .. int shNum - 1 do
            let struct (top, len) = stringHeaders[i]
            if top <> 0u || len <> 0u then
                failwithf "StringHeader[%d] (Top=%d, Len=%d) 未被引用但非空，当前工具不支持" i top len

        { Type = typ
          TrailingEmptyStringHeaders = trailing
          Records =
            [| for (nameIdx, textIdx, ps) in textHeaders ->
                   { Key = readName (int nameIdx)
                     Value = readValue (int textIdx)
                     TextParam = ps } |] }

    let parse (path: string) : AtfFile = parseBytes (File.ReadAllBytes path)

    // ---------------- Build ----------------

    let buildBytes (atf: AtfFile) : byte[] =
        let n = atf.Records.Length

        // key 必须为 ASCII（ATF 的 name 池是单字节编码）
        for r in atf.Records do
            if r.Key |> Seq.exists (fun c -> int c > 0x7F) then
                failwithf "Key '%s' 含非 ASCII 字符，无法写入 name 字符串池" r.Key

        let keyBytes = atf.Records |> Array.map (fun r -> Encoding.ASCII.GetBytes(r.Key))
        let valueUtf16Len = atf.Records |> Array.map (fun r -> r.Value.Length) // UTF-16 码元数

        // 字符串池：name 每条占 (字节数 + 2) 字节（内容 + '\0' + 1 字节填充）
        //           value 每条占 (码元数 + 1) 个码元（内容 + 终止符）
        let strTotal = keyBytes |> Array.sumBy (fun k -> k.Length + 2)
        let wstrTotal = valueUtf16Len |> Array.sumBy (fun l -> l + 1)

        let shNum = 2 * n + atf.TrailingEmptyStringHeaders

        // 区段布局（紧密排列，无间隙）
        let thOff = 0x50u
        let thSize = uint32 (0x30 * n)
        let shOff = thOff + thSize
        let shSize = uint32 (0x10 * shNum)
        let sOff = shOff + shSize
        let wOff = sOff + uint32 strTotal

        use fs = new MemoryStream()
        use bw = new BinaryWriter(fs)

        // FileHeader
        bw.Write(atf.Type)
        bw.Write(uint32 n) // TextNum
        bw.Write(0u)
        bw.Write(0u)

        // ParamHeader ×4（每个 16 字节，末 4 字节为 0 填充）
        let writeParamHeader (offset: uint32) (num: uint32) (size: uint32) =
            bw.Write(offset)
            bw.Write(num)
            bw.Write(size)
            bw.Write(0u)

        writeParamHeader thOff (uint32 n) thSize
        writeParamHeader shOff (uint32 shNum) shSize
        writeParamHeader sOff (uint32 strTotal) (uint32 strTotal) // name 池：Num = Size = 字节数
        writeParamHeader wOff (uint32 wstrTotal) (uint32 (wstrTotal * 2)) // value 池：Num = 码元数, Size = 字节数

        // TextHeader ×n（NameIdx=2i, TextIdx=2i+1, TextParam[6], 16 字节 0 填充）
        for i in 0 .. n - 1 do
            bw.Write(uint32 (i * 2))
            bw.Write(uint32 (i * 2 + 1))
            let ps = atf.Records[i].TextParam
            for j in 0..5 do
                bw.Write(if j < ps.Length then ps[j] else 0u)
            for _ in 1..4 do
                bw.Write(0u)

        // StringHeader：(name, value) 交替，末尾补空条目
        let mutable strTop = 0u
        let mutable wstrTop = 0u

        let writeStringHeader (top: uint32) (len: uint32) =
            bw.Write(top)
            bw.Write(len)
            bw.Write(0u)
            bw.Write(0u)

        for i in 0 .. n - 1 do
            // name：Len 含 '\0'
            writeStringHeader strTop (uint32 (keyBytes[i].Length + 1))
            strTop <- strTop + uint32 (keyBytes[i].Length + 2)
            // value：Len 不含终止符
            writeStringHeader wstrTop (uint32 valueUtf16Len[i])
            wstrTop <- wstrTop + uint32 (valueUtf16Len[i] + 1)

        for _ in 1 .. atf.TrailingEmptyStringHeaders do
            writeStringHeader 0u 0u

        // name 字符串池（ASCII + 2 个 0 字节）
        for k in keyBytes do
            if k.Length > 0 then bw.Write(k)
            bw.Write(0us)

        // value 字符串池（UTF-16LE + 终止符）
        for r in atf.Records do
            if r.Value.Length > 0 then bw.Write(Encoding.Unicode.GetBytes(r.Value))
            bw.Write(0us)

        fs.ToArray()

    let build (atf: AtfFile) (path: string) =
        File.WriteAllBytes(path, buildBytes atf)
