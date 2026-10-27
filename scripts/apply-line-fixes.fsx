// 行宽改写应用器：把人工写好的短译文按表写回 work/atf-cn
//
// 输入 TSV（制表符分隔，无表头）：`文件 <TAB> key <TAB> 新译文`
//   - 新译文里的换行用字面 `\n` 表示（会被还原成 CRLF）
//   - 文件里可以写多行（同一个 key 只能出现一次）
//
// 校验（任何一条不过就整体不写）：
//   1. 新译文每行 ≤ 24 全角（游戏单行上限，见 docs/formats/atf.md §6.1）
//   2. **ruby 标签序列必须与原值完全一致**（改写绝不能吞掉 <ruby>…</ruby>）
//   3. key 必须存在于对应 json
//
// 用法:
//   dotnet fsi scripts/apply-line-fixes.fsx <fix.tsv>            # 试运行
//   dotnet fsi scripts/apply-line-fixes.fsx <fix.tsv> --apply    # 写入
open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let cnDir = Path.Combine(workspace, "work", "atf-cn")
let jopts = JsonDocumentOptions(AllowTrailingCommas = true)
let rubyRe = Regex(@"<ruby>[^<]*<[^/>]*/ruby>", RegexOptions.Compiled)
let maxHalf = 48

let charHalf (c: char) =
    let v = int c
    if v < 0x80 then 1 elif v >= 0xFF61 && v <= 0xFF9F then 1 else 2

/// 显示宽度（半角单位）：把每个 `<ruby>基字<注音/ruby>` 换成基字后统一累加。
/// ⚠️ 用正则分组取基字，别手工 Substring —— `<ruby>` 是 6 个字符，从 5 开始取会把 `>` 算进去
/// （2026-10-20 踩过：导致宽度虚高、误报超宽）。
let rubyKeepBase = Regex(@"<ruby>([^<]*)<[^/>]*/ruby>", RegexOptions.Compiled)
let visibleWidth (s: string) =
    rubyKeepBase.Replace(s, "$1") |> Seq.sumBy charHalf

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 1 then
    eprintfn "用法: dotnet fsi scripts/apply-line-fixes.fsx <fix.tsv> [--apply]"
    exit 2
let fixPath = args.[0]
let apply = args |> Array.contains "--apply"
if not (File.Exists fixPath) then
    eprintfn "找不到 %s" fixPath
    exit 2

let fixLines =
    File.ReadAllLines fixPath
    |> Array.filter (fun l -> l.Trim() <> "" && not (l.StartsWith "#"))
    |> Array.map (fun l ->
        let f = l.Split('\t')
        if f.Length < 3 then failwithf "行格式错误（要 3 列）：%s" l
        f.[0].Trim(), f.[1].Trim(), f.[2].Replace("\\n", "\r\n"))

printfn "待应用 %d 条改写" fixLines.Length

// 按文件分组：读文件 → 校验 → 改 → 写
let byFile = fixLines |> Array.groupBy (fun (f, _, _) -> f)
let mutable okCount = 0
let errors = ResizeArray<string>()

for (file, items) in byFile do
    let path = Path.Combine(cnDir, file + ".json")
    if not (File.Exists path) then errors.Add(sprintf "缺文件 %s" path)
    else
        let text = File.ReadAllText path
        let doc = JsonDocument.Parse(text, jopts)
        let values = Collections.Generic.Dictionary<string, string>()
        for r in doc.RootElement.GetProperty("records").EnumerateArray() do
            values.[r.GetProperty("key").GetString()] <- (try r.GetProperty("value").GetString() with _ -> "")
        for (_, key, nv) in items do
            match values.TryGetValue key with
            | false, _ -> errors.Add(sprintf "%s: 没有 key %s" file key)
            | true, ov ->
                let rubyOld = rubyRe.Matches(ov) |> Seq.map (fun m -> m.Value) |> List.ofSeq
                let rubyNew = rubyRe.Matches(nv) |> Seq.map (fun m -> m.Value) |> List.ofSeq
                if rubyOld <> rubyNew then
                    errors.Add(sprintf "%s %s: ruby 标签不一致\n  旧=%A\n  新=%A" file key rubyOld rubyNew)
                for l in nv.Replace("\r\n", "\n").Split('\n') do
                    if l.Trim() <> "" && visibleWidth l > maxHalf then
                        errors.Add(sprintf "%s %s: 新译文仍超宽（%d 半角）：%s" file key (visibleWidth l) l)
                if errors.Count = 0 then
                    okCount <- okCount + 1

if errors.Count > 0 then
    eprintfn "\n%d 处校验失败（未写入任何文件）：" errors.Count
    errors |> Seq.truncate 30 |> Seq.iter (eprintfn "  %s")
    exit 1

printfn "校验通过 %d 条" okCount

if apply then
    for (file, items) in byFile do
        let path = Path.Combine(cnDir, file + ".json")
        let text = File.ReadAllText path
        let nl = if text.Contains "\r\n" then "\r\n" else "\n"
        let lines = ResizeArray<string>(text.Replace("\r\n", "\n").Split('\n'))
        for (_, key, nv) in items do
            let keyNeedle = sprintf "\"key\": \"%s\"" key
            match lines |> Seq.tryFindIndex (fun l -> l.Contains keyNeedle) with
            | Some i when i + 1 < lines.Count ->
                let indent = lines.[i + 1].Substring(0, lines.[i + 1].IndexOf '"')
                let enc = JsonSerializerOptions(Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
                lines.[i + 1] <- indent + "\"value\": " + JsonSerializer.Serialize(nv, enc)
            | _ -> errors.Add(sprintf "%s: 定位不到 key %s 的 value 行" file key)
        File.WriteAllText(path, String.Join(nl, lines), UTF8Encoding false)
    printfn "已写入 %d 个文件 / %d 条" byFile.Length okCount
else
    printfn "\n（加 --apply 才写入）"
