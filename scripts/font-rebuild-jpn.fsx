// jpn_glyph 重建（v4）：UI 全局字库，Gray16 + 统一字号渲染 + 按「真实上屏文本」裁剪
//
// 背景与四次踩坑史见 docs/tables/font-coverage.md §6–§7。v4 针对前几轮失败点逐条对齐：
//
//   1. **必须 Gray16**（原版就是 16bpp 灰度 = 单通道** alpha 遮罩，只能白字，无描边）。
//      v1/v3 用 Indexed8 无论怎么配调色板都渲染成黑色 —— 全游戏 1055 个 HIP 里**没有一个
//      8bpp 索引文件**（docs/formats/hip.md §1.2），引擎的索引路径根本没被测过，别再试。
//   2. **统一字号 + 统一绘制原点**（照 font-rebuild.fsx 的 calibrate）：v2/v3 的
//      「逐字把墨迹拉伸到 39 高」会让笔画少的字被放大 → 中文字面大小不一。现在所有字形共用
//      同一字号、同一原点，字身在框内的位置与原版日文字形一致（实测「漢」bbox 落在 (1,5)-(31,34)）。
//   3. **按真实需求集裁剪**：原库 2859 个字形里 695 个 CJK 用不到（体积瓶颈）。
//      需求集 = `jpn_idlist.txt`（UI 固定文本的本地化表，897 字符，✅ 只缺 1 个字形）
//             ∪ 译文 ∪ 未译 pac 原文 ∪ 脚本字符串 ∪ ASCII。
//      **非 CJK 字形（假名/符号/拉丁）一律保留**，只裁 CJK —— 漏字的后果是显示空白（不崩），
//      且可以用 --keep-all 一键回到「原库全保留」。
//
// 用法:
//   dotnet fsi scripts/font-rebuild-jpn.fsx                 # 试运行：只算体积 + 出预览图
//   dotnet fsi scripts/font-rebuild-jpn.fsx --keep-all      # 不裁原字形（体积会超，仅对照）
//   dotnet fsi scripts/font-rebuild-jpn.fsx --apply         # 写 work/patch-jpn/
//   dotnet fsi scripts/font-rebuild-jpn.fsx --apply --install  # 装机（自动备份）
//
// ⚠️ 装机后必须完全退出游戏再启动（引擎缓存旧纹理）。
#r "nuget: System.Drawing.Common, 8.0.0"
#load "hip.fsx"

open System
open System.IO
open System.Text.Json
open System.Drawing
open System.Drawing.Imaging
open System.Drawing.Text
open HipLib

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let gameDir = @"<游戏安装目录>\data\text\jpn"
let outDir = Path.Combine(workspace, "work", "patch-jpn")
let backupDir = Path.Combine(workspace, "work", "backup", "text", "jpn")
let pristineDir = Path.Combine(workspace, "work", "pristine", "text-jpn")

let args = fsi.CommandLineArgs |> Array.skip 1
let apply = args |> Array.contains "--apply"
let install = args |> Array.contains "--install"
let keepAll = args |> Array.contains "--keep-all"

// ---------------- 图集 / 字形框参数（按原 jpn_glyph 实测） ----------------
// 原版：图集 2048×2048 Gray16、字形框 **32×39**（2579/2860 恰为 32 宽，全部 39 高）、
// 行高 39 无间隔、紧密 64 列排列。汉字的墨迹在框内落在 (1,5)-(31,34)（墨高 28~30）。
let ATLAS_W = 2048
let CELL_W = 32           // 字形框宽（= 步进）
let CELL_H = 39           // 字形框高（= 行高）
let ROW_H = CELL_H           // 行距 = 框高（原版同样紧密，无间隔）
let GAP_X = 0             // 行内间隔 = 0（原版 64 列紧密排列；UV 由引擎内缩半像素）
let BODY_H = 29           // 目标字身墨迹高（原版「漢」= 29）
let INK_LEFT = 1          // 墨迹左边界（原版「漢」dx=1）
let INK_BOTTOM = 34       // 墨迹底边（原版「漢」dy 底 = 34）
let MAX_HIP = 3_600_000   // sub_510050 的 operator new[](3600000)
let MAX_ABC = 600_000
let FONT_PATH = @"C:\Windows\Fonts\Deng.ttf"

