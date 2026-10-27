// abc 字库索引分析（docs/formats/abc-hip-font.md）
//
// 用法:
//   dotnet fsi abc-analysis.fsx <file.abc> info                头部字段 + 统计
//   dotnet fsi abc-analysis.fsx <file.abc> dump                hex dump 未知区块到 <file>.analysis.txt
//   dotnet fsi abc-analysis.fsx <file.abc> glyphs              导出全部字形矩形到 <file>.glyph.txt
//   dotnet fsi abc-analysis.fsx <file.abc> mapping             导出 char code → glyph 映射到 <file>.mapping.txt
//
// 矩形叠加到 PNG 请用 abc-overlay.fsx（需要 ImageSharp）。

#load "abc.fsx"

open System
open System.IO
open System.Text
open AbcLib

let args = fsi.CommandLineArgs |> Array.skip 1

let usage () =
    eprintfn "用法: dotnet fsi abc-analysis.fsx <file.abc> [info|dump|glyphs|mapping]"
    exit 2

if args.Length < 1 then
    usage ()

let path = args[0]

if not (File.Exists path) then
    eprintfn "输入文件不存在: %s" path
    exit 1

let command = if args.Length > 1 then args[1].ToLowerInvariant() else "info"
let abc = AbcLib.parse path

match command with
| "info" ->
    printfn "文件: %s (%d 字节)" path (FileInfo(path).Length)
    printfn "FourCC: %s" (Text.Encoding.UTF8.GetString(abc.FourCC))
    printfn "UNK1-4: %s %s %s %s" (byteToHex abc.Unk1) (byteToHex abc.Unk2) (byteToHex abc.Unk3) (byteToHex abc.Unk4)
    printfn "  UNK1: 0x%02X=字号(高), 0x%02X=特效选项(低)；UNK4 推测为特殊情况字号" abc.Unk1[2] abc.Unk1[3]
    printfn "EntryCount: %d（Mappings 实际长度 %d）" abc.EntryCount abc.Mappings.Length
    printfn "GlyphCount: %d" (glyphCount abc)
    printfn "CharCode 范围: %s" (if abc.CharCodes.Length = 0 then "(无)" else sprintf "0x%04X - 0x%04X" (Array.min abc.CharCodes) (Array.max abc.CharCodes))

| "dump" ->
    let sb = StringBuilder()
    sb.AppendLine($"[{path}]") |> ignore
    sb.AppendLine() |> ignore
    sb.AppendLine($"header: {byteToHex abc.Unk1} {byteToHex abc.Unk2} {byteToHex abc.Unk3} {byteToHex abc.Unk4}") |> ignore
    sb.AppendLine() |> ignore
    sb.AppendLine("== Chunk1 (24B × glyphCount, 12×u16) ==") |> ignore

    for g in abc.Glyphs do
        sb.AppendLine(byteToHexFormat g.Chunk1 [| 2; 2; 2; 2; 2; 2; 2; 2; 2; 2; 2; 2 |]) |> ignore

    let out = Path.GetFileNameWithoutExtension(path) + ".analysis.txt"
    File.WriteAllText(out, sb.ToString())
    printfn "已写出 %s" out

| "glyphs" ->
    let lines =
        abc.Glyphs
        |> Array.mapi (fun i g ->
            let c = abc.CharCodes[i]
            sprintf "glyph %4d  char U+%04X '%c'  rect x1=%d x2=%d y1=%d y2=%d" i c (if c >= 32 && c < 0xD800 then char c else '?') g.Rect[0] g.Rect[1] g.Rect[2] g.Rect[3])

    let out = Path.GetFileNameWithoutExtension(path) + ".glyph.txt"
    File.WriteAllLines(out, lines)
    printfn "已写出 %d 个字形矩形到 %s" lines.Length out

| "mapping" ->
    let sb = StringBuilder()

    abc.Mappings
    |> Array.iteri (fun idx us ->
        if us <> 0us then
            let glyphIdx = abc.CharCodes |> Array.findIndex ((=) idx)
            let display = if idx >= 32 && idx < 0xD800 then string (char idx) else "?"
            sb.AppendLine($"U+{idx:X4} '{display}' -> glyph {glyphIdx} (map={us})") |> ignore)

    let out = Path.GetFileNameWithoutExtension(path) + ".mapping.txt"
    File.WriteAllText(out, sb.ToString())
    printfn "已写出 %d 条映射到 %s" abc.CharCodes.Length out

| other ->
    eprintfn "未知命令: %s" other
    usage ()
