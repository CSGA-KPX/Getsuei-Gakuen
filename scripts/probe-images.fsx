// 探针图：判定「UI 文字到底是图像还是 Font00 实时渲染」
// （docs/tables/ui-images-triage.md §5.2；2026-10-20 版：判据已改为 aex 纹理名表 + procmon）
//
// 做法：把目标 .hip 涂成**不透明纯色**（每张一个专属颜色，尺寸/格式/偏移一律保持原样），
// 回写「内层 tex.pac → 外层 pac」两层包，进对应界面看色块是否上屏。
//
// 用法:
//   dotnet fsi scripts/probe-images.fsx list                # 打印探针表
//   dotnet fsi scripts/probe-images.fsx export              # 导出原图/探针图 PNG 到 work/probe-preview/
//   dotnet fsi scripts/probe-images.fsx apply               # 装机（自动备份 → work/backup/AE/）
//   dotnet fsi scripts/probe-images.fsx apply 1 3           # 只装指定编号
//   dotnet fsi scripts/probe-images.fsx verify              # 检查游戏目录里哪些探针已生效
//   dotnet fsi scripts/probe-images.fsx restore              # 从 work/backup/AE/ 还原
//
// ⚠️ 改完 AE 下的 pac 必须**完全退出游戏再进**，否则引擎沿用旧纹理。
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "Pac.fsx"
#load "hip.fsx"

open System
open System.IO
open HipLib
open PacLib
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let gameData = @"<游戏安装目录>\data"
let aeRoot = Path.Combine(gameData, "AE")
let backupRoot = Path.Combine(workspace, "work", "backup", "AE")
let previewDir = Path.Combine(workspace, "work", "probe-preview")

/// 探针表：编号 / 颜色(RGB) / 外层 pac（相对 data\AE）/ 内层 pac / 成员 / 界面 / 观察点
type Probe =
    { No: int
      Color: byte * byte * byte
      Pac: string
      Inner: string
      Member: string
      Ui: string
      Watch: string }

let probes =
    [ { No = 1; Color = (255uy, 0uy, 255uy); Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"
        Member = "ST_modo_moji_01.hip"; Ui = "系统菜单"
        Watch = "第 1 项「セーブ」→ 品红块（原图内容已确认是「セーブ」）" }
      { No = 2; Color = (0uy, 255uy, 255uy); Pac = "system_menu/system_menu.pac"; Inner = "tex.pac"
        Member = "ST_title_moji.hip"; Ui = "系统菜单"
        Watch = "标题行「項目を選択して下さい」→ 青色块" }
      { No = 3; Color = (255uy, 255uy, 0uy); Pac = "option/options_screen.pac"; Inner = "tex.pac"
        Member = "BGM_modo_text01_on.hip"; Ui = "设置"
        Watch = "「BGM音量」高亮态 → 黄色块" }
      { No = 4; Color = (0uy, 255uy, 0uy); Pac = "backlog/back_log.pac"; Inner = "tex.pac"
        Member = "back_log_title_moji01.hip"; Ui = "回想"
        Watch = "标题「バックログ」的首字「バ」→ 绿色块" }
      { No = 5; Color = (0uy, 0uy, 255uy); Pac = "special/between_the_open.pac"; Inner = "tex.pac"
        Member = "open_mode_moji_01.hip"; Ui = "开放之间"
        Watch = "模式名「CG鑑賞」→ 蓝色块" }
      { No = 6; Color = (255uy, 128uy, 0uy); Pac = "common/g_dialog.pac"; Inner = "tex.pac"
        Member = "g_dialog_st_save_message.hip"; Ui = "系统数据保存"
        Watch = "「システムデータをセーブしています」→ 橙色块" }
      { No = 7; Color = (128uy, 0uy, 255uy); Pac = "common/UI_29_nowloading.pac"; Inner = "tex.pac"
        Member = "nowloading_word_1.hip"; Ui = "加载画面"
        Watch = "NOW LOADING 动画的一帧「読」→ 紫色块" }
      // ---- 2026-10-20 第二轮：设置界面（options_screen）专测 ----
      { No = 8; Color = (0uy, 200uy, 120uy); Pac = "option/options_screen.pac"; Inner = "tex.pac"
        Member = "BGM_modo_text01_off.hip"; Ui = "设置"
        Watch = "「BGM音量」普通态 → 青绿色块" }
      { No = 9; Color = (255uy, 0uy, 128uy); Pac = "option/options_screen.pac"; Inner = "tex.pac"
        Member = "option_mode_Description_01.hip"; Ui = "设置"
        Watch = "底部说明「BGMの音量を変更します」→ 玫红色块" }
      { No = 10; Color = (150uy, 80uy, 255uy); Pac = "option/options_screen.pac"; Inner = "tex.pac"
        Member = "option_title_moji01.hip"; Ui = "设置"
        Watch = "顶部标题「オプション」→ 淡紫色块" } ]

