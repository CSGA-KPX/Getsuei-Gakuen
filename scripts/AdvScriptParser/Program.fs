module AdvScriptParser.Program

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open AdvScriptParser.ScriptFormat

// ---------------------------------------------------------------------------
// JSON 表示（通用转储，不依赖具体游戏的命令定义）
//
// {
//   "format": "ADVScript/2",
//   "encoding": "shift_jis",
//   "header":  { "type": "EVT", "typeRaw": n, "version": n, "commandNum": n, "flag": n },
//   "strings": [ "dummy", ... ],        // 可由命令推导时省略
//   "commands": [
//     { "name": "...", "args": [ n | "...", ... ] }
//   ]
// }
//
// 设计原则：JSON 中只保留真实数据，一切可由内容推导的字段在还原时重建：
//  - strings：字符串表可由命令推导（无未引用条目、无重复、顺序与编译器构造
//    规则一致，见 deriveStrings）时整体省略，还原时按同一规则重建。
//  - paramHeaders / stringHeaders：标准布局（见 isStandardLayout）时整体省略，
//    还原时按 0x40 基址 + 命令数 + 字符串表重算；非标准布局时才原样保存。
//  - 字符串参数直接写成 JSON 字符串，StrFlag 由参数实际类型自动重建；
//    仅当原始 StrFlag 无法由参数推导（异常位、越界索引）时才输出 "strFlag"。
//  - 命令名按文本保存在 "name"，还原时在字符串表中查找（首次出现位置），
//    找不到则追加；仅当原始索引与推导结果不同时才输出 "nameIndex"。
//  - 末尾未使用的 0 参数直接省略，还原时补齐到 14 个。
//
// 因此未编辑的 JSON 还原后与原始 bin 逐字节一致；编辑文本内容时所有
// 偏移 / 索引 / 表头自动重建，无需手工维护任何 magic number。
// ---------------------------------------------------------------------------

let [<Literal>] FormatTag = "ADVScript/2"

let private zeroPadding = Array.zeroCreate<byte> 8

let private toHex (b: byte[]) = Convert.ToHexString(b)
let private fromHex (s: string) = Convert.FromHexString(s)

/// 字符串能否经当前编码无损往返
let private isLossless (s: string) =
    let bytes = stringEncoding.GetBytes(s)
    stringEncoding.GetString(bytes) = s

// ------------------------------- 转储 -------------------------------

