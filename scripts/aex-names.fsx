// 从 data\AE 各 pac 内嵌的 .aex（ArcSys UI 定义）提取纹理名表
//
// 背景（2026-10-20 验证 ✅）：UI 图的名字**不写在 exe 里**，而是写在同 pac 的 aex 数据中
// （ASCII 单字节串，形如 `TEX\system-menu_moji_01.png`）—— 这解释了
// docs/tables/ui-images-triage.md §4.2「文字图不在 exe 名字表、框线却在」的现象：
// 不是 Font00 实时渲染，而是名字来自 aex。aex 内无任何日文/全角文本（已验证），
// 只有纹理名与布局数据。
//
// 输出: work/inventory/aex-texture-names.txt  每行 `<外层pac相对路径>\t<图中基名>`
// 用法: dotnet fsi scripts/aex-names.fsx
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
#load "./Pac.fsx"
open PacLib

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let aeRoot = @"<游戏安装目录>\data\AE"
let outPath = Path.Combine(workspace, @"work\inventory\aex-texture-names.txt")

/// aex 里的名字是 ASCII 单字节串，用白名单字符集过滤二进制噪声
let nameRe = Regex(@"^[A-Za-z0-9_][A-Za-z0-9_\-\\/\.]{2,79}$", RegexOptions.Compiled)

/// 归一化：去 TEX\ 前缀与 .png 后缀 → 图中基名
let normalize (s: string) =
    let mutable t = s.Replace('/', '\\')
    if t.StartsWith("TEX\\", StringComparison.OrdinalIgnoreCase) then t <- t.Substring 4
    if t.EndsWith(".png", StringComparison.OrdinalIgnoreCase) then t <- t.Substring(0, t.Length - 4)
    if t.EndsWith(".hip", StringComparison.OrdinalIgnoreCase) then t <- t.Substring(0, t.Length - 4)
    t

/// 提取一段字节里的 ASCII 可打印串
let asciiStrings (b: byte[]) =
    let res = ResizeArray<string>()
    let sb = StringBuilder()
    let flush () =
        if sb.Length >= 3 then res.Add(sb.ToString())
        sb.Clear() |> ignore
    for x in b do
        if x >= 0x20uy && x < 0x7Fuy then sb.Append(char x) |> ignore
        else flush ()
    flush ()
    res

let rows = ResizeArray<string * string>()
let mutable aexCount = 0

/// 递归展开嵌套 pac，找 .aex 成员
let rec walk (outerRel: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        let data = extract pac e
        let lower = e.Filename.ToLowerInvariant()
        if lower.EndsWith ".pac" && depth < 3 then
            walk outerRel (readBytes data) (depth + 1)
        elif lower.EndsWith ".aex" then
            aexCount <- aexCount + 1
            let names =
                asciiStrings data
                |> Seq.filter (fun s -> nameRe.IsMatch s)
                |> Seq.filter (fun s -> not (s.Contains "\\") || s.StartsWith("TEX\\", StringComparison.OrdinalIgnoreCase))
                // 只要带扩展名的引用，或形如 xxx_moji_01 这类资源名前缀
                |> Seq.map normalize
                |> Seq.distinct
            for n in names do rows.Add((outerRel, n))

let pacs = Directory.GetFiles(aeRoot, "*.pac", SearchOption.AllDirectories) |> Array.sort
for p in pacs do
    let rel = Path.GetRelativePath(aeRoot, p).Replace('\\', '/')
    try walk rel (PacLib.read p) 0
    with ex -> eprintfn "⚠️ %s 读取失败: %s" rel ex.Message

Directory.CreateDirectory(Path.GetDirectoryName outPath) |> ignore
File.WriteAllLines(outPath, rows |> Seq.map (fun (p, n) -> p + "\t" + n) |> Seq.distinct, UTF8Encoding false)

let uniqNames = rows |> Seq.map snd |> Seq.distinct |> Seq.length
printfn "扫描 %d 个 pac / %d 个 aex，提取 %d 条引用（唯一图名 %d 个）" pacs.Length aexCount rows.Count uniqNames
printfn "已写出 %s" outPath

printfn "\n--- 抽查：system_menu / back_log / options_screen 的 moji 引用 ---"
rows
|> Seq.filter (fun (p, n) -> n.Contains "moji" && (p.Contains "system_menu" || p.Contains "back_log" || p.Contains "options_screen"))
|> Seq.map (fun (p, n) -> sprintf "  %-34s %s" p n)
|> Seq.sort
|> Seq.truncate 40
|> Seq.iter (printfn "%s")
