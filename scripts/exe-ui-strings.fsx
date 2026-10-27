// 从 Getsuei.exe 提取 UI/菜单/成就文本（UTF-16LE）
//
// 判据演进（每步都踩过坑，别轻易简化）：
//   1. 只扫实测密度最高的区段 0xD80000-0xDD0000
//   2. 前后 NUL 终止（C 字符串）
//   3. 含假名 + 假名占比 >= 12%
//      ASCII 串被两两组合会"看起来像"日文（"title_menu_1_on" -> 「楴汴彥敭畮た弱湯」），
//      这类伪串汉字多假名少，占比判据能滤掉大部分
//   4. 位置聚类：真实文本表成片连续存放（间距 < 64 字节），孤立的伪串丢弃
//   不可用：用「同偏移的 ASCII 连续可打印长度」判真伪 —— 日文 UTF-16 的低字节
//   常落在 ASCII 可打印区（如 U+5168「全」= 68 51），会误杀真串。
open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Encodings.Web
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let exe = @"<游戏安装目录>\Getsuei.exe"
let bytes = File.ReadAllBytes exe

/// 允许的 UTF-16 code unit（只收常用区，剔除扩展区生僻字）
let ok (u: int) =
    (u >= 0x20 && u <= 0x7E)
    || (u >= 0x3000 && u <= 0x30FF)
    || (u >= 0x4E00 && u <= 0x9FA5)
    || (u >= 0xFF01 && u <= 0xFF60)
    || (u >= 0xFF61 && u <= 0xFF9F)
    || u = 0x2015 || u = 0x2014 || u = 0x2026 || u = 0x2025 || u = 0x25CB || u = 0x3005

let isKana (u: int) = (u >= 0x3041 && u <= 0x30FF)

let lo, hi = 0xCE0000, min 0xDD0000 bytes.Length
let raw = ResizeArray<int * string>()

let mutable i = lo
while i < hi - 4 do
    // 起点必须是真正的串边界：前两字节为 NUL（放宽判据会引入 ASCII 错位伪串，实测 46->185 条且出现大量误报）
    let prevNul = i < 2 || (bytes.[i - 2] = 0uy && bytes.[i - 1] = 0uy)
    let u = int bytes.[i] ||| (int bytes.[i + 1] <<< 8)
    if prevNul && ok u then
        let sb = StringBuilder()
        let mutable j = i
        let mutable go = true
        while go && j < hi - 1 do
            let v = int bytes.[j] ||| (int bytes.[j + 1] <<< 8)
            if ok v then
                sb.Append(char v) |> ignore
                j <- j + 2
            else go <- false
        let s = sb.ToString().Trim()
        let nulEnd = j + 1 < bytes.Length && bytes.[j] = 0uy && bytes.[j + 1] = 0uy
        let kana = s |> Seq.filter (fun c -> isKana (int c)) |> Seq.length
        let ratio = if s.Length = 0 then 0.0 else float kana / float s.Length
        if nulEnd && s.Length >= 2 && kana >= 1 && ratio >= 0.12 then
            raw.Add(i, s)
        i <- max (i + 2) (if j > i then j else i + 2)
    else i <- i + 2

/// 位置聚类：只保留与相邻条目间距 < 64 字节的（真实文本表成片存放）
let clustered =
    let sorted = raw |> Seq.sortBy fst |> Array.ofSeq
    [| for idx in 0 .. sorted.Length - 1 do
         let prevGap = if idx > 0 then fst sorted.[idx] - fst sorted.[idx - 1] else 999999
         let nextGap = if idx < sorted.Length - 1 then fst sorted.[idx + 1] - fst sorted.[idx] else 999999
         if prevGap < 64 || nextGap < 64 then yield sorted.[idx] |]

printfn "原始 %d 条 -> 聚类后 %d 条（滤掉 %d 条孤立伪串）" raw.Count clustered.Length (raw.Count - clustered.Length)
printfn "区段 0x%X-0x%X（%d KB）" lo hi ((hi - lo) / 1024)

let cls (s: string) =
    if s.Contains "%" then "格式串"
    elif s.Length <= 6 then "短词（6 字以内）"
    elif s.Length <= 14 then "中等（7-14 字）"
    else "长句（14 字以上）"

printfn "\n| 类别 | 条数 |"
printfn "|---|---|"
clustered |> Seq.countBy (fun (_, s) -> cls s) |> Seq.sortByDescending snd
|> Seq.iter (fun (k, v) -> printfn "| %s | %d |" k v)

printfn "\n===== 全部 %d 条 =====" clustered.Length
for (off, s) in clustered do
    printfn "  0x%06X  %s" off s

Directory.CreateDirectory "work/inventory" |> ignore
let jopts = JsonWriterOptions(Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let fs = new FileStream("work/inventory/exe-ui-strings.json", FileMode.Create)
let jw = new Utf8JsonWriter(fs, jopts)
jw.WriteStartObject()
jw.WriteNumber("total", clustered.Length)
jw.WriteString("rangeStart", sprintf "0x%X" lo)
jw.WriteString("rangeEnd", sprintf "0x%X" hi)
jw.WriteStartArray("strings")
for (off, s) in clustered do
    jw.WriteStartObject()
    jw.WriteString("offset", sprintf "0x%X" off)
    jw.WriteString("text", s)
    jw.WriteString("kind", cls s)
    jw.WriteEndObject()
jw.WriteEndArray()
jw.WriteEndObject()
jw.Dispose()
fs.Dispose()
printfn "\n-> work/inventory/exe-ui-strings.json"