let colorName (r, g, b) =
    match r, g, b with
    | 255uy, 0uy, 255uy -> "品红"
    | 0uy, 255uy, 255uy -> "青色"
    | 255uy, 255uy, 0uy -> "黄色"
    | 0uy, 255uy, 0uy -> "绿色"
    | 0uy, 0uy, 255uy -> "蓝色"
    | 255uy, 128uy, 0uy -> "橙色"
    | 128uy, 0uy, 255uy -> "紫色"
    | 0uy, 200uy, 120uy -> "青绿"
    | 255uy, 0uy, 128uy -> "玫红"
    | 150uy, 80uy, 255uy -> "淡紫"
    | _ -> sprintf "RGB(%d,%d,%d)" r g b

let pacPathOf (p: Probe) = Path.Combine(aeRoot, p.Pac.Replace('/', '\\'))

/// 从外层 pac 里取出目标 HIP 的字节
let extractProbe (p: Probe) (outer: PacFile) =
    let innerEntry = outer.Entries |> Array.find (fun e -> e.Filename = p.Inner)
    let inner = PacLib.readBytes (PacLib.extract outer innerEntry)
    let imgEntry = inner.Entries |> Array.find (fun e -> e.Filename = p.Member)
    inner, imgEntry, PacLib.extract inner imgEntry

/// 生成纯色探针 HIP：保持 Format/Encoding/画布/尺寸/偏移，只重写像素与调色板
let makeProbeHip (orig: HipImage) (r: byte, g: byte, b: byte) =
    let bgra = [| b; g; r; 255uy |]
    match orig.Format with
    | Indexed8 ->
        // 调色板每项都设成目标色：任何索引都显示目标色（长度不变，避免越界）
        let pal = orig.Palette.Value |> Array.map (fun _ -> Array.copy bgra)
        { orig with Palette = Some pal; Pixels = Array.zeroCreate orig.Pixels.Length }
    | Argb32 ->
        let px = Array.zeroCreate orig.Pixels.Length
        for i in 0 .. (px.Length / 4) - 1 do
            Array.blit bgra 0 px (i * 4) 4
        { orig with Pixels = px }
    | Gray16 | Rgb565 | FormatUnknown _ ->
        // 少见格式：整体转 32bpp Raw（与 hip-tool 默认行为一致）
        let px = Array.zeroCreate (orig.ImageWidth * orig.ImageHeight * 4)
        for i in 0 .. (px.Length / 4) - 1 do
            Array.blit bgra 0 px (i * 4) 4
        { Format = HipLib.Argb32
          Encoding = HipLib.EncRaw
          CanvasWidth = orig.ImageWidth
          CanvasHeight = orig.ImageHeight
          ImageWidth = orig.ImageWidth
          ImageHeight = orig.ImageHeight
          OffsetX = 0
          OffsetY = 0
          Layered = false
          Palette = None
          Pixels = px }

