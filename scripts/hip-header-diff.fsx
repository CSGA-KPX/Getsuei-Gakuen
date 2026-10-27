// 对比原始 HIP 与「原样重编码」的 header 差异（排查重编码是否被游戏拒绝）
#load "../scripts/Pac.fsx"
#load "../scripts/hip.fsx"

open System
open System.IO
open PacLib

let game = @"<游戏安装目录>"
let outerPath = Path.Combine(game, @"data\AE\title\UI_02_title.pac")

let outer = PacLib.read outerPath
let texIdx = outer.Entries |> Array.findIndex (fun e -> e.Filename = "tex.pac")
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-hdr.pac")
File.WriteAllBytes(tmp, PacLib.extract outer outer.Entries.[texIdx])
let tex = PacLib.read tmp
File.Delete tmp

let dump (label: string) (b: byte[]) (n: int) =
    printfn "%s（%d 字节）前 %d：" label b.Length n
    for i in 0 .. 8 .. n - 1 do
        let chunk = b.[i .. min (i + 7) (b.Length - 1)] |> Array.map (fun x -> x.ToString("X2")) |> String.concat " "
        printfn "  +%03X  %s" i chunk

for name in [ "title_menu_00.hip"; "title_logo.hip" ] do
    let e = tex.Entries |> Array.find (fun e -> e.Filename = name)
    let orig = PacLib.extract tex e
    let img = HipLib.decode orig
    printfn "\n========== %s ==========" name
    printfn "解码: %dx%d 画布 %dx%d 偏移(%d,%d) 格式 %A 编码 %A layered=%b 调色板=%s"
        img.ImageWidth img.ImageHeight img.CanvasWidth img.CanvasHeight img.OffsetX img.OffsetY
        img.Format img.Encoding img.Layered
        (match img.Palette with Some p -> sprintf "%d 色" p.Length | None -> "无")

    // 原样重编码（不改任何像素）
    let reenc = HipLib.encode img
    printfn "原文件 %d 字节 → 原样重编码 %d 字节 %s"
        orig.Length reenc.Length (if orig.Length = reenc.Length then "（长度相同）" else "（长度不同！）")

    dump "原始 header" orig 48
    dump "重编码 header" reenc 48

    // 逐字节比较 header 前 48
    let minLen = min 48 (min orig.Length reenc.Length)
    let diffs = [ for i in 0 .. minLen - 1 do if orig.[i] <> reenc.[i] then yield i ]
    printfn "header 前 %d 字节差异位置: %s" minLen
        (if diffs.IsEmpty then "无" else diffs |> List.map (sprintf "+0x%02X") |> String.concat " ")
