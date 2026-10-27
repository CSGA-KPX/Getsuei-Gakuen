// font-rebuild.fsx — 字库整体重建：全量重渲染 2048x2048 图集 + 中文新字形 + abc 重建 + atf 注入
//
// 用法:
//   dotnet fsi font-rebuild.fsx <pac名>     单个 pac（试点/调试）
//   dotnet fsi font-rebuild.fsx all         全量 237 个 → work/patch-pac/
//
// 方案（2026-10-18 定稿，侦查依据见 docs 待补）：
//   - 字体：华康新综艺w7.ttf 单字体（CJK 全覆盖：假名/标点/简体/日汉字，实测缺的都是无笔画占位码位）
//   - 原字形也用它重渲染（游戏原字体为综艺体系，DF 对比证实风格一致；渲染参数按原字面校准）
//   - 字形矩形统一 34x49（与原版一致），定宽行排 60 列 x 41 行 = 2460 容量（最大 pac 1851 字）
//   - 图集统一重建 2048x2048：TGA pac → 256 灰阶调色板 type9；HIP pac → Gray16 EncRawRepeat
//   - abc：mapping 沿用+追加；rec24 UV 重算、度量沿用（原 glyph）/克隆（新增）；rec32 rect 重算、尾 16B 沿用/克隆
//   - 度量语义未破解（技术债），沿用/克隆既有值 —— smoke B 已实机验证该做法上屏正确
//   - atf 用 work/atf-cn-manual（手工，优先）或 work/atf-cn（GalTransl）译文重建；无译文的 pac 原样重建
// 产物: work/patch-pac/<name>.pac（不直接写游戏；安装用 install 子命令或手动复制）
#r "nuget: System.Drawing.Common, 8.0.0"
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "Pac.fsx"
#load "AtfJson/Atf.fs"
#load "AtfJson/Json.fs"
#load "tga.fsx"
#load "hip.fsx"

open System
open System.IO
open System.Collections.Generic
open System.Drawing
open System.Drawing.Imaging
open System.Drawing.Text
open PacLib
open AtfJson
open TgaLib
open HipLib

let gameTextDir = @"<游戏安装目录>\data\story\text\jpn"
/// 译文目录（按优先级）：**手工翻译**优先，其次 GalTransl 回填结果。
/// 分开存放是为了能一眼看出哪些是我手写的、哪些是机翻+人工校对的，互不覆盖。
/// 注意 galtransl.fsx import 会写 work/atf-cn，所以手工译文不能放那里。
let atfCnDirs = [ "work/atf-cn-manual"; "work/atf-cn" ]

/// 取某 pac 的译文 JSON 路径（manual 优先）
let cnJsonPath (name: string) =
    atfCnDirs
    |> List.map (fun d -> Path.Combine(d, name + ".json"))
    |> List.tryFind File.Exists
let outDir = "work/patch-pac"

let args = fsi.CommandLineArgs |> Array.skip 1
let target = if args.Length = 0 then "" else args[0]

// ---------------- 常量（按原字库校准） ----------------
let CELL_W = 34
let CELL_H = 49
// 行内**列间隔 1px**：定宽格子紧排时，笔画宽的汉字墨迹会贴到格子左右边界，与相邻字形
// 的边缘连成一条 1px 竖线（实机反馈「深色背景下能、我左边有细线」）。加 1px 空白后，
// 加上引擎 UV 半像素内缩，两格墨迹视觉相距 ~2px。代价：列数 60 → 58，容量 2460 → 2378
// （当前最大单 pac 1854 格，仍有余量）。
let CELL_STRIDE = CELL_W + 1
let COLS = 2048 / CELL_STRIDE     // 58
let ROWS = 2048 / CELL_H          // 41
let CAPACITY = COLS * ROWS        // 2378
let GLYPH_PX = 34 * 49            // 单字形矩形像素数
let RENDER_SIZE = 30f             // 初始渲染字号（校准用起点）
let TARGET_BODY_H = 29            // 原字形 bbox 高（49 框内汉字 y=11..39）
let TARGET_LEFT = 3               // 汉字 bbox 左边界（原版实测 1..3）
let TARGET_TOP = 11               // 汉字 bbox 上边界（原版实测 9..11）

