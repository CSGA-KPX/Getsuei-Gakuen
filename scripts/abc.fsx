// abc 字库索引格式解析库（docs/formats/abc-hip-font.md）
// 被 abc-analysis.fsx / abc-overlay.fsx 以 #load 引用。
module AbcLib

open System
open System.IO

/// 每个字形：24 字节未知块 + 32 字节块（前 16 字节为字形矩形 x1 x2 y1 y2，像素）
type AbcGlyph =
    { Chunk1: byte[] // 24 字节，语义未知（12×u16）
      Chunk2: byte[] // 32 字节；前 16 字节为矩形 x1 x2 y1 y2
      Rect: int[] } // = Chunk2 前 16 字节：x1 x2 y1 y2

type AbcFile =
    { File: string
      Version: uint32 // 0x41535720(" WSA") / 5 / 4；5 无 rec32 表（2026-10-18 实锤 Font00.abc）
      FourCC: byte[] // 4 字节
      Unk1: byte[] // 00004442：0x44=字号，0x42=特效选项（改后有效）
      Unk2: byte[] // 00004040：改后无变化
      Unk3: byte[] // 00008040：改后无变化
      Unk4: byte[] // 00002842：推测为特殊情况字号（假名标注？）
      EntryCount: int // u16
      /// 长度 = EntryCount + 3（头部额外 3 项使下标直接等于 char code）
      /// 值非 0 ⇒ 该 char code 有字形
      Mappings: uint16[]
      /// 非 0 映射按顺序对应的字形数据
      Glyphs: AbcGlyph[]
      /// 与 Glyphs 平行：每个字形对应的 char code（= Mappings 中非 0 项的下标）
      CharCodes: int[] }

let glyphCount (abc: AbcFile) = abc.Glyphs.Length

let parseBytes (name: string, bytes: byte[]) : AbcFile =
    use ms = new MemoryStream(bytes, writable = false)
    use br = new BinaryReader(ms)

    let fourCC = br.ReadBytes(4)
    let ver = BitConverter.ToUInt32(fourCC, 0)
    let unk1, unk2, unk3, unk4 = br.ReadBytes(4), br.ReadBytes(4), br.ReadBytes(4), br.ReadBytes(4)

    let entryCount = int (br.ReadUInt16())

    if ver = 5u then
        // Version 5（sub_43F290 确认）：布局同 " WSA" 但**无 rec32 表**，
        // GlyphCount 是独立 u32 字段（含 index0 空白位，比非0映射多 1），
        // mapping 区长度 = EntryCount + 1。实例：cmn\font.pac 的 Font00.abc。
        let mappings = Array.init (entryCount + 1) (fun _ -> br.ReadUInt16())

        let glyphCount = int (br.ReadUInt32())

        let glyphs =
            [| for _ in 1..glyphCount ->
                   let chunk1 = br.ReadBytes(24)

                   { Chunk1 = chunk1; Chunk2 = [||]; Rect = [||] } |]

        if br.PeekChar() <> -1 then
            failwithf "%s：解析结束但流未耗尽，布局与预期不符" name

        let charCodes =
            mappings
            |> Array.mapi (fun i v -> if v <> 0us then Some i else None)
            |> Array.choose id

        { File = name
          Version = ver
          FourCC = fourCC
          Unk1 = unk1
          Unk2 = unk2
          Unk3 = unk3
          Unk4 = unk4
          EntryCount = entryCount
          Mappings = mappings
          Glyphs = glyphs
          CharCodes = charCodes }
    else

    // 头部额外 3 项：使 char code 与数组下标直接对齐，后续无需位移
    let mappings = Array.init (entryCount + 3) (fun _ -> br.ReadUInt16())
    let mapCount = mappings |> Array.sumBy (fun i -> if i = 0us then 0 else 1)

    let glyphs =
        [| for _ in 1..mapCount ->
               let chunk1 = br.ReadBytes(24)
               let chunk2 = br.ReadBytes(32)

               let rect =
                   Array.init 4 (fun i -> BitConverter.ToInt32(chunk2, i * 4))

               { Chunk1 = chunk1; Chunk2 = chunk2; Rect = rect } |]

    if br.PeekChar() <> -1 then
        failwithf "%s：解析结束但流未耗尽，布局与预期不符" name

    let charCodes =
        mappings
        |> Array.mapi (fun i v -> if v <> 0us then Some i else None)
        |> Array.choose id

    { File = name
      Version = ver
      FourCC = fourCC
      Unk1 = unk1
      Unk2 = unk2
      Unk3 = unk3
      Unk4 = unk4
      EntryCount = entryCount
      Mappings = mappings
      Glyphs = glyphs
      CharCodes = charCodes }

