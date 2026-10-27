// atf 行宽重排：把超宽行里的字挪到同一记录的下一行（游戏单行上限 24 全角）
//
// 背景：游戏不自动折行，`\r\n` 是唯一换行手段；单行超过 24 全角就溢出 backlog 边框
// （标定见 docs/formats/atf.md §6.1）。而日文原文 67% 是 1 行、33% 是 2 行，框最多容 2 行，
// 所以**不能靠加行数**解决，只能在"行数不变"的前提下把字挪到下一行。
//
// 本脚本只处理「同一记录内总宽度放得下、只是分配不均」的情况：
//   总宽 ≤ 行数 × 24 全角  → 重新分配（本脚本）
//   总宽 >  行数 × 24 全角  → 必须改写缩短（脚本只报告，不改）
//
// 用法:
//   dotnet fsi scripts/reflow-atf.fsx              # 试运行（只报告）
//   dotnet fsi scripts/reflow-atf.fsx --apply      # 写入 work/atf-cn
//
// ⚠️ 断行不会切断 `<ruby>…</ruby>` 标签（按"可见单元"切分：整块 ruby 算一个单元）。
// ⚠️ 改的是 work/atf-cn，之后要重跑 font-rebuild + 装机才进游戏。
open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let cnDir = Path.Combine(workspace, "work", "atf-cn")
let args = fsi.CommandLineArgs |> Array.skip 1
let apply = args |> Array.contains "--apply"

let maxHalf = 48                       // 24 全角 = 48 半角单位
let jopts = JsonDocumentOptions(AllowTrailingCommas = true)
let rubyRe = Regex(@"<ruby>([^<]*)<[^/>]*/ruby>", RegexOptions.Compiled)

let charHalf (c: char) =
    let v = int c
    if v < 0x80 then 1 elif v >= 0xFF61 && v <= 0xFF9F then 1 else 2

/// 把一个片段切成「可见单元」：整块 ruby 算一个单元（宽度 = 基字之和）
let units (s: string) =
    let res = ResizeArray<string * int>()
    let mutable i = 0
    while i < s.Length do
        let m = rubyRe.Match(s, i)
        if m.Success && m.Index = i then
            let w = m.Groups.[1].Value |> Seq.sumBy charHalf
            res.Add((m.Value, w))
            i <- m.Index + m.Length
        else
            res.Add((string s.[i], charHalf s.[i]))
            i <- i + 1
    res |> Array.ofSeq

let width (s: string) = units s |> Array.sumBy snd

/// 标点（断行优先位置，且断点在这些字符之后）
let isBreak (u: string) =
    u.Length = 1
    && ("，。！？…、；：」』）—―" |> Seq.contains u.[0])

/// 行首禁则：这些字符不能出现在行首，遇到时不允许在它之前断行（必须跟在上一行末尾）
let isClosing (u: string) =
    u.Length = 1
    && ("，。！？、；：」』）" |> Seq.contains u.[0])

/// 把一条可见单元序列按 ≤maxHalf 切成 k 段（优先标点后断、收尾标点不断行）
let reflowUnits (us: (string * int)[]) (k: int) : string[] =
    let total = us |> Array.sumBy snd
    let target = int (Math.Ceiling(float total / float k))
    let lines = ResizeArray<string>()
    let sb = StringBuilder()
    let mutable w = 0
    let mutable lineIdx = 1
    for i in 0 .. us.Length - 1 do
        let (text, uw) = us.[i]
        let remainLines = k - lineIdx
        let restW = us.[i..] |> Array.sumBy snd
        let mustBreak = w + uw > maxHalf
        // 收尾标点不允许出现在行首：即便超一点也跟上一行（宁可这行略超，也不能丢字/丑断）
        let closing = isClosing text
        let shouldBreak =
            remainLines > 0
            && not closing
            && (mustBreak
                || (w + uw > target - 6
                    && isBreak text
                    && restW <= remainLines * maxHalf
                    && w > maxHalf / 2))
        if shouldBreak then
            lines.Add(sb.ToString())
            sb.Clear() |> ignore
            w <- 0
            lineIdx <- lineIdx + 1
        sb.Append(text) |> ignore
        w <- w + uw
    if sb.Length > 0 then lines.Add(sb.ToString())
    lines.ToArray()

/// 行尾只去空白（**不删任何字符** —— 2026-10-20 教训：先前按"行首禁则"删标点会把「…」吃掉）
let trimLineEnd (s: string) = s.TrimEnd(' ', '\u3000')

let mutable reflowed = 0
let mutable stillOver = 0
let filesChanged = Collections.Generic.HashSet<string>()
let samples = ResizeArray<string>()

for f in Directory.GetFiles(cnDir, "*.json") |> Array.sort do
    let name = Path.GetFileNameWithoutExtension f
    let text = File.ReadAllText f
    let d = JsonDocument.Parse(text, jopts)
    let mutable changed = false
    let sb = StringBuilder()
    let mutable recordCount = 0

    // 逐行文本替换（保留原 JSON 排版）：先收集要改的 key → 新值
    let replacements = Collections.Generic.Dictionary<string, string>()
    for r in d.RootElement.GetProperty("records").EnumerateArray() do
        recordCount <- recordCount + 1
        let key = r.GetProperty("key").GetString()
        let v = (try r.GetProperty("value").GetString() with _ -> "")
        let ls = v.Replace("\r\n", "\n").Split('\n') |> Array.filter (fun l -> l.Trim() <> "")
        if ls.Length > 0 && (ls |> Array.exists (fun l -> width l > maxHalf)) then
            let total = ls |> Array.sumBy width
            let cap = ls.Length * maxHalf
            if total <= cap then
                let us = ls |> Array.collect units
                let newLines = reflowUnits us ls.Length
                if newLines.Length = ls.Length
                   && newLines |> Array.forall (fun l -> width l <= maxHalf) then
                    let cleaned = newLines |> Array.map trimLineEnd
                    let nv = cleaned |> String.concat "\r\n"
                    if nv <> v then
                        replacements.[key] <- nv
                        changed <- true
                        reflowed <- reflowed + 1
                        if samples.Count < 8 then
                            samples.Add(sprintf "%s %s\n      旧: %s\n      新: %s" name key (ls |> String.concat " ⏎ ") (cleaned |> String.concat " ⏎ "))
            else
                stillOver <- stillOver + 1

    if changed && apply then
        // 文本级替换：只改 `"value": "..."` 整行，key 用邻近的 "key" 行定位
        let lines = ResizeArray<string>(text.Replace("\r\n", "\n").Split('\n'))
        for kv in replacements do
            let keyNeedle = sprintf "\"key\": \"%s\"" kv.Key
            let idx = lines |> Seq.tryFindIndex (fun l -> l.Contains keyNeedle)
            match idx with
            | Some i when i + 1 < lines.Count ->
                let indent = lines.[i + 1].Substring(0, lines.[i + 1].IndexOf '"')
                lines.[i + 1] <- indent + "\"value\": " + JsonSerializer.Serialize(kv.Value, JsonSerializerOptions(Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping))
            | _ -> ()
        let nl = if text.Contains "\r\n" then "\r\n" else "\n"
        File.WriteAllText(f, String.Join(nl, lines), UTF8Encoding false)
    if changed then filesChanged.Add name |> ignore

printfn "%s：重排 %d 条记录（%d 个文件）；总宽超容量、需人工改写 %d 条"
    (if apply then "已写入" else "试运行") reflowed filesChanged.Count stillOver
if samples.Count > 0 then
    printfn "\n样例："
    samples |> Seq.iter (printfn "  %s")
if not apply then printfn "\n（加 --apply 才写入）"
