// 译文行宽检查：找出超过游戏文本框宽度的行（backlog / 对话框会溢出）
//
// 背景（2026-10-20）：游戏**不会自动折行**，`\r\n` 是唯一换行手段。
// 实测框宽上限 = **24 个全角字符**（日文原文 `勿論、味噌汁も煮物も色がおかしい。というか赤い。` 24 字刚好放下；
// 中文 26 字就溢出到边框外）。ruby 注音不占宽度，比较时先剥掉标签只留基字。
//
// 用法:
//   dotnet fsi scripts/check-line-width.fsx            # 只统计
//   dotnet fsi scripts/check-line-width.fsx --tsv      # 同时写 work/inventory/line-width-issues.tsv
//
// 输出 TSV: 文件 / key / 行号 / 全角宽度 / 日文原文对应行宽 / 文本
open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let cnDir = Path.Combine(workspace, "work", "atf-cn")
let jpDir = Path.Combine(workspace, "work", "atf")
let outTsv = Path.Combine(workspace, @"work\inventory\line-width-issues.tsv")

/// 框宽上限（全角字符数）。24 由实机截图反解（见文件头说明）。
let maxWidth = 24

let jopts = JsonDocumentOptions(AllowTrailingCommas = true)
let rubyRe = Regex(@"<ruby>([^<]*)<[^/>]*/ruby>", RegexOptions.Compiled)
let stripRuby (s: string) = rubyRe.Replace(s, "$1")

/// 半角记 1、全角记 2（返回值是「半角单位」，除以 2 即全角数）
let visualWidth (s: string) =
    s |> Seq.sumBy (fun c ->
        let v = int c
        if v < 0x80 then 1
        elif v >= 0xFF61 && v <= 0xFF9F then 1
        else 2)

let splitLines (v: string) = v.Replace("\r\n", "\n").Split('\n')

let mutable total = 0
let mutable over = 0
let issues = ResizeArray<string * string * int * int * int * string>()
let byFile = Collections.Generic.Dictionary<string, int>()

for f in Directory.GetFiles(cnDir, "*.json") |> Array.sort do
    let name = Path.GetFileNameWithoutExtension f
    let cn = JsonDocument.Parse(File.ReadAllText f, jopts)
    let jpPath = Path.Combine(jpDir, name + ".json")
    let jp =
        if File.Exists jpPath then
            let d = JsonDocument.Parse(File.ReadAllText jpPath, jopts)
            d.RootElement.GetProperty("records").EnumerateArray()
            |> Seq.map (fun r -> r.GetProperty("key").GetString(), r.GetProperty("value").GetString())
            |> dict
        else dict []
    let mutable cnt = 0
    for r in cn.RootElement.GetProperty("records").EnumerateArray() do
        let key = r.GetProperty("key").GetString()
        let v = (try r.GetProperty("value").GetString() with _ -> "")
        let jpLines = if jp.ContainsKey key then splitLines jp.[key] else [||]
        let cnLines = splitLines v
        for i in 0 .. cnLines.Length - 1 do
            let clean = stripRuby cnLines.[i]
            if clean.Trim() <> "" then
                total <- total + 1
                let w = visualWidth clean
                // 只报「中文超宽、但日文原文没超」的行 —— 日文原文也超 24 的记录说明用的是更宽的框
                // （例：text_SP 的特别语音台词原文宽 35~47 全角，游戏能正常显示）
                let jwRaw = if i < jpLines.Length then visualWidth (stripRuby jpLines.[i]) else 0
                if w > maxWidth * 2 && jwRaw <= maxWidth * 2 then
                    over <- over + 1
                    cnt <- cnt + 1
                    let jw = jwRaw
                    issues.Add((name, key, i + 1, w / 2, jw / 2, cnLines.[i]))
    if cnt > 0 then byFile.[name] <- cnt

printfn "译文总行 %d，超过 %d 全角的行 %d（涉及 %d 个 pac）" total maxWidth over byFile.Count

printfn "\n最严重的 10 个 pac："
byFile |> Seq.sortByDescending (fun kv -> kv.Value) |> Seq.truncate 10
|> Seq.iter (fun kv -> printfn "  %-18s %d 行" kv.Key kv.Value)

printfn "\n宽度 >28 全角的行（优先处理）："
let worst = issues |> Seq.filter (fun (_, _, _, w, _, _) -> w > 28) |> Seq.truncate 12
for (name, key, ln, w, jw, text) in worst do
    printfn "  %-16s %-24s L%d %d全角(日文 %d)「%s」" name key ln w jw text

if fsi.CommandLineArgs |> Array.contains "--tsv" then
    Directory.CreateDirectory(Path.GetDirectoryName outTsv) |> ignore
    let header = "文件\tkey\t行号\t全角宽\t日文原文宽\t文本"
    File.WriteAllLines(
        outTsv,
        Seq.append [ header ]
            (issues |> Seq.map (fun (n, k, l, w, jw, t) -> sprintf "%s\t%s\t%d\t%d\t%d\t%s" n k l w jw (t.Replace("\r\n", "\\n")))))
    printfn "\n已写出 %s（%d 行）" outTsv issues.Count
else
    printfn "\n（加 --tsv 可写出完整清单 work/inventory/line-width-issues.tsv）"
