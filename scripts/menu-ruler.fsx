// 菜单定位标尺图生成（一次性诊断工具）
//
// 用途：把 title_menu_0X_off/on 临时替换成"刻度图"，装机后截一张整屏图，
//       就能从截图直接读出：画布在屏幕上的左上角坐标、实际缩放比例（x/y 是否一致）、
//       是否被裁剪/压扁/翻转、on 与 off 是否走同一条定位路径。
//
// 图案（尺寸自适应）：
//   * 1px 白边框 = 画布边界
//   * 四角 6×6 色块：左上红 / 右上绿 / 左下蓝 / 右下黄（判断翻转与裁剪）
//   * 每 16px 一条 1px 灰竖线（数格子算缩放）
//   * 中心 1px 白色十字（判断锚点）
//
// 用法：dotnet fsi scripts/menu-ruler.fsx
//       产物 → work/patch-menu-ruler/
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/tga.fsx"

open System
open System.IO
open System.Drawing
open System.Drawing.Imaging
open TgaLib

let pristine = "work/pristine/story-image"
let outDir = "work/patch-menu-ruler"

/// 画一张标尺图
let drawRuler (cw: int) (ch: int) =
    let bmp = new Bitmap(cw, ch, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.Clear(Color.Transparent)

    let white = new Pen(Color.White, 1.0f)
    // 边框
    g.DrawRectangle(white, 0, 0, cw - 1, ch - 1)
    // 刻度：每 16px 一条灰竖线
    use gray = new Pen(Color.FromArgb(255, 128, 128, 128), 1.0f)
    let mutable x = 16
    while x < cw do
        g.DrawLine(gray, x, 2, x, ch - 3)
        x <- x + 16
    // 四角色块
    let blk (px: int) (py: int) (c: Color) =
        use b = new SolidBrush(c)
        g.FillRectangle(b, px, py, 6, 6)
    blk 2 2 (Color.Red)                    // 左上
    blk (cw - 8) 2 (Color.Lime)            // 右上
    blk 2 (ch - 8) (Color.Blue)            // 左下
    blk (cw - 8) (ch - 8) (Color.Yellow)   // 右下
    // 中心十字
    g.DrawLine(white, cw / 2, 0, cw / 2, ch - 1)
    g.DrawLine(white, 0, ch / 2, cw - 1, ch / 2)
    bmp

/// Bitmap → TGA type9 字节（以原图为模板，保留 Id/Desc/Origin/页脚等字段）
let encodeTga (orig: TgaImage) (cw: int) (ch: int) (bmp: Bitmap) =
    let colors = Collections.Generic.Dictionary<int, byte>()
    let palette = ResizeArray<SixLabors.ImageSharp.PixelFormats.Rgba32>()
    let indices = Array.zeroCreate<byte> (bmp.Width * bmp.Height)
    let idxAt (px: int) (py: int) =
        let c = bmp.GetPixel(px, py)
        let key = ((int c.A) <<< 24) ||| ((int c.R) <<< 16) ||| ((int c.G) <<< 8) ||| (int c.B)
        match colors.TryGetValue key with
        | true, i -> i
        | _ ->
            let i = byte palette.Count
            colors.[key] <- i
            palette.Add(SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, c.A))
            i
    let flipY = (orig.Desc &&& 0x20) = 0
    for y in 0 .. bmp.Height - 1 do
        let sy = if flipY then bmp.Height - 1 - y else y
        for px in 0 .. bmp.Width - 1 do
            indices.[sy * bmp.Width + px] <- idxAt px y
    let t = { orig with Width = cw; Height = ch; Palette = palette.ToArray(); Indices = indices }
    TgaLib.encode t

Directory.CreateDirectory outDir |> ignore

// 目标尺寸在各态原图基础上做覆盖：(文件, 宽覆盖, 高覆盖)
let targets =
    [ "title_menu_06_off", Some 256, Some 64    // 大画布 off
      "title_menu_06_on", Some 48, Some 40      // 原版小画布 on —— 重点看它被怎么画
      "title_menu_07_on", Some 256, Some 64 ]   // 同尺寸 on —— 对照

for (name, wo, ho) in targets do
    let origPath = Path.Combine(pristine, name + ".hip")
    let orig = TgaLib.decode (File.ReadAllBytes origPath)
    let cw = wo |> Option.defaultValue orig.Width
    let ch = ho |> Option.defaultValue orig.Height
    use bmp = drawRuler cw ch
    let bytes = encodeTga orig cw ch bmp
    File.WriteAllBytes(Path.Combine(outDir, name + ".hip"), bytes)
    printfn "  %-22s %3dx%-3d  %6d 字节" name cw ch bytes.Length

printfn "\n标尺图 → %s" outDir
printfn "装机：复制到游戏 data\\story\\image\\ 即可（原文件已在 work/backup/story/image-menu/）"
