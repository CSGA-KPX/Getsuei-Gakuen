// 把 abc 中的字形矩形叠加绘制到 hip 转出的 PNG 上（目检验证字形对应关系）
//
// 用法:
//   dotnet fsi abc-overlay.fsx <file.abc> <image.png> [-o <output.png>]
//
// 需要 NuGet 包 SixLabors.ImageSharp（首次运行自动恢复）。
// PNG 需先用 hip-tool.fsx（hip 专有格式）或通用工具（tga）从 hip 转出。

#r "nuget: SixLabors.ImageSharp, 3.1.3"

#load "abc.fsx"

open System
open System.IO
open AbcLib
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Drawing.Processing

let args = fsi.CommandLineArgs |> Array.skip 1

let usage () =
    eprintfn "用法: dotnet fsi abc-overlay.fsx <file.abc> <image.png> [-o <output.png>]"
    exit 2

if args.Length < 2 then
    usage ()

let abcPath = args[0]
let pngPath = args[1]

let mutable outPathOpt = None
let mutable i = 2

while i < args.Length do
    match args[i] with
    | "-o" | "--output" when i + 1 < args.Length ->
        outPathOpt <- Some args[i + 1]
        i <- i + 2
    | other ->
        eprintfn "未知参数: %s" other
        usage ()

let outPath = defaultArg outPathOpt (Path.GetFileNameWithoutExtension(pngPath) + "-overlay.png")

for f in [ abcPath; pngPath ] do
    if not (File.Exists f) then
        eprintfn "输入文件不存在: %s" f
        exit 1

let abc = AbcLib.parse abcPath

use image = Image.Load<Rgba32>(pngPath)

image.Mutate(fun ctx ->
    let color = Color.Red
    let thickness = 1.0f

    for g in abc.Glyphs do
        let x1, x2, y1, y2 = float32 g.Rect[0], float32 g.Rect[1], float32 g.Rect[2], float32 g.Rect[3]
        let x = Math.Min(x1, x2)
        let y = Math.Min(y1, y2)
        let w = Math.Abs(x2 - x1)
        let h = Math.Abs(y2 - y1)
        ctx.Draw(color, thickness, RectangleF(x, y, w, h)) |> ignore)

image.Save(outPath)
printfn "已叠加 %d 个字形矩形 -> %s" abc.Glyphs.Length outPath
