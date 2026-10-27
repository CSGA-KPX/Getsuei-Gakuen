// 全盘搜：哪些 pac（含嵌套层）里有 title_menu_00.hip / title_menu_01.hip
// 目的：测试补丁没生效 → 确认游戏实际读的图在哪个 pac
#load "../scripts/Pac.fsx"

open System
open System.IO
open PacLib

let root = @"<游戏安装目录>\data"
let targets = [ "title_menu_01_on.hip"; "title_menu_01_off.hip"; "title_menu_02_on.hip"; "title_menu_02_off.hip"; "getuei_title_rogo.hip"; "title_menu_mark_01.hip" ]

let mutable tmpSeq = 0
let readPac (data: byte[]) =
    tmpSeq <- tmpSeq + 1
    let tmp = Path.Combine(Path.GetTempPath(), sprintf "gakuen-search-%d.pac" tmpSeq)
    File.WriteAllBytes(tmp, data)
    let r = PacLib.read tmp
    File.Delete tmp
    r

let hits = ResizeArray<string * string * int>()   // (pac 路径, 命中文件名, 大小)

let rec walk (pathLabel: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        if targets |> List.contains e.Filename then
            hits.Add(pathLabel, e.Filename, e.Length)
        elif e.Filename.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) && depth < 3 then
            try walk (pathLabel + "/" + e.Filename) (readPac (PacLib.extract pac e)) (depth + 1)
            with _ -> ()

let pacs = Directory.GetFiles(root, "*.pac", SearchOption.AllDirectories) |> Array.sort
printfn "扫描 %d 个 pac（递归嵌套）..." pacs.Length

for p in pacs do
    let rel = p.Substring(root.Length + 1).Replace('\\', '/')
    try walk rel (PacLib.read p) 0
    with _ -> ()

printfn "\n命中 %d 处：\n" hits.Count
hits
|> Seq.sortBy (fun (p, f, _) -> p, f)
|> Seq.iter (fun (p, f, sz) -> printfn "  %-40s %-20s %d 字节" p f sz)

// 顺带：也搜文件名里带 title 的 pac，看有没有别的候选
printfn "\n文件名含 title 的 pac："
pacs |> Array.filter (fun p -> Path.GetFileName(p).Contains "title")
|> Array.iter (fun p -> printfn "  %s (%d 字节)" (p.Substring(root.Length + 1)) (FileInfo(p).Length))
