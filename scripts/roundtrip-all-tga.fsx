// TGA type9 roundtrip 全量回归：image 目录全部 .hip（TGA 子格式）
// 三级判定：变体A 字节一致 → 变体B(endRunAsRaw) 字节一致 → decode 语义一致 → FAIL
#r "nuget: SixLabors.ImageSharp"
#load "tga.fsx"

open System
open System.IO
open TgaLib

let imgDir = @"<游戏安装目录>\data\story\image"

let files = Directory.GetFiles(imgDir, "*.hip")
let mutable okA = 0
let mutable okB = 0
let mutable okSemantic = 0
let mutable skipped = 0
let mutable bad = []

let pixelsEqual (a: TgaImage) (b: TgaImage) =
    a.Indices = b.Indices
    && a.Palette.Length = b.Palette.Length
    && Array.forall2 (fun (x: SixLabors.ImageSharp.PixelFormats.Rgba32) y -> x = y) a.Palette b.Palette

for p in files do
    let orig = File.ReadAllBytes p
    if orig.Length >= 3 && Text.Encoding.UTF8.GetString(orig, 0, 3) = "HIP" then
        skipped <- skipped + 1 // HIP 专有格式，不在此验证
    else
        try
            let img = TgaLib.decode orig
            if TgaLib.encodeWith false img = orig then
                okA <- okA + 1
            elif TgaLib.encodeWith true img = orig then
                okB <- okB + 1
            else
                // 语义级：重建后再解码，像素/调色板必须一致
                let rebuilt = TgaLib.decode (TgaLib.encode img)
                if pixelsEqual img rebuilt then
                    okSemantic <- okSemantic + 1
                    eprintfn "SEMANTIC-ONLY: %s" (Path.GetFileName p)
                else
                    bad <- Path.GetFileName p :: bad
                    eprintfn "MISMATCH: %s" p
        with ex ->
            bad <- sprintf "%s (%s)" (Path.GetFileName p) ex.Message :: bad
            eprintfn "ERROR: %s: %s" p ex.Message

printfn "结果: %d OK-A / %d OK-B / %d OK-语义 / %d SKIP(HIP专有) / %d FAIL（共 %d）"
    okA okB okSemantic skipped bad.Length files.Length
if not bad.IsEmpty then
    eprintfn "失败: %A" (List.rev bad)
    exit 1