/// 序列化回 abc 字节（roundtrip 逐字节一致，已全量验证）
let buildBytes (abc: AbcFile) : byte[] =
    // ver5 的字形表含 index0 空白位，比 CharCodes 恰好多 1（其余版本相等）
    if abc.Version = 5u then
        if abc.Glyphs.Length <> abc.CharCodes.Length + 1 then
            invalidArg "abc" "ver5：Glyphs 应比 CharCodes 多 1（index0 空白位）"
    elif abc.Glyphs.Length <> abc.CharCodes.Length then
        invalidArg "abc" "Glyphs 与 CharCodes 数量不一致"

    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms)

    bw.Write(abc.FourCC)
    bw.Write(abc.Unk1)
    bw.Write(abc.Unk2)
    bw.Write(abc.Unk3)
    bw.Write(abc.Unk4)
    bw.Write(uint16 abc.EntryCount)

    // Mappings 中非 0 值必须正好是 glyphs 的 char code 顺序
    let mutable gi = 0
    for i in 0 .. abc.Mappings.Length - 1 do
        if abc.Mappings[i] <> 0us then
            if abc.CharCodes[gi] <> i then
                invalidArg "abc" (sprintf "Mappings 非 0 下标(%d) 与 CharCodes[%d](%d) 不一致" i gi abc.CharCodes[gi])
            gi <- gi + 1
    if gi <> abc.CharCodes.Length then
        invalidArg "abc" "Mappings 非 0 项数与 CharCodes 数量不一致"

    for m in abc.Mappings do
        bw.Write(m)

    if abc.Version = 5u then
        bw.Write(uint32 abc.Glyphs.Length)
        for g in abc.Glyphs do
            if g.Chunk1.Length <> 24 || g.Chunk2.Length <> 0 then
                invalidArg "abc" "ver5：Chunk1 必须 24 字节、Chunk2 必须为空"
            bw.Write(g.Chunk1)
    else
        for g in abc.Glyphs do
            if g.Chunk1.Length <> 24 || g.Chunk2.Length <> 32 then
                invalidArg "abc" "Chunk1 必须 24 字节、Chunk2 必须 32 字节"
            bw.Write(g.Chunk1)
            bw.Write(g.Chunk2)

    ms.ToArray()

let verify (path: string) : bool =
    let bytes = File.ReadAllBytes(path)
    buildBytes (parseBytes (path, bytes)) = bytes

let parse (path: string) : AbcFile =
    parseBytes (path, File.ReadAllBytes(path))

// ---------------- 格式化辅助 ----------------

let byteToHex (data: byte[]) =
    let rawHex = BitConverter.ToString(data).Replace("-", "")

    rawHex
    |> Seq.chunkBySize 8
    |> Seq.map String
    |> String.concat "-"

/// 按指定字节分组格式化（如 [| 2;2;... |] 把 24 字节格式化为 12 组 u16）
let byteToHexFormat (data: byte[]) (format: int[]) =
    if Array.sum format <> data.Length then
        invalidArg "format" "分组大小之和不等于数据长度"

    let chunks =
        seq {
            let mutable idx = 0

            for f in format do
                yield BitConverter.ToString(data, idx, f).Replace("-", "")
                idx <- idx + f
        }

    String.concat "-" chunks
