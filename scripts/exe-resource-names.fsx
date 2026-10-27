// 从 Getsuei.exe 提取「UI 资源名字表」（判定现役 vs 遗留的关键依据）
//
// 背景（2026-10-20 侦查）：exe 的 .rdata 里存着一张 ASCII 资源名清单，条目形如
//   title_menu_01_on / chapter_title_date_1-10_in / open_title_moji_00_effect_%02d.hip / system_menu.pac
// 其中带 %02d 的是 sprintf 格式串（说明该资源按序号批量加载）。
// **名字表命中 = 现役候选；不命中 = 疑似遗留**，最终判定仍需 procmon / 实机探针（见 docs/tables/ui-images-triage.md）。
//
// 用法: dotnet fsi scripts/exe-resource-names.fsx [输出文件]
//   默认输出 work/inventory/exe-resource-names.txt（每行一个名字，去重排序）
// 备注: scripts/dump-resource-names.fsx 是早期版本（只 dump 两个固定偏移池），已被本脚本取代。

open System
open System.IO
open System.Text

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let exe =
    @"<游戏安装目录>\Getsuei.exe"

let outPath =
    match fsi.CommandLineArgs |> Array.skip 1 with
    | [| p |] -> p
    | _ -> Path.Combine(workspace, @"work\inventory\exe-resource-names.txt")

/// 只保留「像资源名」的串：ASCII、长度 ≥6、含 _ 或 . 或 %，且不是 C++ 符号 / DLL / 系统 API
let isResourceName (s: string) =
    s.Length >= 6
    && s.Length <= 120
    && (s.Contains "_" || s.Contains "." || s.Contains "%")
    && not (s.StartsWith ".")
    && not (s.StartsWith "?")
    && not (s.IndexOf("dll", StringComparison.OrdinalIgnoreCase) >= 0)
    && not (s.Contains "@@")
    && not (s.Contains "std")
    && not (s.Contains ":\\")
    && (s |> Seq.forall (fun c -> (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                                 || c = '_' || c = '-' || c = '.' || c = '/' || c = '%'))

let bytes = File.ReadAllBytes exe
let hits = Collections.Generic.SortedSet<string>(StringComparer.Ordinal)

let sb = StringBuilder()
let mutable i = 0
while i < bytes.Length do
    let b = bytes.[i]
    if b >= 0x20uy && b <= 0x7Euy then sb.Append(char b) |> ignore
    else
        if sb.Length >= 6 then
            let s = sb.ToString()
            if isResourceName s then hits.Add s |> ignore
        sb.Clear() |> ignore
    i <- i + 1

Directory.CreateDirectory(Path.GetDirectoryName outPath) |> ignore
File.WriteAllLines(outPath, hits)
printfn "从 %s 提取资源名 %d 条 → %s" (Path.GetFileName exe) hits.Count outPath
