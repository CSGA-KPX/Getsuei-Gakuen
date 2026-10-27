// 假设 B 验证：exe 里是否存在 Shift-JIS 编码的日文 UI 文本（区别于已确认的 UTF-16 成就表）
open System
open System.IO
open System.Text
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let exe = @"<游戏安装目录>\Getsuei.exe"
let bytes = File.ReadAllBytes exe
let sjis = Encoding.GetEncoding 932

let isLead (b: int) = (b >= 0x81 && b <= 0x84) || (b >= 0x87 && b <= 0x9F) || (b >= 0xE0 && b <= 0xEF)
let isTrail (b: int) = (b >= 0x40 && b <= 0x7E) || (b >= 0x80 && b <= 0xFC)
let isKana (c: char) = let u = int c in (u >= 0x3041 && u <= 0x30FF)

let hits = ResizeArray<int * string>()
let sb = StringBuilder()
let mutable start = 0
let mutable i = 0
while i < bytes.Length do
    let b = int bytes.[i]
    if b >= 0x20 && b <= 0x7E then
        if sb.Length = 0 then start <- i
        sb.Append(char b) |> ignore
        i <- i + 1
    elif isLead b && i + 1 < bytes.Length && isTrail (int bytes.[i + 1]) then
        if sb.Length = 0 then start <- i
        sb.Append(sjis.GetString(bytes, i, 2)) |> ignore
        i <- i + 2
    else
        if sb.Length >= 3 && (sb.ToString() |> Seq.exists isKana) then hits.Add(start, sb.ToString())
        sb.Clear() |> ignore
        i <- i + 1

printfn "exe 中含假名的 SJIS 串：%d 条" hits.Count
let uniq = hits |> Seq.map snd |> Seq.distinct |> Array.ofSeq
printfn "去重 %d 条\n" uniq.Length
for (off, s) in hits |> Seq.truncate 30 do
    printfn "  0x%06X  %s" off s

// 顺带：搜特定菜单词（SJIS）
printfn "\n===== 菜单词 SJIS 精确搜索 ====="
let words = [ "はじ"; "つづ"; "オプション"; "セーブ"; "ロード"; "設定"; "終了"; "タイトル"; "ゲーム"; "スタート"; "コンティニュ" ]
for w in words do
    let p = sjis.GetBytes w
    let mutable n = 0
    let mutable k = 0
    while k <= bytes.Length - p.Length do
        let mutable ok = true
        let mutable j = 0
        while ok && j < p.Length do
            if bytes.[k + j] <> p.[j] then ok <- false
            j <- j + 1
        if ok then
            n <- n + 1
            k <- k + p.Length
        else k <- k + 1
    printfn "  %-14s %d 次" w n
