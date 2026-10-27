// 批量 atf 提取与统计（阶段 1 提取工作流；翻译底稿由未来工具消费）
//
// 用法:
//   dotnet fsi atf-batch.fsx extract [-o <outdir>]   全部 text pac → 每 pac 一个 JSON（含 roundtrip 回归）
//   dotnet fsi atf-batch.fsx stats  [-o <out.json>]  文本量统计（不写 JSON 提取产物）
//
// 产出:
//   extract: <outdir>/<pac名>.json（AtfJson 格式，可直接 build 回 atf）
//   stats:   记录数/行数/ruby 比例/字符分布/与 image 目录 key 重叠等，供排版与字库方案参考
#load "Pac.fsx"
#load "AtfJson/Atf.fs"
#load "AtfJson/Json.fs"

open System
open System.IO
open System.Collections.Generic
open System.Text.Json
open PacLib
open AtfJson

let gameDir = @"<游戏安装目录>\data"
let textDir = Path.Combine(gameDir, @"story\text\jpn")
let imageDir = Path.Combine(gameDir, "story", "image")

let args = fsi.CommandLineArgs |> Array.skip 1

let argOpt (flag: string) =
    match Array.tryFindIndex (fun a -> a = flag) args with
    | Some i when i + 1 < args.Length -> Some args[i + 1]
    | _ -> None

// 全部对话 text pac（排除更新前旧版备份 *.pac_）
let textPacs =
    Directory.GetFiles(textDir, "*.pac")
    |> Array.sortBy id

/// 从 pac 中提取 string.atf 的原始字节与 atf 模型
let loadAtf (pacPath: string) =
    let pac = PacLib.read pacPath
    let entry = pac.Entries |> Array.tryFind (fun e -> e.Filename = "string.atf")
    match entry with
    | None -> None
    | Some e ->
        let bytes = PacLib.extract pac e
        Some(bytes, AtfBinary.parseBytes bytes)

// WBND（Wave Bank）等非 FPAC 容器不属于 pac 格式（docs/formats/pac.md），跳过
let isPacContainer (path: string) =
    use fs = File.OpenRead(path)
    let fourcc = Array.zeroCreate<byte> 4
    fs.Read(fourcc, 0, 4) |> ignore
    Text.Encoding.ASCII.GetString fourcc = "FPAC"

let pacTargets = textPacs |> Array.filter isPacContainer

match if args.Length = 0 then "" else args[0] with
| "extract" ->
    let outDir = defaultArg (argOpt "-o") "work/atf"
    Directory.CreateDirectory(outDir) |> ignore
    let mutable ok = 0
    let mutable bad = []
    for p in pacTargets do
        let name = Path.GetFileNameWithoutExtension(p)
        try
            match loadAtf p with
            | None ->
                bad <- (name + " (无 string.atf)") :: bad
                eprintfn "SKIP: %s 无 string.atf" name
            | Some(bytes, atf) ->
                // roundtrip 回归：parse → build 逐字节一致才产出
                if AtfBinary.buildBytes atf <> bytes then
                    bad <- (name + " (roundtrip MISMATCH)") :: bad
                    eprintfn "FAIL roundtrip: %s" name
                else
                    AtfJsonIO.writeJson atf (Path.Combine(outDir, name + ".json"))
                    ok <- ok + 1
        with ex ->
            bad <- (name + " (" + ex.Message + ")") :: bad
            eprintfn "ERROR: %s: %s" name ex.Message
    printfn "extract: %d OK / %d FAIL（共 %d pac）-> %s" ok bad.Length pacTargets.Length outDir
    if not bad.IsEmpty then
        eprintfn "失败: %A" (List.rev bad)
        exit 1

