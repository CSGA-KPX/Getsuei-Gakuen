// remake-telop.fsx — 预渲染台词图（テロップ）中文重制：atf 译文 → 美术字图 → TGA type9
//
// 用法:
//   dotnet fsi scripts/remake-telop.fsx all                # 全部文字图 → work/telop-png + work/patch-image
//   dotnet fsi scripts/remake-telop.fsx 00_0-01_01_001 ... # 指定 key
//   dotnet fsi scripts/remake-telop.fsx install            # work/patch-image → 游戏目录（先备份）
//
// 依据（docs/formats/tga-type9.md + 2026-10-19 实测）：
//   - 台词图是 TGA type9（1024×128，透明底），运行时**优先于 atf 文本**显示，
//     无图即空白，所以只能重制图（删图回退不可行）。
//   - 原版版式：白色填充 + 黑色描边（笔宽 7 → 外扩 3.5px），逐行水平居中；
//     正文行带 1 = y 20..63、行带 2 = y 84..127；ruby 注音在 y 1..21（中文无 ruby，不画）。
//   - 一张图画哪几行由**原图墨迹行段**决定（剔除 ruby 小字行）：1 段 = 单行，
//     2 段 = 两行；单段图可能是「两句合并成一行」（如 00_0-01_01_021_2），
//     用原图墨迹宽度与原文字数的比对来判定。
//   - 文本源：work/atf-cn（译文）；译文缺失时退回 work/atf 原文。
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "tga.fsx"
#load "AtfJson/Atf.fs"
#load "AtfJson/Json.fs"

open System
open System.IO
open System.Drawing
open System.Drawing.Drawing2D
open System.Drawing.Imaging
open System.Drawing.Text
open System.Collections.Generic
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Processing.Processors.Quantization
open TgaLib
open AtfJson

let gameDir = @"<游戏安装目录>\data\story\image"
let outPngDir = "work/telop-png"
let outHipDir = "work/patch-image"
let backupDir = "work/backup/story/image"

let args = fsi.CommandLineArgs |> Array.skip 1

// ---------------- 字体与版式常量（按原图实测校准） ----------------
let pfc = PrivateFontCollection()
pfc.AddFontFile("font/华康新综艺w7.ttf")
let family = pfc.Families[0]

let TARGET_TELOP_BODY_H = 37.0f   // 汉字墨迹高（原图正文墨高 44 − 描边外扩 3.5×2）
let STROKE_W = 7.0f               // 描边笔宽（外扩 3.5px）
let ORIG_CHAR_W = 35.3f           // 原版日文字宽（实测：001 行宽 355 / 10 字）
let SIDE_MARGIN = 30f             // 行宽自适应留边

let telopKeys () =
    Directory.GetFiles(gameDir)
    |> Array.map Path.GetFileNameWithoutExtension
    |> Array.filter (fun k -> k.StartsWith("00_0-01_01_0") || k.StartsWith("10_1-06_01_0"))
    |> Array.sort

// ---------------- 原图解析：正文行段 ----------------
type SrcImage =
    { Width: int; Height: int
      Centers: float32[]   // 正文各行垂直中心（已剔除 ruby 小字行）
      InkWidth: int }      // 整幅墨迹宽度

let loadSrc (key: string) : SrcImage =
    let t = TgaLib.decode (File.ReadAllBytes(Path.Combine(gameDir, key + ".hip")))
    let alpha x y =
        let sy = if t.Desc &&& 0x20 <> 0 then y else t.Height - 1 - y
        int t.Palette[int t.Indices[sy * t.Width + x]].A
    let nz =
        [| for y in 0 .. t.Height - 1 ->
               let mutable c = 0
               for x in 0 .. t.Width - 1 do
                   if alpha x y > 8 then c <- c + 1
               c |]
    let maxNz = Array.max nz
    if maxNz = 0 then failwithf "%s: 原图全透明" key
    // 正文行阈值：ruby 小字行像素数远低于正文（实测 ≤ 25% maxNz）
    let thr = max 20 (int (float maxNz * 0.3))
    let segs = ResizeArray<int * int>()
    let mutable start = -1
    let mutable last = -2
    for y in 0 .. t.Height - 1 do
        if nz[y] >= thr then
            if start < 0 then start <- y
            last <- y
        elif start >= 0 && y - last > 2 then
            segs.Add(start, last)
            start <- -1
    if start >= 0 then segs.Add(start, last)
    let centers = segs |> Seq.map (fun (a, b) -> float32 (a + b) / 2.0f) |> Seq.toArray
    let mutable x1, x2 = t.Width, -1
    for y in 0 .. t.Height - 1 do
        for x in 0 .. t.Width - 1 do
            if alpha x y > 8 then
                x1 <- min x1 x
                x2 <- max x2 x
    { Width = t.Width; Height = t.Height; Centers = centers; InkWidth = x2 - x1 + 1 }

