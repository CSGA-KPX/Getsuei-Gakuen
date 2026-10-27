// pac 重打包脚本（docs/formats/pac.md）
//
// 用法:
//   dotnet fsi pac-pack.fsx verify <orig.pac>            # 内存中 解包→重打包 逐字节对比
//   dotnet fsi pac-pack.fsx pack <orig.pac> <filesdir> -o <out.pac>
//       # 以 orig.pac 为清单来源（条目顺序/FileId/StringSize/PacStyle/FourCC），
//       # 文件内容从 filesdir 按条目名读取（.hip 自动回退 .tga 双格式判别名）
//
// 打包约定（2026-10-18 全量实测验证，见 docs/formats/pac.md §3）：
//   - 数据区每条目起始偏移对齐 16 字节，不足补 0；TotalSize 也对齐 16（尾部补 0）。
//   - FileNameHash = hashFileName(文件名小写)（原名含大写 "_File.bin" 的条目实测如此）。

#load "Pac.fsx"

open System
open System.IO
open PacLib

let args = fsi.CommandLineArgs |> Array.skip 1

let usage () =
    eprintfn "用法:"
    eprintfn "  dotnet fsi pac-pack.fsx verify <orig.pac>"
    eprintfn "  dotnet fsi pac-pack.fsx pack <orig.pac> <filesdir> -o <out.pac>"
    exit 2

if args.Length < 2 then
    usage ()

match args[0] with
| "verify" ->
    let orig = PacLib.read args[1]
    let contents = orig.Entries |> Array.map (PacLib.extract orig)
    let rebuilt = build orig contents
    let ok = rebuilt = orig.Bytes
    printfn "%s: %s (重建 %d 字节 / 原 %d 字节)" args[1] (if ok then "ROUNDTRIP OK" else "MISMATCH") rebuilt.Length orig.Bytes.Length
    if not ok then
        // 定位首个差异
        let n = min rebuilt.Length orig.Bytes.Length
        let diff =
            Seq.init n id |> Seq.tryFind (fun i -> rebuilt[i] <> orig.Bytes[i])
        match diff with
        | Some i -> eprintfn "首个差异偏移=0x%X 重建=0x%02X 原=0x%02X" i (int rebuilt[i]) (int orig.Bytes[i])
        | None -> eprintfn "长度不同"
        exit 1

| "pack" ->
    if args.Length < 3 then usage ()
    let origPath, filesDir = args[1], args[2]
    let outOpt =
        let idx = Array.tryFindIndex (fun a -> a = "-o" || a = "--output") args
        match idx with
        | Some i when i + 1 < args.Length -> Some args[i + 1]
        | _ -> None
    let outPath =
        match outOpt with
        | Some o -> o
        | None -> eprintfn "缺少 -o <out.pac>"; exit 2

    let orig = PacLib.read origPath
    let contents =
        orig.Entries
        |> Array.map (fun e ->
            let p1 = Path.Combine(filesDir, e.Filename)
            let path =
                if File.Exists p1 then p1
                else
                    // hip 双格式：解包时 .hip 可能按实际格式存成 .tga（或反之）
                    let stem = Path.GetFileNameWithoutExtension(e.Filename)
                    let alt = Path.Combine(filesDir, stem + (if Path.GetExtension(e.Filename) = ".hip" then ".tga" else ".hip"))
                    if File.Exists alt then alt
                    else failwithf "filesdir 中找不到条目文件: %s" e.Filename
            File.ReadAllBytes path)
    let bytes = build orig contents
    File.WriteAllBytes(outPath, bytes)
    printfn "已打包 %s -> %s (%d 字节, %d 个文件)" filesDir outPath bytes.Length orig.Entries.Length

| other ->
    eprintfn "未知子命令: %s" other
    usage ()