let dumpJson (script: ScriptData) : string =
    let strings = decodeStrings script
    // 每个字符串首次出现的位置（用于判断索引是否可推导）
    let firstOccurrence = System.Collections.Generic.Dictionary<string, int>()
    for i, s in strings |> Seq.indexed do
        if not (firstOccurrence.ContainsKey s) then firstOccurrence[s] <- i

    let standard = isStandardLayout script
    let root = JsonObject()

    root["format"] <- FormatTag
    root["encoding"] <- stringEncoding.WebName

    let header = JsonObject()
    header["type"] <- script.Header.TypeName
    header["typeRaw"] <- script.Header.TypeRaw
    header["version"] <- script.Header.Version
    header["commandNum"] <- script.Header.CommandNum
    header["flag"] <- script.Header.Flag
    root["header"] <- header

    if not standard then
        let phs = JsonArray()
        for ph in script.ParamHeaders do
            let o = JsonObject()
            o["offset"] <- ph.Offset
            o["num"] <- ph.Num
            if ph.Padding <> zeroPadding then o["paddingHex"] <- toHex ph.Padding
            phs.Add(o)
        root["paramHeaders"] <- phs

    // 字符串表可由命令推导（无未引用条目、无重复、顺序与编译器构造规则一致、
    // 且全部可无损编码）时省略 strings 字段，还原时按 deriveStrings 同一规则重建
    let derived = deriveStrings strings script.Commands
    let stringsDerivable =
        derived.Length = strings.Length
        && List.forall2 (=) derived (List.ofArray strings)
        && (strings |> Array.forall isLossless)

    if not stringsDerivable then
        let strs = JsonArray()
        for i, s in strings |> Seq.indexed do
            if isLossless s then
                strs.Add(JsonValue.Create(s))
            else
                let o = JsonObject()
                o["$hex"] <- toHex script.StringBytes[i]
                strs.Add(o)
        root["strings"] <- strs

    if not standard then
        let shs = JsonArray()
        for sh in script.StringHeaders do
            let o = JsonObject()
            o["stringIdx"] <- sh.StringIdx
            o["length"] <- sh.Length
            if sh.Padding <> zeroPadding then o["paddingHex"] <- toHex sh.Padding
            shs.Add(o)
        root["stringHeaders"] <- shs

    let cmds = JsonArray()
    for cmd in script.Commands do
        let o = JsonObject()
        let name = commandName strings cmd
        o["name"] <- name
        // nameIndex 仅在无法由名称推导（重复字符串的非首次引用、越界索引）时输出
        let derivable =
            match firstOccurrence.TryGetValue name with
            | true, i -> i
            | _ -> -1
        if cmd.NameIndex <> derivable then
            o["nameIndex"] <- cmd.NameIndex

        let argsArr = JsonArray()
        let mutable computedFlag = 0
        let mutable representable = true
        for j in 0 .. ArgCount - 1 do
            let isStr = cmd.StrFlag >>> j &&& 1 = 1
            let idx = cmd.Args[j]
            if isStr then
                if idx >= 0 && idx < strings.Length && isLossless strings[idx] then
                    computedFlag <- computedFlag ||| (1 <<< j)
                    argsArr.Add(JsonValue.Create(strings[idx]))
                else
                    representable <- false // 无法用字符串表示，按整数原样输出
                    argsArr.Add(JsonValue.Create(idx))
            else
                argsArr.Add(JsonValue.Create(idx))
        // 去掉末尾未使用的 0 参数
        let mutable last = argsArr.Count - 1
        while last >= 0 &&
              (match argsArr[last] with
               | :? JsonValue as v -> (match v.TryGetValue<int>() with true, n -> n = 0 | _ -> false)
               | _ -> false) do
            last <- last - 1
        while argsArr.Count > last + 1 do
            argsArr.RemoveAt(argsArr.Count - 1)
        o["args"] <- argsArr
        // strFlag 仅在无法由参数推导时输出
        if not representable || computedFlag <> cmd.StrFlag then
            o["strFlag"] <- cmd.StrFlag
        cmds.Add(o)
    root["commands"] <- cmds

    let opts = JsonSerializerOptions(WriteIndented = true, Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
    root.ToJsonString(opts)

// ------------------------------- 还原 -------------------------------

type RestoreWarning = string

let restoreJson (json: string) : ScriptData * RestoreWarning list =
    let warnings = ResizeArray()
    let root = JsonNode.Parse(json).AsObject()

    let fmtTag = match root["format"] with null -> "" | n -> n.GetValue<string>()
    if fmtTag <> FormatTag then
        warnings.Add(sprintf "format 字段为 \"%s\"，期望 %s" fmtTag FormatTag)

    let encName = match root["encoding"] with null -> "" | n -> n.GetValue<string>()
    if encName <> stringEncoding.WebName then
        warnings.Add(sprintf "encoding 为 %s，与解析时使用的 %s 不同，字符串可能被重新编码" encName stringEncoding.WebName)

    let headerNode = root["header"].AsObject()
    let typeRaw =
        match headerNode["typeRaw"] with
        | null ->
            let t = headerNode["type"].GetValue<string>()
            let b = Array.create 4 0uy
            Encoding.ASCII.GetBytes(t).AsSpan(0, min 4 t.Length).CopyTo(b)
            BitConverter.ToUInt32(b)
        | n -> n.GetValue<uint32>()

    // 字符串表：JSON 显式给出（string 或 { "$hex": "..." } 元素），
    // 或省略时按编译器规则从命令推导重建（与 deriveStrings 同一规则）
    let stringList = ResizeArray<byte[]>()
    let stringText = ResizeArray<string>()

    let addStringText (s: string) =
        if not (isLossless s) then
            warnings.Add(sprintf "字符串 \"%s\" 无法用 %s 无损编码" s stringEncoding.WebName)
        stringList.Add(stringEncoding.GetBytes(s))
        stringText.Add(s)

    match root["strings"] with
    | null ->
        let seen = System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        let add (s: string) = if seen.Add s then addStringText s
        add "dummy"
        let cmdsNode = root["commands"].AsArray()
        for c in cmdsNode do
            match c.AsObject()["args"] with
            | null -> ()
            | argsNode ->
                for a in argsNode.AsArray() do
                    match a with
                    | :? JsonValue as v ->
                        match v.TryGetValue<int>() with
                        | true, _ -> ()
                        | _ -> add (v.GetValue<string>())
                    | _ -> ()
        for c in cmdsNode do
            add (c.AsObject().["name"].GetValue<string>())
    | strsNode ->
        for el in strsNode.AsArray() do
            match el with
            | :? JsonValue as v when v.TryGetValue<string>() |> fst ->
                addStringText (v.GetValue<string>())
            | _ ->
                let hex = el.AsObject().["$hex"].GetValue<string>()
                let b = fromHex hex
                stringList.Add(b)
                stringText.Add(stringEncoding.GetString(b))

    let findString (s: string) =
        stringText |> Seq.tryFindIndex ((=) s)

    /// 按文本解析字符串表索引：优先首次出现位置，找不到则追加
    let resolveString (context: string) (s: string) =
        match findString s with
        | Some i -> i
        | None ->
            warnings.Add(sprintf "%s：字符串 \"%s\" 不在字符串表中，已追加" context s)
            stringList.Add(stringEncoding.GetBytes(s))
            stringText.Add(s)
            stringText.Count - 1

    let commands =
        [| for c in root["commands"].AsArray() ->
            let o = c.AsObject()
            let name = o["name"].GetValue<string>()
            let nameIndex =
                match o["nameIndex"] with
                | null -> resolveString (sprintf "命令名 \"%s\"" name) name
                | n ->
                    let i = n.GetValue<int>()
                    if i < 0 || i >= stringText.Count then
                        warnings.Add(sprintf "命令名 \"%s\" 的 nameIndex %d 越界" name i)
                    elif stringText[i] <> name then
                        warnings.Add(sprintf "命令名 \"%s\" 与 nameIndex %d 处的 \"%s\" 不一致，以 nameIndex 为准" name i stringText[i])
                    i
            let argsNode = o["args"].AsArray()
            if argsNode.Count > ArgCount then
                warnings.Add(sprintf "命令 \"%s\" 参数数量为 %d，超过 %d，多余参数被忽略" name argsNode.Count ArgCount)
            let args = Array.zeroCreate ArgCount
            let mutable computedFlag = 0
            for j in 0 .. min argsNode.Count ArgCount - 1 do
                match argsNode[j] with
                | :? JsonValue as v ->
                    match v.TryGetValue<int>() with
                    | true, n -> args[j] <- n
                    | _ ->
                        let s = v.GetValue<string>()
                        args[j] <- resolveString (sprintf "命令 \"%s\" 参数 %d" name j) s
                        computedFlag <- computedFlag ||| (1 <<< j)
                | _ -> warnings.Add(sprintf "命令 \"%s\" 参数 %d 类型无效，按 0 处理" name j)
            let strFlag =
                match o["strFlag"] with
                | null -> computedFlag
                | n -> n.GetValue<int>()
            { NameIndex = nameIndex; Args = args; StrFlag = strFlag } |]

    match headerNode["commandNum"] with
    | null -> ()
    | n ->
        let cn = n.GetValue<uint32>()
        if cn <> uint32 commands.Length then
            warnings.Add(sprintf "commands 数量 (%d) 与 header.commandNum (%d) 不一致，以实际数量为准" commands.Length cn)

    // ---- 表头重建：标准布局整体重算；JSON 中显式保存过（非标准布局）则按原样使用 ----
    let stringHeaders =
        match root["stringHeaders"] with
        | null ->
            let shs = Array.zeroCreate<StringHeader> stringList.Count
            let mutable running = 0u
            for i in 0 .. stringList.Count - 1 do
                shs[i] <-
                    { StringIdx = running
                      Length = uint32 stringList[i].Length
                      Padding = zeroPadding }
                running <- running + uint32 stringList[i].Length + 1u
            shs
        | shsNode ->
            let saved =
                [| for sh in shsNode.AsArray() ->
                    let o = sh.AsObject()
                    {| StringIdx = o["stringIdx"].GetValue<uint32>()
                       Padding = (match o["paddingHex"] with null -> zeroPadding | n -> fromHex (n.GetValue<string>())) |} |]
            if saved.Length <> stringList.Count then
                warnings.Add(sprintf "字符串数量 (%d) 与 stringHeaders 数量 (%d) 不一致" stringList.Count saved.Length)
            [| for i in 0 .. stringList.Count - 1 ->
                if i < saved.Length then
                    { StringIdx = saved[i].StringIdx
                      Length = uint32 stringList[i].Length
                      Padding = saved[i].Padding }
                else
                    let prev = saved[i - 1].StringIdx + uint32 stringList[i - 1].Length + 1u
                    { StringIdx = prev; Length = uint32 stringList[i].Length; Padding = zeroPadding } |]

    let strDataSize =
        stringHeaders |> Array.fold (fun (acc: uint32) sh -> acc + sh.Length + 1u) 0u

    let paramHeaders =
        match root["paramHeaders"] with
        | null ->
            let cmdOffset = 0x40u
            let strHdrOffset = cmdOffset + uint32 (commands.Length * CommandSize)
            let strDataOffset = strHdrOffset + uint32 (stringHeaders.Length * 0x10)
            [| { Offset = cmdOffset; Num = uint32 commands.Length; Padding = zeroPadding }
               { Offset = strHdrOffset; Num = uint32 stringHeaders.Length; Padding = zeroPadding }
               { Offset = strDataOffset; Num = strDataSize; Padding = zeroPadding } |]
        | phsNode ->
            let saved =
                [| for ph in phsNode.AsArray() ->
                    let o = ph.AsObject()
                    { Offset = o["offset"].GetValue<uint32>()
                      Num = o["num"].GetValue<uint32>()
                      Padding = (match o["paddingHex"] with null -> zeroPadding | n -> fromHex (n.GetValue<string>())) } |]
            if saved.Length <> ParamHeaderCount then
                warnings.Add(sprintf "paramHeaders 数量为 %d，期望 %d" saved.Length ParamHeaderCount)
            // 数量字段始终按实际内容重建
            saved
            |> Array.mapi (fun i ph ->
                match i with
                | 0 -> { ph with Num = uint32 commands.Length }
                | 1 -> { ph with Num = uint32 stringHeaders.Length }
                | 2 -> { ph with Num = strDataSize }
                | _ -> ph)

    let script =
        { Header =
            { TypeRaw = typeRaw
              Version = headerNode["version"].GetValue<uint32>()
              CommandNum = uint32 commands.Length
              Flag = headerNode["flag"].GetValue<uint32>() }
          ParamHeaders = paramHeaders
          Commands = commands
          StringHeaders = stringHeaders
          StringBytes = stringList.ToArray() }

    script, List.ofSeq warnings

// ------------------------------- 命令 -------------------------------

let private printUsage () =
    eprintfn "用法:"
    eprintfn "  AdvScriptParser dump    <input.bin>  [output.json]   转储 bin -> json"
    eprintfn "  AdvScriptParser restore <input.json> [output.bin]    还原 json -> bin"
    eprintfn "  AdvScriptParser verify  <input.bin>                  内存中 roundtrip 校验"
    eprintfn "  AdvScriptParser info    <input.bin>                  打印结构信息"

let private defaultOut (input: string) (ext: string) =
    Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input) + ext)

