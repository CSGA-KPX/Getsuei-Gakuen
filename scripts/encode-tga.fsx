// PNG → TGA type9 (RLE colormapped) 编码（重制中文台词图用）
// 用法: dotnet fsi encode-tga.fsx <in.png> <out.tga|out.hip>
//
// 输出约定（对齐原文件，roundtrip 已验证，见 docs/formats/tga-type9.md）：
//   调色板 256 色（按出现顺序，不足补 0）、bottom-up（desc=0x08）、
//   RLE 包化算法复刻原工具（原始包在 run 第二个像素前截断）、TGA2.0 页脚 26B。
#r "nuget: SixLabors.ImageSharp"
#load "tga.fsx"

open System
open System.IO
open System.Collections.Generic
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Processing.Processors.Quantization
open TgaLib

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 2 then
    eprintfn "用法: dotnet fsi encode-tga.fsx <in.png> <out.tga>"
    exit 2

use bmp: Image<Rgba32> = Image.Load<Rgba32>(args[0])

// 先量化到 ≤256 色（Wu），再收集调色板（按出现顺序，与原文件行为一致）
let quantized = bmp.Clone(fun (x: Processing.IImageProcessingContext) -> x.Quantize(WuQuantizer(QuantizerOptions(MaxColors = 256))) |> ignore)

let palette = ResizeArray<Rgba32>()
let colorToIdx = Dictionary<Rgba32, byte>()
let w, h = quantized.Width, quantized.Height
let indices = Array.zeroCreate<byte> (w * h)

for y in 0 .. h - 1 do
    for x in 0 .. w - 1 do
        let c = quantized[x, y]
        match colorToIdx.TryGetValue c with
        | true, idx -> indices[y * w + x] <- idx
        | false, _ ->
            if palette.Count >= 256 then failwith "量化后仍超过 256 色（不应发生）"
            let idx = byte palette.Count
            palette.Add c
            colorToIdx[c] <- idx
            indices[y * w + x] <- idx

// 补满 256 色
while palette.Count < 256 do
    palette.Add(Rgba32(0uy, 0uy, 0uy, 0uy))

// bottom-up 存储
let fileOrder = Array.zeroCreate<byte> (w * h)
for y in 0 .. h - 1 do
    Array.blit indices (y * w) fileOrder ((h - 1 - y) * w) w

// 页脚尺寸字段 = 宽高向上取 2 的幂（u16 LE，与原版工具行为一致）
let nextPow2 (n: int) =
    let mutable p = 1
    while p < n do
        p <- p * 2
    p
let footerDim = Array.concat [ BitConverter.GetBytes(uint16 (nextPow2 w)); BitConverter.GetBytes(uint16 (nextPow2 h)) ]

let tga =
    { Id = Array.empty; CmapStart = 0; Palette = palette.ToArray()
      Width = w; Height = h; Desc = 0x08
      XOrigin = 0; YOrigin = 0
      Indices = fileOrder
      RleTrailing = Array.empty
      FooterDim = footerDim }

File.WriteAllBytes(args[1], TgaLib.encode tga)
printfn "已编码 %s (%d×%d, 实际用色 %d) -> %s (%d 字节)"
    args[0] w h colorToIdx.Count args[1] (FileInfo(args[1]).Length)
