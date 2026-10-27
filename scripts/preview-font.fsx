// 字库离线预览：按引擎语义把文本排版成 PNG（验证 mapping / UV / 步进 / 字面大小）
//
// 目的：装机前目检「字形缺失、mapping 偏移一位、中文字面大小不一」这类问题，
// 不用每轮都进游戏。引擎语义见 docs/formats/abc-hip-font.md §5.0–§5.1。
//
// 用法:
//   dotnet fsi scripts/preview-font.fsx --abc <x.abc> --hip <x.hip> --out preview.png "文本1" "文本2"
//   例：dotnet fsi scripts/preview-font.fsx --abc work/patch-jpn/jpn_glyph.abc `
//         --hip work/patch-jpn/jpn_glyph.hip --out work/patch-jpn/render.png "セーブ" "顺便一提，最受欢迎的"
#r "nuget: System.Drawing.Common, 8.0.0"
#load "hip.fsx"

open System
open System.Drawing
open System.Drawing.Imaging
open HipLib

let args = fsi.CommandLineArgs |> Array.skip 1
let getOpt name =
    let i = args |> Array.tryFindIndex ((=) name)
    match i with Some i when i + 1 < args.Length -> Some args.[i + 1] | _ -> None
let opts = [ "--abc"; "--hip"; "--out" ]
let lines =
    let picked = ResizeArray<string>()
    let mutable i = 0
    while i < args.Length do
        if opts |> List.contains args.[i] then i <- i + 2
        elif args.[i].StartsWith "--" then i <- i + 1
        else picked.Add args.[i]; i <- i + 1
    picked.ToArray()
let abcPath = getOpt "--abc" |> Option.defaultWith (fun () -> failwith "需要 --abc")
let hipPath = getOpt "--hip" |> Option.defaultWith (fun () -> failwith "需要 --hip")
let outPath = getOpt "--out" |> Option.defaultValue "preview-render.png"
let bgDark = args |> Array.contains "--light" |> not

let abc = IO.File.ReadAllBytes abcPath
let img = HipLib.decode (IO.File.ReadAllBytes hipPath)
let bpp = img.Format.Bpp
printfn "abc %d 字节 / hip %dx%d %A %A" abc.Length img.ImageWidth img.ImageHeight img.Format img.Encoding

let version = BitConverter.ToUInt32(abc, 0)
let entryCount = int (BitConverter.ToUInt16(abc, 20))
let countOff = 24 + 2 * entryCount
let glyphCount = BitConverter.ToInt32(abc, countOff)
let rec24Base = countOff + 4
let ver5 = version = 5u
let rec32Base = rec24Base + 24 * glyphCount
printfn "Version=0x%08X EntryCount=%d GlyphCount=%d rec32=%s"
    version entryCount glyphCount (if ver5 then "无（ver5 走 float UV）" else "有")

let tw, th = img.ImageWidth, img.ImageHeight
/// 图集灰度（Gray16 单通道 = alpha；Indexed8 取调色板 alpha；Argb32 取 alpha）
let alpha (x: int) (y: int) =
    if x < 0 || y < 0 || x >= tw || y >= th then 0uy
    else
        let i = y * tw + x
        match img.Format with
        | Gray16 -> byte ((int img.Pixels.[i * 2] ||| (int img.Pixels.[i * 2 + 1] <<< 8)) * 255 / 65535)
        | Indexed8 ->
            match img.Palette with
            | Some p when int img.Pixels.[i] < p.Length -> p.[int img.Pixels.[i]].[3]
            | _ -> img.Pixels.[i]
        | _ -> img.Pixels.[i * bpp + 3]

/// 字形：像素矩形 + 步进（rec24 +16/+20 = 比例模式步进）
let glyphInfo (cp: int) =
    if cp > entryCount then None
    else
        let g = int (BitConverter.ToUInt16(abc, 22 + 2 * cp))
        if g = 0 || g >= glyphCount then None
        else
            let o24 = rec24Base + 24 * g
            let step = int (BitConverter.ToUInt16(abc, o24 + 16)) + int (BitConverter.ToUInt16(abc, o24 + 20))
            let rectPx =
                if ver5 then
                    let u1 = BitConverter.ToSingle(abc, o24)
                    let v1 = BitConverter.ToSingle(abc, o24 + 4)
                    let u2 = BitConverter.ToSingle(abc, o24 + 8)
                    let v2 = BitConverter.ToSingle(abc, o24 + 12)
                    (int (Math.Round(float (u1 * float32 tw))), int (Math.Round(float (u2 * float32 tw))),
                     int (Math.Round(float (v1 * float32 th))), int (Math.Round(float (v2 * float32 th))))
                else
                    let o32 = rec32Base + 32 * g
                    (BitConverter.ToInt32(abc, o32), BitConverter.ToInt32(abc, o32 + 4),
                     BitConverter.ToInt32(abc, o32 + 8), BitConverter.ToInt32(abc, o32 + 12))
            Some(rectPx, (if step > 0 then step else (let (x1, x2, _, _) = rectPx in x2 - x1)))

// 排版
let lineH = int (BitConverter.ToSingle(abc, 4))
let margin = 8
let width = 1100
let height = margin * 2 + lineH * max 1 lines.Length
use bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb)
use gfx = Graphics.FromImage bmp
gfx.Clear(if bgDark then Color.FromArgb(255, 24, 28, 36) else Color.FromArgb(255, 232, 232, 232))
let fg = if bgDark then Color.White else Color.Black

let mutable missing = System.Collections.Generic.List<string>()
for li in 0 .. lines.Length - 1 do
    let mutable penX = margin
    let baseY = margin + li * lineH
    for ch in lines.[li] do
        match glyphInfo (int ch) with
        | None -> missing.Add(sprintf "U+%04X(%c)" (int ch) ch); penX <- penX + lineH
        | Some((x1, x2, y1, y2), step) ->
            for y in y1 .. y2 - 1 do
                // 引擎竖直方向：框顶对齐行顶（rec32 y1 相对图集，这里按框高贴基线下方）
                let dy = baseY + (y - y1)
                for x in x1 .. x2 - 1 do
                    let a = int (alpha x y)
                    if a > 0 then
                        let c = Color.FromArgb(a, fg)
                        bmp.SetPixel(penX + (x - x1), dy, c)
            penX <- penX + step
bmp.Save(outPath, ImageFormat.Png)
printfn "已输出 %s（%d 行，行高 %d）" outPath lines.Length lineH
if missing.Count > 0 then
    printfn "⚠️ 缺字形 %d 处：%s" missing.Count
        (String.Join(" ", missing |> Seq.take (min 40 missing.Count)))
else printfn "✅ 文本全部有字形"

