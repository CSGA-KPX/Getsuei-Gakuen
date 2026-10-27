// 导出 AE pac 内 HIP 图 → PNG（重制 UI 文字图的第一步：先看清每张图是什么）
//
// 用法:
//   dotnet fsi scripts/ae-export.fsx <相对 data\AE 的 pac> <成员> [成员...]
//   dotnet fsi scripts/ae-export.fsx option/options_screen.pac --all
//   dotnet fsi scripts/ae-export.fsx option/options_screen.pac --filter moji     # 名字含 moji
//
// 输出: work/ae-png/<pac 基名>/<成员名>.png（alpha 合成到深灰底，白字看得清）
//
// 说明: AE 下每个 pac 是「aex.pac（界面定义）+ tex.pac（图）」双层嵌套，本脚本自动取 tex.pac。
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "Pac.fsx"
#load "hip.fsx"

open System
open System.IO
open HipLib
open PacLib
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let aeRoot = @"<游戏安装目录>\data\AE"
let outRoot = Path.Combine(workspace, "work", "ae-png")

/// HipImage → PNG（默认 alpha 合成到深灰底；--rgba 时保留原始 alpha，供 alpha 分析）
let keepAlpha = fsi.CommandLineArgs |> Array.contains "--rgba"
let savePng (img: HipImage) (path: string) =
    let bg = 40uy
    let blend (c: Rgba32) =
        let a = int c.A
        if keepAlpha then c
        else
            Rgba32(byte ((int c.R * a + int bg * (255 - a)) / 255),
                  byte ((int c.G * a + int bg * (255 - a)) / 255),
                  byte ((int c.B * a + int bg * (255 - a)) / 255),
                  255uy)
    use png = new Image<Rgba32>(img.ImageWidth, img.ImageHeight)
    let frame = png.Frames.RootFrame
    let w, h, px = img.ImageWidth, img.ImageHeight, img.Pixels
    match img.Format with
    | Indexed8 ->
        let pal = img.Palette.Value
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let e = pal.[int px.[y * w + x]]
                frame.[x, y] <- blend (Rgba32(e.[2], e.[1], e.[0], e.[3]))
    | Argb32 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 4
                frame.[x, y] <- blend (Rgba32(px.[p + 2], px.[p + 1], px.[p], px.[p + 3]))
    | Gray16 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 2
                let v16 = int px.[p] ||| (int px.[p + 1] <<< 8)
                let v = byte ((v16 * 255 + 32767) / 65535)
                frame.[x, y] <- Rgba32(v, v, v, 255uy)
    | Rgb565 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 2
                let v = uint16 px.[p] ||| (uint16 px.[p + 1] <<< 8)
                let s5 (n: uint16) = byte ((n * 255us + 15us) / 31us)
                let s6 (n: uint16) = byte ((n * 255us + 31us) / 63us)
                frame.[x, y] <- Rgba32(s5 ((v >>> 11) &&& 0x1Fus), s6 ((v >>> 5) &&& 0x3Fus), s5 (v &&& 0x1Fus), 255uy)
    | FormatUnknown _ -> ()
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
    png.Save path

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 2 then
    eprintfn "用法: dotnet fsi scripts/ae-export.fsx <相对 data\\AE 的 pac> <成员...|--all|--filter 子串>"
    exit 2

let pacRel = args.[0]
let pacPath = Path.Combine(aeRoot, pacRel.Replace('/', '\\'))
if not (File.Exists pacPath) then
    eprintfn "找不到 %s" pacPath
    exit 2

let outer = PacLib.read pacPath
let inner =
    match outer.Entries |> Array.tryFind (fun e -> e.Filename = "tex.pac") with
    | Some e -> PacLib.readBytes (PacLib.extract outer e)
    | None -> outer

let targets =
    match args.[1].ToLowerInvariant() with
    | "--all" -> inner.Entries |> Array.map (fun e -> e.Filename)
    | "--filter" when args.Length > 2 ->
        let sub = args.[2]
        inner.Entries |> Array.map (fun e -> e.Filename) |> Array.filter (fun n -> n.Contains(sub, StringComparison.OrdinalIgnoreCase))
    | _ -> args |> Array.skip 1

let outDir =
    Path.Combine(outRoot, Path.GetFileNameWithoutExtension pacRel + (if keepAlpha then "-rgba" else ""))
let mutable ok = 0
for m in targets do
    match inner.Entries |> Array.tryFind (fun e -> e.Filename = m) with
    | None -> eprintfn "  缺失 %s" m
    | Some e ->
        let bytes = PacLib.extract inner e
        if not (HipLib.isHip bytes) then
            eprintfn "  跳过 %s（不是 HIP 专有格式）" m
        else
            let img = HipLib.decode bytes
            let out = Path.Combine(outDir, Path.GetFileNameWithoutExtension m + ".png")
            savePng img out
            ok <- ok + 1
            printfn "  %-42s %5dx%-5d %A" m img.ImageWidth img.ImageHeight img.Format
printfn "\n导出 %d 张 → %s" ok outDir