// ---------------- 文本：atf 译文/原文 ----------------
let atfCache = Dictionary<string, TextRecord[] * TextRecord[]>()
let atfRecs (atf: string) =
    match atfCache.TryGetValue atf with
    | true, v -> v
    | _ ->
        let o = (AtfJsonIO.readJson (sprintf "work/atf/%s.json" atf)).Records
        let cnPath = sprintf "work/atf-cn/%s.json" atf
        let c = if File.Exists cnPath then (AtfJsonIO.readJson cnPath).Records else o
        atfCache[atf] <- (o, c)
        o, c

let atfNameOf (key: string) =
    let p = key.Split('_')
    sprintf "text_%s_%s" p[0] p[1]

let splitLines (s: string) = s.Split('\n') |> Array.map (fun x -> x.TrimEnd('\r'))

/// 去掉 <ruby>…</ruby> 之类的标记，返回可见文字（用于字数统计）
let stripTags (s: string) =
    let sb = Text.StringBuilder()
    let mutable depth = 0
    for c in s do
        if c = '<' then depth <- depth + 1
        elif c = '>' then depth <- max 0 (depth - 1)
        elif depth = 0 then sb.Append(c) |> ignore
    sb.ToString()

/// key → (原文各行, 译文各行)
let textsOf (key: string) : string[] * string[] =
    let o, c = atfRecs (atfNameOf key)
    let baseKey =
        if (key.EndsWith("_1") || key.EndsWith("_2"))
           && (o |> Array.exists (fun r -> r.Key = key.Substring(0, key.Length - 2))) then
            key.Substring(0, key.Length - 2)
        else key
    let pick (rs: TextRecord[]) =
        match rs |> Array.tryFind (fun r -> r.Key = baseKey) with
        | Some r -> r.Value
        | None -> failwithf "%s: atf 中找不到 key %s" key baseKey
    splitLines (pick o), splitLines (pick c)

/// 决定画哪几行文本：
/// - 原图正文行段数 ≥2 → 逐行对应译文各行（如 10_1-06_01_035_1）
/// - 1 段：带 `_1` 后缀 = 第一句单独显示；带 `_2` 后缀 = 第二句单独显示，
///   但若墨迹宽度明显超过第二句、且贴近两句合计字数，则是「两句合并成一行」（如 021_2）
let decideText (key: string) (src: SrcImage) (orig: string[]) (cn: string[]) : string[] =
    let charCount (a: int) (b: int) =
        orig[a..b] |> Array.sumBy (fun s -> float32 (stripTags s).Length)
    let origChars = float32 src.InkWidth / ORIG_CHAR_W
    if src.Centers.Length >= 2 then
        Array.init src.Centers.Length (fun i -> if i < cn.Length then cn[i] else "")
    elif orig.Length <= 1 then
        [| String.concat "" cn |]
    elif key.EndsWith("_1") then
        [| cn[0] |]
    elif key.EndsWith("_2") then
        let n2 = charCount 1 1
        let nAll = charCount 0 (orig.Length - 1)
        if origChars > n2 * 1.4f && abs (origChars - nAll) <= nAll * 0.15f then
            [| String.concat "" cn |]
        else
            [| cn[min 1 (cn.Length - 1)] |]
    else
        [| String.concat "" cn |]

// ---------------- 渲染 ----------------
let pathOf (text: string) (x: float32) (y: float32) (size: float32) =
    let p = new GraphicsPath()
    p.AddString(text, family, int FontStyle.Regular, size, System.Drawing.PointF(x, y), StringFormat.GenericTypographic)
    p

/// 校准字号：使「国」的墨迹高 = TARGET_TELOP_BODY_H
let bodySize () =
    let mutable size = 38.0f
    for _ in 1..4 do
        use p = pathOf "国" 0f 0f size
        let b = p.GetBounds()
        if b.Height > 0.5f then size <- size * (TARGET_TELOP_BODY_H / b.Height)
    size

let kanjiBounds (size: float32) =
    use p = pathOf "国" 0f 0f size
    p.GetBounds()

let renderTelop (canvasW: int) (canvasH: int) (rows: (string * float32)[]) (size: float32) (kanji: System.Drawing.RectangleF) : Bitmap =
    let bmp = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage(bmp)
    g.SmoothingMode <- SmoothingMode.AntiAlias
    g.TextRenderingHint <- TextRenderingHint.AntiAlias
    g.Clear(System.Drawing.Color.Transparent)
    use pen = new Pen(System.Drawing.Color.Black, STROKE_W)
    pen.LineJoin <- LineJoin.Round
    for (text, centerY) in rows do
        if text.Trim() <> "" then
            // 统一基线：以「国」bbox 中心对齐原图行中心（同一图内多行共用同一基准）
            let originY = centerY - (kanji.Y + kanji.Height / 2.0f)
            use p = pathOf text 0f originY size
            let b = p.GetBounds()
            let dx = (float32 canvasW - b.Width) / 2.0f - b.X
            use m = new System.Drawing.Drawing2D.Matrix()
            m.Translate(dx, 0f)
            p.Transform(m)
            g.DrawPath(pen, p)            // 黑色描边（外扩）
            g.FillPath(Brushes.White, p)  // 白色填充
    bmp

