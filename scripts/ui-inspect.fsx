// 量 UI 文字图的「文字墨迹 bbox / 边框内缩」—— 重制前定字号与清除区域的依据
//
// 用法: dotnet fsi scripts/ui-inspect.fsx <png 目录> [--min-alpha 16]
// 输出每张图：尺寸、整体 alpha bbox、去掉边缘框后的内容 bbox、边框厚度估计
#r "nuget: System.Drawing.Common, 8.0.0"

open System
open System.Drawing
open System.Drawing.Imaging
open System.IO

let args = fsi.CommandLineArgs |> Array.skip 1
let dir = args |> Array.find (fun a -> not (a.StartsWith "--"))
let minAlpha = byte 16

let alphaOf (bmp: Bitmap) =
    let w, h = bmp.Width, bmp.Height
    let d = bmp.LockBits(Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
    let a = Array.zeroCreate<byte> (w * h)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            a.[y * w + x] <- Runtime.InteropServices.Marshal.ReadByte(d.Scan0, y * d.Stride + x * 4 + 3)
    bmp.UnlockBits d
    a

let bbox (a: byte[]) (w: int) (h: int) (x0: int) (y0: int) (x1: int) (y1: int) =
    let mutable bx1, by1, bx2, by2 = Int32.MaxValue, Int32.MaxValue, -1, -1
    for y in y0 .. y1 - 1 do
        for x in x0 .. x1 - 1 do
            if a.[y * w + x] >= minAlpha then
                if x < bx1 then bx1 <- x
                if x + 1 > bx2 then bx2 <- x + 1
                if y < by1 then by1 <- y
                if y + 1 > by2 then by2 <- y + 1
    if bx2 < 0 then (0, 0, 0, 0) else (bx1, by1, bx2, by2)

let rowFill (a: byte[]) (w: int) (y: int) =
    let mutable n = 0
    for x in 0 .. w - 1 do if a.[y * w + x] >= minAlpha then n <- n + 1
    float n / float w
let colFill (a: byte[]) (w: int) (h: int) (x: int) =
    let mutable n = 0
    for y in 0 .. h - 1 do if a.[y * w + x] >= minAlpha then n <- n + 1
    float n / float h

for f in Directory.GetFiles(Path.GetFullPath dir, "*.png") |> Array.sortBy id do
    use bmp = new Bitmap(f)
    let w, h = bmp.Width, bmp.Height
    let a = alphaOf bmp
    let (bx1, by1, bx2, by2) = bbox a w h 0 0 w h
    // 边框：顶/底整行填充率、左/右整列填充率
    let top, bottom = rowFill a w 0, rowFill a w (h - 1)
    let left, right = colFill a w h 0, colFill a w h (w - 1)
    let hasFrame = top > 0.5 && bottom > 0.5 && left > 0.5 && right > 0.5
    printfn "%-32s %4dx%-4d bbox=(%d,%d)-(%d,%d) 文字 %dx%d  边框=%s (T%.2f B%.2f L%.2f R%.2f)"
        (Path.GetFileNameWithoutExtension f) w h bx1 by1 bx2 by2 (bx2 - bx1) (by2 - by1)
        (if hasFrame then "有" else "无") top bottom left right