// ---------------- 读原字库（**必须读 pristine**，游戏目录/备份都可能已是产物） ----------------
let srcDir =
    if File.Exists(Path.Combine(pristineDir, "jpn_glyph.abc")) then pristineDir
    elif File.Exists(Path.Combine(backupDir, "jpn_glyph.abc")) then backupDir
    else gameDir
let abc = File.ReadAllBytes(Path.Combine(srcDir, "jpn_glyph.abc"))
let srcHipBytes = File.ReadAllBytes(Path.Combine(srcDir, "jpn_glyph.hip"))
let src = HipLib.decode srcHipBytes
if src.Format <> Gray16 then failwithf "预期 Gray16，实际 %A" src.Format
printfn "源字库 %s：abc %d / hip %d 字节（%dx%d %A %A）" srcDir
    abc.Length srcHipBytes.Length src.ImageWidth src.ImageHeight src.Format src.Encoding

let entryCount = int (BitConverter.ToUInt16(abc, 20))
let countOff = 24 + 2 * entryCount
let srcGlyphCount = BitConverter.ToInt32(abc, countOff)
let rec24Base = countOff + 4
let rec32Base = rec24Base + 24 * srcGlyphCount
printfn "EntryCount=%d（最大码位 U+%04X） GlyphCount=%d" entryCount (entryCount - 1) srcGlyphCount

/// 原图集灰度（Gray16 → 0..255）
let srcGray = Array.zeroCreate<byte> (src.ImageWidth * src.ImageHeight)
for i in 0 .. srcGray.Length - 1 do
    let v16 = int src.Pixels.[i * 2] ||| (int src.Pixels.[i * 2 + 1] <<< 8)
    srcGray.[i] <- byte ((v16 * 255 + 32767) / 65535)

let srcRect (g: int) =
    let o = rec32Base + 32 * g
    BitConverter.ToInt32(abc, o), BitConverter.ToInt32(abc, o + 4),
    BitConverter.ToInt32(abc, o + 8), BitConverter.ToInt32(abc, o + 12)

let mapOf (cp: int) = if cp <= entryCount then int (BitConverter.ToUInt16(abc, 22 + 2 * cp)) else 0

// ---------------- 需求集：真实会上屏的字符 ----------------
let needed = Collections.Generic.HashSet<int>()
let addRange (a: int) (b: int) = for cp in a .. b do needed.Add cp |> ignore
// ASCII 全收（数字/时间/EPISODE 等）
addRange 0x20 0x7E

// ① UI 固定文本的本地化表 —— **这是前几轮"无法静态穷举"的那个缺口**
let idlistPath = Path.Combine(gameDir, "jpn_idlist.txt")
if File.Exists idlistPath then
    let txt = File.ReadAllText(idlistPath, Text.Encoding.Unicode)
    let mutable n = 0
    for line in txt.Split('\n') do
        if not (line.TrimStart().StartsWith "//") then
            for c in line do needed.Add(int c) |> ignore; n <- n + 1
    printfn "① jpn_idlist.txt：%d 字符入需求集" n
else eprintfn "⚠️ 找不到 %s（UI 固定文本会缺字形）" idlistPath

// ② 译文（work/atf-cn）
let jopts = JsonDocumentOptions(AllowTrailingCommas = true)
let readJsonChars (dir: string) (filter: string -> bool) =
    if not (Directory.Exists dir) then 0
    else
        let mutable n = 0
        for f in Directory.GetFiles(dir, "*.json") do
            if filter (Path.GetFileNameWithoutExtension f) then
                let d = JsonDocument.Parse(File.ReadAllText f, jopts)
                for r in d.RootElement.GetProperty("records").EnumerateArray() do
                    let v = (try r.GetProperty("value").GetString() with _ -> "")
                    for c in v do needed.Add(int c) |> ignore; n <- n + 1
        n
