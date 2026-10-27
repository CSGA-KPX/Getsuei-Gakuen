// UI 文字图汉化重制：把 AE pac 里的日文文字图（HIP）换成等线中文，**保持原图尺寸/格式/偏移**
//
// 背景：UI 文字是预渲染图（docs/tables/ui-images-triage.md §7，10/10 探针实证）。
// 做法（照 remake-telop 的路线，但目标是 pac 内嵌 HIP Argb32）：
//   1. 解码原 HIP → RGBA，量出**文字墨迹 bbox**（alpha ≥ 16）与主色；
//   2. 把 bbox（外扩 2px）清成透明；
//   3. 用 Deng.ttf 按「漢」墨迹高 = BodyH 校准字号，渲染中文，**水平居中于原 bbox、基线与原墨迹底对齐**；
//   4. 编码回 HIP（Argb32 + 原编码）→ 回写 tex.pac → 外层 pac（首次自动备份 work/backup/AE/）。
//
// ⚠️ 引擎按**原图尺寸**算槽位（标题菜单踩过坑），所以画布尺寸一律不变。
// ⚠️ 改完必须**完全退出游戏**再进，否则引擎沿用旧纹理。
// ⚠️ 认不出的艺术字（`UI_29_nowloading` 的「読」等）不重制 —— 用户明确要求，见 ui-images-triage §7。
//
// 用法:
//   dotnet fsi scripts/remake-ui-text.fsx list [pac 过滤]      # 打印重制表
//   dotnet fsi scripts/remake-ui-text.fsx preview [pac 过滤]   # 出「原图 | 新图」对比 PNG → work/ui-remake/
//   dotnet fsi scripts/remake-ui-text.fsx apply [pac 过滤]     # 写回游戏（自动备份）
//   dotnet fsi scripts/remake-ui-text.fsx restore              # 从 work/backup/AE/ 还原
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#r "nuget: System.Drawing.Common, 8.0.0"
#load "Pac.fsx"
#load "hip.fsx"

open System
open System.Drawing
open System.Drawing.Drawing2D
open System.Drawing.Imaging
open System.Drawing.Text
open System.IO
open HipLib
open PacLib

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let aeRoot = @"<游戏安装目录>\data\AE"
let backupRoot = Path.Combine(workspace, "work", "backup", "AE")
let outRoot = Path.Combine(workspace, "work", "ui-remake")
let FONT_PATH = @"C:\Windows\Fonts\Deng.ttf"

/// 一条重制记录：源图 + 中文（空串 = 只清空不写字，用于逐字动画多出来的帧）
type Ent =
    { Pac: string        // 相对 data\AE
      Inner: string      // 内层 pac（AE 目录是双层：aex.pac + tex.pac）
      Member: string     // 成员名（含 .hip）
      Cn: string
      BodyH: int         // 汉字墨迹目标高度（填空串时忽略）
      Note: string }

// ---------------- 字体校准 ----------------
let pfc = PrivateFontCollection()
pfc.AddFontFile FONT_PATH
let fontFamily = pfc.Families[0]

