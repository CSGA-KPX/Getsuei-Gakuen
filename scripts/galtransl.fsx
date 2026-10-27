// galtransl 桥接：atf-json ⇄ GalTransl name-message JSON（双向）
//
// 用法:
//   dotnet fsi galtransl.fsx export [-atf <atfJsonDir>] [-o <outDir>]
//       atf-json → GalTransl JSON，默认 work/atf → work/galtransl-raw
//   dotnet fsi galtransl.fsx import [-proc <procDir>] [-atf <atfJsonDir>] [-o <outDir>]
//       译文合并回 atf-json，默认 work/galtransl-proc + work/atf → work/atf-cn
//   dotnet fsi galtransl.fsx verify [-atf <atfJsonDir>]
//       恒等回归：export 产物原样 import，合并结果须与原始 atf-json 逐记录一致
//
// 文本纳入规则（2026-10-18 与用户确认：剧情+test 全部纳入，目录数据排除 ✅）：
//   NAME_                                     → 人名（导出到 name 字段；【】与 $/& 控制后缀
//                                               由本工具剥离/还原，不送翻、不依赖译者保留）
//   数字开头 / RGN_ / SP_ / FontCheckScript_  → 消息文本（剧情 + test/SP 文件对话）
//   ending_c3_ / VOICE_TEST_                  → 消息文本（结局对话 / 语音条目标签）
//   chara_test_ / chara_test_sub2/3/4_        → 消息文本（角色查看器 test 模式菜单句）
//   chara_test_sub1_                          → 立绘姿势/表情目录（条目名≠说话人），排除
//   其它（CHID_ / effect* / ENDING_ / cg_ 等指令、标签与目录数据）→ 不导出，import 时原样保留
//   纯 name-only 文件（无任何消息 key）→ 写空数组 [] 占位（不送翻；保持 raw 与
//                                   atf 目录一一对应，下游缺文件会报错）
//
// 导出对象额外携带 "atf-json-key" 字段（锚定 atf record key，import 首选匹配依据）。
// GalTransl 是否原样保留该字段待实测；若被丢弃，import 自动回退：
//   ① 全部对象带 atf-json-key → 按键精确匹配
//   ② 否则对象带 post_jp/pre_jp（transl_cache 形状）→ 按 post_jp（回退 pre_jp）匹配原文
//   ③ 否则纯 name/message → 按顺序位置对齐（要求两边对象数一致）
// 译文取值：proofread_zh → pre_zh → message（transl_cache 形状）；name 字段剥一层【】。
#load "AtfJson/Atf.fs"
#load "AtfJson/Json.fs"

open System
open System.IO
open System.Collections.Generic
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.RegularExpressions
open AtfJson

let args = fsi.CommandLineArgs |> Array.skip 1

let argOpt (flag: string) =
    match Array.tryFindIndex (fun a -> a = flag) args with
    | Some i when i + 1 < args.Length -> Some args[i + 1]
    | _ -> None

// ---------------- 纳入规则 ----------------

let isNameKey (k: string) = k.StartsWith("NAME_")

let isMessageKey (k: string) =
    if k.StartsWith("chara_test_sub1_") then false // 立绘姿势/表情目录：条目名≠说话人，排除
    else
        k.Length > 0
        && (Char.IsDigit k[0]
            || k.StartsWith("RGN_")
            || k.StartsWith("SP_")
            || k.StartsWith("FontCheckScript_")
            || k.StartsWith("ending_c3_")      // 结局对话（小写 ending_c3，区别于 ENDING_ 路线标签）
            || k.StartsWith("VOICE_TEST_")     // 语音测试条目标签
            || k.StartsWith("chara_test_")     // 角色查看器 test 菜单句（含 sub2/3/4）
            || k.StartsWith("chara_test_sub2_")
            || k.StartsWith("chara_test_sub3_")
            || k.StartsWith("chara_test_sub4_"))

// ---------------- 对话模型（export/import 共用同一份确定性映射） ----------------

