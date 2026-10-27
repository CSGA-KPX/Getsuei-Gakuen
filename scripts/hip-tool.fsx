// HIP 专有格式 ⇄ PNG 转换工具（本地化替代 GeoArcSysAIOCLITool 的 HIP 模式）
// 用法：
//   dotnet fsi scripts/hip-tool.fsx decode <in.hip> -o <out.png>
//   dotnet fsi scripts/hip-tool.fsx encode <in.png> -o <out.hip> [--encoding raw|rawrepeat]
//                                       [--format 32bpp|indexed] [--palette-from <ref.hip>]
//   dotnet fsi scripts/hip-tool.fsx batch-decode <dir|pac> [-o <outdir>]   （递归扫描 pac 内嵌 + 散装 hip）
//   dotnet fsi scripts/hip-tool.fsx info <in.hip>
//
// encode 默认 32bpp Raw（与 GeoArcSys 工具默认行为一致，实机验证过）。
// --format indexed：量化为 ≤256 色调色板（8bpp），可配 --palette-from 复用参考 HIP 的调色板
// （字库改图场景：保留原调色板，只改字形像素）。
// batch-decode 静默覆盖已有输出，无交互询问（对照旧外部工具的最大坑）。

#load "hip.fsx"
#load "Pac.fsx"
#r "nuget: SixLabors.ImageSharp, 3.1.3"

open System
open System.IO
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open HipLib
open PacLib

/// 递归收集 pac（含嵌套，depth ≤3）内的 HIP 专有格式条目，返回 (label, filename, bytes)
let collectPacHips (pacPath: string) : (string * string * byte[]) list =
    let results = ResizeArray<string * string * byte[]>()
    let rec walk (lbl: string) (pac: PacFile) (d: int) =
        for e in pac.Entries do
            let data = extract pac e
            let lower = e.Filename.ToLowerInvariant()
            if lower.EndsWith(".pac") && d < 3 then
                try walk (lbl + "__" + e.Filename) (readBytes data) (d + 1)
                with _ -> ()
            elif lower.EndsWith(".hip") && isHip data then
                results.Add(lbl, e.Filename, data)
    walk (Path.GetFileNameWithoutExtension pacPath) (read pacPath) 0
    Seq.toList results

let fail msg =
    eprintfn "错误: %s" msg
    exit 1

let parseArgs (argv: string[]) =
    let mutable inp = None
    let mutable out = None
    let mutable encoding = "raw"
    let mutable format = "32bpp"
    let mutable paletteFrom = None
    let mutable i = 1
    while i < argv.Length do
        match argv[i].ToLowerInvariant() with
        | "-o" | "--output" -> out <- Some argv[i+1]; i <- i + 1
        | "--encoding" -> encoding <- argv[i+1].ToLowerInvariant(); i <- i + 1
        | "--format" -> format <- argv[i+1].ToLowerInvariant(); i <- i + 1
        | "--palette-from" -> paletteFrom <- Some argv[i+1]; i <- i + 1
        | s when s.StartsWith("-") -> failwithf "未知选项 %s" argv[i]
        | _ when inp.IsNone -> inp <- Some argv[i]
        | _ -> failwithf "多余参数 %s" argv[i]
        i <- i + 1
    inp, out, encoding, format, paletteFrom

// ---------- HipImage ⇄ Image<Rgba32> ----------

let private paletteColor (entry: byte[]) : Rgba32 =
    // 文件调色板为 BGRA 字节
    Rgba32(entry[2], entry[1], entry[0], entry[3])

let toRgba (img: HipImage) : Image<Rgba32> =
    let out = new Image<Rgba32>(img.ImageWidth, img.ImageHeight)
    let frame = out.Frames.RootFrame
    let w, h, px = img.ImageWidth, img.ImageHeight, img.Pixels
    match img.Format with
    | Indexed8 ->
        let pal = img.Palette.Value
        let fallback = Rgba32(0uy, 0uy, 0uy, 255uy)
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let idx = int px[y * w + x]
                frame[x, y] <- if idx < pal.Length then paletteColor pal[idx] else fallback
    | Argb32 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 4
                frame[x, y] <- Rgba32(px[p+2], px[p+1], px[p], px[p+3]) // BGRA → RGBA
    | Gray16 ->
        // 源实现转 48bpp RGB 后由 GDI+ 存 PNG（16→8 位舍入）；对齐：v8 = (v16*255 + 32767) / 65535
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 2
                let v16 = int px[p] ||| (int px[p+1] <<< 8)
                let v = byte ((v16 * 255 + 32767) / 65535)
                frame[x, y] <- Rgba32(v, v, v, 255uy)
    | Rgb565 ->
        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                let p = (y * w + x) * 2
                let v = uint16 px[p] ||| (uint16 px[p+1] <<< 8)
                let r5 = (v >>> 11) &&& 0x1Fus
                let g6 = (v >>> 5) &&& 0x3Fus
                let b5 = v &&& 0x1Fus
                let s5 n = byte ((n * 255us + 15us) / 31us)
                let s6 n = byte ((n * 255us + 31us) / 63us)
                frame[x, y] <- Rgba32(s5 r5, s6 g6, s5 b5, 255uy)
    | FormatUnknown b ->
        failwithf "unknown pixel format 0x%02X" b
    out