// ---------------- 字体 ----------------
// 字体：等线（Windows 自带 Deng.ttf）。字重实测（「世」墨迹像素）：原版 178 / 华文细黑 260 /
// 等线 339 / 微软雅黑 363 / 华康新综艺 568。华文细黑实机偏细，取等线（比细黑粗一档、仍远细于华康）。
// 覆盖率实测：译文 ∪ 假名 ∪ 全角标点 3298 码位 0 空渲染。换字体只改这行（校准自动重算）。
let FONT_PATH = @"C:\Windows\Fonts\Deng.ttf"
let pfc = PrivateFontCollection()
pfc.AddFontFile FONT_PATH
let fontFamily = pfc.Families[0]

/// 大画布渲染码位 → (bbox, 灰度裁剪)；绘制原点固定 (40,40)，调用方可换算相对偏移
let renderBig (size: float32) (cp: int) : int * int * int * int * byte[] =
    use bmp = new Bitmap(160, 160, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage(bmp)
    g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
    g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
    g.Clear(Color.Black)
    use brush = new SolidBrush(Color.White)
    g.DrawString(string (char cp), new Font(fontFamily, size, GraphicsUnit.Pixel), brush, PointF(40f, 40f))
    let data = bmp.LockBits(Rectangle(0, 0, 160, 160), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
    let gray = Array.zeroCreate<byte> (160 * 160)
    let mutable x1, y1, x2, y2 = 160, 160, 0, 0
    let mutable nz = 0
    for y in 0 .. 159 do
        for x in 0 .. 159 do
            let v = System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, y * data.Stride + x * 4 + 2)
            gray[y * 160 + x] <- v
            if v > 0uy then
                nz <- nz + 1
                if x < x1 then x1 <- x
                if x + 1 > x2 then x2 <- x + 1
                if y < y1 then y1 <- y
                if y + 1 > y2 then y2 <- y + 1
    bmp.UnlockBits(data)
    if nz = 0 then 0, 0, 0, 0, [||]
    else
        let w, h = x2 - x1, y2 - y1
        let crop = Array.zeroCreate<byte> (w * h)
        for y in 0 .. h - 1 do
            Array.blit gray ((y1 + y) * 160 + x1) crop (y * w) w
        x1, y1, w, h, crop

/// 校准：字号使「漢」bbox 高 = 29，原点使该 bbox 在格内**水平居中**、顶边落在 y=11（原版基线）
/// 关键：全字库共用同一绘制原点（= 同一基线），**不再按各字形 bbox 顶端二次对齐**，
/// 否则 `一`/`。`/`、` 这类"高矮不一"的字形会被顶到格子上沿（2026-10-19 实机反馈修复）。
let calibrate () : float32 * float32 * float32 =
    let mutable size = RENDER_SIZE
    for _ in 1..3 do
        let _, _, _, h, _ = renderBig size (int '漢')
        if h > 0 then size <- size * (float32 TARGET_BODY_H / float32 h)
    let x1, y1, w, h, _ = renderBig size (int '漢')
    if h = 0 then failwith "校准失败：「漢」渲染为空"
    // 水平居中（不同字体的字面宽度不同，居中可保证左右描边对称且不被格子裁掉）
    let x0 = float32 (CELL_W - w) / 2.0f - float32 (x1 - 40)
    let y0 = float32 TARGET_TOP - float32 (y1 - 40)
    printfn "字体校准: %s @ %.2f px（漢 bbox %d×%d → 高 %d；绘制原点 x=%.1f y=%.1f）"
        fontFamily.Name size w h TARGET_BODY_H x0 y0
    size, x0, y0

/// 破折号类码位：字体里 U+2014/U+2500/U+2501 与「一」几乎同形（宽 30、粗 4px），
/// U+2015 字形更是异常（渲染成顶端 1px 细线），统一改成**自绘全宽细横线**，
/// 与「一」（宽 28、粗 5px 的字体字形）明显区分（2026-10-19 实机反馈修复）。
let DASH_CPS = set [ 0x2014; 0x2015; 0x2500; 0x2501 ]

/// 描边外扩像素。原版约 2px 基准 + 右下投影（见 outlineMask），黑边四边实测 左 1-2 / 右 3-4 / 上 2 / 下 3
let OUTLINE_PX = 2

/// 黑边右下偏移（像素）：复刻原版"白字 + 右下投影"的立体感
let SHADOW_DX = 2
let SHADOW_DY = 1

/// 字形覆盖率（34x49，0..255）：统一原点绘制，字形按字体自身基线/中宫落位
let renderGlyph (size: float32, x0: float32, y0: float32) (cp: int) : byte[] =
    if DASH_CPS.Contains cp then
        // 破折号：**直接在 mask 上写整数像素行**（不走 GDI）。
        // 用 FillRectangle 时半像素坐标会被抗锯齿摊成 3 行不等亮（50%/100%/50%），
        // 经亮度提升后边缘参差 → 实机"描边有毛刺"。直写 2 行纯白才是平直干净。
        Array.init GLYPH_PX (fun i ->
            let y = i / CELL_W
            if y = 24 || y = 25 then 255uy else 0uy)
    else
        use bmp = new Bitmap(CELL_W, CELL_H, PixelFormat.Format32bppArgb)
        use g = Graphics.FromImage(bmp)
        g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
        g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
        g.Clear(Color.Black)
        use brush = new SolidBrush(Color.White)
        g.DrawString(string (char cp), new Font(fontFamily, size, GraphicsUnit.Pixel), brush, PointF(x0, y0))
        let data = bmp.LockBits(Rectangle(0, 0, CELL_W, CELL_H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        let gray = Array.zeroCreate<byte> GLYPH_PX
        for y in 0 .. CELL_H - 1 do
            for x in 0 .. CELL_W - 1 do
                gray[y * CELL_W + x] <-
                    System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, y * data.Stride + x * 4 + 2)
        bmp.UnlockBits(data)
        gray

/// 覆盖率膨胀（可分离 1D 最大值滤波），用于生成描边覆盖率
let dilate (mask: byte[]) (r: int) : byte[] =
    let tmp = Array.zeroCreate<byte> GLYPH_PX
    let out = Array.zeroCreate<byte> GLYPH_PX
    for y in 0 .. CELL_H - 1 do
        for x in 0 .. CELL_W - 1 do
            let mutable m = 0uy
            for dx in -r .. r do
                let xx = x + dx
                if xx >= 0 && xx < CELL_W && mask[y * CELL_W + xx] > m then m <- mask[y * CELL_W + xx]
            tmp[y * CELL_W + x] <- m
    for y in 0 .. CELL_H - 1 do
        for x in 0 .. CELL_W - 1 do
            let mutable m = 0uy
            for dy in -r .. r do
                let yy = y + dy
                if yy >= 0 && yy < CELL_H && tmp[yy * CELL_W + x] > m then m <- tmp[yy * CELL_W + x]
            out[y * CELL_W + x] <- m
    out

/// 从图集边界 flood fill 出"字形外部的背景"掩码。
/// 只对这些像素加黑描边，字内空隙（如「口」「田」的封闭内腔）保持透明 —— 否则描边会把
/// 笔画间隙塞满，小字号下整片糊成一团（实机观感"挤/乱"的主因）。
let exteriorMask (body: byte[]) : bool[] =
    let ext = Array.zeroCreate<bool> GLYPH_PX
    let q = Queue<int>()
    let tryPush (i: int) =
        if not ext[i] && int body[i] < 16 then
            ext[i] <- true
            q.Enqueue i
    for x in 0 .. CELL_W - 1 do
        tryPush x
        tryPush ((CELL_H - 1) * CELL_W + x)
    for y in 0 .. CELL_H - 1 do
        tryPush (y * CELL_W)
        tryPush (y * CELL_W + CELL_W - 1)
    while q.Count > 0 do
        let i = q.Dequeue()
        let x = i % CELL_W
        let y = i / CELL_W
        if x > 0 then tryPush (i - 1)
        if x < CELL_W - 1 then tryPush (i + 1)
        if y > 0 then tryPush (i - CELL_W)
        if y < CELL_H - 1 then tryPush (i + CELL_W)
    ext

/// 描边遮罩：膨胀 + 右下偏移后取并集。
/// 原版字形的黑边**本来就不对称**（实测左 1-2 / 右 3-4 / 上 2 / 下 3 —— 白字 + 右下投影的立体感），
/// 均匀描边会显得扁平且更容易糊（2026-10-19 对照原版字库确认）。
let outlineMask (body: byte[]) : byte[] =
    let od = dilate body OUTLINE_PX
    let shifted =
        Array.init GLYPH_PX (fun i ->
            let sx = (i % CELL_W) - SHADOW_DX
            let sy = (i / CELL_W) - SHADOW_DY
            if sx >= 0 && sx < CELL_W && sy >= 0 && sy < CELL_H then od[sy * CELL_W + sx] else 0uy)
    Array.init GLYPH_PX (fun i -> max od[i] shifted[i])

/// 字形 → TGA 索引：
/// - 128..255 = 白色字身，**不透明灰度**（调色板给 (g,g,g,255)，g 随索引从暗到亮）。
///   必须不透明：若字身也用 alpha 渐变，游戏把 49px 行缩小显示时白色会被背景稀释，
///   实机表现为"字发灰/发蓝"（2026-10-19 反馈修复；原版字身条目 idx197-211 同样是 (灰度,255)）。
/// - 1..127 = 黑色描边，alpha 渐变（只有最外缘半透明做抗锯齿，核心是纯黑）。
/// - 内腔（不与画布边界连通的封闭空隙，如「口」「目」的内框）只描 1px：完全不描会显得字是空心的，
///   但用 2px 内填会把笔画间隙塞死、整片糊（2026-10-19 反馈修复）。
let composeIndex (body: byte[]) (outlineExt: byte[]) (outlineInt: byte[]) : byte[] =
    let ext = exteriorMask body
    Array.init GLYPH_PX (fun i ->
        let t = int body[i]
        if t >= 16 then
            // 字身：覆盖率先做亮度提升（×2 饱和）——GDI 抗锯齿下小字号笔画中心常只到 150~200，
            // 直接映射会让字整体发灰、笔画变虚；提升后笔画中心稳定纯白，边缘仍按 t 渐变
            byte (128 + min 127 (min 255 (t * 2) * 127 / 255))
        else
            let o = if ext[i] then int outlineExt[i] else int outlineInt[i]
            if o >= 16 then byte (1 + min 126 (o * 4))   // o>=32 即饱和为纯黑
            else 0uy)

// ---------------- abc 结构 ----------------
type AbcInfo =
    { Bytes: byte[]
      EntryCount: int; GlyphCount: int
      MapOff: int; CountOff: int; Rec24Base: int; Rec32Base: int }

let abcParse (b: byte[]) : AbcInfo =
    let ec = int (BitConverter.ToUInt16(b, 20))
    let countOff = 24 + 2 * ec
    let gc = BitConverter.ToInt32(b, countOff)
    let r24 = countOff + 4
    { Bytes = b; EntryCount = ec; GlyphCount = gc
      MapOff = 22; CountOff = countOff; Rec24Base = r24; Rec32Base = r24 + 24 * gc }

// ---------------- 单 pac 重建 ----------------
let rebuildPac (fontParams: float32 * float32 * float32) (pacPath: string) (outPath: string) : string[] =
    let warnings = ResizeArray<string>()
    let name = Path.GetFileNameWithoutExtension(pacPath)
    let pac = PacLib.read pacPath
    let find n = pac.Entries |> Array.find (fun e -> e.Filename = n)
    let abcBytes = PacLib.extract pac (find "font.abc")
    let hipBytes = PacLib.extract pac (find "font.hip")
    let atfBytes = PacLib.extract pac (find "string.atf")
    let abc = abcParse abcBytes

    // 1. 码位集：原 mapping 非零 ∪ 译文新增汉字
    let cnPath = cnJsonPath name
    let newChars = ResizeArray<char>()
    match cnPath with
    | Some p ->
        let a = AtfJsonIO.readJson p
        let b = AtfJsonIO.readJson (Path.Combine("work/atf", name + ".json"))
        let hs = HashSet<char>()
        for i in 0 .. a.Records.Length - 1 do
            if a.Records[i].Value <> b.Records[i].Value then
                for c in a.Records[i].Value do
                    // 码位合法范围是 0..EntryCount（含端点：EntryCount 即最大 char code，
                    // U+FF1F=65311 恰为某 pac 的 EntryCount，用 `<` 会丢掉该字）。
                    // 除汉字外，译文用到的**全角标点**（，。！？「」…… 等）原日文字库多半没有，
                    // 同样必须追加，否则上屏空白（2026-10-19 复查发现）。
                    if int c > 0x20 && int c <= abc.EntryCount then hs.Add c |> ignore
        for c in hs |> Seq.sort do
            if int (BitConverter.ToUInt16(abc.Bytes, abc.MapOff + 2 * int c)) = 0 then
                newChars.Add c
    | None -> ()
    let newCount = newChars.Count
    let total = abc.GlyphCount + newCount
    if total > CAPACITY then
        failwithf "%s: 字形总数 %d 超容量 %d" name total CAPACITY

    // 码位 → glyph 索引（原：mapping 值；新增：GlyphCount + i）
    let cpToGlyph = Dictionary<int, int>()
    for cp in 0 .. abc.EntryCount do
        let v = int (BitConverter.ToUInt16(abc.Bytes, abc.MapOff + 2 * cp))
        if v > 0 then cpToGlyph[cp] <- v
    // 防御：mapping 值应 < GlyphCount；越界的丢弃（原数据可能有垃圾槽）
    let badCps = cpToGlyph |> Seq.filter (fun kv -> kv.Value >= abc.GlyphCount) |> Seq.map (fun kv -> kv.Key) |> Seq.toArray
    for cp in badCps do
        cpToGlyph.Remove(cp) |> ignore
        warnings.Add(sprintf "mapping[U+%04X]=%d 越界(>GlyphCount)，已忽略" cp (abc.GlyphCount))
    for i in 0 .. newCount - 1 do
        cpToGlyph[int newChars[i]] <- abc.GlyphCount + i

    // 2. 排版：glyph 索引顺序 0..total-1 → (col,row)；列向右让出 1px 间隔
    let rectOf (g: int) : int * int * int * int =
        let col = g % COLS
        let row = g / COLS
        let x1 = col * CELL_STRIDE
        (x1, x1 + CELL_W, row * CELL_H, (row + 1) * CELL_H)

    // 3. 逐字形渲染（glyph 索引 → 码位 反查）
    let glyphToCp = Array.zeroCreate<int> total
    for kv in cpToGlyph do
        if kv.Value < total then glyphToCp[kv.Value] <- kv.Key
    // 渲染缓存（同码字 across pac 复用由调用方做；这里每 pac 独立）
    let isHip = HipLib.isHip hipBytes

    // 4. 构建新图集像素
    let tgaNew =
        if not isHip then
            let indices = Array.zeroCreate<byte> (2048 * 2048)
            Some indices, None
        else
            let px = Array.zeroCreate<byte> (2048 * 2048 * 2)
            None, Some px
    let (tgaIdx, hipPx) = tgaNew

    let writeGlyph (g: int) (data: byte[]) =
        let (x1, x2, y1, y2) = rectOf g
        match tgaIdx, hipPx with
        | Some idx, None ->
            for y in 0 .. CELL_H - 1 do
                let sy = 2047 - (y1 + y)          // TGA 底行存储
                let dst = sy * 2048 + x1
                for x in 0 .. CELL_W - 1 do
                    idx[dst + x] <- data[y * CELL_W + x]
        | None, Some px ->
            // HIP(Gray16) 单通道 = alpha 遮罩，只能白字（原版 HIP 字库同样无描边）
            for y in 0 .. CELL_H - 1 do
                let dst = ((y1 + y) * 2048 + x1) * 2
                for x in 0 .. CELL_W - 1 do
                    let v = uint16 data[y * CELL_W + x] * 257us   // 8→16 满量程对齐（原图最大值 65535）
                    px[dst + x * 2] <- byte v
                    px[dst + x * 2 + 1] <- byte (v >>> 8)
        | _ -> failwith "unreachable"

    /// 覆盖率 → 写入用数据（TGA 走双色索引编码，HIP 直接用覆盖率）
    /// 破折号**不用右下投影**：投影偏移在连续破折号的接缝处会让第二个格子的左边缘缺 1-2px 黑边，
    /// 实机看就是"描边有毛刺"；水平线用均匀描边才严丝合缝。
    let toData (cp: int) (body: byte[]) =
        if isHip then body
        elif DASH_CPS.Contains cp then
            let od = dilate body OUTLINE_PX
            composeIndex body od od
        else
            composeIndex body (outlineMask body) (dilate body OUTLINE_PX)

    // 渲染所有字形（含原字形重渲染）
    let renderCache = Dictionary<int, byte[]>()
    for g in 0 .. total - 1 do
        let cp = glyphToCp[g]
        if cp = 0 then
            warnings.Add(sprintf "glyph %d 无码位（映射空洞），写空" g)
            writeGlyph g (Array.zeroCreate<byte> GLYPH_PX)
        else
            match renderCache.TryGetValue(cp) with
            | true, data -> writeGlyph g data
            | _ ->
                let body = renderGlyph fontParams cp
                let mutable nz = 0
                for v in body do if v > 0uy then nz <- nz + 1
                if nz = 0 && cp <> 0x3000 && cp <> 0xFF00 then   // 全角空格等无笔画属正常
                    warnings.Add(sprintf "U+%04X(%c) 渲染为空" cp (char cp))
                let data = toData cp body
                renderCache[cp] <- data
                writeGlyph g data

    // 5. 图集编码
    let hipOut =
        if isHip then
            let img =
                { Format = Gray16; Encoding = EncRawRepeat
                  CanvasWidth = 2048; CanvasHeight = 2048
                  ImageWidth = 2048; ImageHeight = 2048
                  OffsetX = 0; OffsetY = 0; Layered = false
                  Palette = None; Pixels = hipPx.Value }
            HipLib.encode img
        else
            // 调色板（复刻原版语义，见 docs/formats/abc-hip-font.md §1.5）：
            //   索引 0       全透明（背景）
            //   索引 1..127  黑描边，(0,0,0,alpha) —— 只有最外缘半透明
            //   索引 128..255 白字身，(g,g,g,**255**) 不透明灰度
            // ⚠️ 三代踩坑（都实机验证过）：(v,v,v,255) 全不透明 → 文字带黑底方块；
            //    (0,0,0,v) 字身也变黑 → 整片黑字；(0,0,0,v) 字身 alpha 渐变 → 缩小显示时
            //    白色被背景稀释，实机"字发灰/发蓝"。字身必须不透明灰度。
            let palette =
                Array.init 256 (fun i ->
                    if i = 0 then SixLabors.ImageSharp.PixelFormats.Rgba32(0uy, 0uy, 0uy, 0uy)
                    elif i < 128 then
                        SixLabors.ImageSharp.PixelFormats.Rgba32(0uy, 0uy, 0uy, byte (min 255 (i * 255 / 127)))
                    else
                        let g = byte (min 255 ((i - 127) * 255 / 128))
                        SixLabors.ImageSharp.PixelFormats.Rgba32(g, g, g, 255uy))
            let img =
                { Id = [||]; CmapStart = 0; Palette = palette
                  Width = 2048; Height = 2048; Desc = 8
                  XOrigin = 0; YOrigin = 0
                  Indices = tgaIdx.Value; RleTrailing = [||]; FooterDim = [| 0uy; 8uy; 0uy; 8uy |] }
            TgaLib.encode img

    // 6. abc 重建
    // 模板 glyph：找一个 CJK 全角原字形（克隆度量/尾 16B 用）
    let templateV =
        let mutable t = 0
        for kv in cpToGlyph do
            if kv.Value < abc.GlyphCount && kv.Key >= 0x4E00 && kv.Key <= 0x9FFF then
                t <- kv.Value
        t
    let ms = new MemoryStream()
    ms.Write(abc.Bytes, 0, abc.MapOff)                                       // 头
    // mapping 区 = [MapOff, CountOff)（长 2*EntryCount+2，尾部 2 字节为未知字段，原样保留）
    let mapBuf = Array.copy abc.Bytes[abc.MapOff .. abc.CountOff - 1]
    for i in 0 .. newCount - 1 do
        BitConverter.GetBytes(uint16 (abc.GlyphCount + i)) |> fun b -> Array.blit b 0 mapBuf (2 * int newChars[i]) 2
    ms.Write(mapBuf, 0, mapBuf.Length)
    // GlyphCount = 原 + 新增
    ms.Write(BitConverter.GetBytes(total), 0, 4)
    // rec24：uv 重算 + 度量沿用/克隆
    let uvF (px: int) = BitConverter.GetBytes(single (float px / 2048.0))
    // 破折号字形：原日文字库的步进不统一（—=36、―=39，而格子只有 34 宽），
    // 连续破折号之间会露出 2-5px 缝、看起来"断线"。统一改成 34（= 格子宽）即可首尾相接。
    let dashGlyphs = HashSet<int>()
    for cp in DASH_CPS do
        match cpToGlyph.TryGetValue cp with
        | true, g -> dashGlyphs.Add g |> ignore
        | _ -> ()
    // 步进 33（格子 34）：游戏采样时 UV 每边内缩半像素，连续破折号之间还会剩 ~1px 缝，
    // 用 33 让相邻破折号重叠 1px（实机"差 1-2px"反馈修复）
    let dashMetric =
        Array.concat [ BitConverter.GetBytes(uint16 0); BitConverter.GetBytes(uint16 (CELL_W - 1))
                       BitConverter.GetBytes(uint16 (CELL_W - 1)); BitConverter.GetBytes(uint16 0) ]
    for g in 0 .. total - 1 do
        let (x1, x2, y1, y2) = rectOf g
        ms.Write(uvF x1, 0, 4)
        ms.Write(uvF y1, 0, 4)
        ms.Write(uvF x2, 0, 4)
        ms.Write(uvF y2, 0, 4)
        let srcOff =
            if g < abc.GlyphCount then abc.Rec24Base + 24 * g
            else abc.Rec24Base + 24 * templateV
        if dashGlyphs.Contains g then ms.Write(dashMetric, 0, 8)              // 破折号：全角步进
        else ms.Write(abc.Bytes, srcOff + 16, 8)                              // 度量沿用/克隆
    // rec32：rect 重算 + 尾 16B 沿用/克隆
    let i32 (v: int) = BitConverter.GetBytes(v)
    for g in 0 .. total - 1 do
        let (x1, x2, y1, y2) = rectOf g
        ms.Write(i32 x1, 0, 4)
        ms.Write(i32 x2, 0, 4)
        ms.Write(i32 y1, 0, 4)
        ms.Write(i32 y2, 0, 4)
        let srcOff =
            if g < abc.GlyphCount then abc.Rec32Base + 32 * g
            else abc.Rec32Base + 32 * templateV
        ms.Write(abc.Bytes, srcOff + 16, 16)
    let abcOut = ms.ToArray()

    // 7. 自检：重新解析
    let abc2 = abcParse abcOut
    if abc2.GlyphCount <> total then failwith "自检失败: GlyphCount"
    if abc2.Rec32Base + 32 * total <> abcOut.Length then failwithf "自检失败: abc 长度 %d" abcOut.Length
    for i in 0 .. newCount - 1 do
        let cp = int newChars[i]
        let v = int (BitConverter.ToUInt16(abcOut, abc2.MapOff + 2 * cp))
        if v <> abc.GlyphCount + i then failwithf "自检失败: mapping[U+%04X]" cp
    // 抽查 UV 与 rect 一致
    let chkG = min (total - 1) (abc.GlyphCount / 2)
    let (cx1, cx2, cy1, cy2) = rectOf chkG
    let u1 = BitConverter.ToSingle(abcOut, abc2.Rec24Base + 24 * chkG)
    let expect1 = single (float cx1 / 2048.0)
    if abs (u1 - expect1) > 1e-6f then failwith "自检失败: rec24 UV"

    // 8. atf 重建
    let atfOut =
        match cnPath with
        | Some p -> AtfBinary.buildBytes (AtfJsonIO.readJson p)
        | None ->
        // 无译文：原样重建（parse→build 恒等已验证）
        AtfBinary.buildBytes (AtfBinary.parseBytes atfBytes)
    // atf roundtrip 自检
    let atf2 = AtfBinary.parseBytes atfOut
    if atf2.Records.Length <> (AtfBinary.parseBytes atfBytes).Records.Length then
        failwith "自检失败: atf 记录数"

    // 9. 回包
    let contents =
        pac.Entries
        |> Array.map (fun e ->
            match e.Filename with
            | "font.hip" -> hipOut
            | "font.abc" -> abcOut
            | "string.atf" -> atfOut
            | f -> failwithf "未知条目 %s" f)
    let pacOut = PacLib.build pac contents
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)) |> ignore
    File.WriteAllBytes(outPath, pacOut)
    warnings.Add(sprintf "%s: %d原+%d新=%d 字形, pac %d→%d 字节" name abc.GlyphCount newCount total pac.Bytes.Length pacOut.Length)
    warnings.ToArray()