let cnNames =
    Directory.GetFiles(Path.Combine(workspace, "work", "atf-cn"), "*.json")
    |> Array.map Path.GetFileNameWithoutExtension
    |> Set.ofArray
printfn "② 译文：%d 字符入需求集" (readJsonChars (Path.Combine(workspace, "work", "atf-cn")) (fun _ -> true))
// ③ 未译 pac 的日文原文（已译 pac 的原文不上屏）
printfn "③ 未译原文：%d 字符入需求集"
    (readJsonChars (Path.Combine(workspace, "work", "atf")) (fun name -> not (cnNames.Contains name)))

// ④ 脚本字符串（说话人名等）
let scrPath = Path.Combine(workspace, "work", "inventory", "script-sjis-strings.txt")
if File.Exists scrPath then
    let mutable n = 0
    for line in File.ReadLines scrPath do
        for c in line do needed.Add(int c) |> ignore; n <- n + 1
    printfn "④ 脚本字符串：%d 字符入需求集" n

// ---------------- 保留 / 新增 ----------------
let isCjk (cp: int) = (cp >= 0x3400 && cp <= 0x9FFF) || (cp >= 0xF900 && cp <= 0xFAFF)
let mappedCodes = [| for cp in 0 .. entryCount do if mapOf cp <> 0 then yield cp |]
let keep =
    if keepAll then mappedCodes
    else mappedCodes |> Array.filter (fun cp -> not (isCjk cp) || needed.Contains cp)
let dropped = mappedCodes |> Array.filter (fun cp -> not (keep |> Array.contains cp))
let addCodes = needed |> Seq.filter (fun cp -> cp > 0x1F && cp <= entryCount && mapOf cp = 0) |> Seq.sort |> Array.ofSeq
printfn "原映射 %d → 保留 %d（非CJK 全留）/ 丢弃 %d / 新增 %d ⇒ 字形总数 %d"
    mappedCodes.Length keep.Length dropped.Length addCodes.Length (keep.Length + addCodes.Length)

// ---------------- 字体校准（统一字号 + 统一原点） ----------------
let pfc = PrivateFontCollection()
pfc.AddFontFile FONT_PATH
let fontFamily = pfc.Families[0]