/// 一个导出对象对应的 atf 侧信息。
/// Key：消息 key；孤立人名（其后无消息）时为 NAME key。
type Item =
    { Key: string
      Message: string option            // 消息原文；孤立人名为 None
      NameRecKey: string option         // 关联 NAME record key
      HadBrackets: bool                 // 原 NAME value 是否带【】
      NameInner: string                 // 剥离【】与控制后缀后的显示名（"" = 【　】无名占位）
      NameSuffix: string                // 原值中】之后的控制后缀（$spy / &hood 等），还原时原样接回
      NameOrig: string option }         // NAME record 原始 value（诊断用）

/// 纯 name-only 文件（没有任何消息 key）= 目录/指令文件，不导出（防御 chara_test_sub1 类目录）。
let hasAnyMessage (items: Item[]) =
    items |> Array.exists (fun it -> it.Message.IsSome)

/// NAME value 拆分为（是否带【】，显示名，控制后缀）。【　】视为无名占位。
let splitNameValue (v: string) =
    let m = Regex.Match(v, "^【(.*?)】(.*)$", RegexOptions.Singleline)
    if m.Success then
        let inner = m.Groups[1].Value
        let disp = if inner.Trim().Length = 0 then "" else inner
        true, disp, m.Groups[2].Value
    else
        false, v, ""

/// atf records → 导出对象序列（确定性：import 端对原文重放同一函数即可对齐）。
let buildItems (records: TextRecord[]) =
    let items = ResizeArray<Item>()
    let mutable pending: (string * string) option = None // (nameKey, nameValue)
    let flushPending() =
        match pending with
        | Some(nk, nv) ->
            let hadB, inner, suffix = splitNameValue nv
            items.Add { Key = nk
                        Message = None
                        NameRecKey = Some nk
                        HadBrackets = hadB
                        NameInner = inner
                        NameSuffix = suffix
                        NameOrig = Some nv }
            pending <- None
        | None -> ()
    for r in records do
        if isNameKey r.Key then
            flushPending()
            pending <- Some(r.Key, r.Value)
        elif isMessageKey r.Key then
            let namePart =
                match pending with
                | Some(nk, nv) ->
                    let hadB, inner, suffix = splitNameValue nv
                    Some(struct (nk, hadB, inner, suffix))
                | None -> None
            pending <- None
            items.Add { Key = r.Key
                        Message = Some r.Value
                        NameRecKey = namePart |> Option.map (fun (struct (nk, _, _, _)) -> nk)
                        HadBrackets = namePart |> Option.exists (fun (struct (_, b, _, _)) -> b)
                        NameInner = namePart |> Option.map (fun (struct (_, _, i, _)) -> i) |> Option.defaultValue ""
                        NameSuffix = namePart |> Option.map (fun (struct (_, _, _, s)) -> s) |> Option.defaultValue ""
                        NameOrig = None }
        // 其它 key（CHID_ 等指令）跳过，不影响挂起的人名
    flushPending()
    items.ToArray()

// ---------------- GalTransl JSON 读写 ----------------

