// pac 逐字段 diff：验证「改过的 pac 只改了该改的条目」
//
// 为什么需要它：roundtrip（解包→重打包 逐字节一致）只证明「读回来的值 → 写回去」自洽，
// 证明不了「值本身没被我们改写」。2026-10-20 就踩过：`PacLib.build` 重算 FileNameHash，
// 对 options_screen.pac 内层 8 条名字被替换成 U+FFFD 的 `window_frame_?k_light_*` 算出不同哈希，
// 一进设置菜单闪退，而全量 roundtrip 2201 个 pac 全绿（它们名字都是 ASCII）。
// ⇒ 改完 pac 后用本脚本与原版比一遍，确认差异只落在预期条目上。
//
// 用法:
//   dotnet fsi scripts/pac-diff.fsx <原版.pac> <新版.pac> [--depth 3]
//   dotnet fsi scripts/pac-diff.fsx --backup <相对 data\AE 的路径>      # 自动用 work/backup/AE 对比游戏目录
//
// 输出: 头字段 / 条目名 / FileId / Length / FileNameHash / 内容 SHA256 的差异；内层 .pac 递归展开。
// 退出码: 0 = 无差异或仅有内容差异；1 = 出现「结构差异」（名字/ID/哈希变了，通常是 bug）。
open System
open System.IO
open System.Security.Cryptography
#load "Pac.fsx"
open PacLib

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let aeRoot = @"<游戏安装目录>\data\AE"
let backupRoot = Path.Combine(workspace, "work", "backup", "AE")

let sha (b: byte[]) =
    use h = SHA256.Create()
    h.ComputeHash b |> Array.map (fun x -> x.ToString("x2")) |> Seq.truncate 8 |> String.concat ""

let mutable structuralDiffs = 0
let mutable contentDiffs = 0

/// 递归比较两个 pac，返回是否发现结构差异
let rec compare (label: string) (a: PacFile) (b: PacFile) (depth: int) =
    let indent = String.replicate (depth * 2) " "
    printfn "%s%s" indent label
    if a.Entries.Length <> b.Entries.Length then
        structuralDiffs <- structuralDiffs + 1
        printfn "%s  ❌ FileCount %d → %d" indent a.Entries.Length b.Entries.Length
    if a.Header.StringSize <> b.Header.StringSize then
        structuralDiffs <- structuralDiffs + 1
        printfn "%s  ❌ StringSize %d → %d" indent a.Header.StringSize b.Header.StringSize
    if int a.Header.PacStyle <> int b.Header.PacStyle then
        structuralDiffs <- structuralDiffs + 1
        printfn "%s  ❌ PacStyle %d → %d" indent (int a.Header.PacStyle) (int b.Header.PacStyle)
    let n = min a.Entries.Length b.Entries.Length
    for i in 0 .. n - 1 do
        let ea = a.Entries.[i]
        let eb = b.Entries.[i]
        let ca = extract a ea
        let cb = extract b eb
        let nameDiff = ea.Filename <> eb.Filename
        let idDiff = ea.FileId <> eb.FileId
        let hashDiff = ea.FileNameHash <> eb.FileNameHash
        let lenDiff = ea.Length <> eb.Length
        let contentDiff = sha ca <> sha cb
        if nameDiff || idDiff || hashDiff then
            structuralDiffs <- structuralDiffs + 1
            printfn "%s  ❌ [%d] 结构差异 %s" indent i ea.Filename
            if nameDiff then printfn "%s       名字 %s → %s" indent ea.Filename eb.Filename
            if idDiff then printfn "%s       FileId %d → %d" indent ea.FileId eb.FileId
            if hashDiff then
                printfn "%s       FileNameHash %s → %s"
                    indent (BitConverter.ToString ea.FileNameHash) (BitConverter.ToString eb.FileNameHash)
        elif lenDiff || contentDiff then
            contentDiffs <- contentDiffs + 1
            printfn "%s  • [%d] %s  %d → %d 字节（内容变了）" indent i ea.Filename ea.Length eb.Length
            // 内容变了但仍是 pac → 递归进去看
            if ea.Filename.ToLowerInvariant().EndsWith ".pac" && depth < 6 then
                try compare (ea.Filename + ":") (PacLib.readBytes ca) (PacLib.readBytes cb) (depth + 1)
                with ex -> printfn "%s      ⚠️ 内层解析失败: %s" indent ex.Message

let args = fsi.CommandLineArgs |> Array.skip 1
let depthLimit =
    match args |> Array.tryFindIndex ((=) "--depth") with
    | Some i when i + 1 < args.Length -> Int32.Parse args.[i + 1]
    | _ -> 3

let paths =
    match args with
    | [| "--backup"; rel |] -> Some(Path.Combine(backupRoot, rel.Replace('/', '\\')), Path.Combine(aeRoot, rel.Replace('/', '\\')))
    | [| a; b |] when not (a.StartsWith "--") -> Some(a, b)
    | _ -> None

match paths with
    | None ->
        eprintfn "用法: dotnet fsi scripts/pac-diff.fsx <原版.pac> <新版.pac> [--depth N]"
        eprintfn "      dotnet fsi scripts/pac-diff.fsx --backup <相对 data\\AE 的路径>"
        exit 2
    | Some(pa, pb) ->
        if not (File.Exists pa) then eprintfn "缺少 %s" pa; exit 2
        if not (File.Exists pb) then eprintfn "缺少 %s" pb; exit 2
        printfn "原版: %s\n新版: %s\n" pa pb
        compare (Path.GetFileName pa) (PacLib.read pa) (PacLib.read pb) 0
        printfn "\n结论: 结构差异 %d / 内容差异 %d" structuralDiffs contentDiffs
        if structuralDiffs > 0 then
            eprintfn "⚠️ 有结构差异（名字/ID/哈希变了）—— 通常意味着写回逻辑有 bug，实机会崩。"
            exit 1
        else
            printfn "✅ 结构一致，差异仅在预期条目内容"
