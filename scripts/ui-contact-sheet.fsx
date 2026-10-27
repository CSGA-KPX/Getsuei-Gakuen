// UI 图拼版：把 ae-export.fsx 导出的 PNG 目录拼成带文件名的对照图（重制前逐张核对用）
//
// 用法:
//   dotnet fsi scripts/ui-contact-sheet.fsx <png 目录> [-o 输出.png] [--page 20] [--scale 1.0]
// 产物：<目录>/../<目录名>-sheet-1.png ...（每页 --page 行）
//
// 透明区域用深灰底合成（UI 文字图几乎都是白字 + 透明底），这样一眼能看清原文内容。
#r "nuget: System.Drawing.Common, 8.0.0"

open System
open System.Drawing
open System.Drawing.Imaging
open System.IO

let args = fsi.CommandLineArgs |> Array.skip 1
let dir = args |> Array.tryFind (fun a -> not (a.StartsWith "--") && not (a.EndsWith ".png")) |> Option.defaultWith (fun () -> failwith "需要 PNG 目录")
let getOpt name dflt =
    let i = args |> Array.tryFindIndex ((=) name)
    match i with Some i when i + 1 < args.Length -> args.[i + 1] | _ -> dflt
let outOpt = args |> Array.tryFind (fun a -> a.EndsWith ".png")
let perPage = int (getOpt "--page" "20")
let scale = float (getOpt "--scale" "1.0")
let baseDir = Path.GetFullPath dir
let files = Directory.GetFiles(baseDir, "*.png") |> Array.sortBy id
printfn "%d 张 PNG" files.Length

let labelW = 300
let pad = 6
let pages = (files.Length + perPage - 1) / perPage
let font = new Font("Consolas", 11f, GraphicsUnit.Pixel)
let brush = Brushes.White
let bg = Color.FromArgb(255, 32, 34, 40)

for page in 0 .. pages - 1 do
    let slice = files |> Array.skip (page * perPage) |> Array.truncate perPage
    // 预读尺寸
    let imgs =
        slice |> Array.map (fun f ->
            let b = new Bitmap(f)
            f, b, int (float b.Width * scale), int (float b.Height * scale))
    let rowH = (imgs |> Array.map (fun (_, _, _, h) -> h) |> Array.max) + pad
    let contentW = imgs |> Array.map (fun (_, _, w, _) -> w) |> Array.max
    let width = labelW + contentW + pad * 2
    let height = rowH * imgs.Length + pad * 2
    use sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage sheet
    g.Clear bg
    g.TextRenderingHint <- Drawing.Text.TextRenderingHint.ClearTypeGridFit
    // 棋盘格底（识别透明）
    for i in 0 .. imgs.Length - 1 do
        let name, bmp, w, h = imgs.[i]
        let y = pad + i * rowH
        g.DrawString(Path.GetFileNameWithoutExtension name, font, brush, float32 pad, float32 (y + 4))
        g.DrawString(sprintf "%dx%d" bmp.Width bmp.Height, font, Brushes.Gray, float32 pad, float32 (y + 20))
        g.DrawImage(bmp, Rectangle(labelW, y, w, h))
        g.DrawRectangle(Pens.DimGray, Rectangle(labelW, y, w - 1, h - 1))
        bmp.Dispose()
    let outPath =
        match outOpt with
        | Some p when pages = 1 -> p
        | Some p -> Path.Combine(Path.GetDirectoryName p, Path.GetFileNameWithoutExtension p + sprintf "-%d.png" (page + 1))
        | None -> Path.Combine(baseDir, sprintf "sheet-%d.png" (page + 1))
    sheet.Save(outPath, ImageFormat.Png)
    printfn "→ %s" outPath