let jsonWriterOpts =
    JsonWriterOptions(Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

let writeGtJson (items: Item[]) (path: string) =
    use fs = new FileStream(path, FileMode.Create, FileAccess.Write)
    use w = new Utf8JsonWriter(fs, jsonWriterOpts)
    w.WriteStartArray()
    for it in items do
        w.WriteStartObject()
        if it.NameInner <> "" then
            w.WriteString("name", it.NameInner)
        match it.Message with
        | Some m -> w.WriteString("message", m)
        | None -> ()
        w.WriteString("atf-json-key", it.Key)
        w.WriteEndObject()
    w.WriteEndArray()
    w.Flush()
    fs.WriteByte('\n'B)

/// GalTransl 译文对象（兼容三种形状：带 key 的 name/message、transl_cache、纯 name/message）。
type GtObj =
    { Key: string option
      Name: string option
      Message: string option
      PreJp: string option
      PostJp: string option
      PreZh: string option
      ProofreadZh: string option }

let readGtJson (path: string) : GtObj[] =
    use doc = JsonDocument.Parse(File.ReadAllText(path))
    [| for o in doc.RootElement.EnumerateArray() ->
           let prop (n: string) =
               match o.TryGetProperty(n) with
               | true, p when p.ValueKind = JsonValueKind.String -> Some(p.GetString())
               | _ -> None
           { Key = prop "atf-json-key"
             Name = prop "name"
             Message = prop "message"
             PreJp = prop "pre_jp"
             PostJp = prop "post_jp"
             PreZh = prop "pre_zh"
             ProofreadZh = prop "proofread_zh" } |]

// ---------------- 译文提取 ----------------

let private nonEmpty (s: string option) =
    match s with
    | Some t when t.Length > 0 -> Some t
    | _ -> None

/// 中文译文：proofread_zh → pre_zh → message。
let zhMessage (o: GtObj) =
    match nonEmpty o.ProofreadZh with
    | Some t -> Some t
    | None ->
        match nonEmpty o.PreZh with
        | Some t -> Some t
        | None -> nonEmpty o.Message

/// name 字段剥一层【】（译者若原样保留括号则去掉，还原时由本工具统一包回）。
let stripNameBrackets (s: string) =
    let m = Regex.Match(s, "^【(.*?)】$", RegexOptions.Singleline)
    if m.Success then m.Groups[1].Value.Trim() else s.Trim()

// ---------------- import 匹配 ----------------

type MatchMode =
    | ByKey of Dictionary<string, GtObj>
    | ByPreJp of Dictionary<string, Queue<GtObj>>
    | Positional

/// 按文件选择匹配模式：① 全部带 key → 按键；② 带 post_jp/pre_jp → 按日文；③ 否则按位置。
let decideMode (objs: GtObj[]) =
    if objs.Length > 0 && objs |> Array.forall (fun o -> o.Key.IsSome) then
        let d = Dictionary<string, GtObj>()
        let dup = HashSet<string>()
        for o in objs do
            let k = o.Key.Value
            if d.ContainsKey k then dup.Add k |> ignore else d[k] <- o
        if dup.Count > 0 then
            eprintfn "  警告: %d 个重复 atf-json-key（取首个）: %s" dup.Count (String.concat ", " dup)
        ByKey d
    elif objs |> Array.exists (fun o -> o.PostJp.IsSome || o.PreJp.IsSome) then
        let d = Dictionary<string, Queue<GtObj>>()
        for o in objs do
            let jp = nonEmpty o.PostJp |> Option.orElse (nonEmpty o.PreJp)
            match jp with
            | Some jp ->
                if not (d.ContainsKey jp) then d[jp] <- Queue()
                d[jp].Enqueue o
            | None -> ()
        ByPreJp d
    else
        Positional

/// 为单个导出对象找译文对象。
let resolveObj (mode: MatchMode) (idx: int) (item: Item) (objs: GtObj[]) =
    match mode with
    | ByKey d ->
        match d.TryGetValue(item.Key) with
        | true, o -> Some o
        | _ -> None
    | ByPreJp d ->
        // 消息按原文匹配；孤立人名按显示名匹配
        let lookupText = item.Message |> Option.orElse (if item.NameInner = "" then None else Some item.NameInner)
        match lookupText with
        | Some t ->
            match d.TryGetValue(t) with
            | true, q when q.Count > 0 -> Some(q.Dequeue())
            | _ -> None
        | None -> None
    | Positional ->
        if idx < objs.Length then Some objs[idx] else None

// ---------------- import 合并 ----------------

/// 把译文对象合并进 atf：返回（合并后 AtfFile，警告列表）。
let applyTranslations (atf: AtfFile) (items: Item[]) (objs: GtObj[]) =
    let mode = decideMode objs
    let overrides = Dictionary<string, string>()
    let warns = ResizeArray<string>()
    for idx in 0 .. items.Length - 1 do
        let item = items[idx]
        match resolveObj mode idx item objs with
        | None ->
            warns.Add(sprintf "%s: 无匹配译文对象，保留原文" item.Key)
        | Some o ->
            // 消息
            match item.Message with
            | Some orig ->
                match zhMessage o with
                | Some t -> overrides[item.Key] <- t
                | None -> warns.Add(sprintf "%s: 译文 message 为空，保留原文" item.Key)
            | None -> ()
            // 人名
            match item.NameRecKey with
            | Some nameKey ->
                match nonEmpty (o.Name |> Option.map stripNameBrackets) with
                | None -> () // 无名或无译文，保留原值
                | Some n ->
                    // 译者原样保留控制后缀时先剥掉，避免还原时重复
                    let n2 =
                        if item.NameSuffix <> "" && n.EndsWith(item.NameSuffix) then
                            n.Substring(0, n.Length - item.NameSuffix.Length).Trim()
                        else
                            n
                    if item.NameInner = "" then
                        // 【　】无名占位：忽略译者误填的名字，保持版式
                        if n2 <> "" then
                            warns.Add(sprintf "%s: 无名占位【　】被填了名字「%s」，已忽略" item.Key n2)
                    elif n2 = "" || n2 = item.NameInner then
                        () // 空或未翻译，保留原值
                    else
                        let v =
                            if item.HadBrackets then "【" + n2 + "】" + item.NameSuffix
                            else n2 + item.NameSuffix
                        overrides[nameKey] <- v
            | None -> ()
    let records =
        [| for r in atf.Records ->
               match overrides.TryGetValue(r.Key) with
               | true, v -> { r with Value = v }
               | _ -> r |]
    { atf with Records = records }, warns, mode

// ---------------- 子命令 ----------------

let atfDir = defaultArg (argOpt "-atf") "work/atf"

match if args.Length = 0 then "" else args[0] with
| "export" ->
    let outDir = defaultArg (argOpt "-o") "work/galtransl-raw"
    Directory.CreateDirectory(outDir) |> ignore
    // 清理旧的导出产物，避免规则调整后残留过时文件（如已排除的目录文件）
    let stale = Directory.GetFiles(outDir, "*.json")
    for s in stale do
        File.Delete s
    if stale.Length > 0 then
        printfn "export: 清理旧产物 %d 个" stale.Length
    let files = Directory.GetFiles(atfDir, "*.json") |> Array.sortBy id
    let mutable totalItems = 0
    let mutable totalNamed = 0
    let mutable written = 0
    let mutable emptyPlaceholder = 0
    for f in files do
        let atf = AtfJsonIO.readJson f
        let items = buildItems atf.Records
        totalItems <- totalItems + items.Length
        totalNamed <- totalNamed + (items |> Array.filter (fun it -> it.NameInner <> "") |> Array.length)
        if hasAnyMessage items then
            writeGtJson items (Path.Combine(outDir, Path.GetFileName(f)))
            written <- written + 1
        else
            // 目录/无语句文件：写空数组占位，保持 raw 与 atf 目录一一对应（下游按文件清单工作，缺文件会报错）
            writeGtJson [||] (Path.Combine(outDir, Path.GetFileName(f)))
            emptyPlaceholder <- emptyPlaceholder + 1
    printfn "export: %d/%d 文件有文本（空占位 %d），共 %d 对象（%d 带人名）-> %s"
        written files.Length emptyPlaceholder totalItems totalNamed outDir

| "import" ->
    let procDir = defaultArg (argOpt "-proc") "work/galtransl-proc"
    let outDir = defaultArg (argOpt "-o") "work/atf-cn"
    Directory.CreateDirectory(outDir) |> ignore
    let procFiles = Directory.GetFiles(procDir, "*.json") |> Array.sortBy id
    if procFiles.Length = 0 then
        eprintfn "import: %s 中没有 JSON 文件" procDir
        exit 1
    let mutable warnsTotal = 0
    for f in procFiles do
        let name = Path.GetFileName(f)
        let atfPath = Path.Combine(atfDir, name)
        if not (File.Exists atfPath) then
            eprintfn "SKIP: %s 在 %s 无对应 atf-json" name atfDir
        else
            let atf = AtfJsonIO.readJson atfPath
            let items = buildItems atf.Records
            if not (hasAnyMessage items) then
                eprintfn "SKIP: %s 无可翻译消息（纯目录/指令文件），不产出" name
            else
                let objs = readGtJson f
                if objs.Length <> items.Length then
                    eprintfn "警告: %s 对象数不一致（导出 %d vs 译文 %d），未改动的对象将按可用信息匹配" name items.Length objs.Length
                let merged, warns, mode =
                    applyTranslations atf items objs
                let modeName =
                    match mode with
                    | ByKey _ -> "atf-json-key"
                    | ByPreJp _ -> "post_jp/pre_jp"
                    | Positional -> "位置对齐"
                AtfJsonIO.writeJson merged (Path.Combine(outDir, name))
                warnsTotal <- warnsTotal + warns.Count
                if warns.Count > 0 then
                    eprintfn "%s: %d 警告（%s 匹配）" name warns.Count modeName
                    for w in warns do
                        eprintfn "  - %s" w
                else
                    printfn "%s: OK（%s 匹配，%d 对象）" name modeName items.Length
    printfn "import: %d 文件 -> %s（共 %d 警告）" procFiles.Length outDir warnsTotal

| "verify" ->
    let files = Directory.GetFiles(atfDir, "*.json") |> Array.sortBy id
    let mutable checkedFiles = 0
    let mutable checkedItems = 0
    for f in files do
        let atf = AtfJsonIO.readJson f
        let items = buildItems atf.Records
        // 与 export 同规则：纯 name-only 目录文件不导出，也不参与恒等回归
        if items.Length > 0 && hasAnyMessage items then
            // 恒等译文：与 export 产出完全相同的对象
            let identity =
                [| for it in items ->
                       { Key = Some it.Key
                         Name = (if it.NameInner = "" then None else Some it.NameInner)
                         Message = it.Message
                         PreJp = None
                         PostJp = None
                         PreZh = None
                         ProofreadZh = None } |]
            let merged, warns, _ = applyTranslations atf items identity
            // 1. 合并结果须与原始 atf-json 逐记录一致（恒等翻译 = 零改动）
            let sameRecords =
                merged.Records.Length = atf.Records.Length
                && Array.forall2 (fun (a: TextRecord) (b: TextRecord) -> a.Key = b.Key && a.Value = b.Value && a.TextParam = b.TextParam)
                       merged.Records atf.Records
            // 2. 对合并结果重放 buildItems，导出结构须不变
            let items2 = buildItems merged.Records
            let sameItems =
                items2.Length = items.Length
                && Array.forall2 (fun (a: Item) (b: Item) ->
                       a.Key = b.Key && a.Message = b.Message && a.NameRecKey = b.NameRecKey
                       && a.HadBrackets = b.HadBrackets && a.NameInner = b.NameInner && a.NameSuffix = b.NameSuffix)
                       items2 items
            if not sameRecords || not sameItems || warns.Count > 0 then
                eprintfn "FAIL: %s（records 一致=%b, items 一致=%b, 警告=%d）"
                    (Path.GetFileName f) sameRecords sameItems warns.Count
                for w in warns do
                    eprintfn "  - %s" w
                exit 1
            checkedFiles <- checkedFiles + 1
            checkedItems <- checkedItems + items.Length
    printfn "verify: %d 文件 / %d 对象恒等回归全部通过（export→import 零改动）" checkedFiles checkedItems

| other ->
    eprintfn "未知子命令: %s（应为 export / import / verify）" other
    eprintfn "用法: dotnet fsi galtransl.fsx export|import|verify [-atf <dir>] [-o <dir>] [-proc <dir>]"
    exit 2
