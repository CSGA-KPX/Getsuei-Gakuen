// 实机复验补丁 v2：同时改「标题 logo」和「菜单第一项」
// v1 只改 title_menu_00 却没看到变化，但已本地验证 HIP 原样重编码逐字节相同、pac 原样重建 SHA 一致，
// 所以怀疑是游戏未重启（运行中替换磁盘文件不会重新加载）。这次连 logo 一起改作为最强信号：
//   title_logo.hip     → 月英学園 logo（标题画面必显示，验证「游戏是否读到这个 pac」）
//   title_menu_00.hip  → 菜单第一项「はじめから」普通态
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "../scripts/Pac.fsx"
#load "../scripts/hip.fsx"

open System
open System.IO
open PacLib
open HipLib

let game = @"<游戏安装目录>"
let outerPath = Path.Combine(game, @"data\AE\title\UI_02_title.pac")

/// 涂成不透明品红（保留非透明区域形状，alpha 拉满 —— 原图文字 alpha 只有 1，只改 RGB 看不见）
let paintMagenta (img: HipImage) =
    match img.Format, img.Palette with
    | Indexed8, Some pal ->
        let pal2 =
            pal |> Array.map (fun c ->
                if c.[3] = 0uy then c
                else [| 255uy; 0uy; 255uy; 255uy |])   // BGRA 品红
        { img with Palette = Some pal2 }
    | Argb32, _ ->
        let px = Array.copy img.Pixels
        for i in 0 .. 4 .. px.Length - 4 do
            if px.[i + 3] <> 0uy then
                px.[i] <- 255uy      // B
                px.[i + 1] <- 0uy    // G
                px.[i + 2] <- 255uy  // R
                px.[i + 3] <- 255uy  // A
        { img with Pixels = px }
    | _ -> img

let targets = [ "title_logo.hip"; "title_menu_00.hip" ]

let outer = PacLib.read outerPath
let texIdx = outer.Entries |> Array.findIndex (fun e -> e.Filename = "tex.pac")
let tmp = Path.Combine(Path.GetTempPath(), "gakuen-v2-tex.pac")
File.WriteAllBytes(tmp, PacLib.extract outer outer.Entries.[texIdx])
let tex = PacLib.read tmp
File.Delete tmp

printfn "改动清单："
let texContents =
    tex.Entries
    |> Array.map (fun e ->
        if targets |> List.contains e.Filename then
            let raw = PacLib.extract tex e
            let img = HipLib.decode raw
            let painted = paintMagenta img
            let out = HipLib.encode painted
            printfn "  %-20s %dx%d %A → 品红（%d → %d 字节）"
                e.Filename img.ImageWidth img.ImageHeight img.Format raw.Length out.Length
            out
        else PacLib.extract tex e)

let newTex = PacLib.build tex texContents
let outerContents =
    outer.Entries |> Array.mapi (fun i e -> if i = texIdx then newTex else PacLib.extract outer e)
let newOuter = PacLib.build outer outerContents

Directory.CreateDirectory "work/patch-test" |> ignore
File.WriteAllBytes("work/patch-test/UI_02_title.pac", newOuter)

// 自检：重新解包确认改动确实写进去了
let chkOuter = PacLib.read "work/patch-test/UI_02_title.pac"
let cTexBytes = PacLib.extract chkOuter chkOuter.Entries.[texIdx]
let tmp2 = Path.Combine(Path.GetTempPath(), "gakuen-v2-chk.pac")
File.WriteAllBytes(tmp2, cTexBytes)
let cTex = PacLib.read tmp2
File.Delete tmp2
printfn "\n自检（重新解包后采样）："
for t in targets do
    let e = cTex.Entries |> Array.find (fun e -> e.Filename = t)
    let img = HipLib.decode (PacLib.extract cTex e)
    let sample =
        match img.Format, img.Palette with
        | Argb32, _ ->
            let mutable s = ""
            let mutable i = 0
            while s = "" && i < img.Pixels.Length - 3 do
                if img.Pixels.[i + 3] > 0uy then
                    s <- sprintf "BGRA=(%d,%d,%d,%d)" img.Pixels.[i] img.Pixels.[i + 1] img.Pixels.[i + 2] img.Pixels.[i + 3]
                i <- i + 4
            s
        | _ -> "（索引图，已改调色板）"
    printfn "  %-20s %dx%d %s" t img.ImageWidth img.ImageHeight sample

printfn "\n→ work/patch-test/UI_02_title.pac（%d 字节）" newOuter.Length
printfn "装机：dotnet fsi scripts/install-title-test.fsx"
