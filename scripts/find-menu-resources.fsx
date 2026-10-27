// 宽松全盘搜索：列出 data 下所有 pac（递归嵌套）里文件名含 title_menu / 開放 / menu 的资源
#load "../scripts/Pac.fsx"

open System
open System.IO
open PacLib

let root = @"<游戏安装目录>\data"

let mutable tmpSeq = 0
let readPac (data: byte[]) =
    tmpSeq <- tmpSeq + 1
    let tmp = Path.Combine(Path.GetTempPath(), sprintf "gakuen-wide-%d.pac" tmpSeq)
    File.WriteAllBytes(tmp, data)
    let r = PacLib.read tmp
    File.Delete tmp
    r

let hits = ResizeArray<string * string * int>()
let allNames = ResizeArray<string * string>()   // (pac, filename) 用于统计

let rec walk (pathLabel: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        allNames.Add(pathLabel, e.Filename)
        if e.Filename.Contains "title_menu" || e.Filename.Contains "menu" then
            hits.Add(pathLabel, e.Filename, e.Length)
        if e.Filename.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) && depth < 5 then
            try walk (pathLabel + "/" + e.Filename) (readPac (PacLib.extract pac e)) (depth + 1)
            with _ -> ()

let pacs = Directory.GetFiles(root, "*.pac", SearchOption.AllDirectories) |> Array.sort
printfn "扫描 %d 个 pac（递归至 5 层）..." pacs.Length
for p in pacs do
    let rel = p.Substring(root.Length + 1).Replace('\\', '/')
    try walk rel (PacLib.read p) 0
    with _ -> ()

printfn "共枚举 %d 个资源文件\n" allNames.Count
printfn "===== 文件名含 title_menu / menu 的（%d 处）=====" hits.Count
hits |> Seq.sortBy (fun (p, f, _) -> f, p)
|> Seq.iter (fun (p, f, sz) -> printfn "  %-46s %-32s %d" p f sz)

// 统计：哪些 pac 含最多的图（找出 UI 相关的容器）
printfn "\n===== 含 .hip 最多的 pac（Top 12）====="
allNames
|> Seq.filter (fun (_, f) -> f.EndsWith(".hip"))
|> Seq.countBy fst
|> Seq.sortByDescending snd
|> Seq.truncate 12
|> Seq.iter (fun (p, n) -> printfn "  %-50s %d" p n)
