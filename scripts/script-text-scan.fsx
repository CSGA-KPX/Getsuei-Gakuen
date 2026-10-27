// 假设 A 验证：菜单文本是否藏在 ADVScript（scr_*.pac 内的 bin）字符串池里
//
// ADVScript 字符串池 = NUL 结尾的字符串流，编码为 Shift-JIS(932)（见 docs/formats/advscript.md）。
// 思路：解包全部 scr pac → 对每个 bin 暴力提取 SJIS 日文串 → 按「菜单关键词」过滤。
#load "../scripts/Pac.fsx"

open System
open System.IO
open System.Text
open PacLib
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let sjis = Encoding.GetEncoding 932
let dir = @"<游戏安装目录>\data\story\script"

/// SJIS 双字节首字节范围
let isLead (b: int) = (b >= 0x81 && b <= 0x84) || (b >= 0x87 && b <= 0x9F) || (b >= 0xE0 && b <= 0xEF)
let isTrail (b: int) = (b >= 0x40 && b <= 0x7E) || (b >= 0x80 && b <= 0xFC)

/// 从字节流提取 SJIS 字符串（连续 ASCII 可见 / 双字节日文，长度 >= 2）
let extract (data: byte[]) =
    let res = ResizeArray<string>()
    let sb = StringBuilder()
    let mutable i = 0
    while i < data.Length do
        let b = int data.[i]
        if b >= 0x20 && b <= 0x7E then
            sb.Append(char b) |> ignore
            i <- i + 1
        elif isLead b && i + 1 < data.Length && isTrail (int data.[i + 1]) then
            sb.Append(sjis.GetString(data, i, 2)) |> ignore
            i <- i + 2
        else
            if sb.Length >= 2 then res.Add(sb.ToString())
            sb.Clear() |> ignore
            i <- i + 1
    if sb.Length >= 2 then res.Add(sb.ToString())
    res

/// 只要含假名的串（真正的日文文本，而非资源名/命令名）
let isKana (c: char) = let u = int c in (u >= 0x3041 && u <= 0x30FF)
let kanaCount (s: string) = s |> Seq.filter isKana |> Seq.length

let menuKeywords =
    [ "はじ"; "つづ"; "オプション"; "セーブ"; "ロード"; "設定"; "終了"; "タイトル"
      "コンティニュ"; "戻る"; "閉じる"; "音量"; "画面"; "操作"; "実績"; "おまけ" ]

let all = ResizeArray<string * string>()   // (pac, text)
let pacs = Directory.GetFiles(dir, "*.pac") |> Array.sort
let mutable fileCount = 0

for p in pacs do
    let pac = PacLib.read p
    for e in pac.Entries do
        fileCount <- fileCount + 1
        let data = PacLib.extract pac e
        for s in extract data do
            all.Add(Path.GetFileName p, s)

printfn "扫描 %d 个 scr pac / %d 个内嵌文件，含假名的 SJIS 串 %d 条" pacs.Length fileCount all.Count
let uniq = all |> Seq.map snd |> Seq.distinct |> Array.ofSeq
printfn "去重后 %d 条\n" uniq.Length

printfn "===== 按菜单关键词过滤 ====="
let hits =
    uniq
    |> Array.filter (fun s -> menuKeywords |> List.exists s.Contains)
    |> Array.sort
printfn "命中 %d 条：" hits.Length
for s in hits |> Array.truncate 40 do
    printfn "  %s" s

printfn "\n===== 长度 >= 4 的日文串抽样（看有没有菜单类文本）====="
uniq |> Array.filter (fun s -> s.Length >= 4) |> Array.sort
|> Array.truncate 40 |> Array.iter (fun s -> printfn "  %s" s)

// 落盘
Directory.CreateDirectory "work/inventory" |> ignore
File.WriteAllLines("work/inventory/script-sjis-strings.txt", uniq |> Array.sort, UTF8Encoding false)
printfn "\n→ work/inventory/script-sjis-strings.txt（%d 条）" uniq.Length
