// AE/UI 目录嵌套 pac 内图像盘点：子格式判别 + 文字图候选标记
// 产出 work/inventory/ae-image-inventory.json
#load "Pac.fsx"

open System
open System.IO
open System.Collections.Generic
open System.Text.Json
open PacLib

let dataDir = @"<游戏安装目录>\data"
let outPath = "work/inventory/ae-image-inventory.json"

// 文字图候选：文件名含这些词（大小写不敏感）
let textHints =
    [ "text"; "title"; "logo"; "menu"; "name"; "date"; "caption"; "label" ]

let results = ResizeArray<JsonElement>()

let w = new Utf8JsonWriter(new FileStream(outPath, FileMode.Create), JsonWriterOptions(Indented = true))
w.WriteStartObject()
w.WriteStartArray("packs")

// 递归解 pac（最多 3 层），收集所有条目
let rec walk (label: string) (pac: PacFile) (depth: int) =
    for e in pac.Entries do
        let data = PacLib.extract pac e
        let lower = e.Filename.ToLowerInvariant()
        if lower.EndsWith(".pac") && depth < 3 then
            try
                walk (label + "/" + e.Filename) (PacLib.readBytes data) (depth + 1)
            with _ ->
                () // 非 FPAC 嵌套（如 WBND），忽略
        elif lower.EndsWith(".hip") || lower.EndsWith(".dds") || lower.EndsWith(".tga") || lower.EndsWith(".aex") then
            let isHipProprietary =
                lower.EndsWith(".hip")
                && data.Length >= 3
                && Text.Encoding.UTF8.GetString(data, 0, 3) = "HIP"
            let isTga =
                lower.EndsWith(".hip")
                && data.Length >= 3
                && Text.Encoding.UTF8.GetString(data, 0, 3) <> "HIP"
            let textHint = textHints |> List.exists (fun h -> lower.Contains(h))

            w.WriteStartObject()
            w.WriteString("pack", label)
            w.WriteString("file", e.Filename)
            w.WriteNumber("size", data.Length)
            w.WriteString("format", if isHipProprietary then "HIP" elif isTga then "TGA" else "other")
            if textHint then w.WriteBoolean("textHint", true)
            w.WriteEndObject()

// 扫描 AE / usual / saveui / cmn 下所有 pac
let roots = [ "AE"; "usual"; "saveui"; "cmn" ]
let mutable pacCount = 0
for root in roots do
    for p in Directory.GetFiles(Path.Combine(dataDir, root), "*.pac", SearchOption.AllDirectories) |> Array.sortBy id do
        try
            walk (root + "/" + Path.GetFileNameWithoutExtension(p)) (PacLib.read p) 0
            pacCount <- pacCount + 1
        with ex ->
            eprintfn "ERROR: %s: %s" p ex.Message

w.WriteEndArray()
w.WriteNumber("pacCount", pacCount)
w.WriteEndObject()
w.Flush()
printfn "完成：%d 个 pac -> %s" pacCount outPath
