// 标题菜单中文重制：9 项 × 2 态 = 18 张
// 原图规格（实测）：画布尺寸各异（136~256 宽 / 40~64 高），off = 白字(254,255,255)+黑描边，
// on = 红字(225,12,37)+白描边。字体选华康新综艺（对比 6 款后最接近原图的粗笔圆润手写感，
// 且与已验收的台词图风格统一）。
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/tga.fsx"

open System
open System.IO
open System.Drawing
open System.Drawing.Drawing2D
open System.Drawing.Text
open System.Drawing.Imaging
open TgaLib

// ⚠️ 原图一律从 work/pristine/ 取（Steam 校验后的只读基准）。
// 绝不要读游戏目录：那里随时被自己的产物覆盖，尺寸会一路漂移（上轮 160→164→154 就是这么来的）。
let gameDir = @"work/pristine/story-image"
let outDir = "work/patch-menu"

let OFF_FILL = Color.FromArgb(254, 255, 255)
let OFF_EDGE = Color.Black
let ON_FILL = Color.FromArgb(225, 12, 37)
let ON_EDGE = Color.FromArgb(254, 255, 255)

/// 字芯高（= 原图字形本身的高度，不含描边）与描边宽，按**主菜单 / 子菜单**分两档：
///
///   * 主菜单 01~04（画布 40~48 高、行距 80）：沿用第三版观感 —— 字芯 34、描边每边 1.5
///     （用户确认「主菜单完美」，不要动）；
///   * 子菜单 05~09（序章/浩/匡/抗/返回；行距仅 45 屏幕 px）：统一小字 —— 字芯 **22**、
///     描边每边 **3（off）/ 2（on）**。比原版的粗描边（6.5/4.5）细一半，留出呼吸空间。
///     墨迹高 = 22+6 = 28（off）/ 22+4 = 26（on），画布 64 / 40 都有余量，
///     on 也因此能完整做 −4px 的垂直补偿（原版字芯 29 时墨迹 38，画布 40 装不下上移）。
///
/// ⚠️ **alpha 墨迹高 ≠ 字高**：原图 `_01_off` 墨迹高 37 里字芯只有 24，其余全是描边。
/// ⚠️ GDI+ 的 `Pen(width)` 是**笔的直径**（向外各扩 width/2），所以传「每边 × 2」。
let subMenu (prefix: string) = prefix >= "title_menu_05"

let coreEdge (prefix: string) (suffix: string) =
    if subMenu prefix then
        if suffix = "off" then (22.0f, 3.0f * 2.0f) else (22.0f, 2.0f * 2.0f)
    else
        (34.0f, 3.0f)

/// (文件前缀, 中文文字, 画布宽覆盖)
///
/// 排版规则（2026-10-19 实机量化，见 docs/tables/ui-text-source.md §13.5/§13.7）：
///   * 引擎按**画布中心**锚点定位（反推 C≈220 对多项吻合）；
///   * 但纹理宽度会被**对齐到 16 的倍数**再算中心 —— 136/168/40 宽会因此整体偏左 4~12 UI px。
///     所以画布宽一律取 16 的倍数（下面把 136/168 改成 160）。
///   * ⇒ 只要**墨迹居中于画布**，屏幕位置就自动对齐 C / 各行中心。
let items =
    [ "title_menu_01", "从头开始", None
      "title_menu_02", "继续游戏", None
      "title_menu_03", "设置", Some 160        // 原 168（非 16 倍数 → 偏右 4 UI px）
      "title_menu_04", "开放之间", Some 160    // 原 136（非 16 倍数 → 偏左 12 UI px）
      "title_menu_05", "序章", None
      "title_menu_06", "浩", None
      "title_menu_07", "匡", None
      "title_menu_08", "抗", None
      "title_menu_09", "返回", None ]

let pfc = new PrivateFontCollection()
pfc.AddFontFile @"font\华康新综艺w7.ttf"
let fam = pfc.Families.[0]
printfn "字体：%s   主菜单 字芯34/描边1.5   子菜单 字芯22/描边3(off)、2(on)" fam.Name

