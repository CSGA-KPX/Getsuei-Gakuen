// 实机验证补丁生成器（docs/testing/smoke-test.md）
//
// 用法:
//   dotnet fsi smoke-patch.fsx adv <text_xxx.pac>
//       测试A: 把「こ」(U+3053) 的字形位图替换成品红实心块（验证 ADV 用 pac 内嵌字库 + 位图链路）
//       测试B: 追加新字形「汉」(U+6C49) 映射到图集空白区 + atf 行 00_..._005 插入「汉」
//               （验证映射追加路径：Mappings/24B/32B/hip 同步增长 + UTF-16 中文上屏）
//   dotnet fsi smoke-patch.fsx image <data\story\image\xxx.hip>
//       测试C: 台词图叠加品红边框+大 X 后重编码（验证 TGA type9 重编码产物被游戏接受）
//   dotnet fsi smoke-patch.fsx hip <data\....hip>
//       测试D: 本地 hip-tool HIP→PNG→HIP 重编码替换（验证编码产物被游戏接受）
//
// 约定: 修改游戏文件前自动备份到 work/backup/<相对 data 的路径>；产物就地覆盖游戏文件。

#r "nuget: SixLabors.ImageSharp"
#load "Pac.fsx"
#load "AtfJson/Atf.fs"
#load "tga.fsx"
#load "hip.fsx"

open System
open System.IO
open System.Diagnostics
open PacLib
open AtfJson
open TgaLib
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing

let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let backupRoot = Path.Combine(workspace, "work", "backup")
/// 本地 HIP⇄PNG 工具（hip-tool.fsx；GeoArcSysAIOCLITool 已移除）
let hipCli () = Path.Combine(workspace, "scripts", "hip-tool.fsx")

/// 备份游戏文件（已存在则跳过），返回备份路径
let backup (gameFile: string) =
    let dataRoot = @"<游戏安装目录>\data"
    let rel = Path.GetRelativePath(dataRoot, gameFile)
    let bak = Path.Combine(backupRoot, rel)
    if not (File.Exists bak) then
        Directory.CreateDirectory(Path.GetDirectoryName bak) |> ignore
        File.Copy(gameFile, bak)
        printfn "已备份 %s -> %s" gameFile bak
    bak

// ---------------- abc 运行时语义解析（sub_43F290 逆向结论，docs/formats/abc-hip-font.md §2） ----------------

type AbcRuntime =
    { Bytes: byte[]
      EntryCount: int
      MapOff: int          // mapping 表字节偏移（下标=charcode，u16，0=无）
      CountOff: int        // u32 GlyphCount 偏移
      Rec24Base: int       // GlyphCount × 24B：[f32 x1 y1 x2 y2][u16×4 度量]
      Rec32Base: int       // GlyphCount × 32B：[i32 x1 x2 y1 y2 像素][16B 未知]
      GlyphCount: int }

let abcParse (bytes: byte[]) : AbcRuntime =
    let ver = BitConverter.ToUInt32(bytes, 0)
    if ver <> 0x41535720u then failwithf "非预期 abc 版本 0x%08X（本工具仅支持 \" WSA\"）" ver
    let ec = int (BitConverter.ToUInt16(bytes, 20))
    let countOff = 24 + 2 * ec
    let gc = BitConverter.ToInt32(bytes, countOff)
    let r24 = countOff + 4
    { Bytes = bytes; EntryCount = ec; MapOff = 22; CountOff = countOff
      Rec24Base = r24; Rec32Base = r24 + 24 * gc; GlyphCount = gc }

let mapGet (abc: AbcRuntime) (charcode: int) = int (BitConverter.ToUInt16(abc.Bytes, abc.MapOff + 2 * charcode))
let rec32Rect (abc: AbcRuntime) (v: int) =
    let o = abc.Rec32Base + 32 * v
    (BitConverter.ToInt32(abc.Bytes, o),      // x1
     BitConverter.ToInt32(abc.Bytes, o + 4),  // x2
     BitConverter.ToInt32(abc.Bytes, o + 8),  // y1
     BitConverter.ToInt32(abc.Bytes, o + 12)) // y2

