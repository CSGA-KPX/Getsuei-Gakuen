// 导出 AE/title/UI_02_title.pac → tex.pac 里**全部**文件，找出哪张图真正带菜单文字
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/Pac.fsx"
#load "../scripts/hip.fsx"

open System
open System.IO
open System.Drawing
open System.Drawing.Imaging
open PacLib
open HipLib
open SixLabors.ImageSharp

let game = @"<游戏安装目录>"
let outerPath = Path.Combine(game, @"data\AE\title\UI_02_title.pac")

let hipToBitmap (bytes: byte[]) =
    let h = HipLib.decode bytes
    let w, hh = h.ImageWidth, h.ImageHeight
    let img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(max 1 w, max 1 hh)
    let bpp = h.Format.Bpp
    for y in 0 .. hh - 1 do
        for x in 0 .. w - 1 do
            let i = (y * w + x) * bpp
            let r, g, b, a =
                match h.Format with
                | Indexed8 ->
                    match h.Palette with
                    | Some p when int h.Pixels.[i] < p.Length ->
                        let c = p.[int h.Pixels.[i]]
                        int c.[2], int c.[1], int c.[0], int c.[3]
                    | _ -> 255, 0, 255, 255
                | Gray16 ->
                    let v = int h.Pixels.[i] ||| (int h.Pixels.[i + 1] <<< 8)
                    let gv = v >>> 8
                    gv, gv, gv, 255
                | Rgb565 ->
                    let v = int h.Pixels.[i] ||| (int h.Pixels.[i + 1] <<< 8)
                    ((v >>> 11) &&& 0x1F) * 8, ((v >>> 5) &&& 0x3F) * 4, (v &&& 0x1F) * 8, 255
                | _ -> int h.Pixels.[i + 2], int h.Pixels.[i + 1], int h.Pixels.[i], int h.Pixels.[i + 3]
            img.[x, y] <- SixLabors.ImageSharp.PixelFormats.Rgba32(byte r, byte g, byte b, byte a)
    use ms = new MemoryStream()
    img.SaveAsPng ms
    img.Dispose()
    ms.Position <- 0L
    new Bitmap(ms), (w, hh)

let outer = PacLib.read outerPath
let texIdx = outer.Entries |> Array.findIndex (fun e -> e.Filename = "tex.pac")
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-all-tex.pac")
File.WriteAllBytes(tmp, PacLib.extract outer outer.Entries.[texIdx])
let tex = PacLib.read tmp
File.Delete tmp

let files = tex.Entries |> Array.sortBy (fun e -> e.Filename)
printfn "tex.pac 共 %d 个文件：" files.Length
files |> Array.iter (fun e -> printfn "  %-28s %d 字节" e.Filename e.Length)

let cols = 3
let cellW, cellH = 400, 150
let rows = (files.Length + cols - 1) / cols
let sheet = new Bitmap(cols * cellW, rows * (cellH + 16), PixelFormat.Format32bppArgb)
let g = Graphics.FromImage sheet
g.Clear(System.Drawing.Color.FromArgb(30, 30, 36))
let font = new Font("Consolas", 11.0f, FontStyle.Regular, GraphicsUnit.Pixel)
let white = new SolidBrush(System.Drawing.Color.White)

for idx in 0 .. files.Length - 1 do
    let e = files.[idx]
    let ox = (idx % cols) * cellW
    let oy = (idx / cols) * (cellH + 16)
    try
        let data = PacLib.extract tex e
        if e.Filename.EndsWith(".hip", StringComparison.OrdinalIgnoreCase) then
            let bmp, (w, hh) = hipToBitmap data
            g.FillRectangle(System.Drawing.Brushes.Black, ox + 1, oy + 1, cellW - 2, cellH - 2)
            let scale = min (float32 (cellW - 8) / float32 w) (float32 (cellH - 8) / float32 hh)
            let dw = max 1 (int (float32 w * scale))
            let dh = max 1 (int (float32 hh * scale))
            g.DrawImage(bmp, System.Drawing.Rectangle(ox + 4, oy + 4, dw, dh))
            g.DrawString(sprintf "%s %dx%d" e.Filename w hh, font, white, float32 ox + 4f, float32 oy + float32 cellH) |> ignore
            bmp.Dispose()
        else
            g.DrawString(sprintf "%s（非图像）" e.Filename, font, white, float32 ox + 4f, float32 oy + 20f) |> ignore
    with ex ->
        g.DrawString(sprintf "%s ERR" e.Filename, font, Brushes.Magenta, float32 ox + 4f, float32 oy + 20f) |> ignore

sheet.Save("work/title-all-files.png", ImageFormat.Png)
printfn "\n→ work/title-all-files.png（%d 格）" files.Length