/// 测原图墨迹 bbox（alpha > 0，含极淡抗锯齿边缘与描边）
let inkBbox (t: TgaImage) =
    let flip = (t.Desc &&& 0x20) = 0
    let mutable minX, minY, maxX, maxY = t.Width, t.Height, -1, -1
    for y in 0 .. t.Height - 1 do
        let sy = if flip then t.Height - 1 - y else y
        for x in 0 .. t.Width - 1 do
            let c = t.Palette.[int t.Indices.[sy * t.Width + x]]
            if int c.A > 0 then
                minX <- min minX x
                maxX <- max maxX x
                minY <- min minY y
                maxY <- max maxY y
    if maxX < 0 then (0, 0, t.Width - 1, t.Height - 1) else (minX, minY, maxX, maxY)

/// 渲染一张：字形墨迹**居中于画布**（可选整体垂直补偿 dy），字芯高按参数（不含描边）
///
/// 实测依据（2026-10-19 标尺图实验，见 docs/tables/ui-text-source.md §13.6）：
///   * 引擎给每个菜单项开**槽位**（尺寸由 off 图决定），on 图**按原版槽位尺寸**定位 ——
///     on 图画布左边缘恒在 UI x≈142，与给多大的纹理无关。所以 **on 图必须保持原版画布尺寸**，
///     改成 256×64 会整块右溢 105 UI px（第 5 版的失败原因）。
///   * on 图在槽位里居中，但实测**垂直低 4px**，用 dy = -4 补偿。
let renderGlyph (cw: int) (ch: int) (text: string) (coreH: float32) (dy: float32) (fill: Color) (edge: Color) (edgeW: float32) =
    let bmp = new Bitmap(cw, ch, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.SmoothingMode <- SmoothingMode.AntiAlias
    g.TextRenderingHint <- TextRenderingHint.AntiAlias
    g.Clear(Color.Transparent)

    let sf = StringFormat.GenericTypographic
    sf.FormatFlags <- sf.FormatFlags ||| StringFormatFlags.NoClip

    // targetH 是**字芯**高（GraphicsPath.GetBounds 量的就是字形轮廓，不含描边）
    let targetH = coreH

    // 用 GraphicsPath.GetBounds() 直接量字形实际尺寸（MeasureString 含行距，折算不准会溢出画布）
    let mutable size = 64.0f
    for _ in 1 .. 6 do
        let probe = new GraphicsPath()
        probe.AddString(text, fam, int FontStyle.Regular, size, PointF(0.0f, 0.0f), sf)
        let pb = probe.GetBounds()
        probe.Dispose()
        if pb.Height > 0.0f && pb.Width > 0.0f then size <- size * (targetH / pb.Height)

    let path = new GraphicsPath()
    path.AddString(text, fam, int FontStyle.Regular, size, PointF(0.0f, 0.0f), sf)

    // ① 字形墨迹左上角移到原点（AddString 的 bounds 原点可能为负）
    let b0 = path.GetBounds()
    use m0 = new Matrix()
    m0.Translate(-b0.X, -b0.Y)
    path.Transform m0

    // ② 宽度超出画布时**以左边缘为基准横向压缩**（字高不变）——
    // 中文方块字比日文假名宽（假名「戻る」宽/字高 ≈ 0.75，汉字 ≈ 1.03），换字体无效：
    // 实测 7 款中文字体宽/高比都是 1.01~1.06。压缩后再整体居中。
    let avail = float32 cw - 2.0f - edgeW
    let mutable inkW = b0.Width
    if b0.Width > avail then
        use m1 = new Matrix()
        m1.Scale(avail / b0.Width, 1.0f)
        path.Transform m1
        inkW <- avail

    // ③ 整体居中于画布（独立矩阵，避免缩放连带平移 —— GDI+ 的已知坑）
    use m2 = new Matrix()
    m2.Translate((float32 cw - inkW) / 2.0f, (float32 ch - targetH) / 2.0f + dy)
    path.Transform m2

    if edgeW > 0.0f then
        use pen = new Pen(edge, edgeW)
        pen.LineJoin <- LineJoin.Round
        g.DrawPath(pen, path)
    use brush = new SolidBrush(fill)
    g.FillPath(brush, path)
    path.Dispose()
    bmp

/// Bitmap → TGA type9 字节（复用 tga.fsx 的编码，仅 256 色量化）
let encodeTga (orig: TgaImage) (cw: int) (ch: int) (bmp: Bitmap) =
    let colors = Collections.Generic.Dictionary<int, byte>()
    let palette = ResizeArray<SixLabors.ImageSharp.PixelFormats.Rgba32>()
    let indices = Array.zeroCreate<byte> (bmp.Width * bmp.Height)
    let idxAt (x: int) (y: int) =
        let c = bmp.GetPixel(x, y)
        let key = ((int c.A) <<< 24) ||| ((int c.R) <<< 16) ||| ((int c.G) <<< 8) ||| (int c.B)
        match colors.TryGetValue key with
        | true, i -> i
        | _ ->
            let i = byte palette.Count
            colors.[key] <- i
            palette.Add(SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, c.A))
            i
    // TGA Desc bit5 = 0 表示首行是图像底行（bottom-left origin），写入时必须翻转 Y
    let flipY = (orig.Desc &&& 0x20) = 0
    for y in 0 .. bmp.Height - 1 do
        let sy = if flipY then bmp.Height - 1 - y else y
        for x in 0 .. bmp.Width - 1 do
            indices.[sy * bmp.Width + x] <- idxAt x y
    if palette.Count > 256 then failwithf "调色板超 256 色：%d" palette.Count
    // 以原图为模板，只换调色板与索引 —— 保留 Id/CmapStart/Desc/Origin/RleTrailing/页脚等全部字段
    // ⚠️ Width/Height 必须同步更新（画布宽有改动），否则索引错位会实机花屏
    let t = { orig with Width = cw; Height = ch; Palette = palette.ToArray(); Indices = indices }
    TgaLib.encode t, palette.Count