/// 测试图案颜色：品红；优先占用一个图集未使用的调色板槽并改色，否则追加新槽位
let magenta = Rgba32(255uy, 0uy, 255uy, 255uy)

/// rec32 像素矩形 (x1,x2,y1,y2) → 通用单元格 (x1,y1,x2,y2)
let toCell (x1, x2, y1, y2) = (x1, y1, x2, y2)

let acquireMagentaIndex (img: TgaImage) (paintCells: (int * int * int * int) list) : int * Rgba32[] =
    let w, h = img.Width, img.Height
    let inCells x y =
        paintCells |> List.exists (fun (x1, y1, x2, y2) -> x >= x1 && x < x2 && y >= y1 && y < y2)
    let usage = Array.zeroCreate<int> img.Palette.Length
    for si in 0 .. img.Indices.Length - 1 do
        let x = si % w
        let y = h - 1 - (si / w)   // 存储序 → 顶行原点
        if not (inCells x y) then usage[int img.Indices[si]] <- usage[int img.Indices[si]] + 1
    let freeIdx = usage |> Array.tryFindIndex ((=) 0)
    match freeIdx with
    | Some i ->
        let pal = Array.copy img.Palette
        pal[i] <- magenta
        i, pal
    | None ->
        img.Palette.Length, Array.append img.Palette [| magenta |]

/// 在索引位图（顶行原点坐标）上填充实心矩形
let paintCell (img: TgaImage) (idx: byte) (x1, y1, x2, y2) =
    for y in y1 .. y2 - 1 do
        let sy = img.Height - 1 - y
        for x in x1 .. x2 - 1 do
            img.Indices[sy * img.Width + x] <- idx

let exportCrop (img: TgaImage) (rect: int * int * int * int) (outPng: string) =
    let x1, y1, x2, y2 = rect
    let w, h = x2 - x1, y2 - y1
    let png = new Image<Rgba32>(w, h)
    for y in 0 .. h - 1 do
        let sy = img.Height - 1 - (y1 + y)
        for x in 0 .. w - 1 do
            png[x, y] <- img.Palette[int img.Indices[sy * img.Width + (x1 + x)]]
    png.Save(outPng)

// ---------------- 子命令 ----------------