/// 从 RGBA 像素构造 32bpp HipImage（BGRA 字节序）
let rgbaToHip32 (frame: ImageFrame<Rgba32>) : HipImage =
    let w, h = frame.Width, frame.Height
    let px = Array.zeroCreate (w * h * 4)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            let c = frame[x, y]
            let p = (y * w + x) * 4
            px[p] <- c.B; px[p+1] <- c.G; px[p+2] <- c.R; px[p+3] <- c.A
    {
        Format = Argb32; Encoding = EncRaw
        CanvasWidth = w; CanvasHeight = h
        ImageWidth = w; ImageHeight = h
        OffsetX = 0; OffsetY = 0; Layered = false
        Palette = None; Pixels = px
    }

/// 索引化：exact/最近色匹配到调色板，返回 (调色板 BGRA 项, 索引像素)
let indexedPixels (frame: ImageFrame<Rgba32>) (palette: Rgba32[]) : byte[][] * byte[] =
    let exact = System.Collections.Generic.Dictionary<Rgba32, byte>()
    palette |> Array.iteri (fun i c -> if not (exact.ContainsKey c) then exact.Add(c, byte i))
    let nearest (c: Rgba32) : byte =
        let mutable best = 0
        let mutable bestD = Int32.MaxValue
        for i in 0 .. palette.Length - 1 do
            let p = palette[i]
            let dr = int c.R - int p.R
            let dg = int c.G - int p.G
            let db = int c.B - int p.B
            let da = int c.A - int p.A
            let d = dr*dr + dg*dg + db*db + da*da
            if d < bestD then bestD <- d; best <- i
        byte best
    let w, h = frame.Width, frame.Height
    let idx = Array.zeroCreate (w * h)
    for y in 0 .. h - 1 do
        for x in 0 .. w - 1 do
            let c = frame[x, y]
            idx[y * w + x] <-
                match exact.TryGetValue c with
                | true, i -> i
                | _ -> nearest c
    let palBytes = palette |> Array.map (fun (c: Rgba32) -> [| c.B; c.G; c.R; c.A |])
    palBytes, idx

/// 量化构建 ≤256 色调色板（ImageSharp Octree 先收敛颜色，再按首次出现排序）
let quantizePalette (img: Image<Rgba32>) : Rgba32[] =
    img.Mutate(fun ctx -> ctx.Quantize(KnownQuantizers.Octree) |> ignore)
    let frame = img.Frames.RootFrame
    let seen = System.Collections.Generic.Dictionary<Rgba32, int>()
    let order = ResizeArray<Rgba32>()
    for y in 0 .. frame.Height - 1 do
        for x in 0 .. frame.Width - 1 do
            let c = frame[x, y]
            if not (seen.ContainsKey c) then
                seen.Add(c, order.Count)
                order.Add(c)
    if order.Count > 256 then failwithf "量化后仍有 %d 色（>256）" order.Count
    order.ToArray()

// ---------- 主流程 ----------

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 2 then
    eprintfn "用法: dotnet fsi scripts/hip-tool.fsx <decode|encode|batch-decode|info> <文件/目录> [-o 输出] [选项...]"
    exit 1

let mode = argv[0].ToLowerInvariant()
let inp, out, encoding, format, paletteFrom = parseArgs argv

let input =
    match inp with
    | Some i -> i
    | None -> fail "缺少输入文件"

match mode with
| "decode" ->
    let img =
        try HipLib.decode (File.ReadAllBytes input)
        with HipException e -> fail (sprintf "解码失败: %s" e)
    use png = toRgba img
    let outPath = out |> Option.defaultValue (Path.ChangeExtension(input, ".png"))
    png.Save(outPath)
    printfn "OK %s → %s (%dx%d %A %A)" input outPath img.ImageWidth img.ImageHeight img.Format img.Encoding