let isProbeColor (img: HipImage) (r: byte, g: byte, b: byte) =
    let px = img.Pixels
    match img.Format with
    | Indexed8 ->
        match img.Palette with
        | Some pal ->
            pal |> Array.forall (fun e -> e.[0] = b && e.[1] = g && e.[2] = r && e.[3] = 255uy)
            && (px |> Array.forall (fun v -> v = 0uy))
        | None -> false
    | Argb32 ->
        px.Length >= 4
        && px.[0] = b && px.[1] = g && px.[2] = r && px.[3] = 255uy
    | _ -> false

/// HipImage → PNG（预览用；alpha 合成到深灰底，方便看清白字）
let savePng (img: HipImage) (path: string) =
    let bg = 40uy
    let blend (c: Rgba32) =
        let a = int c.A
        Rgba32(byte ((int c.R * a + int bg * (255 - a)) / 255),
              byte ((int c.G * a + int bg * (255 - a)) / 255),
              byte ((int c.B * a + int bg * (255 - a)) / 255),
              255uy)
    use png = new Image<Rgba32>(img.ImageWidth, img.ImageHeight)
    let frame = png.Frames.RootFrame
    let w, h, px = img.ImageWidth, img.ImageHeight, img.Pixels
    match img.Format with
    | Indexed8 ->
        let pal = img.Palette.Value
        let color (e: byte[]) = blend (Rgba32(e.[2], e.[1], e.[0], e.[3]))
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let idx = int px.[y * w + x]
                frame.[x, y] <- if idx < pal.Length then color pal.[idx] else Rgba32(bg, bg, bg, 255uy)
    | Argb32 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 4
                frame.[x, y] <- blend (Rgba32(px.[p + 2], px.[p + 1], px.[p], px.[p + 3]))
    | Gray16 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 2
                let v = byte ((int px.[p] ||| (int px.[p + 1] <<< 8)) * 255 / 65535)
                frame.[x, y] <- Rgba32(v, v, v, 255uy)
    | _ ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                frame.[x, y] <- Rgba32(bg, bg, bg, 255uy)
    png.Save path

// ---------------- 子命令 ----------------

let listProbes () =
    printfn "%-3s %-6s %-34s %-38s %s" "No" "颜色" "pac" "成员" "界面 / 观察点"
    for p in probes do
        printfn "%-3d %-6s %-34s %-38s %s / %s" p.No (colorName p.Color) p.Pac p.Member p.Ui p.Watch

let exportPreview only =
    Directory.CreateDirectory previewDir |> ignore
    for p in probes |> List.filter (fun p -> only |> List.isEmpty || only |> List.contains p.No) do
        let outer = PacLib.read (pacPathOf p)
        let _, _, hipBytes = extractProbe p outer
        let img = HipLib.decode hipBytes
        let stem = sprintf "probe%d_%s" p.No (Path.GetFileNameWithoutExtension p.Member)
        savePng img (Path.Combine(previewDir, stem + "_orig.png"))
        savePng (makeProbeHip img p.Color) (Path.Combine(previewDir, stem + "_probe.png"))
        printfn "探针 %d  %-38s %dx%d %A  → %s_orig.png / _probe.png"
            p.No p.Member img.ImageWidth img.ImageHeight img.Format stem

