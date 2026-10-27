// ruby 修复：按 work/inventory/ruby-fix-list.tsv 修 work/atf-cn 里的 53 处 ruby
// （台账与判定依据见 docs/tables/ruby-triage.md §3）
//
// 用法:
//   dotnet fsi scripts/fix-ruby.fsx            # 试运行（只报告，不写文件）
//   dotnet fsi scripts/fix-ruby.fsx --apply    # 实际写入 work/atf-cn
//   dotnet fsi scripts/fix-ruby.fsx --rebuild  # 写入后统计残留（可与 --apply 同用）
//
// 三类修法（TSV 第 1 列）:
//   P 拼音注音 → 改成语义中文注音（映射见下方 semantic 表，依据 ruby-triage §3.1）
//   D 長音符误作破折号 → `—`(U+2014) 改回 `ー`(U+30FC)
//   R 基字 = 注音（冗余）→ 去掉 ruby，只留基字
//
// ⚠️ 只改 work/atf-cn 的 value 字段（逐行文本替换，保留 JSON 原格式与编码）。
//    `scripts/galtransl.fsx import` 会用 work/galtransl-proc 整体重写 atf-cn，
//    重跑 import 后必须再跑一次本脚本（幂等：已修的行不会二次命中）。
open System
open System.IO
open System.Text

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let tsv = Path.Combine(workspace, @"work\inventory\ruby-fix-list.tsv")
let atfCn = Path.Combine(workspace, "work", "atf-cn")
let utf8 = UTF8Encoding false

let args = fsi.CommandLineArgs |> Array.skip 1
let apply = args |> Array.contains "--apply"

if not (File.Exists tsv) then
    eprintfn "缺少 %s" tsv
    exit 2

/// P 类：基字 → 语义中文注音（ruby-triage §3.1 建议值）
let semantic =
    dict [
        "生命", "命"
        "前奏曲", "序曲"
        "能力", "力量"
        "删除", "抹消"
    ]

/// TSV 一行：类型 / 文件 / key / 基字 / 现注音
let rows =
    File.ReadAllLines tsv
    |> Array.map (fun l -> l.Split('\t'))
    |> Array.filter (fun f -> f.Length >= 5)
    |> Array.map (fun f -> f.[0], f.[1], f.[2], f.[3], f.[4])

if rows.Length = 0 then
    eprintfn "ruby-fix-list.tsv 为空"
    exit 2

/// 把 (类型, 基字, 现注音) 翻译成 (旧标签, 新文本)
let rule (kind: string) (baseText: string) (rubyText: string) =
    let oldTag = sprintf "<ruby>%s<%s/ruby>" baseText rubyText
    match kind with
    | "R" -> oldTag, baseText
    | "D" -> oldTag, sprintf "<ruby>%s<%s/ruby>" baseText (rubyText.Replace('—', 'ー'))
    | "P" ->
        match semantic.TryGetValue baseText with
        | true, t -> oldTag, sprintf "<ruby>%s<%s/ruby>" baseText t
        | _ -> failwithf "P 类缺语义映射：基字「%s」" baseText
    | k -> failwithf "未知类型 %s" k

// ---- 按文件分组处理 ----
let byFile = rows |> Array.groupBy (fun (_, f, _, _, _) -> f)
let mutable filesChanged = 0
let mutable totalReplacements = 0
let mutable alreadyDone = 0
let misses = ResizeArray<string>()

/// 按原文件的换行风格/末尾换行读入（避免整文件 diff 噪音）
let readLines (path: string) =
    let text = File.ReadAllText(path, Encoding.UTF8)
    let nl = if text.Contains "\r\n" then "\r\n" else "\n"
    let trailing = text.EndsWith nl
    let parts = ResizeArray<string>(text.Split([| nl |], StringSplitOptions.None))
    if trailing && parts.Count > 0 && parts.[parts.Count - 1] = "" then parts.RemoveAt(parts.Count - 1)
    parts.ToArray(), nl, trailing

let writeLines (path: string) (lines: string[]) (nl: string) (trailing: bool) =
    let sb = StringBuilder()
    for i in 0 .. lines.Length - 1 do
        sb.Append(lines.[i]) |> ignore
        if i < lines.Length - 1 || trailing then sb.Append(nl) |> ignore
    File.WriteAllText(path, sb.ToString(), utf8)

for (file, items) in byFile do
    let path = Path.Combine(atfCn, file + ".json")
    if not (File.Exists path) then
        eprintfn "⚠️ 缺少文件 %s" path
    else
        let lines, nl, trailing = readLines path
        let mutable changedHere = 0
        // key → 该 key 要做的 (旧标签, 新文本) 列表（同一 key 可能有多处 ruby）
        let wanted =
            items
            |> Array.map (fun (k, _, key, b, r) -> key, rule k b r)
            |> Array.groupBy fst
        let hits = Collections.Generic.Dictionary<string, int>()

        for (key, rules) in wanted do
            // 找到 `"key": "<key>",` 行，其下一行是 value
            let keyNeedle = sprintf "\"key\": \"%s\"" key
            let idx = lines |> Array.tryFindIndex (fun l -> l.Contains keyNeedle)
            match idx with
            | None -> misses.Add(sprintf "%s %s（key 未找到）" file key)
            | Some i when i + 1 >= lines.Length -> misses.Add(sprintf "%s %s（value 行缺失）" file key)
            | Some i ->
                let mutable valueLine = lines.[i + 1]
                let mutable fileHit = false
                for (_, (oldTag, newText)) in rules do
                    if valueLine.Contains oldTag then
                        let cnt = valueLine.Split(oldTag).Length - 1
                        valueLine <- valueLine.Replace(oldTag, newText)
                        totalReplacements <- totalReplacements + cnt
                        changedHere <- changedHere + cnt
                        hits.[key] <- cnt
                        fileHit <- true
                    elif valueLine.Contains newText then
                        alreadyDone <- alreadyDone + 1        // 幂等：已修过
                    else
                        misses.Add(sprintf "%s %s（标签未命中：%s）" file key oldTag)
                if fileHit then lines.[i + 1] <- valueLine

        if changedHere > 0 then
            for kv in hits do
                printfn "  %-18s %-24s %d 处" file kv.Key kv.Value
            if apply then
                writeLines path lines nl trailing
            filesChanged <- filesChanged + 1

printfn "\n%s：%d 个文件 / %d 处替换" (if apply then "已写入" else "试运行") filesChanged totalReplacements
if alreadyDone > 0 then printfn "（已修过、本轮跳过 %d 处）" alreadyDone
if misses.Count > 0 then
    printfn "\n⚠️ 未命中 %d 处（请核对 ruby-fix-list.tsv）：" misses.Count
    misses |> Seq.truncate 30 |> Seq.iter (printfn "  %s")
    if misses.Count > 30 then printfn "  ...（共 %d 条）" misses.Count
else
    printfn "✅ 全部 %d 条命中" rows.Length
