module AdvScriptParser.ScriptFormat

open System
open System.Text

// ---------------------------------------------------------------------------
// ADVScript 二进制脚本格式（参考 ADVScript 项目反推，泛化版本）
//
// 布局（全部小端序）：
//   0x00  SAdvScrHeader      16 字节: Type(u32) Version(u32) CommandNum(u32) Flag(u32)
//   0x10  ParamHeader[3]     每个 16 字节: Offset(u32) Num(u32) Padding(8 字节)
//          [0] = 命令区       ([0].Offset 通常为 0x40, Num = CommandNum)
//          [1] = 字符串表头区 (Num = 字符串个数)
//          [2] = 字符串数据区 (Num = 数据总字节数)
//   ...   CommandList        每条 0x40 字节: Command(i32) + Arg[14](i32) + StrFlag(i32)
//   ...   StringHeader[]     每条 16 字节: StringIdx(u32) Length(u32) Padding(8 字节)
//   ...   StringBuffer       连续的 NUL 结尾字符串
//
// StrFlag 的第 j 位为 1 表示 Arg[j] 是字符串表索引，否则是普通整数。
// Command 字段同样是字符串表索引（命令名）。
// ---------------------------------------------------------------------------

let [<Literal>] CommandSize = 0x40
let [<Literal>] ArgCount = 14
let [<Literal>] ParamHeaderCount = 3

type ScriptHeader =
    { TypeRaw: uint32
      Version: uint32
      CommandNum: uint32
      Flag: uint32 }

    member h.TypeName =
        let bytes = BitConverter.GetBytes(h.TypeRaw)
        let len = bytes |> Array.tryFindIndex ((=) 0uy) |> Option.defaultValue 4
        Encoding.ASCII.GetString(bytes, 0, len)

type ParamHeader =
    { Offset: uint32
      Num: uint32
      Padding: byte[] } // 8 字节，通常为全 0

type Command =
    { NameIndex: int
      Args: int[]
      StrFlag: int }

type StringHeader =
    { StringIdx: uint32
      Length: uint32
      Padding: byte[] } // 8 字节，通常为全 0

type ScriptData =
    { Header: ScriptHeader
      ParamHeaders: ParamHeader[]
      Commands: Command[]
      StringHeaders: StringHeader[]
      StringBytes: byte[][] } // 每个字符串的原始字节（不含结尾 NUL）

/// 解码字符串用的编码（该引擎为日系游戏，默认 Shift-JIS；纯 ASCII 时等价）
let stringEncoding =
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
    Encoding.GetEncoding(932)

// ------------------------------- 读取 -------------------------------

let private u32 (buf: byte[]) (pos: int) = BitConverter.ToUInt32(buf, pos)
let private i32 (buf: byte[]) (pos: int) = BitConverter.ToInt32(buf, pos)

let parse (bytes: byte[]) : ScriptData =
    if bytes.Length < 0x40 then
        failwithf "文件太小（%d 字节），不是有效的 ADVScript 二进制" bytes.Length

    let header =
        { TypeRaw = u32 bytes 0
          Version = u32 bytes 4
          CommandNum = u32 bytes 8
          Flag = u32 bytes 12 }

    let paramHeaders =
        [| for i in 0 .. ParamHeaderCount - 1 ->
            let p = 0x10 + i * 0x10
            { Offset = u32 bytes p
              Num = u32 bytes (p + 4)
              Padding = bytes[p + 8 .. p + 15] } |]

    let cmdOffset = int paramHeaders[0].Offset
    if cmdOffset + int header.CommandNum * CommandSize > bytes.Length then
        failwith "命令区超出文件范围"

    let commands =
        [| for i in 0 .. int header.CommandNum - 1 ->
            let p = cmdOffset + i * CommandSize
            { NameIndex = i32 bytes p
              Args = [| for j in 0 .. ArgCount - 1 -> i32 bytes (p + 4 + j * 4) |]
              StrFlag = i32 bytes (p + 4 + ArgCount * 4) } |]

    let strHdrOffset = int paramHeaders[1].Offset
    let strCount = int paramHeaders[1].Num
    if strHdrOffset + strCount * 0x10 > bytes.Length then
        failwith "字符串表头区超出文件范围"

    let stringHeaders =
        [| for i in 0 .. strCount - 1 ->
            let p = strHdrOffset + i * 0x10
            { StringIdx = u32 bytes p
              Length = u32 bytes (p + 4)
              Padding = bytes[p + 8 .. p + 15] } |]

    let strDataOffset = int paramHeaders[2].Offset
    let stringBytes =
        [| for sh in stringHeaders ->
            let idx = strDataOffset + int sh.StringIdx
            let len = int sh.Length
            if idx + len > bytes.Length then
                failwithf "字符串数据超出文件范围 (StringIdx=%d, Length=%d)" sh.StringIdx sh.Length
            bytes[idx .. idx + len - 1] |]

    { Header = header
      ParamHeaders = paramHeaders
      Commands = commands
      StringHeaders = stringHeaders
      StringBytes = stringBytes }

