// 在游戏目录（或任意目录）中按字节模式搜索文件（原 SearchString.fsx 的泛化版）
//
// 用法:
//   dotnet fsi pac-search.fsx <rootDir> <pattern> [--hex]
//
// 示例:
//   dotnet fsi pac-search.fsx "<游戏安装目录>\...\Getsuei Gakuen -kou-" "00_0-01_01_025B"
//   dotnet fsi pac-search.fsx <dir> "46544100" --hex      # 按 hex 字节串搜索（如 ATF 魔数）

open System
open System.IO
open System.Text

let args = fsi.CommandLineArgs |> Array.skip 1

let usage () =
    eprintfn "用法: dotnet fsi pac-search.fsx <rootDir> <pattern> [--hex]"
    exit 2

if args.Length < 2 then
    usage ()

let root = args[0]
let pattern = args[1]
let mutable asHex = false

for a in args |> Array.skip 2 do
    match a with
    | "--hex" -> asHex <- true
    | other ->
        eprintfn "未知参数: %s" other
        usage ()

if not (Directory.Exists root) then
    eprintfn "目录不存在: %s" root
    exit 1

let needle: byte[] =
    if asHex then
        Convert.FromHexString(pattern)
    else
        Encoding.ASCII.GetBytes(pattern)

printfn "在 %s 中搜索 %s (%d 字节)..." root pattern needle.Length

let containsPattern (candidate: byte[]) (pattern: byte[]) =
    candidate.AsSpan().IndexOf(pattern.AsSpan()) >= 0

let mutable hits = 0

for file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories) do
    let b = File.ReadAllBytes(file)

    if containsPattern b needle then
        printfn "%s" file
        hits <- hits + 1

printfn "完成：%d 个文件命中" hits
