// 字库台账：jpn_glyph（UI 全局）/ Font00（菜单）/ 237 内嵌 font.abc 的容量与覆盖清点
#load "./Pac.fsx"

open System
open System.IO
open System.Text
open PacLib

let game = @"<游戏安装目录>"

/// 解析标准 abc（ver4/5/" WSA"）头部与尺寸
let abcInfo (label: string) (bytes: byte[]) =
    let ver = BitConverter.ToInt32(bytes, 0)
    let lineHeight = BitConverter.ToSingle(bytes, 4)
    let ec = int (BitConverter.ToUInt16(bytes, 20))
    let countOff = 24 + 2 * ec
    let gc = BitConverter.ToInt32(bytes, countOff)
    let layout = countOff + 4 + 24 * gc
    let rec32 = if layout + 32 * gc <= bytes.Length then (bytes.Length - layout) / 32 else 0
    // 已映射码位数（mapping 非 0）
    let mutable mapped = 0
    for cp in 0 .. ec do
        if int (BitConverter.ToUInt16(bytes, 22 + 2 * cp)) <> 0 then mapped <- mapped + 1
    label, ver, lineHeight, ec, gc, mapped, bytes.Length, rec32

let rows = ResizeArray<string * int * float32 * int * int * int * int * int>()

// 1) UI 全局字库
let abcPath = Path.Combine(game, @"data\text\jpn\jpn_glyph.abc")
rows.Add(abcInfo "jpn_glyph.abc（UI 全局）" (File.ReadAllBytes abcPath))

// 2) 菜单字库 Font00（cmn/font.pac → abc.pac → Font00.abc）
let fontPac = PacLib.read (Path.Combine(game, @"data\cmn\font.pac"))
printfn "cmn/font.pac 内文件: %s" (fontPac.Entries |> Array.map (fun e -> sprintf "%s(%d)" e.Filename e.Length) |> String.concat ", ")
let abcPacEntry = fontPac.Entries |> Array.tryFind (fun e -> e.Filename.Contains "abc")
match abcPacEntry with
| Some e ->
    let tmp = Path.Combine(Path.GetTempPath(), "font-ledger-abc.pac")
    File.WriteAllBytes(tmp, PacLib.extract fontPac e)
    let inner = PacLib.read tmp
    for ie in inner.Entries do
        if ie.Filename.EndsWith(".abc", StringComparison.OrdinalIgnoreCase) then
            rows.Add(abcInfo (sprintf "%s（菜单）" ie.Filename) (PacLib.extract inner ie))
    File.Delete tmp
| None -> printfn "⚠️ cmn/font.pac 内未找到 abc"

// 3) 237 个内嵌 font.abc（对话字库）—— 统计容量水位
let textDir = Path.Combine(game, @"data\story\text\jpn")
let pacs = Directory.GetFiles(textDir, "*.pac") |> Array.filter (fun p -> not (p.EndsWith(".pac_")))
let mutable maxGlyph = 0
let mutable maxPac = ""
let mutable totalGlyph = 0
for p in pacs do
    try
        let pac = PacLib.read p
        match pac.Entries |> Array.tryFind (fun e -> e.Filename = "font.abc") with
        | Some e ->
            let b = PacLib.extract pac e
            let ec = int (BitConverter.ToUInt16(b, 20))
            let gc = BitConverter.ToInt32(b, 24 + 2 * ec)
            totalGlyph <- totalGlyph + gc
            if gc > maxGlyph then
                maxGlyph <- gc
                maxPac <- Path.GetFileName p
        | None -> ()
    with _ -> ()

printfn "\n内嵌 font.abc：%d 个 pac，字形总数 %d，最大单 pac %d（%s）" pacs.Length totalGlyph maxGlyph maxPac
printfn "容量上限：CELL 34×49，2048² 图集 → %d 格（COLS=60, ROWS=41, CAPACITY=2460）" (60 * 41)

printfn "\n| 字库 | 版本 | LineHeight | EntryCount（码位上限） | GlyphCount | 已映射码位 | 文件字节 | rec32 数 |"
printfn "|---|---|---|---|---|---|---|---|"
for (label, ver, lh, ec, gc, mapped, size, rec32) in rows do
    printfn "| %s | %d | %.1f | %d (U+%04X) | %d | %d | %d | %d |" label ver lh ec ec gc mapped size rec32