// ---------------- 入口 ----------------
let fontParams = calibrate()

match target with
| "" ->
    eprintfn "用法: dotnet fsi font-rebuild.fsx <pac名|all>"
    exit 2
| "all" ->
    Directory.CreateDirectory(outDir) |> ignore
    let pacs = Directory.GetFiles(gameTextDir, "*.pac") |> Array.sortBy id
    let mutable ok = 0
    let mutable warns = 0
    for p in pacs do
        let name = Path.GetFileNameWithoutExtension(p)
        try
            let ws = rebuildPac fontParams p (Path.Combine(outDir, name + ".pac"))
            for w in ws do
                if w.StartsWith(name) then printfn "%s" w
                else
                    warns <- warns + 1
                    eprintfn "  [%s] %s" name w
            ok <- ok + 1
        with ex ->
            eprintfn "FAIL %s: %s" name ex.Message
    printfn "==== font-rebuild: %d/%d OK（%d 警告）-> %s ====" ok pacs.Length warns outDir
| name ->
    let pacPath = Path.Combine(gameTextDir, name + ".pac")
    if not (File.Exists pacPath) then
        eprintfn "找不到 %s" pacPath
        exit 1
    Directory.CreateDirectory(outDir) |> ignore
    let ws = rebuildPac fontParams pacPath (Path.Combine(outDir, name + ".pac"))
    for w in ws do
        printfn "%s" w
    printfn "OK -> %s" (Path.Combine(outDir, name + ".pac"))
