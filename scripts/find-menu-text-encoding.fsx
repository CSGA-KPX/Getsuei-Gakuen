// 用游戏特有名词「開放の間」反查菜单文本的编码与位置
// 已排除了：UTF-16LE（exe 里只有成就）、Shift-JIS（exe/ADVScript 均无）、pac 内图像（旧版）
// 这次把常见编码全试一遍，并同时扫 exe 与全部 pac 数据
#load "../scripts/Pac.fsx"

open System
open System.IO
open System.Text
open PacLib
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let game = @"<游戏安装目录>"
let words = [ "開放の間"; "はじめから"; "つづきから"; "オプション" ]

let encodings =
    [ "UTF-8", box (UTF8Encoding(false))
      "UTF-16LE", box (Encoding.Unicode)
      "UTF-16BE", box (Encoding.BigEndianUnicode)
      "Shift-JIS", box (Encoding.GetEncoding 932)
      "EUC-JP", box (Encoding.GetEncoding 51932) ]

let exeBytes = File.ReadAllBytes(Path.Combine(game, "Getsuei.exe"))

let findAll (hay: byte[]) (needle: byte[]) =
    let res = ResizeArray<int>()
    let mutable i = 0
    while i <= hay.Length - needle.Length do
        let mutable ok = true
        let mutable j = 0
        while ok && j < needle.Length do
            if hay.[i + j] <> needle.[j] then ok <- false
            j <- j + 1
        if ok then
            res.Add i
            i <- i + needle.Length
        else i <- i + 1
    res

printfn "===== exe 内搜索 ====="
for w in words do
    printfn "\n「%s」" w
    for (encName, enc) in encodings do
        let e = enc :?> Encoding
        let pat = e.GetBytes w
        let h = findAll exeBytes pat
        printfn "  %-10s %d 次 %s" encName h.Count
            (if h.Count > 0 then sprintf "（首个文件偏移 0x%X）" h.[0] else "")

// 扫全部 pac（含嵌套）的数据流里搜
printfn "\n===== 全部 pac 内搜索（UTF-16LE / Shift-JIS / UTF-8）====="
let mutable tmpSeq = 0
let readPac (data: byte[]) =
    tmpSeq <- tmpSeq + 1
    let tmp = Path.Combine(Path.GetTempPath(), sprintf "gakuen-enc-%d.pac" tmpSeq)
    File.WriteAllBytes(tmp, data)
    let r = PacLib.read tmp
    File.Delete tmp
    r

let hits = ResizeArray<string * string * int * string>()  // (pac, file, offset, word)

let rec walk (label: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        let data = PacLib.extract pac e
        if e.Filename.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) && depth < 5 then
            try walk (label + "/" + e.Filename) (readPac data) (depth + 1) with _ -> ()
        else
            for w in words do
                for (encName, enc) in [ ("UTF-16LE", box (Encoding.Unicode)); ("Shift-JIS", box (Encoding.GetEncoding 932)); ("UTF-8", box (UTF8Encoding(false))) ] do
                    let ec = enc :?> Encoding
                    let pat = ec.GetBytes w
                    let h = findAll data pat
                    if h.Count > 0 then hits.Add(label, e.Filename, h.[0], sprintf "%s/%s" w encName)

let root = Path.Combine(game, "data")
let pacs = Directory.GetFiles(root, "*.pac", SearchOption.AllDirectories) |> Array.sort
printfn "扫描 %d 个 pac..." pacs.Length
for p in pacs do
    let rel = p.Substring(root.Length + 1).Replace('\\', '/')
    try walk rel (PacLib.read p) 0 with _ -> ()

printfn "\n命中 %d 处：" hits.Count
hits |> Seq.sortBy (fun (p, f, _, _) -> p, f)
|> Seq.truncate 40
|> Seq.iter (fun (p, f, off, w) -> printfn "  %-44s %-30s +0x%X  %s" p f off w)