// ------------------------------- 写出 -------------------------------

/// 由 ScriptData 重建二进制（结构与原始布局一一对应）
let write (script: ScriptData) : byte[] =
    use ms = new IO.MemoryStream()
    let w32 (v: uint32) = ms.Write(BitConverter.GetBytes(v), 0, 4)
    let wi32 (v: int) = ms.Write(BitConverter.GetBytes(v), 0, 4)

    w32 script.Header.TypeRaw
    w32 script.Header.Version
    w32 script.Header.CommandNum
    w32 script.Header.Flag

    for ph in script.ParamHeaders do
        w32 ph.Offset
        w32 ph.Num
        ms.Write(ph.Padding, 0, ph.Padding.Length)

    for cmd in script.Commands do
        wi32 cmd.NameIndex
        for a in cmd.Args do wi32 a
        wi32 cmd.StrFlag

    for sh in script.StringHeaders do
        w32 sh.StringIdx
        w32 sh.Length
        ms.Write(sh.Padding, 0, sh.Padding.Length)

    for sb in script.StringBytes do
        ms.Write(sb, 0, sb.Length)
        ms.WriteByte(0uy)

    ms.ToArray()

// ------------------------------- 校验 -------------------------------

/// 解析时的结构性检查，返回警告列表
let validate (bytes: byte[]) (script: ScriptData) : string list =
    [ let ph = script.ParamHeaders
      if ph[0].Num <> script.Header.CommandNum then
          yield sprintf "ParamHeader[0].Num (%d) 与 CommandNum (%d) 不一致" ph[0].Num script.Header.CommandNum
      let expectStrHdr = ph[0].Offset + uint32 script.Commands.Length * uint32 CommandSize
      if ph[1].Offset <> expectStrHdr then
          yield sprintf "ParamHeader[1].Offset (0x%X) 与计算值 (0x%X) 不一致" ph[1].Offset expectStrHdr
      let expectStrData = ph[1].Offset + uint32 script.StringHeaders.Length * 0x10u
      if ph[2].Offset <> expectStrData then
          yield sprintf "ParamHeader[2].Offset (0x%X) 与计算值 (0x%X) 不一致" ph[2].Offset expectStrData
      let mutable total = 0u
      for sh in script.StringHeaders do
          total <- total + sh.Length + 1u
      if ph[2].Num <> total then
          yield sprintf "ParamHeader[2].Num (%d) 与字符串总字节数 (%d) 不一致" ph[2].Num total
      // StringIdx 连续性
      let mutable running = 0u
      for i, sh in script.StringHeaders |> Seq.indexed do
          if sh.StringIdx <> running then
              yield sprintf "StringHeader[%d].StringIdx (%d) 与连续偏移 (%d) 不一致" i sh.StringIdx running
          running <- running + sh.Length + 1u
      // 字符串结尾 NUL 与尾部对齐
      let dataStart = int ph[2].Offset
      let dataEnd = dataStart + int ph[2].Num
      if dataEnd > bytes.Length then
          yield sprintf "字符串数据区 (结束于 0x%X) 超出文件长度 (0x%X)" dataEnd bytes.Length
      elif dataEnd < bytes.Length then
          yield sprintf "字符串数据区之后还有 %d 字节尾部数据" (bytes.Length - dataEnd)
      for i, sh in script.StringHeaders |> Seq.indexed do
          let nulPos = dataStart + int sh.StringIdx + int sh.Length
          if nulPos < bytes.Length && bytes[nulPos] <> 0uy then
              yield sprintf "字符串 %d 结尾不是 NUL" i
      // 表头填充字节
      for i, p in script.ParamHeaders |> Seq.indexed do
          if p.Padding |> Array.exists ((<>) 0uy) then
              yield sprintf "ParamHeader[%d] 的填充字节非零" i
      for i, sh in script.StringHeaders |> Seq.indexed do
          if sh.Padding |> Array.exists ((<>) 0uy) then
              yield sprintf "StringHeader[%d] 的填充字节非零" i
      // 命令名索引有效性
      for i, cmd in script.Commands |> Seq.indexed do
          if cmd.NameIndex < 0 || cmd.NameIndex >= script.StringBytes.Length then
              yield sprintf "命令 %d 的名称索引 %d 越界" i cmd.NameIndex
          for j in 0 .. ArgCount - 1 do
              if cmd.StrFlag >>> j &&& 1 = 1 then
                  let idx = cmd.Args[j]
                  if idx < 0 || idx >= script.StringBytes.Length then
                      yield sprintf "命令 %d 参数 %d 的字符串索引 %d 越界" i j idx ]

