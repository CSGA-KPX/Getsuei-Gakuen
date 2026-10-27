// 决定性测试：把 UI_02_title.pac 完全原样重建（内容零改动），对比 SHA 是否一致
// 一致 → pac 重建无损，问题在「游戏是否读这个 pac」
// 不一致 → pac 重建有差异，diff 出具体位置
#load "../scripts/Pac.fsx"

open System
open System.IO
open PacLib

let game = @"<游戏安装目录>"
let outerPath = Path.Combine(game, @"data\AE\title\UI_02_title.pac")
let origBytes = File.ReadAllBytes outerPath

let sha (b: byte[]) =
    use s = new MemoryStream(b)
    use h = Security.Cryptography.SHA256.Create()
    h.ComputeHash(s) |> Array.map (fun x -> x.ToString("x2")) |> String.concat ""

printfn "原 pac：%d 字节  SHA=%s" origBytes.Length (sha origBytes)

// ---- 只重建外层（内层 tex.pac 原样）----
let outer = PacLib.read outerPath
let contents = outer.Entries |> Array.map (fun e -> PacLib.extract outer e)
let rebuilt = PacLib.build outer contents
printfn "重建后：%d 字节  SHA=%s" rebuilt.Length (sha rebuilt)
printfn "外层重建是否一致：%s\n" (if sha origBytes = sha rebuilt then "✅ 一致（pac 重建无损）" else "❌ 不一致")

// ---- 若不一致，定位首个差异 ----
if sha origBytes <> sha rebuilt then
    let minLen = min origBytes.Length rebuilt.Length
    let mutable firstDiff = -1
    let mutable i = 0
    while firstDiff < 0 && i < minLen do
        if origBytes.[i] <> rebuilt.[i] then firstDiff <- i
        i <- i + 1
    printfn "首个差异偏移：+0x%X" firstDiff
    let lo = max 0 (firstDiff - 32)
    let dump (label: string) (b: byte[]) =
        printfn "%s:" label
        for k in 0 .. 16 .. 63 do
            if lo + k < b.Length then
                let chunk = b.[lo + k .. min (lo + k + 15) (b.Length - 1)] |> Array.map (fun x -> x.ToString("X2")) |> String.concat " "
                printfn "  +%04X  %s" (lo + k) chunk
    dump "原始" origBytes
    dump "重建" rebuilt

// ---- 再测内层 tex.pac 单独重建 ----
printfn "\n---- 内层 tex.pac ----"
let texIdx = outer.Entries |> Array.findIndex (fun e -> e.Filename = "tex.pac")
let texBytes = contents.[texIdx]
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-rebuild-tex.pac")
File.WriteAllBytes(tmp, texBytes)
let tex = PacLib.read tmp
File.Delete tmp
let texContents = tex.Entries |> Array.map (fun e -> PacLib.extract tex e)
let texRebuilt = PacLib.build tex texContents
printfn "tex.pac 原 %d 字节 SHA=%s" texBytes.Length (sha texBytes)
printfn "tex.pac 重建 %d 字节 SHA=%s" texRebuilt.Length (sha texRebuilt)
printfn "内层重建是否一致：%s" (if sha texBytes = sha texRebuilt then "✅ 一致" else "❌ 不一致")