| "stats" ->
    let outPath = defaultArg (argOpt "-o") "work/inventory/atf-stats.json"
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)) |> ignore

    // image 目录已有的同名 .hip（预渲染台词图 key，这些句子不走 atf 渲染）
    let imageKeys =
        Directory.GetFiles(imageDir, "*.hip")
        |> Array.map (fun p -> Path.GetFileNameWithoutExtension(p))
        |> HashSet

    let w = new Utf8JsonWriter(new FileStream(outPath, FileMode.Create), JsonWriterOptions(Indented = true))
    w.WriteStartObject()
    w.WriteNumber("pacCount", pacTargets.Length)
    w.WriteStartArray("files")

    let mutable totalRecords = 0
    let mutable totalChars = 0L
    let mutable rubyRecords = 0
    let mutable rubyChars = 0L
    let mutable imageKeyRecords = 0
    let mutable nonZeroTextParam = 0
    let mutable maxValueChars = 0
    let mutable maxValueKey = ""
    let allChars = HashSet<char>()
    let mutable failedFiles = ResizeArray<string>()

    for p in pacTargets do
        let name = Path.GetFileNameWithoutExtension(p)
        try
            match loadAtf p with
            | None -> failedFiles.Add(name + " (无 string.atf)")
            | Some(_, atf) ->
                let mutable fileChars = 0
                let mutable fileRuby = 0
                let mutable fileImageKeys = 0
                for r in atf.Records do
                    totalRecords <- totalRecords + 1
                    let chars = r.Value.Length
                    totalChars <- totalChars + int64 chars
                    fileChars <- fileChars + chars
                    for c in r.Value do
                        allChars.Add c |> ignore
                    if r.Value.Contains("<ruby>") then
                        rubyRecords <- rubyRecords + 1
                        fileRuby <- fileRuby + 1
                        rubyChars <- rubyChars + int64 chars
                    if imageKeys.Contains(r.Key) then
                        imageKeyRecords <- imageKeyRecords + 1
                        fileImageKeys <- fileImageKeys + 1
                    if r.TextParam |> Array.exists ((<>) 0u) then
                        nonZeroTextParam <- nonZeroTextParam + 1
                    if chars > maxValueChars then
                        maxValueChars <- chars
                        maxValueKey <- sprintf "%s:%s" name r.Key

                w.WriteStartObject()
                w.WriteString("pac", name)
                w.WriteNumber("records", atf.Records.Length)
                w.WriteNumber("chars", fileChars)
                w.WriteNumber("rubyRecords", fileRuby)
                w.WriteNumber("imageKeyRecords", fileImageKeys)
                w.WriteEndObject()
        with ex ->
            failedFiles.Add(name + " (" + ex.Message + ")")

    w.WriteEndArray()

    // 全局汇总
    w.WriteNumber("totalRecords", totalRecords)
    w.WriteNumber("totalChars", totalChars)
    w.WriteNumber("rubyRecords", rubyRecords)
    w.WriteNumber("rubyChars", rubyChars)
    w.WriteNumber("imageKeyRecords", imageKeyRecords)
    w.WriteNumber("nonZeroTextParamRecords", nonZeroTextParam)
    w.WriteNumber("distinctChars", allChars.Count)
    w.WriteString("maxValue", maxValueKey + sprintf " (%d 字符)" maxValueChars)
    w.WriteStartArray("failedFiles")
    for f in failedFiles do
        w.WriteStringValue(f)
    w.WriteEndArray()
    w.WriteEndObject()
    w.Flush()

    printfn "stats: %d pac, %d 记录, %d 字符, ruby 记录 %d (%.1f%%), 与 image key 重叠 %d, 非零 textParam %d"
        pacTargets.Length totalRecords totalChars rubyRecords
        (float rubyRecords * 100.0 / float (max 1 totalRecords))
        imageKeyRecords nonZeroTextParam
    printfn "去重字符 %d；最长 value: %s；-> %s" allChars.Count maxValueKey outPath

| other ->
    eprintfn "未知子命令: %s（应为 extract 或 stats）" other
    eprintfn "用法: dotnet fsi atf-batch.fsx extract|stats [-o ...]"
    exit 2
