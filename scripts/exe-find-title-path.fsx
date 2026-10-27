// 查 Getsuei.exe 里与 AE/title 资源加载相关的字符串（确认 UI_02_title.pac 是否被引用）
open System
open System.IO
open System.Text
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let exe = @"<游戏安装目录>\Getsuei.exe"
let bytes = File.ReadAllBytes exe
let ascii = Encoding.ASCII

let findAll (s: string) =
    let p = ascii.GetBytes s
    let res = ResizeArray<int>()
    let mutable i = 0
    while i <= bytes.Length - p.Length do
        let mutable ok = true
        let mutable j = 0
        while ok && j < p.Length do
            if bytes.[i + j] <> p.[j] then ok <- false
            j <- j + 1
        if ok then
            res.Add i
            i <- i + p.Length
        else i <- i + 1
    res

printfn "| 字符串 | 命中 | 首个偏移 | 上下文 |"
printfn "|---|---|---|---|"
for s in [ "UI_02_title"; "title_menu"; "AI_02"; "_title"; "AE/"; "AE\\"; "tex.pac"; "aex.pac" ] do
    let h = findAll s
    let ctx =
        if h.Count > 0 then
            let off = h.[0]
            let start = max 0 (off - 30)
            let len = min 80 (bytes.Length - start)
            ascii.GetString(bytes, start, len).Replace("\u0000", "·")
        else "—"
    printfn "| `%s` | %d | %s | %s |" s h.Count (if h.Count > 0 then sprintf "0x%X" h.[0] else "—") ctx
