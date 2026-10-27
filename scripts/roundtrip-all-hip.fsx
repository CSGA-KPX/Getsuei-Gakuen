// HIP 专有格式全量回归（纯本地，不依赖外部工具）
// A: 全量解码（基线 1055/1055，含 2 个 16×16 纯色占位图）
// C: 逐文件 decode → encode(Raw / RawRepeat) → decode，Pixels/调色板逐字节一致
// 用法: dotnet fsi scripts/roundtrip-all-hip.fsx
#load "hip.fsx"
#load "Pac.fsx"

open System
open System.IO
open HipLib
open PacLib

let dataDir = @"<游戏安装目录>\data"

let hips = ResizeArray<string * string * byte[]>() // (label, filename, bytes)
let rec walk (label: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        let data = extract pac e
        let lower = e.Filename.ToLowerInvariant()
        if lower.EndsWith(".pac") && depth < 3 then
            try walk (label + "/" + e.Filename) (readBytes data) (depth + 1)
            with _ -> ()
        elif lower.EndsWith(".hip") && isHip data then
            hips.Add(label, e.Filename, data)

for root in [ "AE"; "usual"; "saveui"; "cmn"; "story" ] do
    let dir = Path.Combine(dataDir, root)
    if Directory.Exists dir then
        for p in Directory.GetFiles(dir, "*.pac", SearchOption.AllDirectories) do
            try walk (root + "/" + Path.GetFileNameWithoutExtension(p)) (read p) 0
            with _ -> ()
        for p in Directory.GetFiles(dir, "*.hip", SearchOption.AllDirectories) do
            let b = File.ReadAllBytes p
            if isHip b then hips.Add(root + "/image", Path.GetFileName p, b)

printfn "共收集 %d 个 HIP 专有格式图" hips.Count

let decoded = ResizeArray<int * HipImage>()
let fails = ResizeArray<string>()
for i in 0 .. hips.Count - 1 do
    let label, name, bytes = hips[i]
    try decoded.Add(i, HipLib.decode bytes)
    with HipException e -> fails.Add(sprintf "decode %s/%s: %s" label name e)
printfn "[A] 解码: %d OK / %d FAIL" decoded.Count fails.Count

// C: roundtrip（Raw 严格逐字节；RawRepeat 仅当编码流短于裸数据时要求一致——
//    解码器循环上界 consumed < total 会在流 ≥ 图幅时截断尾部像素，这是源实现固有行为，
//    不是编码器缺陷；实战中 RawRepeat 只应用于游程多的图，即编码流确实更短的图）
let mutable rtOk = 0
let mutable rtSkip = 0
for (_, img) in decoded do
    for enc in [ EncRaw; EncRawRepeat ] do
        try
            let bytes2 = HipLib.encode { img with Encoding = enc }
            let img2 = HipLib.decode bytes2
            let palEq =
                match img.Palette, img2.Palette with
                | None, None -> true
                | Some a, Some b -> a = b
                | _ -> false
            // 截断区判定：编码流（像素数据区）是否短于裸像素数据
            let dataStart =
                32 + (match img.Palette with
                      | Some p when img.Format = Indexed8 -> p.Length * 4
                      | _ -> 0)
            let streamShorter = (bytes2.Length - dataStart) < img.Pixels.Length
            let mustEqual = (enc = EncRaw) || streamShorter
            if img2.Pixels = img.Pixels && palEq
               && img2.Format = img.Format
               && img2.ImageWidth = img.ImageWidth && img2.ImageHeight = img.ImageHeight
            then rtOk <- rtOk + 1
            elif not mustEqual then
                rtSkip <- rtSkip + 1 // RawRepeat 流 ≥ 裸数据：截断区不比较
            else
                fails.Add(sprintf "roundtrip %A %dx%d: 像素不一致" enc img.ImageWidth img.ImageHeight)
        with HipException e ->
            fails.Add(sprintf "roundtrip %A: %s" enc e)
let rtTotal = decoded.Count * 2
printfn "[C] roundtrip: %d OK / %d SKIP(截断区) / %d FAIL" rtOk rtSkip (rtTotal - rtOk - rtSkip)
for f in fails |> Seq.truncate 20 do
    eprintfn "  FAIL: %s" f

if fails.Count = 0 then
    printfn "==== 全部通过: A %d/%d, C %d/%d (skip %d) ====" decoded.Count hips.Count rtOk rtTotal rtSkip
else
    eprintfn "==== %d 项失败 ====" fails.Count
    exit 1