Directory.CreateDirectory outDir |> ignore
let mutable n = 0
for (prefix, text, wOverride) in items do
    for suffix in [ "off"; "on" ] do
        let fill, edge =
            if suffix = "off" then (OFF_FILL, OFF_EDGE) else (ON_FILL, ON_EDGE)
        let coreH, ew = coreEdge prefix suffix
        let origPath = Path.Combine(gameDir, sprintf "%s_%s.hip" prefix suffix)
        let orig = TgaLib.decode (File.ReadAllBytes origPath)
        // 画布宽取 16 的倍数（引擎按纹理宽对齐后算中心）；**高度一律保持原样**
        let cw =
            match wOverride with
            | Some w -> w
            | None -> if orig.Width % 16 = 0 then orig.Width else (orig.Width / 16 + 1) * 16
        let ch = orig.Height
        // 06~09 的 on 画布（40 高）比 off（64 高）矮，引擎按槽位算出的位置天然低 4px → 补偿上移。
        // 05 的 on/off 画布相同（144×40），没有这个偏差，不补偿。
        let dy = if suffix = "on" && prefix >= "title_menu_06" then -4.0f else 0.0f
        use bmp = renderGlyph cw ch text coreH dy fill edge ew
        let bytes, colors = encodeTga orig cw ch bmp
        let path = Path.Combine(outDir, sprintf "%s_%s.hip" prefix suffix)
        File.WriteAllBytes(path, bytes)
        printfn "  %-22s %3dx%-3d (原 %3dx%-3d)  字芯%.0f 描边%.1f dy=%+.0f  %3d 色  %6d 字节"
            (sprintf "%s_%s.hip" prefix suffix) cw ch orig.Width orig.Height coreH ew dy colors bytes.Length
        n <- n + 1
printfn "\n共生成 %d 张 → %s" n outDir
