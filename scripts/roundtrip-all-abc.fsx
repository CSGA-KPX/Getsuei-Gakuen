// abc 重打包全量回归：jpn_glyph.abc + 237 个 text pac 内嵌 font.abc + cmn/font.pac 嵌套 Font00.abc
#load "Pac.fsx"
#load "abc.fsx"

open System
open System.IO
open PacLib
open AbcLib

let dataDir = @"<游戏安装目录>\data"

let mutable ok = 0
let mutable bad = []

let check (name: string) (bytes: byte[]) =
    try
        if AbcLib.buildBytes (AbcLib.parseBytes (name, bytes)) = bytes then
            ok <- ok + 1
        else
            bad <- name :: bad
            eprintfn "MISMATCH: %s" name
    with ex ->
        bad <- sprintf "%s (%s)" name ex.Message :: bad
        eprintfn "ERROR: %s: %s" name ex.Message

// 1) 全局字库
let jpnAbc = File.ReadAllBytes(Path.Combine(dataDir, @"text\jpn\jpn_glyph.abc"))
check "jpn_glyph.abc" jpnAbc

// 2) 237 个 text pac 内嵌 font.abc
for p in Directory.GetFiles(Path.Combine(dataDir, @"story\text\jpn"), "*.pac") |> Array.sortBy id do
    let pac = PacLib.read p
    match pac.Entries |> Array.tryFind (fun e -> e.Filename = "font.abc") with
    | Some e -> check (Path.GetFileNameWithoutExtension(p) + "/font.abc") (PacLib.extract pac e)
    | None -> eprintfn "SKIP(无 font.abc): %s" (Path.GetFileName p)

// 3) cmn/font.pac 嵌套 abc.pac → Font00.abc
let fontPac = PacLib.read (Path.Combine(dataDir, @"cmn\font.pac"))
match fontPac.Entries |> Array.tryFind (fun e -> e.Filename = "abc.pac") with
| Some e ->
    let abcPac = PacLib.readBytes (PacLib.extract fontPac e)
    match abcPac.Entries |> Array.tryFind (fun x -> x.Filename = "Font00.abc") with
    | Some x -> check "font.pac/abc.pac/Font00.abc" (PacLib.extract abcPac x)
    | None -> eprintfn "ERROR: abc.pac 中无 Font00.abc"
| None -> eprintfn "ERROR: font.pac 中无 abc.pac"

printfn "结果: %d OK / %d FAIL" ok bad.Length
if not bad.IsEmpty then
    eprintfn "失败: %A" (List.rev bad)
    exit 1
