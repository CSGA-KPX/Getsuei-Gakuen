// UI 文字图甄别：data/AE 内每个 .hip × exe 资源名字表 × **aex 纹理名表** × procmon 实机访问 → 现役 / 遗留判定
//
// 前置:
//   dotnet fsi scripts/exe-resource-names.fsx        → work/inventory/exe-resource-names.txt
//   dotnet fsi scripts/aex-names.fsx                 → work/inventory/aex-texture-names.txt（2026-10-20 新增判据）
//   work/inventory/ae-image-inventory-v2.json        ← scripts/ae-rescan.fsx
//   dotnet fsi scripts/procmon-pac-access.fsx        → work/inventory/procmon-accessed-paths-short.txt（必须先在游戏里跑过目标界面）
//
// 输出: work/inventory/ui-images-verdict.tsv（verdict / pack / size / file / aex）
// 判定等级（强→弱）:
//   A 名字命中 + 所属 pac 运行时被加载  → 铁证现役
//   B 所属 pac 运行时被加载（procmon）  → 现役（图名可能由代码拼接）
//   C 图名命中名字表（含 %02d 通配）    → 强候选（本次会话未进该界面）
//   D pac 名命中名字表                  → 候选
//   E 同 pac 内其他文件命中             → 弱候选
//   F 全部未命中                        → 疑似遗留（仍需实机探针确认）
//
// ⚠️「名字表」有两处来源，第 5 列 aex=Y 表示命中 **aex 纹理名表**：
//    UI 图的名字不写在 exe 里，而是同 pac 的 .aex（ArcSys UI 定义）内（ASCII 串，如
//    `TEX\system-menu_moji_01.png`）。这解释了旧版「文字图不在 exe 表、框线却在」的假象
//    （见 docs/tables/ui-images-triage.md §4.2）。
// ⚠️ A 也不等于「里面每张图都在用」：title/UI_02_title.pac 被加载，但 pac 内
//    title_menu_00…71 实机改图无效（现役是 data/story/image/ 下的散装 18 张）。

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let invPath = Path.Combine(workspace, @"work\inventory\ae-image-inventory-v2.json")
let exeNamesPath = Path.Combine(workspace, @"work\inventory\exe-resource-names.txt")
let aexNamesPath = Path.Combine(workspace, @"work\inventory\aex-texture-names.txt")
let procmonPath = Path.Combine(workspace, @"work\inventory\procmon-accessed-paths-short.txt")

for p in [ invPath; exeNamesPath; aexNamesPath; procmonPath ] do
    if not (File.Exists p) then
        eprintfn "缺少输入文件: %s（见脚本头部前置说明）" p
        exit 2

/// aex 纹理名表：`<外层pac相对路径>\t<图中基名>`
let aexNames =
    File.ReadAllLines aexNamesPath
    |> Array.choose (fun l ->
        let f = l.Split('\t')
        if f.Length >= 2 then Some(f.[1].Trim()) else None)
    |> Array.filter (fun s -> s.Length > 0)

let aexSet = Collections.Generic.HashSet<string>(aexNames, StringComparer.OrdinalIgnoreCase)

let exeNames =
    File.ReadAllLines exeNamesPath |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s.Length > 0)

let exeSet = Collections.Generic.HashSet<string>(exeNames, StringComparer.Ordinal)
/// sprintf 格式串（%02d / %d）转正则
let wildcards =
    exeNames
    |> Array.filter (fun s -> s.Contains "%")
    |> Array.map (fun s ->
        let pat = "^" + Regex.Escape(s).Replace("%02d", "\\d{2}").Replace("%d", "\\d+") + "$"
        Regex(pat, RegexOptions.IgnoreCase))

let procmon =
    File.ReadAllLines procmonPath
    |> Array.map (fun s -> s.Trim().ToLowerInvariant())
    |> Array.filter (fun s -> s.StartsWith "data\\")

