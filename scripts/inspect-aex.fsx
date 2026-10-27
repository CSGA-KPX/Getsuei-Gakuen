// 检查 .aex 文件（UI 定义）：AE/title/UI_02_title.pac 里的 aex.pac → UI_02_title.aex
// .aex 之前一直被当作 "other" 跳过，可能是 UI 布局/文本引用定义
#load "../scripts/Pac.fsx"

open System
open System.IO
open System.Text
open PacLib
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let game = @"<游戏安装目录>"
let outerPath = Path.Combine(game, @"data\AE\title\UI_02_title.pac")

let outer = PacLib.read outerPath
printfn "UI_02_title.pac 内文件："
outer.Entries |> Array.iter (fun e -> printfn "  %-20s %d 字节" e.Filename e.Length)

let aexIdx = outer.Entries |> Array.findIndex (fun e -> e.Filename = "aex.pac")
let aexPacBytes = PacLib.extract outer outer.Entries.[aexIdx]
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-aex.pac")
File.WriteAllBytes(tmp, aexPacBytes)
let aexPac = PacLib.read tmp
File.Delete tmp
printfn "\naex.pac 内文件："
aexPac.Entries |> Array.iter (fun e -> printfn "  %-24s %d 字节" e.Filename e.Length)

for e in aexPac.Entries do
    let data = PacLib.extract aexPac e
    printfn "\n===== %s（%d 字节）=====" e.Filename data.Length
    printfn "前 48 字节: %s" ((data.[0 .. min 47 (data.Length - 1)] |> Array.map (fun b -> b.ToString("X2"))) |> String.concat " ")

    // ASCII 串
    let ascii = [| for b in data do if b >= 0x20uy && b <= 0x7Euy then yield char b else yield '\u0000' |]
    let s = String(ascii)
    let parts = s.Split('\u0000') |> Array.filter (fun x -> x.Length >= 3)
    printfn "ASCII 串 %d 条（前 40）：" parts.Length
    parts |> Array.truncate 40 |> Array.iter (fun x -> printfn "  %s" x)

    // SJIS 日文
    let sjis = Encoding.GetEncoding 932
    let isLead (b: int) = (b >= 0x81 && b <= 0x84) || (b >= 0x87 && b <= 0x9F) || (b >= 0xE0 && b <= 0xEF)
    let isTrail (b: int) = (b >= 0x40 && b <= 0x7E) || (b >= 0x80 && b <= 0xFC)
    let kana (c: char) = let u = int c in (u >= 0x3041 && u <= 0x30FF)
    let jp = ResizeArray<string>()
    let sb = StringBuilder()
    let mutable i = 0
    while i < data.Length do
        let b = int data.[i]
        if isLead b && i + 1 < data.Length && isTrail (int data.[i + 1]) then
            sb.Append(sjis.GetString(data, i, 2)) |> ignore
            i <- i + 2
        elif b >= 0x20 && b <= 0x7E then
            sb.Append(char b) |> ignore
            i <- i + 1
        else
            if sb.Length >= 2 && (sb.ToString() |> Seq.exists kana) then jp.Add(sb.ToString())
            sb.Clear() |> ignore
            i <- i + 1
    if sb.Length >= 2 && (sb.ToString() |> Seq.exists kana) then jp.Add(sb.ToString())
    printfn "SJIS 日文串 %d 条（前 20）：" jp.Count
    jp |> Seq.truncate 20 |> Seq.iter (fun x -> printfn "  %s" x)