let applyProbes only (pad: bool) =
    for p in probes |> List.filter (fun p -> only |> List.isEmpty || only |> List.contains p.No) do
        let pacPath = pacPathOf p
        if not (File.Exists pacPath) then eprintfn "⚠️ 缺少 %s" pacPath
        else
            let outer = PacLib.read pacPath
            let inner, _, hipBytes = extractProbe p outer
            let orig = HipLib.decode hipBytes
            let probe = makeProbeHip orig p.Color
            let encoded = HipLib.encode probe
            // --pad：尾部补 0 到与原文件等长 ⇒ pac 的 Length/OffsetStart 全不变（诊断用）
            let probeBytes =
                if pad && encoded.Length < hipBytes.Length then
                    Array.append encoded (Array.zeroCreate (hipBytes.Length - encoded.Length))
                else encoded
            // 自检：新 HIP 能解回来且确实是纯色
            let chk = HipLib.decode probeBytes
            if not (isProbeColor chk p.Color) then failwithf "探针 %d 自检失败：重编码后不是纯色" p.No
            if chk.ImageWidth <> orig.ImageWidth || chk.ImageHeight <> orig.ImageHeight then
                failwithf "探针 %d 尺寸变了" p.No
            if pad && probeBytes.Length <> hipBytes.Length then
                eprintfn "⚠️ 探针 %d 无法等长（编码 %d > 原 %d），退化为变长" p.No encoded.Length hipBytes.Length
            // 备份外层 pac（首次）
            let rel = p.Pac.Replace('/', '\\')
            let bak = Path.Combine(backupRoot, rel)
            if not (File.Exists bak) then
                Directory.CreateDirectory(Path.GetDirectoryName bak) |> ignore
                File.Copy(pacPath, bak)
            // 内层 → 外层回包
            let innerContents =
                inner.Entries
                |> Array.map (fun e -> if e.Filename = p.Member then probeBytes else PacLib.extract inner e)
            let innerBytes = PacLib.build inner innerContents
            let outerContents =
                outer.Entries
                |> Array.map (fun e -> if e.Filename = p.Inner then innerBytes else PacLib.extract outer e)
            let outerBytes = PacLib.build outer outerContents
            File.WriteAllBytes(pacPath, outerBytes)
            // 回读校验
            let back = PacLib.read pacPath
            let _, _, backHip = extractProbe p back
            if not (isProbeColor (HipLib.decode backHip) p.Color) then
                failwithf "探针 %d 回读校验失败（pac 回包后颜色丢失）" p.No
            printfn "探针 %d ✅ %-38s → %s（%d→%d 字节）备份 %s"
                p.No p.Member (colorName p.Color) hipBytes.Length probeBytes.Length bak
    printfn "\n⚠️ 完全退出游戏再启动，否则引擎沿用旧纹理。"

let verifyProbes () =
    for p in probes do
        let pacPath = pacPathOf p
        if not (File.Exists pacPath) then printfn "探针 %d  ⚠️ 缺 pac" p.No
        else
            let outer = PacLib.read pacPath
            let _, _, hipBytes = extractProbe p outer
            let img = HipLib.decode hipBytes
            let state = if isProbeColor img p.Color then "已生效" else "原始"
            printfn "探针 %d  %-38s %-6s %s" p.No p.Member (colorName p.Color) state

let restoreProbes () =
    if not (Directory.Exists backupRoot) then eprintfn "没有备份目录 %s" backupRoot
    else
        for f in Directory.GetFiles(backupRoot, "*.pac", SearchOption.AllDirectories) do
            let rel = Path.GetRelativePath(backupRoot, f)
            let dst = Path.Combine(aeRoot, rel)
            File.Copy(f, dst, true)
            printfn "已还原 %s" rel
        printfn "\n⚠️ 完全退出游戏再启动。"

let args = fsi.CommandLineArgs |> Array.skip 1
let mode = if args.Length > 0 then args.[0].ToLowerInvariant() else "list"
let only = args |> Array.skip 1 |> Array.choose (fun s -> match Int32.TryParse s with | true, v -> Some v | _ -> None) |> Array.toList
let pad = args |> Array.contains "--pad"

match mode with
| "list" -> listProbes ()
| "export" -> exportPreview only
| "apply" -> applyProbes only pad
| "verify" -> verifyProbes ()
| "restore" -> restoreProbes ()
| m -> eprintfn "未知子命令 %s（list / export / apply [编号] [--pad] / verify / restore）" m; exit 2
