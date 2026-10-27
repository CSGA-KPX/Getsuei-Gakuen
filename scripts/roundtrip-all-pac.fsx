// pac 重打包全量回归：对游戏 data 下全部 .pac 做 解包→重打包 逐字节对比
// 用法: dotnet fsi scripts/roundtrip-all-pac.fsx [目录]   （缺省为游戏 data 目录）
#load "Pac.fsx"

open System
open System.IO
open PacLib

let dataDir =
    match fsi.CommandLineArgs |> Array.skip 1 with
    | [| d |] -> d          // 可传别的目录，例如 work\backup\AE 的原版备份
    | _ -> @"<游戏安装目录>\data"

let pacs = Directory.GetFiles(dataDir, "*.pac", SearchOption.AllDirectories)
printfn "共 %d 个 pac" pacs.Length

let mutable ok = 0
let mutable skipped = 0
let mutable bad = []

for p in pacs do
    // WBND（Wave Bank）等非 FPAC 容器不属于 pac 格式，跳过
    if Text.Encoding.ASCII.GetString(File.ReadAllBytes(p)[0..3]) <> "FPAC" then
        skipped <- skipped + 1
        printfn "SKIP(非FPAC): %s" (Path.GetFileName p)
    else
    try
        let orig = PacLib.read p
        let contents = orig.Entries |> Array.map (PacLib.extract orig)
        let rebuilt = PacLib.build orig contents
        if rebuilt = orig.Bytes then
            ok <- ok + 1
        else
            bad <- Path.GetFileName p :: bad
            eprintfn "MISMATCH: %s (重建 %d / 原 %d)" p rebuilt.Length orig.Bytes.Length
    with ex ->
        bad <- Path.GetFileName p :: bad
        eprintfn "ERROR: %s: %s" p ex.Message

printfn "结果: %d OK / %d SKIP(非FPAC) / %d FAIL（共 %d）" ok skipped bad.Length pacs.Length
if not bad.IsEmpty then
    eprintfn "失败清单: %A" (List.rev bad)
    exit 1
