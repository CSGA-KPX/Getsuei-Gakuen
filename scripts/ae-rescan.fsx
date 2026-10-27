// 重新扫描 data\AE 下所有 pac（递归嵌套层），生成 v2 清单
#load "./Pac.fsx"

open System
open System.IO
open PacLib
open System.Text.Json

let root = @"<游戏安装目录>\data\AE"

let fmt (b: byte[]) =
    if b.Length >= 3 && b.[0] = 0x48uy && b.[1] = 0x49uy && b.[2] = 0x50uy then "HIP"
    elif b.Length >= 18 && b.[2] = 2uy then "TGA"
    else "other"

let items = ResizeArray<string * string * int * string>()
let mutable tmpSeq = 0

let readPac (data: byte[]) =
    tmpSeq <- tmpSeq + 1
    let tmp = Path.Combine(Path.GetTempPath(), sprintf "gakuen-ae-%d.pac" tmpSeq)
    File.WriteAllBytes(tmp, data)
    let r = PacLib.read tmp
    File.Delete tmp
    r

/// 递归展开嵌套 pac，prefix 记「外层.pac/内层.pac」
let rec expand (prefix: string) (inner: PacFile) (depth: int) =
    for e in inner.Entries do
        let d = PacLib.extract inner e
        if e.Filename.EndsWith(".pac", StringComparison.OrdinalIgnoreCase) && depth < 4 then
            expand (prefix + "/" + e.Filename) (readPac d) (depth + 1)
        else
            items.Add(prefix, e.Filename, d.Length, fmt d)

let pacs = Directory.GetFiles(root, "*.pac", SearchOption.AllDirectories) |> Array.sort

for p in pacs do
    let rel = p.Substring(root.Length + 1).Replace('\\', '/')
    try expand rel (PacLib.read p) 0
    with ex -> items.Add(rel, "(read-failed) " + ex.Message, 0, "error")

printfn "扫描完成：%d 个外层 pac，%d 个最终资源文件" pacs.Length items.Count
printfn "按格式: %s"
    (items |> Seq.countBy (fun (_, _, _, f) -> f) |> Seq.map (fun (f, c) -> sprintf "%s=%d" f c) |> String.concat "  ")

do
    use fs = new FileStream("work/inventory/ae-image-inventory-v2.json", FileMode.Create)
    use jw = new Utf8JsonWriter(fs, JsonWriterOptions(Indented = true))
    jw.WriteStartObject()
    jw.WriteNumber("pacCount", pacs.Length)
    jw.WriteNumber("fileCount", items.Count)
    jw.WriteStartArray("files")
    for (pack, file, size, format) in items do
        jw.WriteStartObject()
        jw.WriteString("pack", pack)
        jw.WriteString("file", file)
        jw.WriteNumber("size", size)
        jw.WriteString("format", format)
        jw.WriteEndObject()
    jw.WriteEndArray()
    jw.WriteEndObject()

printfn "\n按顶层目录聚合："
items
|> Seq.countBy (fun (p, _, _, _) -> p.Split('/').[0])
|> Seq.sortByDescending snd
|> Seq.iter (fun (d, c) -> printfn "  %-16s %d" d c)
