// 导出 data\story\image\ 下真正的标题菜单图（散装 .hip）
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/hip.fsx"
#load "../scripts/tga.fsx"

open System
open System.IO
open System.Drawing
open System.Drawing.Imaging
open HipLib
open SixLabors.ImageSharp

let dir = @"<游戏安装目录>\data\story\image"

/// 这些 .hip 扩展名的文件实际是 TGA 内容（文档：story/image 410 个全部为 TGA）
let tgaToBitmap (bytes: byte[]) =
    let t = TgaLib.decode bytes
    let w, hh = t.Width, t.Height
    let img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(max 1 w, max 1 hh)
    for y in 0 .. hh - 1 do
        let sy = if t.Desc &&& 0x20 <> 0 then y else hh - 1 - y
        for x in 0 .. w - 1 do
            let c = t.Palette.[int t.Indices.[sy * w + x]]
            img.[x, y] <- SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, c.A)
    use ms = new MemoryStream()
    img.SaveAsPng ms
    img.Dispose()
    ms.Position <- 0L
    new Bitmap(ms), (w, hh)

let hipToBitmap (bytes: byte[]) =
    if bytes.Length >= 3 && bytes.[0] = 0x48uy && bytes.[1] = 0x49uy && bytes.[2] = 0x50uy then
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
    else tgaToBitmap bytes

let files =
    Directory.GetFiles(dir, "*.hip")
    |> Array.filter (fun p -> Path.GetFileName(p).StartsWith "title_menu" || Path.GetFileName(p).StartsWith "title_logo" || Path.GetFileName(p).StartsWith "getuei_title")
    |> Array.sort

printfn "待导出 %d 个文件\n" files.Length

let cols = 3
let cellW, cellH = 420, 130
let rows = (files.Length + cols - 1) / cols
let sheet = new Bitmap(cols * cellW, rows * (cellH + 16), PixelFormat.Format32bppArgb)
let g = Graphics.FromImage sheet
g.Clear(System.Drawing.Color.FromArgb(30, 30, 36))
let font = new Font("Consolas", 11.0f, FontStyle.Regular, GraphicsUnit.Pixel)
let white = new SolidBrush(System.Drawing.Color.White)

for idx in 0 .. files.Length - 1 do
    let p = files.[idx]
    let ox = (idx % cols) * cellW
    let oy = (idx / cols) * (cellH + 16)
    try
        let bmp, (w, hh) = hipToBitmap (File.ReadAllBytes p)
        g.FillRectangle(System.Drawing.Brushes.Black, ox + 1, oy + 1, cellW - 2, cellH - 2)
        let scale = min (float32 (cellW - 8) / float32 w) (float32 (cellH - 8) / float32 hh)
        let dw = max 1 (int (float32 w * scale))
        let dh = max 1 (int (float32 hh * scale))
        g.DrawImage(bmp, System.Drawing.Rectangle(ox + 4, oy + 4, dw, dh))
        g.DrawString(sprintf "%s %dx%d" (Path.GetFileName p) w hh, font, white, float32 ox + 4f, float32 oy + float32 cellH) |> ignore
        bmp.Dispose()
    with ex ->
        g.DrawString(sprintf "%s ERR %s" (Path.GetFileName p) (ex.Message.Substring(0, min 20 ex.Message.Length)), font, Brushes.Magenta, float32 ox + 4f, float32 oy + 20f) |> ignore

sheet.Save("work/story-image-title.png", ImageFormat.Png)
printfn "→ work/story-image-title.png（%d 格）" files.Length