/// 测试 A+B：对话 pac 字形替换 + 新字形追加
let patchAdv (pacPath: string) =
    let orig = PacLib.read pacPath
    let hipBytes = PacLib.extract orig (orig.Entries |> Array.find (fun e -> e.Filename = "font.hip"))
    let abcBytes = PacLib.extract orig (orig.Entries |> Array.find (fun e -> e.Filename = "font.abc"))
    let atfBytes = PacLib.extract orig (orig.Entries |> Array.find (fun e -> e.Filename = "string.atf"))

    let abc = abcParse abcBytes
    let vKo = mapGet abc 0x3053
    if vKo = 0 then failwith "この字库没有 こ 字形？"
    let koRect = rec32Rect abc vKo |> toCell   // → (x1,y1,x2,y2) 像素
    let kx1, ky1, kx2, ky2 = koRect
    printfn "测试A: こ glyph=%d cell=(%d,%d)-(%d,%d)" vKo kx1 ky1 kx2 ky2
    // 新字形「汉」放在图集空白区（整图目检确认 y>510 全空）
    let hanRect = (10, 560, 43, 609)   // (x1,y1,x2,y2) 顶行原点
    printfn "测试B: 新增「汉」(U+6C49) -> glyph %d cell=(10,560)-(43,609)" abc.GlyphCount

    // ---- font.hip：两个格子都填品红 ----
    let img = TgaLib.decode hipBytes
    let magIdx, palette' = acquireMagentaIndex img [ koRect; hanRect ]
    printfn "品红使用调色板槽 #%d（共 %d 槽）" magIdx palette'.Length
    paintCell img (byte magIdx) koRect
    paintCell img (byte magIdx) hanRect
    let hip' = TgaLib.encode { img with Palette = palette' }

    // ---- font.abc：mapping[0x6C49]=新glyph；克隆 こ 的 24B/32B 记录改矩形 ----
    let ms = new MemoryStream()
    ms.Write(abcBytes, 0, abcBytes.Length)
    // 1) mapping
    let mapOffHan = abc.MapOff + 2 * 0x6C49
    if BitConverter.ToUInt16(abcBytes, mapOffHan) <> 0us then failwith "U+6C49 已被占用，换字"
    ms.Position <- int64 mapOffHan
    ms.Write(BitConverter.GetBytes(uint16 abc.GlyphCount), 0, 2)
    // 2) GlyphCount +1
    ms.Position <- int64 abc.CountOff
    ms.Write(BitConverter.GetBytes(abc.GlyphCount + 1), 0, 4)
    // 3) 追加 24B 记录（克隆 こ，改 UV 矩形与度量）
    let newV = abc.GlyphCount
    ms.Position <- int64 abc.Rec32Base   // 原 rec24 表尾 = 新记录插入点（rec32 表整体后移 24B）
    let tail32 = abcBytes[abc.Rec32Base ..]   // 原 rec32 表
    let r24Ko = abc.Rec24Base + 24 * vKo
    let rec24 = Array.copy abcBytes[r24Ko .. r24Ko + 23]
    let W, H = float img.Width, float img.Height
    let uv (px: int) (size: float) = BitConverter.GetBytes(single (float px / size))
    Array.blit (uv 10 W) 0 rec24 0 4
    Array.blit (uv 560 H) 0 rec24 4 4
    Array.blit (uv 43 W) 0 rec24 8 4
    Array.blit (uv 609 H) 0 rec24 12 4
    // u16 度量 (+16..23) 保持 こ 的克隆值
    ms.Write(rec24, 0, 24)
    ms.Write(tail32, 0, tail32.Length)
    // 4) 追加 32B 记录（克隆 こ，改像素矩形 x1 x2 y1 y2）
    ms.Position <- ms.Length
    let r32Ko = abc.Rec32Base + 32 * vKo
    let rec32 = Array.copy abcBytes[r32Ko .. r32Ko + 31]
    Array.blit (BitConverter.GetBytes 10) 0 rec32 0 4
    Array.blit (BitConverter.GetBytes 43) 0 rec32 4 4
    Array.blit (BitConverter.GetBytes 560) 0 rec32 8 4
    Array.blit (BitConverter.GetBytes 609) 0 rec32 12 4
    ms.Write(rec32, 0, 32)
    let abc' = ms.ToArray()

    // ---- string.atf：00_..._005 句插入「汉」----
    let atf = AtfBinary.parseBytes atfBytes
    let target = atf.Records |> Array.tryFind (fun r -> r.Key = "00_0-01_01_005")
    match target with
    | None -> failwith "找不到 atf key 00_0-01_01_005"
    | Some rec5 ->
        if not (rec5.Value.Contains "こちら二班！") then failwithf "005 句内容不符: %s" rec5.Value
        let value' = rec5.Value.Replace("こちら二班！", "こちら二班！汉")
        let atf' = { atf with Records = atf.Records |> Array.map (fun r -> if r.Key = rec5.Key then { r with Value = value' } else r) }
        let atfBytes' = AtfBinary.buildBytes atf'
        printfn "atf: 005 句 -> %s" value'

        // ---- 自检 ----
        let abc'' = abcParse abc'
        if mapGet abc'' 0x6C49 <> newV then failwith "自检失败: mapping[汉]"
        if rec32Rect abc'' newV <> (10, 43, 560, 609) then failwith "自检失败: rec32[汉]"
        let img'' = TgaLib.decode hip'
        exportCrop img'' koRect (Path.Combine(workspace, "work", "smoke-proof-ko.png"))
        exportCrop img'' hanRect (Path.Combine(workspace, "work", "smoke-proof-han.png"))

        // ---- 回写 ----
        backup pacPath
        let contents =
            orig.Entries
            |> Array.map (fun e ->
                match e.Filename with
                | "font.hip" -> hip'
                | "font.abc" -> abc'
                | "string.atf" -> atfBytes'
                | _ -> failwithf "未知条目 %s" e.Filename)
        let pac' = build orig contents
        File.WriteAllBytes(pacPath, pac')
        printfn "已写入 %s (%d 字节, 原 %d 字节)" pacPath pac'.Length orig.Bytes.Length

/// 测试 C：台词图叠加测试图案后重编码
let patchImage (hipPath: string) =
    let bytes = File.ReadAllBytes hipPath
    let img = TgaLib.decode bytes
    // 找一个图内完全未使用的调色板槽位改成品红（索引必须是 byte ≤255）
    let usage = Array.zeroCreate<int> img.Palette.Length
    for i in img.Indices do usage[int i] <- usage[int i] + 1
    let slot =
        match usage |> Array.tryFindIndex ((=) 0) with
        | Some s -> s
        | None -> failwith "调色板 256 槽全被使用，无法安全加测试色"
    let pal = Array.copy img.Palette
    pal[slot] <- magenta
    let magIdx = byte slot
    printfn "品红使用调色板槽 #%d（共 %d 槽）" slot pal.Length
    let img' = { img with Palette = pal }
    let w, h = img'.Width, img'.Height
    let put (x, y) =
        if x >= 0 && x < w && y >= 0 && y < h then
            let sy = img'.Height - 1 - y
            img'.Indices[sy * w + x] <- magIdx
    let b = 6   // 边框粗细
    for x in 0 .. w - 1 do
        for y in 0 .. h - 1 do
            if x < b || x >= w - b || y < b || y >= h - b then put (x, y)
    for t in 0 .. min w h - 1 do   // 对角大 X
        put (t, t); put (w - 1 - t, t)
    let out' = TgaLib.encode img'
    // 自检：能解回来且品红像素存在
    let chk = TgaLib.decode out'
    let hasMag =
        chk.Indices |> Array.exists (fun i -> int i = slot)
        && chk.Palette[slot] = magenta
    if not hasMag then failwith "自检失败：重编码后品红图案丢失"
    backup hipPath
    File.WriteAllBytes(hipPath, out')
    printfn "已写入 %s (%d 字节, 原 %d 字节)" hipPath out'.Length bytes.Length

/// 测试 D：本地 hip-tool HIP→PNG→HIP
let patchHip (hipPath: string) =
    let tool = hipCli ()
    let tmp = Path.Combine(Path.GetTempPath(), "smoke-hip-" + Guid.NewGuid().ToString("N"))
    let decDir = Path.Combine(tmp, "dec")
    let encDir = Path.Combine(tmp, "enc")
    Directory.CreateDirectory decDir |> ignore
    Directory.CreateDirectory encDir |> ignore
    let run args =
        let psi = ProcessStartInfo(FileName = "dotnet", Arguments = sprintf "fsi \"%s\" %s" tool args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
        let p = Process.Start psi
        let so = p.StandardOutput.ReadToEnd()
        let se = p.StandardError.ReadToEnd()
        p.WaitForExit()
        if p.ExitCode <> 0 then failwithf "hip-tool 失败: %s %s" so se
    let pngPath = Path.Combine(decDir, "out.png")
    run (sprintf "decode \"%s\" -o \"%s\"" hipPath pngPath)
    let encPath = Path.Combine(encDir, "out.hip")
    run (sprintf "encode \"%s\" -o \"%s\"" pngPath encPath)
    let outBytes = File.ReadAllBytes encPath
    let origBytes = File.ReadAllBytes hipPath
    backup hipPath
    File.WriteAllBytes(hipPath, outBytes)
    printfn "已写入 %s (%d 字节, 原 %d 字节)" hipPath outBytes.Length origBytes.Length
    Directory.Delete(tmp, true)

// ---------------- 测试 D'：嵌套 pac 内的 HIP 图像（标题画面等 UI 图） ----------------

/// 在 PNG（RGBA）上叠加品红边框 + 对角大 X
let overlayTestPattern (pngPath: string) =
    use img = Image.Load<Rgba32>(pngPath)
    let w, h = img.Width, img.Height
    let b = max 2 (min w h / 16)
    let put x y = if x >= 0 && x < w && y >= 0 && y < h then img[x, y] <- magenta
    for x in 0 .. w - 1 do
        for y in 0 .. h - 1 do
            if x < b || x >= w - b || y < b || y >= h - b then put x y
    for t in 0 .. min w h - 1 do
        put t t
        put (w - 1 - t) t
    img.Save(pngPath)

/// 测试 D'：outer pac → 嵌套 tex.pac → 目标 .hip 重编码（叠加测试图案）→ 两层回包
let patchHipImg (outerPacPath: string) (innerEntry: string) (imgEntry: string) =
    let tool = hipCli ()
    let tmp = Path.Combine(Path.GetTempPath(), "smoke-hipimg-" + Guid.NewGuid().ToString("N"))
    let decDir = Path.Combine(tmp, "dec")
    let encDir = Path.Combine(tmp, "enc")
    Directory.CreateDirectory decDir |> ignore
    Directory.CreateDirectory encDir |> ignore
    let run args =
        let psi = ProcessStartInfo(FileName = "dotnet", Arguments = sprintf "fsi \"%s\" %s" tool args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
        let p = Process.Start psi
        let so = p.StandardOutput.ReadToEnd()
        let se = p.StandardError.ReadToEnd()
        p.WaitForExit()
        if p.ExitCode <> 0 then failwithf "hip-tool 失败: %s %s" so se

    let outer = PacLib.read outerPacPath
    let innerEntry' = outer.Entries |> Array.find (fun e -> e.Filename = innerEntry)
    let inner = PacLib.readBytes (PacLib.extract outer innerEntry')
    let imgEntry' = inner.Entries |> Array.find (fun e -> e.Filename = imgEntry)
    let imgBytes = PacLib.extract inner imgEntry'

    let hipTmp = Path.Combine(tmp, imgEntry)
    File.WriteAllBytes(hipTmp, imgBytes)
    let pngPath = Path.Combine(decDir, "out.png")
    run (sprintf "decode \"%s\" -o \"%s\"" hipTmp pngPath)
    overlayTestPattern pngPath
    let encPath = Path.Combine(encDir, "out.hip")
    run (sprintf "encode \"%s\" -o \"%s\"" pngPath encPath)
    let newHipBytes = File.ReadAllBytes encPath
    printfn "%s: %d -> %d 字节" imgEntry imgBytes.Length newHipBytes.Length

    // 自检：新 HIP 可回解
    let chkDir = Path.Combine(tmp, "chk")
    Directory.CreateDirectory chkDir |> ignore
    let chkHip = Path.Combine(tmp, "chk.hip")
    File.WriteAllBytes(chkHip, newHipBytes)
    run (sprintf "decode \"%s\" -o \"%s\"" chkHip (Path.Combine(chkDir, "chk.png")))

    // 回包：内层 tex.pac → 外层 pac
    let innerContents =
        inner.Entries
        |> Array.map (fun e -> if e.Filename = imgEntry then newHipBytes else PacLib.extract inner e)
    let inner' = build inner innerContents
    let outerContents =
        outer.Entries
        |> Array.map (fun e -> if e.Filename = innerEntry then inner' else PacLib.extract outer e)
    let outer' = build outer outerContents
    backup outerPacPath
    File.WriteAllBytes(outerPacPath, outer')
    printfn "已写入 %s (%d 字节, 原 %d 字节)" outerPacPath outer'.Length outer.Bytes.Length
    Directory.Delete(tmp, true)

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 2 then
    eprintfn "用法: adv <pac> | image <hip-tga> | hip <hip-proprietary> | hipimg <outer.pac> <innerEntry> <img.hip>"
    exit 2
match args[0] with
| "adv" -> patchAdv args[1]
| "image" -> patchImage args[1]
| "hip" -> patchHip args[1]
| "hipimg" ->
    if args.Length < 4 then eprintfn "hipimg 需要 <outer.pac> <innerEntry> <img.hip>"; exit 2
    patchHipImg args[1] args[2] args[3]
| c -> eprintfn "未知子命令 %s" c; exit 2
