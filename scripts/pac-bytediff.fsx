// pac 纯字节 diff（**不经过任何解析器**，用于给 pac-diff 的结论做最终裁判）
//
// 为什么需要：pac-diff 与 roundtrip 都会用 PacLib 解析文件。如果解析器本身把值改了
// （2026-10-20 就踩过：Shift-JIS 文件名 0x82 0x6B 被 UTF-8 解码成 U+FFFD 再写回），
// 两边都用同一个坏解析器 → 差异被"抵消" → 假阴性。本脚本只比原始字节，不做解码。
//
// 用法:
//   dotnet fsi scripts/pac-bytediff.fsx <原版.pac> <新版.pac>
//   dotnet fsi scripts/pac-bytediff.fsx --backup <相对 data\AE 的路径>
//
// 输出: 差异段（合并相邻 <32 字节的差异）、首尾字节对照。退出码 0 恒为 0，看内容判断。
open System
open System.IO

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let aeRoot = @"<游戏安装目录>\data\AE"
let backupRoot = Path.Combine(workspace, "work", "backup", "AE")

let args = fsi.CommandLineArgs |> Array.skip 1
let paths =
    match args with
    | [| "--backup"; rel |] ->
        Some(Path.Combine(backupRoot, rel.Replace('/', '\\')), Path.Combine(aeRoot, rel.Replace('/', '\\')))
    | [| a; b |] -> Some(a, b)
    | _ -> None

match paths with
| None ->
    eprintfn "用法: dotnet fsi scripts/pac-bytediff.fsx <原版.pac> <新版.pac>"
    eprintfn "      dotnet fsi scripts/pac-bytediff.fsx --backup <相对 data\\AE 的路径>"
    exit 2
| Some(pa, pb) ->
    if not (File.Exists pa) then eprintfn "缺少 %s" pa; exit 2
    if not (File.Exists pb) then eprintfn "缺少 %s" pb; exit 2
    let a = File.ReadAllBytes pa
    let b = File.ReadAllBytes pb
    printfn "原版 %s  %d 字节" (Path.GetFileName pa) a.Length
    printfn "新版 %s  %d 字节  差值 %+d" (Path.GetFileName pb) b.Length (b.Length - a.Length)

    // 收集连续差异段
    let diffs = ResizeArray<int * int>()
    let n = min a.Length b.Length
    let mutable i = 0
    while i < n do
        if a.[i] <> b.[i] then
            let s = i
            while i < n && a.[i] <> b.[i] do i <- i + 1
            diffs.Add((s, i))
        else
            i <- i + 1

    // 合并间隔 < 32 的段
    let merged = ResizeArray<int * int>()
    for (s, e) in diffs do
        if merged.Count > 0 && s - (snd merged.[merged.Count - 1]) < 32 then
            merged.[merged.Count - 1] <- (fst merged.[merged.Count - 1], e)
        else
            merged.Add((s, e))

    let hex (bs: byte[]) (s: int) (len: int) =
        let e = min (s + len) bs.Length
        bs.[s..e - 1] |> Array.map (fun x -> x.ToString("x2")) |> String.concat ""

    let ascii (bs: byte[]) (s: int) (len: int) =
        let e = min (s + len) bs.Length
        bs.[s..e - 1] |> Array.map (fun x -> if x >= 32uy && x < 127uy then char x else '.') |> Array.ofSeq |> String

    printfn "\n差异段 %d 个（原始非连续差异字节 %d）" merged.Count (diffs |> Seq.sumBy (fun (s, e) -> e - s))
    for (s, e) in merged do
        printfn "  0x%06X..0x%06X  (%d 字节)" s e (e - s)
        printfn "    原: %s" (hex a s 32)
        printfn "    新: %s" (hex b s 32)
        let ta, tb = ascii a s 32, ascii b s 32
        if ta <> tb then
            printfn "    原a: %s" ta
            printfn "    新a: %s" tb
    if a.Length <> b.Length then
        printfn "\n尾部长度差：原 %d / 新 %d" a.Length b.Length
    if merged.Count = 0 && a.Length = b.Length then
        printfn "\n✅ 两文件逐字节完全相同"