/// Bitmap → ImageSharp → Wu 量化 → TGA type9 字节
let encodeTga (bmp: Bitmap) : byte[] * int =
    use ms = new MemoryStream()
    bmp.Save(ms, ImageFormat.Png)
    ms.Position <- 0L
    use src = Image.Load<Rgba32>(ms)
    use q = src.Clone(fun (x: IImageProcessingContext) -> x.Quantize(WuQuantizer(QuantizerOptions(MaxColors = 256))) |> ignore)
    let palette = ResizeArray<Rgba32>()
    let colorToIdx = Dictionary<Rgba32, byte>()
    let w, h = q.Width, q.Height
    let indices = Array.zeroCreate<byte> (w * h)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            let c = q[x, y]
            match colorToIdx.TryGetValue c with
            | true, idx -> indices[y * w + x] <- idx
            | false, _ ->
                if palette.Count >= 256 then failwith "量化后仍超过 256 色"
                let idx = byte palette.Count
                palette.Add c
                colorToIdx[c] <- idx
                indices[y * w + x] <- idx
    while palette.Count < 256 do
        palette.Add(Rgba32(0uy, 0uy, 0uy, 0uy))
    let fileOrder = Array.zeroCreate<byte> (w * h)
    for y in 0 .. h - 1 do
        Array.blit indices (y * w) fileOrder ((h - 1 - y) * w) w
    let nextPow2 (n: int) =
        let mutable p = 1
        while p < n do
            p <- p * 2
        p
    let footerDim =
        Array.concat [ BitConverter.GetBytes(uint16 (nextPow2 w)); BitConverter.GetBytes(uint16 (nextPow2 h)) ]
    let tga =
        { Id = Array.empty; CmapStart = 0; Palette = palette.ToArray()
          Width = w; Height = h; Desc = 0x08
          XOrigin = 0; YOrigin = 0
          Indices = fileOrder
          RleTrailing = Array.empty
          FooterDim = footerDim }
    TgaLib.encode tga, colorToIdx.Count

// ---------------- 单图处理 ----------------
let baseSize = bodySize()
let baseKanji = kanjiBounds baseSize
printfn "字体: %s @ %.2f px（国 %.1f×%.1f）  描边笔宽 %.1f"
    family.Name baseSize baseKanji.Width baseKanji.Height STROKE_W

let renderOne (key: string) =
    let src = loadSrc key
    let orig, cn = textsOf key
    let texts = decideText key src orig cn
    // 宽度自适应：任何一行超宽则整图等比缩字号
    let maxW = float32 src.Width - SIDE_MARGIN * 2.0f
    let mutable scale = 1.0f
    for t in texts do
        if t.Trim() <> "" then
            use p = pathOf t 0f 0f baseSize
            let bw = p.GetBounds().Width
            if bw > maxW then scale <- min scale (maxW / bw)
    let size = baseSize * scale
    let kanji = if scale = 1.0f then baseKanji else kanjiBounds size
    let rows = texts |> Array.mapi (fun i t -> (t, src.Centers[min i (src.Centers.Length - 1)]))
    let bmp = renderTelop src.Width src.Height rows size kanji
    Directory.CreateDirectory(outPngDir) |> ignore
    bmp.Save(Path.Combine(outPngDir, key + ".png"), ImageFormat.Png)
    let tgaBytes, colors = encodeTga bmp
    bmp.Dispose()
    Directory.CreateDirectory(outHipDir) |> ignore
    File.WriteAllBytes(Path.Combine(outHipDir, key + ".hip"), tgaBytes)
    printfn "%-22s 行段=%A 缩放=%.2f 用色=%d → %s.hip (%d 字节)"
        key (src.Centers |> Array.map int) scale colors key tgaBytes.Length
    for (t, _) in rows do
        printfn "      「%s」" t

// ---------------- 安装 ----------------
let install () =
    Directory.CreateDirectory(backupDir) |> ignore
    let mutable n = 0
    for f in Directory.GetFiles(outHipDir, "*.hip") do
        let name = Path.GetFileName f
        let dst = Path.Combine(gameDir, name)
        if File.Exists dst && not (File.Exists(Path.Combine(backupDir, name))) then
            File.Copy(dst, Path.Combine(backupDir, name))
        File.Copy(f, dst, true)
        n <- n + 1
    printfn "已安装 %d 个台词图（原文件备份在 %s）" n backupDir

match args with
| [||] ->
    eprintfn "用法: dotnet fsi remake-telop.fsx all|install|<key>..."
    exit 2
| [| "install" |] -> install ()
| [| "all" |] -> for k in telopKeys () do renderOne k
| keys -> for k in keys do renderOne k