| "encode" ->
    let outPath = out |> Option.defaultValue (Path.ChangeExtension(input, ".hip"))
    use img = Image.Load<Rgba32>(input)
    let hip =
        match format with
        | "32bpp" ->
            let h = rgbaToHip32 (img.Frames.RootFrame)
            { h with Encoding = if encoding = "rawrepeat" then EncRawRepeat else EncRaw }
        | "indexed" ->
            let palette =
                match paletteFrom with
                | Some refPath ->
                    let ref = HipLib.decode (File.ReadAllBytes refPath)
                    match ref.Palette with
                    | Some pal when pal.Length > 0 -> pal |> Array.map paletteColor
                    | _ -> fail (sprintf "%s 无调色板" refPath)
                | None -> quantizePalette img
            let palBytes, idx = indexedPixels (img.Frames.RootFrame) palette
            {
                Format = Indexed8
                Encoding = if encoding = "rawrepeat" then EncRawRepeat else EncRaw
                CanvasWidth = img.Width; CanvasHeight = img.Height
                ImageWidth = img.Width; ImageHeight = img.Height
                OffsetX = 0; OffsetY = 0; Layered = false
                Palette = Some palBytes; Pixels = idx
            }
        | f -> fail (sprintf "未知 --format %s（可用 32bpp|indexed）" f)
    let bytes = HipLib.encode hip
    // RawRepeat 流 ≥ 裸数据时，解码器循环上界（consumed < 图幅字节数）会截断尾部像素：提醒改用 raw
    if encoding = "rawrepeat" then
        let palLen = match hip.Palette with Some p -> p.Length * 4 | None -> 0
        let rawSize = hip.ImageWidth * hip.ImageHeight * hip.Format.Bpp
        if bytes.Length - 32 - palLen >= rawSize then
            eprintfn "警告: RawRepeat 流(%dB) 不短于裸数据(%dB)，解码器可能截断尾部像素，建议改用 --encoding raw"
                (bytes.Length - 32 - palLen) rawSize
    File.WriteAllBytes(outPath, bytes)
    printfn "OK %s → %s (%d 字节, %dx%d %s %s)" input outPath bytes.Length
        hip.ImageWidth hip.ImageHeight format encoding
| "info" ->
    let img =
        try HipLib.decode (File.ReadAllBytes input)
        with HipException e -> fail (sprintf "解析失败: %s" e)
    printfn "文件: %s" input
    printfn "格式: %A / 编码: %A" img.Format img.Encoding
    printfn "画布: %dx%d  图像: %dx%d  偏移: (%d,%d)  图层: %b"
        img.CanvasWidth img.CanvasHeight img.ImageWidth img.ImageHeight img.OffsetX img.OffsetY img.Layered
    match img.Palette with
    | Some p -> printfn "调色板: %d 色" p.Length
    | None -> ()
| "batch-decode" ->
    let outDir = out |> Option.defaultValue (Path.Combine(Path.GetDirectoryName input, "hip-png"))
    Directory.CreateDirectory(outDir) |> ignore
    // 输入可以是 pac 文件、散装 hip 文件或目录（递归找 *.pac / *.hip）
    let sources = ResizeArray<(string * string * byte[])>()
    let attr = File.GetAttributes(input)
    if attr.HasFlag(FileAttributes.Directory) then
        for p in Directory.GetFiles(input, "*.pac", SearchOption.AllDirectories) do
            sources.AddRange(collectPacHips p)
        for p in Directory.GetFiles(input, "*.hip", SearchOption.AllDirectories) do
            let b = File.ReadAllBytes p
            if isHip b then sources.Add(Path.GetFileNameWithoutExtension p, Path.GetFileName p, b)
    elif input.ToLowerInvariant().EndsWith(".pac") then
        sources.AddRange(collectPacHips input)
    else
        let b = File.ReadAllBytes input
        if isHip b then sources.Add(Path.GetFileNameWithoutExtension input, Path.GetFileName input, b)
    let mutable ok = 0
    let fails = ResizeArray<string>()
    let mutable n = 0
    for (label, name, bytes) in sources do
        n <- n + 1
        try
            let img = HipLib.decode bytes
            use png = toRgba img
            let outPath = Path.Combine(outDir, sprintf "%s__%s.png" label (Path.GetFileNameWithoutExtension name))
            png.Save(outPath)
            ok <- ok + 1
        with HipException e ->
            fails.Add(sprintf "%s/%s: %s" label name e)
    printfn "batch-decode: %d 共 %d 成功，%d 失败 → %s" sources.Count ok fails.Count outDir
    for f in fails |> Seq.take (min 20 fails.Count) do
        eprintfn "FAIL: %s" f
| m -> fail (sprintf "未知模式 %s（可用 decode|encode|batch-decode|info）" m)
