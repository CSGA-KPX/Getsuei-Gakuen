// 生成菜单重制对比图：原图 vs 中文版（逐项并排）
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/tga.fsx"

open System
open System.IO
open System.Drawing
open System.Drawing.Imaging
open TgaLib

// ⚠️ 原图取 work/pristine/（Steam 校验后的只读基准），不读游戏目录 ——
// 装机后游戏目录里已是重制产物，读它就变成"自己和自己比"。
let gameDir = @"work/pristine/story-image"
let newDir = "work/patch-menu"

let loadTga (path: string) =
    let t = TgaLib.decode (File.ReadAllBytes path)
    let img = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppArgb)
    for y in 0 .. t.Height - 1 do
        let sy = if t.Desc &&& 0x20 <> 0 then y else t.Height - 1 - y
        for x in 0 .. t.Width - 1 do
            let c = t.Palette.[int t.Indices.[sy * t.Width + x]]
            img.SetPixel(x, y, System.Drawing.Color.FromArgb(int c.A, int c.R, int c.G, int c.B))
    img, t.Width, t.Height

let prefixes =
    [ "title_menu_01"; "title_menu_02"; "title_menu_03"; "title_menu_04"; "title_menu_05"
      "title_menu_06"; "title_menu_07"; "title_menu_08"; "title_menu_09" ]

let rowH = 76
let sheet = new Bitmap(1120, prefixes.Length * rowH + 30, PixelFormat.Format32bppArgb)
use g = Graphics.FromImage sheet
// 模拟游戏标题画面的深蓝背景
g.Clear(Color.FromArgb(45, 65, 95))
let font = new Font("Consolas", 10.0f, FontStyle.Regular, GraphicsUnit.Pixel)
let white = new SolidBrush(Color.White)
let yellow = new SolidBrush(Color.FromArgb(255, 220, 120))

g.DrawString("原始（日文）", font, yellow, 4.0f, 2.0f)
g.DrawString("重制（中文）", font, yellow, 4.0f, 16.0f)
g.DrawString("原始（高亮）", font, yellow, 560.0f, 2.0f)
g.DrawString("重制（高亮）", font, yellow, 560.0f, 16.0f)

for i in 0 .. prefixes.Length - 1 do
    let p = prefixes.[i]
    let y0 = 34 + i * rowH
    // 原图 off / on
    let (oOff, w1, h1) = loadTga (Path.Combine(gameDir, p + "_off.hip"))
    let (oOn, _, _) = loadTga (Path.Combine(gameDir, p + "_on.hip"))
    // 新图 off / on
    let (nOff, w2, h2) = loadTga (Path.Combine(newDir, p + "_off.hip"))
    let (nOn, _, _) = loadTga (Path.Combine(newDir, p + "_on.hip"))

    g.DrawImage(oOff, 4, y0)
    g.DrawImage(nOff, 280, y0)
    g.DrawImage(oOn, 560, y0)
    g.DrawImage(nOn, 840, y0)
    g.DrawString(sprintf "%s %dx%d" p w1 h1, font, white, 4.0f, float32 (y0 + 50))
    g.DrawString(sprintf "%dx%d" w2 h2, font, white, 280.0f, float32 (y0 + 50))

    oOff.Dispose(); oOn.Dispose(); nOff.Dispose(); nOn.Dispose()

sheet.Save("work/menu-rebuild-compare.png", ImageFormat.Png)
printfn "→ work/menu-rebuild-compare.png"