/// 字符串表（解码后）
let decodeStrings (script: ScriptData) : string[] =
    script.StringBytes |> Array.map (fun b -> stringEncoding.GetString(b))

/// 命令名解析
let commandName (strings: string[]) (cmd: Command) : string =
    if cmd.NameIndex >= 0 && cmd.NameIndex < strings.Length then strings[cmd.NameIndex]
    else sprintf "<invalid:%d>" cmd.NameIndex

/// 是否为“标准布局”：各区块偏移 / 数量 / StringIdx 均可由内容推导。
/// 标准布局的文件在 JSON 中无需保存 paramHeaders / stringHeaders，还原时整体重建。
let isStandardLayout (script: ScriptData) : bool =
    let ph = script.ParamHeaders
    let mutable ok =
        ph.Length = ParamHeaderCount
        && ph[0].Offset = 0x40u
        && ph[0].Num = uint32 script.Commands.Length
        && ph[1].Offset = ph[0].Offset + uint32 (script.Commands.Length * CommandSize)
        && ph[1].Num = uint32 script.StringHeaders.Length
        && ph[2].Offset = ph[1].Offset + uint32 (script.StringHeaders.Length * 0x10)
        && (ph |> Array.forall (fun p -> p.Padding = Array.zeroCreate 8))
        && (script.StringHeaders |> Array.forall (fun sh -> sh.Padding = Array.zeroCreate 8))
    let mutable running = 0u
    for i, sh in script.StringHeaders |> Seq.indexed do
        ok <- ok && sh.StringIdx = running && int sh.Length = script.StringBytes[i].Length
        running <- running + sh.Length + 1u
    ok && ph[2].Num = running

/// 按编译器规则从命令推导字符串表（与 ADVScript CParsedScript.Compile 的构造顺序一致）：
/// "dummy" + 各命令字符串参数（按槽位首次出现序）+ 各命令名（首次出现序），序数去重。
/// 结果与实际字符串表完全一致时，JSON 中可省略 strings，还原时按同一规则重建。
let deriveStrings (strings: string[]) (commands: Command[]) : string list =
    let result = ResizeArray()
    let seen = System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
    let add (s: string) = if seen.Add s then result.Add s
    add "dummy"
    for cmd in commands do
        for j in 0 .. ArgCount - 1 do
            if cmd.StrFlag >>> j &&& 1 = 1 then
                let idx = cmd.Args[j]
                if idx >= 0 && idx < strings.Length then add strings[idx]
    for cmd in commands do
        if cmd.NameIndex >= 0 && cmd.NameIndex < strings.Length then
            add strings[cmd.NameIndex]
    List.ofSeq result
