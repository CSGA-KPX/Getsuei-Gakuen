// pac 解包脚本（docs/formats/pac.md）
//
// 用法:
//   dotnet fsi pac-unpack.fsx <input.pac> [-o <outdir>] [--only .abc,.hip]
//
// 示例:
//   dotnet fsi pac-unpack.fsx "<游戏安装目录>\...\data\story\text\jpn\text_10_1-01.pac" -o dump\text_10_1-01
//   dotnet fsi pac-unpack.fsx <pac> --only .abc,.hip     # 只提取字库相关文件（hip 自动判别 .hip/.tga）

#load "Pac.fsx"

open System
open System.IO
open PacLib

let args = fsi.CommandLineArgs |> Array.skip 1

let usage () =
    eprintfn "用法: dotnet fsi pac-unpack.fsx <input.pac> [-o <outdir>] [--only .abc,.hip]"
    exit 2

if args.Length < 1 then
    usage ()

let input = args[0]

if not (File.Exists input) then
    eprintfn "输入文件不存在: %s" input
    exit 1

let rec parseArgs i (outdir: string option) (only: string list) =
    if i >= args.Length then
        outdir, only
    else
        match args[i] with
        | "-o" | "--output" when i + 1 < args.Length -> parseArgs (i + 2) (Some args[i + 1]) only
        | "--only" when i + 1 < args.Length ->
            let exts = args[i + 1].Split(',') |> Array.map (fun s -> s.Trim().ToLowerInvariant()) |> List.ofArray
            parseArgs (i + 2) outdir exts
        | other ->
            eprintfn "未知参数: %s" other
            usage ()

let outdirOpt, only = parseArgs 1 None []
let outdir = defaultArg outdirOpt (Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input) + "_unpack"))
Directory.CreateDirectory(outdir) |> ignore

let pac = PacLib.read input
let h = pac.Header
printfn "%s: FourCC=%s FileCount=%d DataStart=0x%X TotalSize=%d Style=%A StringSize=%d" input h.FourCC h.FileCount h.DataStart h.TotalSize h.PacStyle h.StringSize

let mutable extracted = 0

for entry in pac.Entries do
    let ext = Path.GetExtension(entry.Filename).ToLowerInvariant()

    let wanted =
        match only with
        | [] -> true
        | exts ->
            List.contains ext exts
            // --only .hip 时也匹配判别后的 .tga（hip 双格式）
            || (ext = ".hip" && List.contains ".hip" exts)

    if wanted then
        let data = PacLib.extract pac entry
        // hip 双格式：按文件头判别真实扩展名
        let outName =
            if ext = ".hip" then Path.GetFileNameWithoutExtension(entry.Filename) + PacLib.hipExtension data
            else Path.GetFileName(entry.Filename)

        let outPath = Path.Combine(outdir, outName)
        File.WriteAllBytes(outPath, data)
        extracted <- extracted + 1
        printfn "  %s (%d 字节)" outName data.Length

printfn "完成：提取 %d/%d 个文件到 %s" extracted pac.Entries.Length outdir