[<EntryPoint>]
let main argv =
    try
        match List.ofArray argv with
        | "dump" :: input :: rest ->
            let bytes = File.ReadAllBytes(input)
            let script = parse bytes
            for w in validate bytes script do eprintfn "[警告] %s" w
            if not (isStandardLayout script) then
                eprintfn "[警告] 非标准布局，paramHeaders / stringHeaders 已原样保存到 JSON"
            let output = rest |> List.tryHead |> Option.defaultValue (defaultOut input ".json")
            File.WriteAllText(output, dumpJson script, Encoding.UTF8)
            let cmdCount = script.Commands.Length
            let strCount = script.StringBytes.Length
            printfn "已转储 %s -> %s（%d 条命令，%d 个字符串）" input output cmdCount strCount
            0
        | "restore" :: input :: rest ->
            let script, warnings = File.ReadAllText(input, Encoding.UTF8) |> restoreJson
            for w in warnings do eprintfn "[警告] %s" w
            let output = rest |> List.tryHead |> Option.defaultValue (defaultOut input ".bin")
            let outBytes = write script
            File.WriteAllBytes(output, outBytes)
            printfn "已还原 %s -> %s（%d 字节）" input output outBytes.Length
            0
        | "verify" :: input :: _ ->
            let bytes = File.ReadAllBytes(input)
            let script = parse bytes
            let warnings = validate bytes script
            for w in warnings do printfn "[警告] %s" w
            // 1) bin -> 模型 -> bin
            let rebuilt = write script
            let binOk = bytes.AsSpan().SequenceEqual(rebuilt)
            printfn "bin -> model -> bin : %s" (if binOk then "一致" else "不一致")
            // 2) bin -> json -> bin
            let json = dumpJson script
            let script2, w2 = restoreJson json
            for w in w2 do printfn "[json 还原警告] %s" w
            let rebuilt2 = write script2
            let jsonOk = bytes.AsSpan().SequenceEqual(rebuilt2)
            printfn "bin -> json -> bin : %s" (if jsonOk then "一致" else "不一致")
            if not binOk || not jsonOk then
                let firstDiff =
                    Seq.tryFindIndex (fun i -> bytes[i] <> rebuilt2[i]) (seq { 0 .. min bytes.Length rebuilt2.Length - 1 })
                match firstDiff with
                | Some i -> printfn "首个差异位于 0x%X: 原始 0x%02X, 重建 0x%02X" i bytes[i] rebuilt2[i]
                | None -> printfn "长度不同: 原始 %d, 重建 %d" bytes.Length rebuilt2.Length
            if binOk && jsonOk && List.isEmpty warnings then 0 else 1
        | "info" :: input :: _ ->
            let bytes = File.ReadAllBytes(input)
            let script = parse bytes
            let strings = decodeStrings script
            printfn "文件: %s (%d 字节)" input bytes.Length
            printfn "Type: %s (raw 0x%08X)" script.Header.TypeName script.Header.TypeRaw
            printfn "Version: %d" script.Header.Version
            printfn "CommandNum: %d" script.Header.CommandNum
            printfn "Flag: 0x%08X" script.Header.Flag
            for i, ph in script.ParamHeaders |> Seq.indexed do
                printfn "ParamHeader[%d]: Offset=0x%X Num=%d" i ph.Offset ph.Num
            printfn "字符串数: %d (去重后 %d)" strings.Length (strings |> Seq.distinct |> Seq.length)
            printfn "布局: %s" (if isStandardLayout script then "标准" else "非标准（paramHeaders/stringHeaders 将原样保存）")
            let warnings = validate bytes script
            if List.isEmpty warnings then printfn "结构校验: 通过"
            else for w in warnings do printfn "[警告] %s" w
            0
        | _ ->
            printUsage ()
            1
    with ex ->
        eprintfn "错误: %s" ex.Message
        2