/// 在大画布上渲染码位（固定原点 80,80），返回灰度图 + 墨迹 bbox
let renderBig (size: float32) (cp: int) =
    let W = 240
    use bmp = new Bitmap(W, W, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
    g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
    g.Clear(Color.Black)
    use brush = new SolidBrush(Color.White)
    use font = new Font(fontFamily, size, GraphicsUnit.Pixel)
    g.DrawString(string (char cp), font, brush, PointF(60f, 60f))
    let data = bmp.LockBits(Rectangle(0, 0, W, W), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
    let gray = Array.zeroCreate<byte> (W * W)
    let mutable x1, y1, x2, y2 = W, W, 0, 0
    for y in 0 .. W - 1 do
        for x in 0 .. W - 1 do
            let v = Runtime.InteropServices.Marshal.ReadByte(data.Scan0, y * data.Stride + x * 4 + 2)
            gray.[y * W + x] <- v
            if v > 8uy then
                if x < x1 then x1 <- x
                if x + 1 > x2 then x2 <- x + 1
                if y < y1 then y1 <- y
                if y + 1 > y2 then y2 <- y + 1
    bmp.UnlockBits data
    gray, (x1, x2, y1, y2)

let mutable fontSize = 40f
let mutable drawX, drawY = 0f, 0f
// 校准：字号使「漢」墨高 = BODY_H；原点使「漢」墨迹左上落在 (INK_LEFT, INK_BOTTOM - BODY_H)
do
    for _ in 1 .. 4 do
        let _, (x1, x2, y1, y2) = renderBig fontSize (int '漢')
        if y2 - y1 > 0 then
            fontSize <- fontSize * float32 BODY_H / float32 (y2 - y1)
    let _, (x1, x2, y1, y2) = renderBig fontSize (int '漢')
    drawX <- float32 INK_LEFT - float32 (x1 - 60) + 60f
    drawY <- float32 (INK_BOTTOM - BODY_H) - float32 (y1 - 60) + 60f
    printfn "字体校准：%s @ %.2f px，「漢」墨迹 %dx%d → 框内 (%d,%d)-(%d,%d)"
        fontFamily.Name fontSize (x2 - x1) (y2 - y1) INK_LEFT (INK_BOTTOM - BODY_H) (INK_LEFT + x2 - x1) INK_BOTTOM

/// 渲染码位 → 32×39 灰度格（统一字号 + 统一原点，与原版日文字形同基线）
let renderCell (cp: int) : byte[] =
    let W = 240
    use bmp = new Bitmap(W, W, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.SmoothingMode <- Drawing2D.SmoothingMode.AntiAlias
    g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
    g.Clear(Color.Black)
    use brush = new SolidBrush(Color.White)
    use font = new Font(fontFamily, fontSize, GraphicsUnit.Pixel)
    g.DrawString(string (char cp), font, brush, PointF(drawX, drawY))
    let data = bmp.LockBits(Rectangle(0, 0, W, W), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
    let out = Array.zeroCreate<byte> (CELL_W * CELL_H)
    // 从校准原点 (60,60) 起裁 —— 校准已保证「漢」墨迹左上落在该点 + (INK_LEFT, INK_BOTTOM-BODY_H)
    for y in 0 .. CELL_H - 1 do
        for x in 0 .. CELL_W - 1 do
            out.[y * CELL_W + x] <-
                Runtime.InteropServices.Marshal.ReadByte(data.Scan0, (60 + y) * data.Stride + (60 + x) * 4 + 2)
    bmp.UnlockBits data
    out

// 溢出自检：若字形墨迹贴到框左右边界（说明被裁），提示调小 BODY_H
let mutable overflow = 0
let probes = "漢国職鬱翻戴養驚鑑愛譜標準議護"
for c in probes do
    let cell = renderCell (int c)
    let mutable leftInk = false
    let mutable rightInk = false
    for y in 0 .. CELL_H - 1 do
        if cell.[y * CELL_W] > 40uy then leftInk <- true
        if cell.[y * CELL_W + CELL_W - 1] > 40uy then rightInk <- true
    if leftInk || rightInk then overflow <- overflow + 1
printfn "溢出自检：%d/%d 个探针字形贴到框边界（>0 说明字号偏大，需调 BODY_H 或 INK_LEFT）" overflow probes.Length

// ---------------- 打包到图集 ----------------
type Placed = { Code: int; X: int; Y: int; W: int; H: int; Pixels: byte[]; Src: int }

let placed = ResizeArray<Placed>()
let mutable curX = 0
let mutable curRow = 0
let mutable atlasH = 0
// 索引 0 = 1×1 透明占位（mapping=0 的码位渲染空白；引擎按 0-based 直接索引，见 §6.5）
placed.Add({ Code = 0; X = 0; Y = 0; W = 1; H = 1; Pixels = [| 0uy |]; Src = -1 })
atlasH <- 1

let place (cp: int) (pixels: byte[]) (w: int) (h: int) (srcGlyph: int) =
    if curX + w > ATLAS_W then
        curX <- 0
        curRow <- curRow + 1
    let y = curRow * ROW_H
    let p = { Code = cp; X = curX; Y = y; W = w; H = h; Pixels = pixels; Src = srcGlyph }
    placed.Add p
    atlasH <- max atlasH (y + h)
    curX <- curX + w + GAP_X

// 保留的原字形：按 **原框尺寸** 原样复制（⚠️ 不能统一成 32 宽：rec24 步进沿用的是原宽
// 26/24/16 等，UV 框若是 32 宽而步进 26 会让相邻字形重叠 6px）
for cp in keep do
    let g = mapOf cp
    let x1, x2, y1, y2 = srcRect g
    let w, h = x2 - x1, y2 - y1
    let buf = Array.zeroCreate<byte> (w * h)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            buf.[y * w + x] <- srcGray.[(y1 + y) * src.ImageWidth + x1 + x]
    place cp buf w h g
let keptCount = placed.Count - 1
// 新增字形：Deng.ttf 统一字号渲染
// 灰度量化：Gray16 + RawRepeat 的体积完全取决于"同值 run 的长度"，而 GDI 抗锯齿会在一格里
// 产生 30+ 个不同灰阶 → 平均 run 只有 1~2 像素（每 run 编码 3 字节，比裸 2 字节还差）。
// 量化到 QUANT 级（默认 24 级，步长 ~11）能显著拉长 run 而不影响观感：字形上屏时还会被
// 引擎按 4/3 缩放 + alpha 混合，肉眼分辨不出 24 级以下的台阶。
let quantLevel =
    let idx = args |> Array.tryFindIndex ((=) "--levels")
    match idx with
    | Some i when i + 1 < args.Length -> int args.[i + 1]
    | _ -> 24
let quantAll = args |> Array.contains "--quant-all"
let quant (v: byte) =
    if v = 0uy || quantLevel <= 0 then v
    else byte (int ((int v * quantLevel + 127) / 255) * 255 / quantLevel)
let mutable blank = 0
for cp in addCodes do
    let cell = renderCell cp |> Array.map quant
    if not (Array.exists (fun v -> v > 0uy) cell) then
        blank <- blank + 1
        eprintfn "⚠️ U+%04X（%c）渲染为空" cp (char cp)
    else place cp cell CELL_W CELL_H -1
printfn "已布局 %d 个字形（保留 %d + 新增 %d），渲染空 %d；图集高度 %d"
    placed.Count keptCount (placed.Count - 1 - keptCount) blank atlasH

let atlasH' = ((atlasH + ROW_H - 1) / ROW_H) * ROW_H
printfn "图集尺寸：%d × %d（原版 %dx%d）" ATLAS_W atlasH' src.ImageWidth src.ImageHeight

let atlas = Array.zeroCreate<byte> (ATLAS_W * atlasH')
for p in placed do
    for y in 0 .. p.H - 1 do
        Array.blit p.Pixels (y * p.W) atlas ((p.Y + y) * ATLAS_W + p.X) p.W
// 保留的原字形也可量化（--quant-all）：原版字形本身灰阶就很少，量化几乎无视觉差
if quantAll then
    for i in 0 .. atlas.Length - 1 do atlas.[i] <- quant atlas.[i]

// 诊断：灰阶种类数 + 相邻同值 run 总数（决定 RawRepeat 体积）
do
    let hist = Array.zeroCreate<int> 256
    for v in atlas do hist.[int v] <- hist.[int v] + 1
    let kinds = hist |> Array.filter (fun c -> c > 0) |> Array.length
    let mutable runs = 0
    for i in 1 .. atlas.Length - 1 do
        if atlas.[i] <> atlas.[i - 1] then runs <- runs + 1
    printfn "图集诊断：灰阶 %d 种，run 数 %d（平均 run 长 %.2f）" kinds runs (float atlas.Length / float (runs + 1))

// ---------------- 重建 abc ----------------
let mapBuf = Array.zeroCreate<byte> (2 * (entryCount + 1))
let glyphCount = placed.Count
let newRec24 = Array.zeroCreate<byte> (24 * glyphCount)
let newRec32 = Array.zeroCreate<byte> (32 * glyphCount)
/// 度量模板：原版汉字 rec24 尾 8B = (0, 32, 32, 0)（左撑/宽/advance/0）
let templateGlyph =
    mappedCodes |> Array.tryFind (fun cp -> cp >= 0x4E00 && cp <= 0x9FFF && mapOf cp <> 0)
    |> Option.defaultValue mappedCodes.[0] |> mapOf

for i in 0 .. glyphCount - 1 do
    let p = placed.[i]
    // mapping：码位 → 字形索引（0-based 直接索引）
    if p.Code <= entryCount then
        let b = BitConverter.GetBytes(uint16 i)
        mapBuf.[2 * p.Code] <- b.[0]
        mapBuf.[2 * p.Code + 1] <- b.[1]
    let u1 = float32 p.X / float32 ATLAS_W
    let v1 = float32 p.Y / float32 atlasH'
    let u2 = float32 (p.X + p.W) / float32 ATLAS_W
    let v2 = float32 (p.Y + p.H) / float32 atlasH'
    let o24 = 24 * i
    Array.blit (BitConverter.GetBytes u1) 0 newRec24 o24 4
    Array.blit (BitConverter.GetBytes v1) 0 newRec24 (o24 + 4) 4
    Array.blit (BitConverter.GetBytes u2) 0 newRec24 (o24 + 8) 4
    Array.blit (BitConverter.GetBytes v2) 0 newRec24 (o24 + 12) 4
    if p.Src >= 0 then Array.blit abc (rec24Base + 24 * p.Src + 16) newRec24 (o24 + 16) 8
    else Array.blit abc (rec24Base + 24 * templateGlyph + 16) newRec24 (o24 + 16) 8
    let o32 = 32 * i
    Array.blit (BitConverter.GetBytes p.X) 0 newRec32 o32 4
    Array.blit (BitConverter.GetBytes (p.X + p.W)) 0 newRec32 (o32 + 4) 4
    Array.blit (BitConverter.GetBytes p.Y) 0 newRec32 (o32 + 8) 4
    Array.blit (BitConverter.GetBytes (p.Y + p.H)) 0 newRec32 (o32 + 12) 4
    if p.Src >= 0 then Array.blit abc (rec32Base + 32 * p.Src + 16) newRec32 (o32 + 16) 16
    else Array.blit abc (rec32Base + 32 * templateGlyph + 16) newRec32 (o32 + 16) 16

// 布局：22 字节头 + mapping(2×(EntryCount+1)) + u32 GlyphCount + rec24 + rec32
// （⚠️ 头是 **22** 字节，不是 24 —— 见 docs/formats/abc-hip-font.md §2）
let newAbc = Array.zeroCreate<byte> (22 + 2 * (entryCount + 1) + 4 + 24 * glyphCount + 32 * glyphCount)
Array.blit abc 0 newAbc 0 24
Array.blit mapBuf 0 newAbc 22 mapBuf.Length
Array.blit (BitConverter.GetBytes glyphCount) 0 newAbc countOff 4
Array.blit newRec24 0 newAbc (countOff + 4) newRec24.Length
Array.blit newRec32 0 newAbc (rec24Base + 24 * glyphCount) newRec32.Length

// ---------------- 编码 HIP：Gray16 + RawRepeat（与原版一致） ----------------
let gray16 = Array.zeroCreate<byte> (ATLAS_W * atlasH' * 2)
for i in 0 .. atlas.Length - 1 do
    let v = uint16 atlas.[i] * 257us      // 8bit → 16bit 满量程（原版最大值 65535）
    gray16.[i * 2] <- byte v
    gray16.[i * 2 + 1] <- byte (v >>> 8)
let hipImg =
    { Format = Gray16
      Encoding = EncRawRepeat
      CanvasWidth = ATLAS_W; CanvasHeight = atlasH'
      ImageWidth = ATLAS_W; ImageHeight = atlasH'
      OffsetX = 0; OffsetY = 0
      Layered = false
      Palette = None
      Pixels = gray16 }
let newHip = HipLib.encode hipImg

printfn "\n新 abc：%d 字节（上限 %d，%.0f%%）" newAbc.Length MAX_ABC (float newAbc.Length * 100.0 / float MAX_ABC)
printfn "新 hip：%d 字节（上限 %d，%.0f%%）" newHip.Length MAX_HIP (float newHip.Length * 100.0 / float MAX_HIP)
let over = newHip.Length > MAX_HIP || newAbc.Length > MAX_ABC
if over then eprintfn "❌ 超出装载硬上限，不能装机。"

// 自检：回解尺寸一致 + 映射抽查（v1 曾因 mapping 偏移一位把「Page」显示成「Qbhf」）
let chk = HipLib.decode newHip
if chk.ImageWidth <> ATLAS_W || chk.ImageHeight <> atlasH' then eprintfn "❌ 回解尺寸不符"
elif chk.Format <> Gray16 then eprintfn "❌ 回解格式不符"
else printfn "✅ 回解校验通过（Gray16 %dx%d）" chk.ImageWidth chk.ImageHeight

let lookup = Collections.Generic.Dictionary<int, Placed>()
for p in placed do if not (lookup.ContainsKey p.Code) then lookup.[p.Code] <- p
let inkCount (p: Placed) =
    let mutable n = 0
    for v in p.Pixels do if v > 8uy then n <- n + 1
    n
printfn "映射抽查（码位 → 字形索引 → 框内墨迹像素数）："
for c in "Page1顺欢买后边柜読字あ" do
    match lookup.TryGetValue(int c) with
    | true, p -> printfn "  %c U+%04X → glyph %d  框(%d,%d)+%dx%d  墨迹 %d" c (int c) (Array.IndexOf(placed.ToArray(), p)) p.X p.Y p.W p.H (inkCount p)
    | _ -> eprintfn "  ⚠️ %c U+%04X 无映射" c (int c)

// 预览图：把若干新增字形 + 保留字形拼成一张 PNG（深灰底，看大小/位置是否统一）
let previewPath = Path.Combine(workspace, "work", "patch-jpn", "preview.png")
do
    Directory.CreateDirectory outDir |> ignore
    let cols = 24
    let newSample = [| for cp in addCodes -> cp, "new" |]
    let sample =
        Array.append
            (Array.sub newSample 0 (min 200 newSample.Length))
            [| for cp in [ '漢'; '日'; 'あ'; 'ア'; 'A'; '1'; '。'; '、'; 'ー'; '読'; '字'; '国' ] -> int cp, "old" |]
    let rows = (sample.Length + cols - 1) / cols
    let scale = 2
    use bmp = new Bitmap(cols * CELL_W * scale, rows * (CELL_H + 4) * scale, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.Clear(Color.FromArgb(255, 40, 40, 48))
    let lookup = Collections.Generic.Dictionary<int, Placed>()
    for p in placed do if not (lookup.ContainsKey p.Code) then lookup.[p.Code] <- p
    for idx in 0 .. sample.Length - 1 do
        let cp, _ = sample.[idx]
        let col, row = idx % cols, idx / cols
        match lookup.TryGetValue cp with
        | true, p ->
            for y in 0 .. p.H - 1 do
                for x in 0 .. p.W - 1 do
                    let v = int p.Pixels.[y * p.W + x]
                    if v > 0 then
                        let c = Color.FromArgb(v, 255, 255, 255)
                        g.FillRectangle(new SolidBrush(c),
                            Rectangle((col * CELL_W + x) * scale, (row * (CELL_H + 4) + y) * scale, scale, scale))
        | _ -> ()
    bmp.Save(previewPath, ImageFormat.Png)
    printfn "预览图：%s（前 %d 个新增字形 + 日文原字形对照）" previewPath (min 200 addCodes.Length)

if not apply then
    printfn "\n（试运行；加 --apply 写 work/patch-jpn/）"
elif over then
    eprintfn "已跳过写盘。"
else
    Directory.CreateDirectory outDir |> ignore
    File.WriteAllBytes(Path.Combine(outDir, "jpn_glyph.abc"), newAbc)
    File.WriteAllBytes(Path.Combine(outDir, "jpn_glyph.hip"), newHip)
    printfn "已写出 %s" outDir
    if install then
        Directory.CreateDirectory backupDir |> ignore
        for n in [ "jpn_glyph.abc"; "jpn_glyph.hip" ] do
            let dst = Path.Combine(gameDir, n)
            let bak = Path.Combine(backupDir, n)
            if not (File.Exists bak) then File.Copy(dst, bak)
            File.Copy(Path.Combine(outDir, n), dst, true)
            printfn "已装机 %s（原文件备份 %s）" n bak
        printfn "\n⚠️ 完全退出游戏再启动。"