let procmonPacFiles =
    procmon |> Array.filter (fun p -> p.EndsWith ".pac")
    |> fun a -> Collections.Generic.HashSet<string>(a |> Array.map Path.GetFileName, StringComparer.OrdinalIgnoreCase)

let procmonHipFiles =
    procmon |> Array.filter (fun p -> p.EndsWith ".hip")
    |> fun a -> Collections.Generic.HashSet<string>(a |> Array.map Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)

let doc = JsonDocument.Parse(File.ReadAllText(invPath, Encoding.UTF8))
let files =
    [| for e in doc.RootElement.GetProperty("files").EnumerateArray() ->
         e.GetProperty("pack").GetString(), e.GetProperty("file").GetString(), e.GetProperty("size").GetInt64(), e.GetProperty("format").GetString() |]
    |> Array.filter (fun (_, _, _, fmt) -> fmt = "HIP")

printfn "AE 内 HIP %d ；exe 资源名 %d ；aex 纹理名 %d ；procmon pac %d / hip %d"
    files.Length exeNames.Length aexSet.Count procmonPacFiles.Count procmonHipFiles.Count

/// 图名（去 .hip）是否出现在 exe 名字表 / procmon / aex 纹理名表
let hitSource (file: string) =
    let n = if file.EndsWith ".hip" then file.Substring(0, file.Length - 4) else file
    let inExe = exeSet.Contains n || wildcards |> Array.exists (fun r -> r.IsMatch n)
    let inAex = aexSet.Contains n
    inExe || inAex || procmonHipFiles.Contains n, inAex

let nameHit (file: string) = file |> hitSource |> fst

/// "adv/UI_00_key_help.pac/tex.pac" → "UI_00_key_help.pac"
let outerPacName (pack: string) =
    let parts = pack.Split('/')
    if parts.Length >= 2 then parts.[parts.Length - 2] else pack

let byPack = files |> Array.groupBy (fun (p, _, _, _) -> p)
let rows = ResizeArray<string * string * int64 * string * bool>()   // pack, file, size, verdict, aexHit

for (pack, items) in byPack do
    let pac = outerPacName pack
    let pacInProcmon = procmonPacFiles.Contains pac
    let pacInExe = exeSet.Contains pac || exeSet.Contains(pac + ".gz")
    let siblingHit = items |> Array.exists (fun (_, f, _, _) -> nameHit f)
    for (_, f, size, _) in items do
        let hit, inAex = hitSource f
        let verdict =
            if hit && pacInProcmon then "A"
            elif pacInProcmon then "B"
            elif hit then "C"
            elif pacInExe then "D"
            elif siblingHit then "E"
            else "F"
        rows.Add((pack, f, size, verdict, inAex))

printfn "\n--- 按文件 ---"
rows |> Seq.groupBy (fun (_, _, _, v, _) -> v) |> Seq.sortBy fst
|> Seq.iter (fun (k, g) -> printfn "  %s  %d" k (Seq.length g))

let aexOnly = rows |> Seq.filter (fun (_, _, _, _, a) -> a) |> Seq.length
printfn "  其中名字命中来自 aex 纹理名表：%d" aexOnly

printfn "\n--- 按 pac（取最强判定） ---"
byPack
|> Array.map (fun (pack, items) ->
    let vs = rows |> Seq.filter (fun (p, _, _, _, _) -> p = pack) |> Seq.map (fun (_, _, _, v, _) -> v) |> Seq.toArray
    (vs |> Array.min, pack, items.Length))
|> Array.sortBy (fun (b, p, _) -> b, p)
|> Array.iter (fun (b, p, n) -> printfn "  [%s] %-56s %d 文件" b p n)

let outPath = Path.Combine(workspace, @"work\inventory\ui-images-verdict.tsv")
File.WriteAllLines(
    outPath,
    rows |> Seq.map (fun (p, f, s, v, a) -> sprintf "%s\t%s\t%d\t%s\t%s" v p (int s) f (if a then "aex" else "")))
printfn "\n已写出 %s" outPath
