// TGA type9 (RLE colormapped) 解码 → PNG（验证 image 目录文字图用）
// 用法: dotnet fsi decode-tga.fsx <in.tga|in.hip> <out.png>
#r "nuget: SixLabors.ImageSharp"
#load "tga.fsx"

open System
open System.IO
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open TgaLib

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 2 then
    eprintfn "用法: dotnet fsi decode-tga.fsx <in> <out.png>"
    exit 2

let data = File.ReadAllBytes(args[0])
let img = TgaLib.decode data
printfn "%s: %d×%d palette=%d desc=0x%X idLen=%d" args[0] img.Width img.Height img.Palette.Length img.Desc img.Id.Length

// desc bit5=0 → bottom-up，翻转
let topOrigin = (img.Desc &&& 0x20) <> 0
use bmp = new Image<Rgba32>(img.Width, img.Height)
for y in 0 .. img.Height - 1 do
    let sy = if topOrigin then y else img.Height - 1 - y
    for x in 0 .. img.Width - 1 do
        bmp[x, y] <- img.Palette[int img.Indices[sy * img.Width + x]]

bmp.Save(args[1])
printfn "已保存 %s" args[1]