/// 渲染一行文字 → (alpha 蒙版, 宽, 高)。字号按「漢」墨迹高 = bodyH 校准（统一字号，不逐字拉伸）
let renderText (text: string) (bodyH: int) (maxW: int) =
    let mutable size = float32 bodyH * 1.6f
    let measure (sz: float32) (t: string) =
        use bmp = new Bitmap(4, 4)
        use g = Graphics.FromImage bmp
        use font = new Font(fontFamily, sz, GraphicsUnit.Pixel)
        g.MeasureString(t, font, PointF(0f, 0f), StringFormat.GenericTypographic)
    // 1) 按「漢」校准字号
    let mutable bboxH = 0
    for _ in 1 .. 3 do
        use bmp = new Bitmap(400, 400, PixelFormat.Format32bppArgb)
        use g = Graphics.FromImage bmp
        g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
        g.Clear(Color.Black)
        use font = new Font(fontFamily, size, GraphicsUnit.Pixel)
        g.DrawString("漢", font, Brushes.White, PointF(60f, 60f))
        let d = bmp.LockBits(Rectangle(0, 0, 400, 400), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        let mutable y1, y2 = 400, 0
        for y in 0 .. 399 do
            for x in 0 .. 399 do
                if Runtime.InteropServices.Marshal.ReadByte(d.Scan0, y * d.Stride + x * 4 + 2) > 8uy then
                    if y < y1 then y1 <- y
                    if y + 1 > y2 then y2 <- y + 1
        bmp.UnlockBits d
        bboxH <- y2 - y1
        if bboxH > 0 then size <- size * float32 bodyH / float32 bboxH
    // 2) 缩到不超宽（多行文本取最长一行）
    let parts = text.Split('\n')
    let mutable guard = 0
    let measureAll (sz: float32) =
        parts |> Array.map (fun p -> (measure sz p).Width) |> Array.max
    let mutable mw0 = measureAll size
    while mw0 > float32 maxW && guard < 20 do
        size <- size * 0.95f
        mw0 <- measureAll size
        guard <- guard + 1
    // 3) 正式渲染（四周留 10px 边距：on 态要在这里画辉光，边距不足会把光晕裁掉）
    //    多行：行距 = bodyH × 1.35（原版 512×64 的两行提示就是这个比例）
    let pad = 10
    let lineStep = int (Math.Round(float bodyH * 1.35))
    let w = max 1 (int (Math.Ceiling(float mw0)) + pad * 2)
    let h = max 1 (lineStep * (parts.Length - 1) + int (Math.Ceiling(float bodyH * 1.8)) + pad * 2)
    use bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)
    use g = Graphics.FromImage bmp
    g.TextRenderingHint <- TextRenderingHint.AntiAliasGridFit
    g.SmoothingMode <- SmoothingMode.AntiAlias
    g.Clear(Color.Black)
    use font = new Font(fontFamily, size, GraphicsUnit.Pixel)
    for i in 0 .. parts.Length - 1 do
        g.DrawString(parts.[i], font, Brushes.White,
                     PointF(float32 pad, float32 (pad + i * lineStep)), StringFormat.GenericTypographic)
    let d = bmp.LockBits(Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
    let mask = Array.zeroCreate<byte> (w * h)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            mask.[y * w + x] <- Runtime.InteropServices.Marshal.ReadByte(d.Scan0, y * d.Stride + x * 4 + 2)
    bmp.UnlockBits d
    mask, w, h

// ---------------- HIP ⇄ RGBA ----------------
/// HIP → RGBA（字节序 BGRA → RGBA）。非 Argb32 返回 None（本表只处理 Argb32）
let toRgba (img: HipImage) : byte[] option =
    if img.Format <> Argb32 then None
    else
        let px = img.Pixels
        let out = Array.zeroCreate<byte> (img.ImageWidth * img.ImageHeight * 4)
        for i in 0 .. (out.Length / 4) - 1 do
            out.[i * 4] <- px.[i * 4 + 2]
            out.[i * 4 + 1] <- px.[i * 4 + 1]
            out.[i * 4 + 2] <- px.[i * 4]
            out.[i * 4 + 3] <- px.[i * 4 + 3]
        Some out

let fromRgba (orig: HipImage) (rgba: byte[]) : HipImage =
    let px = Array.zeroCreate<byte> (rgba.Length)
    for i in 0 .. (rgba.Length / 4) - 1 do
        px.[i * 4] <- rgba.[i * 4 + 2]
        px.[i * 4 + 1] <- rgba.[i * 4 + 1]
        px.[i * 4 + 2] <- rgba.[i * 4]
        px.[i * 4 + 3] <- rgba.[i * 4 + 3]
    { orig with Format = Argb32; Encoding = EncRawRepeat; Palette = None; Pixels = px }

/// 量指定区域内的文字 bbox 与颜色：
///   `ink`  = alpha ≥ 16 → 清除范围（对带框的按钮图，用 `inset` 把框排除掉）；
///   `body` = alpha ≥ 200 且不太暗（排除黑描边）→ **定位用这个**（on 态 bbox 被辉光撑大，用 ink 定位会把字压低）。
///   `bodyColor` = body 里最亮 30% 的均值（on 态是「白色字芯 + 蓝色边缘」，取全均值会偏暗）；
///   `glowColor` = alpha 16..150 里饱和度最高的像素（on 态的蓝色辉光色）。
let inspect (rgba: byte[]) (w: int) (h: int) (rx1: int, ry1: int, rx2: int, ry2: int) (brightOnly: bool) =
    let mutable ix1, iy1, ix2, iy2 = Int32.MaxValue, Int32.MaxValue, -1, -1
    let mutable bx1, by1, bx2, by2 = Int32.MaxValue, Int32.MaxValue, -1, -1
    let bright = ResizeArray<byte * byte * byte>()
    let mutable gcr, gcg, gcb, gsat = 255uy, 255uy, 255uy, -1
    for y in max 0 ry1 .. min (h - 1) (ry2 - 1) do
        for x in max 0 rx1 .. min (w - 1) (rx2 - 1) do
            let i = (y * w + x) * 4
            let a = rgba.[i + 3]
            if a >= 16uy then
                if x < ix1 then ix1 <- x
                if x + 1 > ix2 then ix2 <- x + 1
                if y < iy1 then iy1 <- y
                if y + 1 > iy2 then iy2 <- y + 1
                let r = rgba.[i]
                let g = rgba.[i + 1]
                let b = rgba.[i + 2]
                let sat = max r (max g b) - min r (min g b)
                let isBright = max r (max g b) >= 90uy
                // 有底的按钮图（brightOnly）：只有"亮"像素才算字身 —— 否则半透明底会把 bbox 撑满整个区域
                if a >= 200uy && (isBright || not brightOnly) then
                    if x < bx1 then bx1 <- x
                    if x + 1 > bx2 then bx2 <- x + 1
                    if y < by1 then by1 <- y
                    if y + 1 > by2 then by2 <- y + 1
                    if isBright then bright.Add((r, g, b))
                elif a <= 150uy && int sat > gsat then
                    gsat <- int sat; gcr <- r; gcg <- g; gcb <- b
    let bodyColor =
        if bright.Count = 0 then (255uy, 255uy, 255uy)
        else
            // 优先用「最饱和」像素的颜色：彩色美术字（達成率/開放率 是红字 + 红辉光，
            // 字芯白、描边红）如果取逐通道最大会得到白色，跟原版差得远。
            // 只有饱和度确实很高（>100）才用饱和色，纯白/浅蓝高亮态仍走逐通道最大。
            let mutable bestSat, bestR, bestG, bestB = -1, 255uy, 255uy, 255uy
            for (r, g, b) in bright do
                let sat = int (max r (max g b)) - int (min r (min g b))
                if sat > bestSat then
                    bestSat <- sat; bestR <- r; bestG <- g; bestB <- b
            if bestSat > 100 then (bestR, bestG, bestB)
            else
                let mutable r0, g0, b0 = 0uy, 0uy, 0uy
                for (r, g, b) in bright do
                    if r > r0 then r0 <- r
                    if g > g0 then g0 <- g
                    if b > b0 then b0 <- b
                (r0, g0, b0)
    let glowColor = if gsat > 30 then (gcr, gcg, gcb) else bodyColor
    ((ix1, iy1, ix2, iy2), (bx1, by1, bx2, by2)), bodyColor, glowColor

/// 复合图只替换其中一半（用「图标/标签」与「正文/占位符」之间的空白列自动切分）：
///   `Right` = 只替换右半 —— `Icon_*` / `icon_16` / `Kom_*` 按键提示条（左半是手柄小图标，不能清）
///   `Left`  = 只替换左半 —— `open_item_description_BGM/CG/bonus_scenario/ending/profile`
///             （右半是「▮▮冊 / ▮▮冊 ▮▮%」占位符模板，清掉就没了）
///   `None`  = 整块
type Split = SplitNone | SplitLeft | SplitRight

let splitMode (memberName: string) =
    let m = memberName
    // 只有「左侧确实有手柄小图标」的这几张才切分（其余按键条是纯文字，整块替换即可）：
    // Icon_01 選択 / Icon_04 移動 / Icon_10 非表示（Q）/ Icon_14 ボイス再生（X）/ Icon_15・icon_16 選択（方向键）
    let hasIcon = m.StartsWith("Icon_") || m.StartsWith("icon_") || m.StartsWith("Kom_")
    if hasIcon then SplitRight
    elif m.StartsWith("open_item_description_")
         && (m.Contains "BGM" || m.Contains "CG" || m.Contains "bonus_scenario"
             || m.Contains "ending" || m.Contains "profile") then SplitLeft
    else SplitNone

/// 按键条专用：找到**第一段连续有内容的列**（= 手柄小图标），在其右侧留 4px 作为分界。
/// 图标与文字之间往往只有 2~3px 空隙，用"找空白列"会失败（2026-10-20 踩过）。
let splitAfterIcon (rgba: byte[]) (w: int) (rx1: int, ry1: int, rx2: int, ry2: int) =
    let n = rx2 - rx1
    if n < 20 then None
    else
        let colHasInk =
            Array.init n (fun i ->
                let mutable hit = false
                for y in ry1 .. ry2 - 1 do
                    if not hit && rgba.[(y * w + rx1 + i) * 4 + 3] >= 16uy then hit <- true
                hit)
        let mutable i = 0
        while i < n && not colHasInk.[i] do i <- i + 1      // 跳过左侧空白
        while i < n && colHasInk.[i] do i <- i + 1          // 走过第一段（图标）
        let rel = i + 4
        if rel >= int (float n * 0.20) && rel <= int (float n * 0.75) then Some(rx1 + rel)
        else None
let findSplit (rgba: byte[]) (w: int) (rx1: int, ry1: int, rx2: int, ry2: int) =
    let n = rx2 - rx1
    if n < 20 then None
    else
        let colHasInk =
            Array.init n (fun i ->
                let mutable hit = false
                for y in ry1 .. ry2 - 1 do
                    if not hit && rgba.[(y * w + rx1 + i) * 4 + 3] >= 16uy then hit <- true
                hit)
        let lo = int (float n * 0.15)
        let hi = int (float n * 0.65)
        let mutable i = lo
        let mutable found = -1
        while found < 0 && i < hi do
            if not colHasInk.[i] then
                let mutable j = i
                while j < n && not colHasInk.[j] do j <- j + 1
                if j - i >= 6 then found <- i + (j - i) / 2
                i <- j
            else i <- i + 1
        if found < 0 then None else Some(rx1 + found)

/// 全图量一遍（只为拿到 ink bbox，用于确定 inset / split 后的实际区域）
let inkBBox (rgba: byte[]) (w: int) (h: int) =
    let ((ink, _), _, _) = inspect rgba w h (0, 0, w, h) false
    ink

/// 原图 → 中文图（返回新的 RGBA）
let remake (rgba: byte[]) (w: int) (h: int) (ent: Ent) =
    // 带框的按钮图（`*botan*`，胶囊形底 + 亮边）不能整块清掉，否则框没了 —— 把清除区内缩 12px
    let inset = if ent.Member.Contains "botan" then 12 else 0
    let (fx1, fy1, fx2, fy2) = inkBBox rgba w h
    let (rx1, ry1, rx2, ry2) = (fx1 + inset, fy1 + inset, fx2 - inset, fy2 - inset)
    // 复合图（图标+文字 / 标签+占位符）只处理其中一半
    let (rx1, ry1, rx2, ry2) =
        match splitMode ent.Member with
        | SplitNone -> (rx1, ry1, rx2, ry2)
        | mode ->
            let split =
                if mode = SplitRight then splitAfterIcon rgba w (rx1, ry1, rx2, ry2)
                else findSplit rgba w (rx1, ry1, rx2, ry2)
            match split with
            | None ->
                // 按键条的手柄小图标固定约 30px 宽，找不到分界时按固定偏移切
                if mode = SplitRight then (min (rx1 + 36) (rx2 - 20), ry1, rx2, ry2)
                else
                    eprintfn "  ⚠️ %s 找不到标签与正文之间的空白列，退回整块" ent.Member
                    (rx1, ry1, rx2, ry2)
            | Some sx ->
                if mode = SplitLeft then (rx1, ry1, sx, ry2) else (sx, ry1, rx2, ry2)
    let ((ix1, iy1, ix2, iy2), (bx1, by1, bx2, by2)), (cr0, cg0, cb0), (gr, gg, gb) =
        inspect rgba w h (rx1, ry1, rx2, ry2) (inset > 0)
    // 按键提示条一律白字：图上还有手柄/鼠标图标的彩色像素（如红色按键），
    // 取"最饱和颜色"会被图标带偏成红字（2026-10-20 实机踩过）
    let (cr, cg, cb) =
        if splitMode ent.Member = SplitRight then (255uy, 255uy, 255uy) else (cr0, cg0, cb0)
    // 兜底：亮像素一个都没找到（灰字/暗底）时退回整个内缩区域的 bbox
    let (bx1, by1, bx2, by2) =
        if bx2 < 0 then (rx1, ry1, rx2, ry2) else (bx1, by1, bx2, by2)
    if ix2 < 0 then eprintfn "  ⚠️ %s 原图没有文字墨迹，跳过" ent.Member
    let out = Array.copy rgba
    // 清除区填充色：透明底图必须填全透明；有底的图（胶囊按钮等）填底色本身，否则框/底会被清掉。
    // ⚠️ 判据用「不透明像素占比」而不是中位数（2026-10-20 实机踩坑）：
    //    2~4 个字的图，文字几乎占满清除区，中位数会落在文字上 ⇒ 填出色块（设置界面到处是黑方块/蓝方块）。
    let regionPx =
        [| for y in max 0 ry1 .. min (h - 1) (ry2 - 1) do
             for x in max 0 rx1 .. min (w - 1) (rx2 - 1) do
                 let i = (y * w + x) * 4
                 yield (int rgba.[i] * 299 + int rgba.[i + 1] * 587 + int rgba.[i + 2] * 114,
                        rgba.[i], rgba.[i + 1], rgba.[i + 2], rgba.[i + 3]) |]
    let fillColor =
        if inset = 0 then (0uy, 0uy, 0uy, 0uy)     // 纯文字图（绝大多数）→ 一律填透明
        elif regionPx.Length = 0 then (0uy, 0uy, 0uy, 0uy)
        else
            // 只有胶囊按钮（inset > 0）才有底。底色取**四角小块**的中位数：
            // 胶囊内部有横纹、alpha 不均，取整区中位数会取到偏淡的像素 ⇒ 清完看起来"底部透明"。
            // 四角一定落在"底"上（不含文字），最稳。
            let cw = max 1 (min 8 ((rx2 - rx1) / 6))
            let ch = max 1 (min 8 ((ry2 - ry1) / 4))
            let corners =
                [| for (cx, cy) in [ (rx1, ry1); (rx2 - cw, ry1); (rx1, ry2 - ch); (rx2 - cw, ry2 - ch) ] do
                     for y in max 0 cy .. min (h - 1) (cy + ch - 1) do
                         for x in max 0 cx .. min (w - 1) (cx + cw - 1) do
                             let i = (y * w + x) * 4
                             yield (int rgba.[i] * 299 + int rgba.[i + 1] * 587 + int rgba.[i + 2] * 114,
                                    rgba.[i], rgba.[i + 1], rgba.[i + 2], rgba.[i + 3]) |]
            let sorted = corners |> Array.sortBy (fun (l, _, _, _, _) -> l)
            let (_, r, g, b, a) = sorted.[max 0 (sorted.Length / 2)]
            (r, g, b, a)
    // 清空原文字（用底色填充）
    let (fr, fg, fb, fa) = fillColor
    for y in max 0 ry1 .. min (h - 1) (ry2 - 1) do
        for x in max 0 rx1 .. min (w - 1) (rx2 - 1) do
            let i = (y * w + x) * 4
            out.[i] <- fr; out.[i + 1] <- fg; out.[i + 2] <- fb; out.[i + 3] <- fa
    if ent.Cn <> "" then
        let mask, mw, mh = renderText ent.Cn ent.BodyH (min (w - 4) (rx2 - rx1 - 4))
        // 蒙版内文字墨迹 bbox
        let mutable mx1, my1, mx2, my2 = Int32.MaxValue, Int32.MaxValue, -1, -1
        for y in 0 .. mh - 1 do
            for x in 0 .. mw - 1 do
                if mask.[y * mw + x] > 8uy then
                    if x < mx1 then mx1 <- x
                    if x + 1 > mx2 then mx2 <- x + 1
                    if y < my1 then my1 <- y
                    if y + 1 > my2 then my2 <- y + 1
        if mx2 < 0 then eprintfn "  ⚠️ %s 中文「%s」渲染为空" ent.Member ent.Cn
        else
            // 位置：水平居中于原文字 bbox 中心；垂直也居中（原文字与中文行高不同，居中比贴底稳）
            let dx = (bx1 + bx2) / 2 - (mx1 + mx2) / 2
            let dy = (by1 + by2) / 2 - (my1 + my2) / 2
            /// 膨胀蒙版：每个像素取半径 r 邻域内的最大 alpha（用于描边 / 辉光）
            let dilate (r: int) =
                let d = Array.zeroCreate<byte> (mw * mh)
                for y in 0 .. mh - 1 do
                    for x in 0 .. mw - 1 do
                        let mutable m = 0uy
                        for yy in max 0 (y - r) .. min (mh - 1) (y + r) do
                            for xx in max 0 (x - r) .. min (mw - 1) (x + r) do
                                let v = mask.[yy * mw + xx]
                                if v > m then m <- v
                        d.[y * mw + x] <- m
                d
            /// 把一层蒙版以「标准 alpha over 合成」画到 out：
            /// ⚠️ 不能用"alpha 取大者"—— 按钮图底色是不透明的，字身抗锯齿边缘的 alpha 比底色低，
            /// 取大者会让字只剩下字芯（实机看图是空白的，2026-10-20 踩过）。
            let blit (layer: byte[]) (col: byte * byte * byte) (gain: float) =
                let (r, g, b) = col
                for y in 0 .. mh - 1 do
                    for x in 0 .. mw - 1 do
                        let a = layer.[y * mw + x]
                        if a > 0uy then
                            let na = min 255.0 (float a * gain)
                            let tx, ty = dx + x, dy + y
                            if na > 0.0 && tx >= 0 && tx < w && ty >= 0 && ty < h then
                                let i = (ty * w + tx) * 4
                                let sa = na / 255.0
                                let da = float out.[i + 3] / 255.0
                                let oa = sa + da * (1.0 - sa)
                                if oa > 0.0 then
                                    let mix (s: byte) (d: byte) =
                                        byte (Math.Round((float s * sa + float d * da * (1.0 - sa)) / oa))
                                    out.[i] <- mix r out.[i]
                                    out.[i + 1] <- mix g out.[i + 1]
                                    out.[i + 2] <- mix b out.[i + 2]
                                    out.[i + 3] <- byte (Math.Round(oa * 255.0))
            /// 3×3 均值滤波（box blur）：辉光要柔和渐变，硬膨胀出来的会是"一层硬轮廓"
            let blur (src: byte[]) (iters: int) =
                let mutable cur = src
                for _ in 1 .. iters do
                    let nxt = Array.zeroCreate<byte> (mw * mh)
                    for y in 0 .. mh - 1 do
                        for x in 0 .. mw - 1 do
                            let mutable s = 0
                            let mutable c = 0
                            for yy in max 0 (y - 1) .. min (mh - 1) (y + 1) do
                                for xx in max 0 (x - 1) .. min (mw - 1) (x + 1) do
                                    s <- s + int cur.[yy * mw + xx]
                                    c <- c + 1
                            nxt.[y * mw + x] <- byte (s / c)
                    cur <- nxt
                cur
            // on 态辉光：**只按原图自身有没有辉光判断**，不能只看 `_on` 后缀（2026-10-20 实机踩坑）：
            // 设置界面里 `option_mode_Description_02_on/_off` 的 on/off 指「开关状态」，不是高亮态，
            // 按后缀加辉光会变成"奇怪的光晕"。判据 = ink bbox 面积显著大于字身 bbox（辉光把框撑大）。
            let inkArea = float ((ix2 - ix1) * (iy2 - iy1))
            let bodyArea = float (max 1 ((bx2 - bx1) * (by2 - by1)))
            // 小字（说明文字，BodyH < 28）即使原图有微光也不重建 —— 20px 的字加辉光会糊成一团
            let hasGlow = ent.BodyH >= 28 && inkArea / bodyArea > 1.25
            if hasGlow then blit (blur (dilate 2) 6) (gr, gg, gb) 1.5
            // 描边：原版白字配黑边；小字用 1px + 较轻的 gain（2px/0.95 会让 20px 高的字发糊）
            let outlineR = if ent.BodyH >= 28 then 2 else 1
            blit (dilate outlineR) (0uy, 0uy, 0uy) (if ent.BodyH >= 28 then 0.95 else 0.75)
            // 字身：等线比原版 UI 字体细一档，所以再补一层 dilate(1) 同色把笔画加粗 ~1px
            blit (dilate 1) (cr, cg, cb) 0.92
            blit mask (cr, cg, cb) 1.0
    out

/// 生成对比预览 PNG（左原图右新图，深灰底）
let saveCompare (orig: byte[]) (neu: byte[]) (w: int) (h: int) (path: string) =
    let bg = Color.FromArgb(255, 30, 32, 38)
    let draw (dst: Bitmap) (src: byte[]) (ox: int) =
        use g = Graphics.FromImage dst
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let i = (y * w + x) * 4
                let a = int src.[i + 3]
                if a > 0 then
                    let bl (c: byte) = byte ((int c * a + 30 * (255 - a)) / 255)
                    dst.SetPixel(ox + x, y, Color.FromArgb(255, int (bl src.[i]), int (bl src.[i + 1]), int (bl src.[i + 2])))
    use dst = new Bitmap(w * 2 + 8, h, PixelFormat.Format32bppArgb)
    use g0 = Graphics.FromImage dst
    g0.Clear bg
    draw dst orig 0
    draw dst neu (w + 8)
    g0.DrawLine(Pens.DimGray, w + 3, 0, w + 3, h)
    dst.Save(path, ImageFormat.Png)

// ---------------- 重制表 ----------------
// 译法沿用标题菜单已定稿的用词：设置 / 开放之间 / 返回。
let entries: Ent list =
    [ // ---- 系统菜单：菜单项（清晰大图；`system-menu_moji_01..09` 是逐字滑入的模糊动画帧，不重制）----
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_01.hip"; Cn = "存档"; BodyH = 30; Note = "セーブ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_02.hip"; Cn = "读取"; BodyH = 30; Note = "ロード" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_03.hip"; Cn = "设置"; BodyH = 32; Note = "オプション" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_04.hip"; Cn = "返回标题"; BodyH = 32; Note = "タイトルへ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_05.hip"; Cn = "返回"; BodyH = 34; Note = "戻る" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_06.hip"; Cn = "删除存档"; BodyH = 32; Note = "データ削除" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_07.hip"; Cn = "前往开放之间"; BodyH = 32; Note = "開放の間へ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_modo_moji_08.hip"; Cn = "存档"; BodyH = 30; Note = "セーブ（小）" }
      // ---- 系统菜单：标题行 + 逐字动画（原版 8 字「システムメニュー」，中文 4 字 → 后 4 帧清空）----
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji.hip"; Cn = "请选择项目"; BodyH = 16; Note = "項目を選択して下さい" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_01.hip"; Cn = "系"; BodyH = 22; Note = "シ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_02.hip"; Cn = "统"; BodyH = 22; Note = "ス" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_03.hip"; Cn = "菜"; BodyH = 22; Note = "テ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_04.hip"; Cn = "单"; BodyH = 22; Note = "ム" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_05.hip"; Cn = ""; BodyH = 22; Note = "メ（中文只有 4 字，后续帧清空）" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_06.hip"; Cn = ""; BodyH = 22; Note = "ニ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_07.hip"; Cn = ""; BodyH = 22; Note = "ュ" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_title_moji_08.hip"; Cn = ""; BodyH = 22; Note = "ー" }
      // ---- 系统菜单：底部说明条 ----
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_01.hip"; Cn = "进行存档"; BodyH = 22; Note = "セーブします" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_02.hip"; Cn = "读取存档"; BodyH = 22; Note = "ロードします" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_03.hip"; Cn = "打开设置窗口"; BodyH = 22; Note = "オプションウィンドウを開きます" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_04.hip"; Cn = "返回标题画面"; BodyH = 22; Note = "タイトルに戻ります" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_05.hip"; Cn = "关闭系统菜单"; BodyH = 22; Note = "システムメニューを終了します" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_06.hip"; Cn = "删除《月英学园 -kou-》的存档"; BodyH = 22; Note = "月英学園 -kou- のセーブデータを削除します" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "ST_mode_Description_07.hip"; Cn = "返回开放之间"; BodyH = 22; Note = "開放の間に戻ります" }
      // ================= 设置（option/options_screen.pac）=================
      // 原图版式：off 态 = 白/灰字 + 黑描边；on 态 = 亮蓝字 + 蓝色辉光（脚本按 `_on` 后缀自动加辉光）。
      // ⚠️ `ST_*` 系列在此 pac 里是遗留复制品（ui-images-triage §7.6 末尾），不要做。
      // ⚠️ `option_title_moji01`（顶部「オプション」）**不重制**：它背后是一层滚动/切换的
      //    「オプション」动画，前景换成 2 字中文会与背景日文叠在一起很难看，且把动画也换中文会
      //    变成 2 个字来回蹦。用户定：这张保留原版（2026-10-20）。
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_title_moji02.hip"; Cn = "消息设置"; BodyH = 16; Note = "メッセージを設定します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_title_moji03.hip"; Cn = "声音设置"; BodyH = 16; Note = "サウンドを設定します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_window_text_01_off.hip"; Cn = "声音"; BodyH = 31; Note = "サウンド（页签·未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_window_text_01_on.hip"; Cn = "声音"; BodyH = 31; Note = "サウンド（页签·选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_window_text_02_off.hip"; Cn = "消息"; BodyH = 31; Note = "メッセージ（页签·未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_window_text_02_on.hip"; Cn = "消息"; BodyH = 31; Note = "メッセージ（页签·选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text01_off.hip"; Cn = "BGM音量"; BodyH = 34; Note = "BGM音量（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text01_on.hip"; Cn = "BGM音量"; BodyH = 34; Note = "BGM音量（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text02_off.hip"; Cn = "音效音量"; BodyH = 34; Note = "SE音量（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text02_on.hip"; Cn = "音效音量"; BodyH = 34; Note = "SE音量（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text03_off.hip"; Cn = "语音音量"; BodyH = 34; Note = "ボイス音量（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "BGM_modo_text03_on.hip"; Cn = "语音音量"; BodyH = 34; Note = "ボイス音量（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text01_off.hip"; Cn = "消息速度"; BodyH = 34; Note = "メッセージ速度（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text01_on.hip"; Cn = "消息速度"; BodyH = 34; Note = "メッセージ速度（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text02_off.hip"; Cn = "自动阅读速度"; BodyH = 34; Note = "オートリード速度（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text02_on.hip"; Cn = "自动阅读速度"; BodyH = 34; Note = "オートリード速度（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text03_off.hip"; Cn = "跳过模式"; BodyH = 34; Note = "スキップモード（未选）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "Message_modo_text03_on.hip"; Cn = "跳过模式"; BodyH = 34; Note = "スキップモード（选中）" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_01.hip"; Cn = "调整 BGM 音量"; BodyH = 20; Note = "BGMの音量を変更します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_02_off.hip"; Cn = "关闭 BGM 静音"; BodyH = 20; Note = "BGMミュートをOFFにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_02_on.hip"; Cn = "开启 BGM 静音"; BodyH = 20; Note = "BGMミュートをONにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_03.hip"; Cn = "调整音效音量"; BodyH = 20; Note = "SEの音量を変更します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_04_off.hip"; Cn = "关闭音效静音"; BodyH = 20; Note = "SEミュートをOFFにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_04_on.hip"; Cn = "开启音效静音"; BodyH = 20; Note = "SEミュートをONにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_05.hip"; Cn = "调整语音音量"; BodyH = 20; Note = "ボイスの音量を変更します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_06_off.hip"; Cn = "关闭语音静音"; BodyH = 20; Note = "ボイスミュートをOFFにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_06_on.hip"; Cn = "开启语音静音"; BodyH = 20; Note = "ボイスミュートをONにします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_07.hip"; Cn = "调整消息速度"; BodyH = 20; Note = "メッセージ速度を変更します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_08.hip"; Cn = "调整自动阅读速度"; BodyH = 20; Note = "オートリード速度を変更します" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_09.hip"; Cn = "仅跳过已读消息"; BodyH = 20; Note = "既読メッセージのみスキップします" }
      { Pac = "option/options_screen.pac"; Inner = "tex.pac"; Member = "option_mode_Description_10.hip"; Cn = "跳过全部消息"; BodyH = 20; Note = "全てのメッセージをスキップします" }
      // ================= 回想（backlog/back_log.pac）=================
      // 标题是逐字图（原版「バ ッ ク ロ グ」5 帧），中文「对话记录」4 字 → 第 5 帧清空。
      // `_moji_light_01..07` 是模糊光斑层（不含文字），保留不动。
      { Pac = "backlog/back_log.pac"; Inner = "tex.pac"; Member = "back_log_title_moji01.hip"; Cn = "对"; BodyH = 20; Note = "バ" }
      { Pac = "backlog/back_log.pac"; Inner = "tex.pac"; Member = "back_log_title_moji02.hip"; Cn = "话"; BodyH = 20; Note = "ッ" }
      { Pac = "backlog/back_log.pac"; Inner = "tex.pac"; Member = "back_log_title_moji03.hip"; Cn = "记"; BodyH = 22; Note = "ク" }
      { Pac = "backlog/back_log.pac"; Inner = "tex.pac"; Member = "back_log_title_moji04.hip"; Cn = "录"; BodyH = 20; Note = "ロ" }
      { Pac = "backlog/back_log.pac"; Inner = "tex.pac"; Member = "back_log_title_moji05.hip"; Cn = ""; BodyH = 20; Note = "グ（中文只有 4 字，末帧清空）" }
      // ================= 确认对话框 / 按钮（common/g_dialog.pac）=================
      // message 图是两行提示（512×64 / 1024×64）；botan 图是胶囊按钮（框保留，只换字）。
      // 角色/剧本译名与正片译文保持一致：泉 / 露露 / 皋月 / 后堂 / 匡 / 抗 / 救世的摇篮曲。
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_01_ending_message.hip"; Cn = "进入英理的结局。\n确定吗？"; BodyH = 24; Note = "英理エンディングをプレイします" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_02_ending_message.hip"; Cn = "进入泉的结局。\n确定吗？"; BodyH = 24; Note = "いずみエンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_03_ending_message.hip"; Cn = "进入露露的结局。\n确定吗？"; BodyH = 24; Note = "ルルエンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_04_ending_message.hip"; Cn = "进入皋月的结局。\n确定吗？"; BodyH = 24; Note = "さつきエンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_05_ending_message.hip"; Cn = "进入律的结局。\n确定吗？"; BodyH = 24; Note = "律エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_06_ending_message.hip"; Cn = "进入后堂的结局。\n确定吗？"; BodyH = 24; Note = "後堂エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_07_ending_message.hip"; Cn = "进入大河的结局。\n确定吗？"; BodyH = 24; Note = "大河エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_08_ending_message.hip"; Cn = "进入雄喜的结局。\n确定吗？"; BodyH = 24; Note = "雄喜エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_09_ending_message.hip"; Cn = "进入「匡」章节的结局。\n确定吗？"; BodyH = 24; Note = "エピソード匡エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_10_ending_message.hip"; Cn = "进入「抗」章节的结局。\n确定吗？"; BodyH = 24; Note = "エピソード抗エンディング" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_go-scenario_message.hip"; Cn = "开始「匡」的剧本。\n确定吗？"; BodyH = 24; Note = "シナリオ「匣」を始めます" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_hiroshi-scenario_message.hip"; Cn = "开始「浩」的剧本。\n确定吗？"; BodyH = 24; Note = "シナリオ「浩」を始めます" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_kou-scenario_message.hip"; Cn = "开始「抗」的剧本。\n确定吗？"; BodyH = 24; Note = "シナリオ「抗」を始めます" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_kito_scenario_message.hip"; Cn = "游玩祁答院慎的剧本。\n确定吗？"; BodyH = 24; Note = "祁答院慎シナリオ" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_kyuse_scenario_message.hip"; Cn = "游玩《救世的摇篮曲》。\n确定吗？"; BodyH = 24; Note = "救世の子守唄" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_mafia_scenario_message.hip"; Cn = "游玩黑帮梶田的剧本。\n确定吗？"; BodyH = 24; Note = "マフィア梶田シナリオ" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_senketsu_scenario_message.hip"; Cn = "游玩《鲜血蜃楼》。\n确定吗？"; BodyH = 24; Note = "鮮血の蜃気楼" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_optino_message.hip"; Cn = "初始化全部设置数据。\n确定吗？"; BodyH = 24; Note = "オプションデータを全て初期化します" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_quick-load_message.hip"; Cn = "执行快速读取。\n确定吗？"; BodyH = 24; Note = "クイックロードをします" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_return_scene_message.hip"; Cn = "返回上一场景。\n确定吗？"; BodyH = 24; Note = "シーンバックを行います" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_start_prologue_message.hip"; Cn = "开始序章。\n确定吗？"; BodyH = 24; Note = "プロローグを始めます" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_titlre_return_message.hip"; Cn = "当前进度与快速存档将丢失。\n确定吗？"; BodyH = 24; Note = "現在進行中のプレイデータ…失われます" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_st_endinginfo_message.hip"; Cn = "结局已更新。\n可在开放之间查看。"; BodyH = 24; Note = "エンディングが更新されました" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_st_road_message.hip"; Cn = "正在读取系统数据。\n读取中请勿退出游戏。"; BodyH = 24; Note = "システムデータをロードしています" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_st_save_message.hip"; Cn = "正在保存系统数据。\n保存中请勿退出游戏。"; BodyH = 24; Note = "システムデータをセーブしています" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_st_savedatacheck_message.hip"; Cn = "正在删除系统数据。\n删除中请勿断电。"; BodyH = 24; Note = "システムデータを削除しています" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_st_slotcheck_message.hip"; Cn = "正在检查存档数据。"; BodyH = 24; Note = "セーブデータをチェックしています（单行）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan_Yes_off.hip"; Cn = "是"; BodyH = 34; Note = "はい（未选）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan_Yes_on.hip"; Cn = "是"; BodyH = 34; Note = "はい（选中）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan_No_off.hip"; Cn = "否"; BodyH = 34; Note = "いいえ（未选）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan_No_on.hip"; Cn = "否"; BodyH = 34; Note = "いいえ（选中）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_No_off.hip"; Cn = "否"; BodyH = 34; Note = "いいえ（未选）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_No_on.hip"; Cn = "否"; BodyH = 34; Note = "いいえ（选中）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_saisho_off.hip"; Cn = "从头开始"; BodyH = 34; Note = "さいしょから（未选）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_saisho_on.hip"; Cn = "从头开始"; BodyH = 34; Note = "さいしょから（选中）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_tuzuki_off.hip"; Cn = "继续游戏"; BodyH = 34; Note = "つづきから（未选）" }
      { Pac = "common/g_dialog.pac"; Inner = "tex.pac"; Member = "g_dialog_botan3_tuzuki_on.hip"; Cn = "继续游戏"; BodyH = 34; Note = "つづきから（选中）" }
      // ================= 开放之间（special/between_the_open.pac）=================
      // 译名与正片译文/标题菜单一致：开放之间 / CG鉴赏 / BGM鉴赏 / 个人资料 / 客串剧本 / 成就 / 结局。
      // ⚠️ 不做的：`open_title_moji_0X_effect_*`（逐字模糊动画帧，同 system-menu_moji）、
      //   `open_item_description_BGM/CG/bonus_scenario/ending/profile`（标签+「▮▮/▮▮ %」占位符合成的图，
      //   整块替换会把占位符清掉）、`open_item_description_hatena`（???）、`open_item_description_0..9`（数字）、
      //   `open_mode_moji_06` / `profile_item11_*`（???）、`profile_item10`（箭头图标）、
      //   `chara_name_21`（マスター 未译，保持与对话说话人名一致）、
      //   `bonus_scenario_text_kitou/mafia`（蓝色发光**美术字**，同 loading 艺术字不重制）、
      //   `Icon_*`/`Kom_*` 按键提示条（128×32 小字，下一批统一处理）。
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_01.hip"; Cn = "开放之间"; BodyH = 24; Note = "開放の間" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_02.hip"; Cn = "CG鉴赏"; BodyH = 24; Note = "CG観賞" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_03.hip"; Cn = "BGM鉴赏"; BodyH = 24; Note = "BGM観賞" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_04.hip"; Cn = "角色资料"; BodyH = 24; Note = "プロフィール" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_05.hip"; Cn = "客串剧本"; BodyH = 24; Note = "ゲストシナリオ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_06.hip"; Cn = "结局"; BodyH = 24; Note = "エンディング" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_title_moji_10.hip"; Cn = "成就"; BodyH = 24; Note = "実績" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_01.hip"; Cn = "CG鉴赏"; BodyH = 32; Note = "CG観賞" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_02.hip"; Cn = "BGM鉴赏"; BodyH = 32; Note = "BGM観賞" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_03.hip"; Cn = "角色资料"; BodyH = 32; Note = "プロフィール" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_04.hip"; Cn = "客串剧本"; BodyH = 32; Note = "ゲストシナリオ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_05.hip"; Cn = "成就一览"; BodyH = 32; Note = "実績一覧" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_07.hip"; Cn = "结局"; BodyH = 32; Note = "エンディング" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_mode_moji_08.hip"; Cn = "返回标题"; BodyH = 32; Note = "タイトルへ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "achievement_rate_moji.hip"; Cn = "达成率"; BodyH = 30; Note = "達成率（红字）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_rate_moji.hip"; Cn = "开放率"; BodyH = 34; Note = "開放率（红字）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_achievement.hip"; Cn = "查看成就一览"; BodyH = 20; Note = "実績の一覧を確認します" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_return_title.hip"; Cn = "返回标题画面"; BodyH = 20; Note = "タイトル画面に戻ります" }
      // ---- 角色名（21 张里做 20 张，`マスター` 未译保留原版）----
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_01.hip"; Cn = "远山浩"; BodyH = 42; Note = "遠山 浩" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_02.hip"; Cn = "御月英理"; BodyH = 42; Note = "御月 英理" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_03.hip"; Cn = "御月大河"; BodyH = 42; Note = "御月 大河" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_04.hip"; Cn = "麻生皋月"; BodyH = 42; Note = "麻生 さつき" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_05.hip"; Cn = "执行律"; BodyH = 42; Note = "執行 律" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_06.hip"; Cn = "阿式露露"; BodyH = 42; Note = "阿式 ルル" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_07.hip"; Cn = "后堂恭一郎"; BodyH = 42; Note = "後堂 恭一郎" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_08.hip"; Cn = "佐藤健"; BodyH = 42; Note = "佐藤 健" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_09.hip"; Cn = "田中太郎"; BodyH = 42; Note = "田中 太郎" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_10.hip"; Cn = "兼城泉"; BodyH = 42; Note = "兼城 いずみ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_11.hip"; Cn = "麻生明"; BodyH = 42; Note = "麻生 明" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_12.hip"; Cn = "双邑刚"; BodyH = 42; Note = "双邑 剛" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_13.hip"; Cn = "御月雄喜"; BodyH = 42; Note = "御月 雄喜" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_14.hip"; Cn = "荣斗圭"; BodyH = 42; Note = "栄斗 圭" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_15.hip"; Cn = "荣斗隆"; BodyH = 42; Note = "栄斗 隆" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_16.hip"; Cn = "久远院朱音"; BodyH = 42; Note = "久遠院朱音" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_17.hip"; Cn = "管家"; BodyH = 42; Note = "執事" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_18.hip"; Cn = "朝仓"; BodyH = 42; Note = "朝倉" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_19.hip"; Cn = "濑川玛丽亚"; BodyH = 42; Note = "瀬川 まりあ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "chara_name_20.hip"; Cn = "执行櫂"; BodyH = 42; Note = "執行 櫂" }
      // ---- 个人资料项（off 白字 / on 蓝字+辉光，辉光由 ink/body 面积比自动识别）----
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item01_off.hip"; Cn = "年龄"; BodyH = 32; Note = "年齢（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item01_on.hip"; Cn = "年龄"; BodyH = 32; Note = "年齢（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item02_off.hip"; Cn = "身高/体重"; BodyH = 32; Note = "身長/体重（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item02_on.hip"; Cn = "身高/体重"; BodyH = 32; Note = "身長/体重（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item03_off.hip"; Cn = "三围"; BodyH = 32; Note = "スリーサイズ（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item03_on.hip"; Cn = "三围"; BodyH = 32; Note = "スリーサイズ（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item04_off.hip"; Cn = "能力"; BodyH = 32; Note = "能力（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item04_on.hip"; Cn = "能力"; BodyH = 32; Note = "能力（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item05_off.hip"; Cn = "喜欢的东西"; BodyH = 32; Note = "好きなモノ（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item05_on.hip"; Cn = "喜欢的东西"; BodyH = 32; Note = "好きなモノ（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item06_off.hip"; Cn = "讨厌的东西"; BodyH = 32; Note = "嫌いなモノ（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item06_on.hip"; Cn = "讨厌的东西"; BodyH = 32; Note = "嫌いなモノ（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item07_off.hip"; Cn = "特征1"; BodyH = 32; Note = "特徴1（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item07_on.hip"; Cn = "特征1"; BodyH = 32; Note = "特徴1（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item08_off.hip"; Cn = "特征2"; BodyH = 32; Note = "特徴2（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item08_on.hip"; Cn = "特征2"; BodyH = 32; Note = "特徴2（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item09_off.hip"; Cn = "特征3"; BodyH = 32; Note = "特徴3（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item09_on.hip"; Cn = "特征3"; BodyH = 32; Note = "特徴3（选中）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item12_off.hip"; Cn = "返回"; BodyH = 32; Note = "戻る（未选）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "profile_item12_on.hip"; Cn = "返回"; BodyH = 32; Note = "戻る（选中）" }
      // ---- 右下角「标签 + ▮▮冊 / ▮▮冊 ▮▮%」模板图：**只替换左侧标签**（splitMode = Left 自动切分）----
      //   数字与「冊 / %」是占位符模板的一部分，右侧原样保留。
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_BGM.hip"; Cn = "BGM鉴赏"; BodyH = 20; Note = "BGM観賞 + 占位符" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_CG.hip"; Cn = "CG鉴赏"; BodyH = 20; Note = "CG観賞 + 占位符" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_bonus_scenario.hip"; Cn = "客串剧本"; BodyH = 20; Note = "ゲストシナリオ + 占位符" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_ending.hip"; Cn = "结局"; BodyH = 20; Note = "エンディング + 占位符" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "open_item_description_profile.hip"; Cn = "角色资料"; BodyH = 20; Note = "プロフィール + 占位符" }
      // ---- 按键提示条（**只替换右侧文字**，左侧手柄小图标保留；splitMode = Right）----
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_01.hip"; Cn = "选择"; BodyH = 24; Note = "選択" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_02.hip"; Cn = "确定"; BodyH = 24; Note = "決定" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_03.hip"; Cn = "返回"; BodyH = 24; Note = "戻る" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_04.hip"; Cn = "移动"; BodyH = 24; Note = "移動" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_05.hip"; Cn = "切换"; BodyH = 24; Note = "切替" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_06.hip"; Cn = "放大"; BodyH = 24; Note = "拡大" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_07.hip"; Cn = "缩小"; BodyH = 24; Note = "縮小" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_08.hip"; Cn = "上一个"; BodyH = 20; Note = "前へ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_09.hip"; Cn = "下一个"; BodyH = 20; Note = "次へ" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_10.hip"; Cn = "隐藏"; BodyH = 24; Note = "非表示" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_14.hip"; Cn = "播放语音"; BodyH = 24; Note = "ボイス再生" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "Icon_15.hip"; Cn = "选择"; BodyH = 24; Note = "選択（方向键图标）" }
      { Pac = "special/between_the_open.pac"; Inner = "tex.pac"; Member = "icon_16.hip"; Cn = "选择"; BodyH = 24; Note = "選択（方向键图标）" }
      { Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"; Member = "icon_16.hip"; Cn = "选择"; BodyH = 24; Note = "選択（方向键图标）" } ]

// ---------------- 文件读写 ----------------
let pacPathOf (pac: string) = Path.Combine(aeRoot, pac.Replace('/', '\\'))

let loadHip (pac: string) (innerName: string) (memberName: string) =
    let outer = PacLib.read (pacPathOf pac)
    let innerEntry =
        match outer.Entries |> Array.tryFind (fun e -> e.Filename = innerName) with
        | Some e -> e
        | None -> failwithf "%s 里找不到内层 %s" pac innerName
    let inner = PacLib.readBytes (PacLib.extract outer innerEntry)
    let imgEntry =
        match inner.Entries |> Array.tryFind (fun e -> e.Filename = memberName) with
        | Some e -> e
        | None -> failwithf "%s/%s 里找不到成员 %s" pac innerName memberName
    outer, inner, PacLib.extract inner imgEntry

let args = fsi.CommandLineArgs |> Array.skip 1
let mode = if args.Length > 0 then args.[0].ToLowerInvariant() else "list"
let filter = match args |> Array.tryItem 1 with Some s -> s | None -> ""
let selected = entries |> List.filter (fun e -> filter = "" || e.Pac.Contains filter || e.Member.Contains filter)

match mode with
| "list" ->
    printfn "%-34s %-26s %-10s %-5s %s" "pac" "成员" "中文" "bodyH" "原图内容"
    for e in selected do
        printfn "%-34s %-26s %-10s %-5d %s" (Path.GetFileNameWithoutExtension e.Pac) e.Member e.Cn e.BodyH e.Note
    printfn "\n共 %d 张（全表 %d 张）" selected.Length entries.Length
| "preview" ->
    Directory.CreateDirectory outRoot |> ignore
    let mutable ok = 0
    for e in selected do
        let _, _, hipBytes = loadHip e.Pac e.Inner e.Member
        let img = HipLib.decode hipBytes
        match toRgba img with
        | None -> eprintfn "  ⚠️ %s 不是 Argb32（%A），跳过" e.Member img.Format
        | Some rgba ->
            let w, h = img.ImageWidth, img.ImageHeight
            let ((ib, _), _, _) = inspect rgba w h (0, 0, w, h) false
            let neu = remake rgba w h e
            let ((ia, _), color, glow) = inspect neu w h (0, 0, w, h) false
            let fmt (x1, y1, x2, y2) = sprintf "(%d,%d)-(%d,%d) %dx%d" x1 y1 x2 y2 (x2 - x1) (y2 - y1)
            let stem = Path.GetFileNameWithoutExtension e.Member
            saveCompare rgba neu w h (Path.Combine(outRoot, Path.GetFileNameWithoutExtension e.Pac + "__" + stem + ".png"))
            ok <- ok + 1
            printfn "  %-26s %4dx%-3d 原 %-22s → 新 %-22s 字色(%d,%d,%d) 光色(%d,%d,%d) %s"
                stem w h (fmt ib) (fmt ia)
                (let (r, _, _) = color in r) (let (_, g, _) = color in g) (let (_, _, b) = color in b)
                (let (r, _, _) = glow in r) (let (_, g, _) = glow in g) (let (_, _, b) = glow in b) e.Cn
    printfn "\n预览 %d 张 → %s（左=原图，右=新图）" ok outRoot
| "apply" ->
    let byPac = selected |> List.groupBy (fun e -> e.Pac)
    for (pac, list) in byPac do
        let pacPath = pacPathOf pac
        // 备份外层 pac（首次）
        let bak = Path.Combine(backupRoot, pac.Replace('/', '\\'))
        if not (File.Exists bak) then
            Directory.CreateDirectory(Path.GetDirectoryName bak) |> ignore
            File.Copy(pacPath, bak)
            printfn "备份 %s → %s" pac bak
        let outer = PacLib.read pacPath
        let innerEntry = outer.Entries |> Array.find (fun e -> e.Filename = list.Head.Inner)
        let inner = PacLib.readBytes (PacLib.extract outer innerEntry)
        let replacements =
            list |> List.map (fun e ->
                let hipBytes = PacLib.extract inner (inner.Entries |> Array.find (fun x -> x.Filename = e.Member))
                let img = HipLib.decode hipBytes
                match toRgba img with
                | None -> failwithf "%s 不是 Argb32" e.Member
                | Some rgba ->
                    let neu = remake rgba img.ImageWidth img.ImageHeight e
                    let encoded = HipLib.encode (fromRgba img neu)
                    // 自检：能解回来、尺寸不变
                    let chk = HipLib.decode encoded
                    if chk.ImageWidth <> img.ImageWidth || chk.ImageHeight <> img.ImageHeight then
                        failwithf "%s 尺寸变了" e.Member
                    printfn "  %-28s %4dx%-4d ← %-22s（%d→%d 字节）" e.Member img.ImageWidth img.ImageHeight e.Cn hipBytes.Length encoded.Length
                    e.Member, encoded)
        let replaceMap = Map.ofList replacements
        let innerContents = inner.Entries |> Array.map (fun x -> if replaceMap.ContainsKey x.Filename then replaceMap.[x.Filename] else PacLib.extract inner x)
        let innerBytes = PacLib.build inner innerContents
        let outerContents = outer.Entries |> Array.map (fun x -> if x.Filename = list.Head.Inner then innerBytes else PacLib.extract outer x)
        File.WriteAllBytes(pacPath, PacLib.build outer outerContents)
        // 回读校验
        let back = PacLib.read pacPath
        let inner2 = PacLib.readBytes (PacLib.extract back (back.Entries |> Array.find (fun e -> e.Filename = list.Head.Inner)))
        for (m, _) in replacements do
            let b = PacLib.extract inner2 (inner2.Entries |> Array.find (fun e -> e.Filename = m))
            let img = HipLib.decode b
            if img.ImageWidth = 0 then failwithf "%s 回读失败" m
        printfn "✅ %s 已写回（%d 张）" pac list.Length
    printfn "\n⚠️ 完全退出游戏再启动，否则引擎沿用旧纹理。"
| "restore" ->
    let mutable n = 0
    for f in Directory.GetFiles(backupRoot, "*.pac", SearchOption.AllDirectories) do
        let rel = Path.GetRelativePath(backupRoot, f)
        File.Copy(f, Path.Combine(aeRoot, rel), true)
        printfn "已还原 %s" rel
        n <- n + 1
    printfn "共 %d 个 pac" n
| m ->
    eprintfn "未知子命令 %s（list / preview / apply / restore）" m
    exit 2
