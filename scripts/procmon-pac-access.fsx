// 从 Procmon 日志里提取 Getsuei.exe 实际访问的资源路径 → work/inventory/procmon-accessed-paths-short.txt
//
// 用法：
//   dotnet fsi scripts/procmon-pac-access.fsx                    # 全部 work/procmon/Logfile-*.xml（跳过 *_full-*）
//   dotnet fsi scripts/procmon-pac-access.fsx <日志1.xml> [...]   # 只扫指定文件
//
// 日志命名约定：`Logfile-<抓包日期>.xml`。Procmon 全量日志 500MB+ 且是同一会话的超集，
// 文件名带 `_full` 的默认跳过（2026-10-18 实测：short 名单只来自非 full 那份）。
//
// 产出（ui-image-triage.fsx 的输入）：游戏根目录相对路径，序数排序去重，每行一条。
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let procmonDir = Path.Combine(workspace, "work", "procmon")
let outPath = Path.Combine(workspace, @"work\inventory\procmon-accessed-paths-short.txt")

let files =
    match fsi.CommandLineArgs |> Array.skip 1 with
    | [||] ->
        if Directory.Exists procmonDir then
            Directory.GetFiles(procmonDir, "Logfile-*.xml")
            |> Array.filter (fun f -> not (Path.GetFileName(f).Contains "_full"))
            |> Array.sort
            |> Array.toList
        else []
    | args -> args |> Array.map Path.GetFullPath |> Array.toList

if files.IsEmpty then
    eprintfn "没找到日志：把 Procmon 导出的 XML 放到 %s（命名 Logfile-<日期>.xml）" procmonDir
    exit 2

let pathRe = Regex(@"<Path>(.*?)</Path>", RegexOptions.Compiled)
/// 游戏根目录后缀（用于把绝对路径裁成相对路径）
let rootMarker = "Getsuei Gakuen -kou-"

let mutable merged = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
let perFile = ResizeArray<string * int * int>()

for f in files do
    let fi = FileInfo f
    let paths = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    let mutable n = 0
    // 日志是 UTF-8，逐行扫（一个 <event> 跨多行，<Path> 独占一行）
    use sr = new StreamReader(f, Encoding.UTF8)
    let mutable line = sr.ReadLine()
    while not (isNull line) do
        if line.Contains "<Path>" && line.Contains rootMarker then
            let m = pathRe.Match line
            if m.Success then
                let p = m.Groups.[1].Value
                let idx = p.IndexOf(rootMarker, StringComparison.OrdinalIgnoreCase)
                // 保留根目录本身（相对路径为空串）之外的路径，裁掉根目录前缀并转成反斜杠相对路径
                let rel =
                    if idx + rootMarker.Length < p.Length then
                        p.Substring(idx + rootMarker.Length).TrimStart('\\', '/').Replace('/', '\\')
                    else ""
                if rel <> "" then
                    paths.Add rel |> ignore
                    n <- n + 1
        line <- sr.ReadLine()

    let before = merged.Count
    merged.UnionWith paths
    perFile.Add((Path.GetFileName f, paths.Count, merged.Count - before))
    printfn "%-28s %6.1f MB  命中 %6d 条 / 去重 %4d 条 → 新增 %d"
        (Path.GetFileName f) (float fi.Length / 1048576.0) n paths.Count (merged.Count - before)

let all = merged |> Seq.sort |> Array.ofSeq
let dataPaths = all |> Array.filter (fun p -> p.StartsWith("data\\", StringComparison.Ordinal))
let pacs = dataPaths |> Array.filter (fun p -> p.EndsWith(".pac", StringComparison.OrdinalIgnoreCase))
let aePacs = pacs |> Array.filter (fun p -> p.StartsWith("data\\AE\\", StringComparison.OrdinalIgnoreCase))

printfn "\n合并去重 %d 条路径，其中 data\\ 下 %d 条（.pac %d 个，data\\AE 下 %d 个）"
    all.Length dataPaths.Length pacs.Length aePacs.Length

printfn "\n--- data\\AE 下实际加载的 pac ---"
aePacs |> Array.iter (printfn "  %s")

let old =
    if File.Exists outPath then
        File.ReadAllLines outPath |> Array.map (fun s -> s.Trim()) |> Set.ofArray
    else
        Set.empty
let now = all |> Set.ofArray
let added = Set.difference now old
printfn "\n相对上一版 short 名单：新增 %d 条 / 消失 %d 条（下面只列图像相关的新增）" added.Count (Set.difference old now).Count
added
|> Set.filter (fun p ->
    let l = p.ToLowerInvariant()
    l.StartsWith "data\\ae\\" || p.EndsWith(".hip", StringComparison.OrdinalIgnoreCase))
|> Set.iter (printfn "  + %s")

Directory.CreateDirectory(Path.GetDirectoryName outPath) |> ignore
File.WriteAllLines(outPath, all, UTF8Encoding false)
printfn "\n已写出 %s（%d 条）" outPath all.Length
