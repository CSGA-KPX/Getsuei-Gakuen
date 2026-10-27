// 假设 C 验证：菜单文字是否为图像（title/UI_02_title.pac 内的 title_menu_*.hip）
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

let root = @"<游戏安装目录>\data\AE"

let rec descend (pac: PacFile) (names: string[]) =
    let e = pac.Entries |> Array.find (fun e -> e.Filename = names.[0])
    let d = PacLib.extract pac e
    if names.Length = 1 then d
    else
        let tmp = Path.Combine(Path.GetTempPath(), "gakuen-c.pac")
        File.WriteAllBytes(tmp, d)
        let r = descend (PacLib.read tmp) names.[1..]
        File.Delete tmp
        r

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

// title pac 里的菜单/提示类图
let outer = Path.Combine(root, @"title\UI_02_title.pac")
let pac = PacLib.read outer
printfn "title/UI_02_title.pac 内文件 %d 个" pac.Entries.Length
let texEntry = pac.Entries |> Array.find (fun e -> e.Filename = "tex.pac")
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-title-tex.pac")
File.WriteAllBytes(tmp, PacLib.extract pac texEntry)
let tex = PacLib.read tmp
printfn "  tex.pac 内文件 %d 个\n" tex.Entries.Length

let wanted =
    tex.Entries
    |> Array.filter (fun e -> e.Filename.Contains "title_menu" || e.Filename.Contains "title_start")
    |> Array.sortBy (fun e -> e.Filename)

let z = 2
let cols = 2
let cellW, cellH = 520, 120
let rows = (wanted.Length + cols - 1) / cols
let sheet = new Bitmap(cols * cellW, rows * (cellH + 16), PixelFormat.Format32bppArgb)
let g = Graphics.FromImage sheet
g.Clear(System.Drawing.Color.FromArgb(30, 30, 36))
let font = new Font("Consolas", 11.0f, FontStyle.Regular, GraphicsUnit.Pixel)
let white = new SolidBrush(System.Drawing.Color.White)

for idx in 0 .. wanted.Length - 1 do
    let e = wanted.[idx]
    let data = PacLib.extract tex e
    let bmp, (w, hh) = hipToBitmap data
    let ox = (idx % cols) * cellW
    let oy = (idx / cols) * (cellH + 16)
    g.FillRectangle(System.Drawing.Brushes.Black, ox + 1, oy + 1, cellW - 2, cellH - 2)
    let scale = min (float32 (cellW - 8) / float32 w) (float32 (cellH - 8) / float32 hh)
    let dw = max 1 (int (float32 w * scale))
    let dh = max 1 (int (float32 hh * scale))
    g.DrawImage(bmp, System.Drawing.Rectangle(ox + 4, oy + 4, dw, dh))
    g.DrawString(sprintf "%s  %dx%d" e.Filename w hh, font, white, float32 ox + 4f, float32 oy + float32 cellH) |> ignore
    bmp.Dispose()

sheet.Save("scratch-title-menu.png", ImageFormat.Png)
File.Delete tmp
printfn "导出 %d 张（title_menu / title_start）→ scratch-title-menu.png" wanted.Length
