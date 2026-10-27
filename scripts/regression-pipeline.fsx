// 全量回归流水线（TODO 阶段 3）：pac → atf → 模拟翻译 → atf → pac → 回解校验
//
// 用法:
//   dotnet fsi regression-pipeline.fsx run           # 全量 237 pac 回归（产物写 work/regression/）
//   dotnet fsi regression-pipeline.fsx run <pac路径>  # 单 pac 回归
//
// 流程（每 pac）:
//   1. PacLib 解包三件套（font.hip / font.abc / string.atf）
//   2. AtfBinary.parseBytes → 逐条 record 做确定性伪翻译（保留 <ruby> 标签语法与 \n，文字字符
//      映射到常用汉字表）→ buildBytes
//   3. PacLib.build 重打包（字库两组件透传原字节）
//   4. 内存回解：重解包 → atf 再 parse → 校验 key 集合/顺序一致、value == 伪翻译结果
//      （font.hip/font.abc 字节相等）
//   5. 报告累计到 work/regression/report.json（不进游戏目录）

#load "Pac.fsx"
#load "AtfJson/Atf.fs"

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Encodings.Web
open PacLib
open AtfJson

let game = @"<游戏安装目录>\data"
let workspace = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let outDir = Path.Combine(workspace, "work", "regression")

// 伪翻译：常用汉字表，按 char code 确定性取模（相同输入永远相同输出）
let hanTable = "汉月英学园灯影风花雪月山川天地人心光暗夜梦星河云海霜雨露言语思忆春夏秋冬青赤黑白金银"
let pseudo (c: char) : char = hanTable[(int c * 31 + int c / 7) % hanTable.Length]

// 保留 <ruby> 标签语法：<ruby> 、< 、/ruby> 作为分隔符不切分内部
let private tagPattern = Text.RegularExpressions.Regex(@"<ruby>|</ruby>|<")

let translateValue (value: string) : string =
    let parts = tagPattern.Split(value)
    let tags = tagPattern.Matches(value)
    let sb = StringBuilder()
    for i in 0 .. parts.Length - 1 do
        // 非标签段：换行保留，其余字符伪翻译
        for c in parts[i] do
            if c = '\n' then sb.Append(c) |> ignore
            else sb.Append(pseudo c) |> ignore
        if i < tags.Count then sb.Append(tags[i].Value) |> ignore
    sb.ToString()

type PacResult =
    { mutable Ok: bool
      mutable Records: int
      mutable Chars: int
      mutable Error: string }

let regressOne (pacPath: string) : PacResult =
    let res = { Ok = false; Records = 0; Chars = 0; Error = "" }
    try
        let orig = PacLib.read pacPath
        let hipEntry = orig.Entries |> Array.find (fun e -> e.Filename = "font.hip")
        let abcEntry = orig.Entries |> Array.find (fun e -> e.Filename = "font.abc")
        let atfEntry = orig.Entries |> Array.find (fun e -> e.Filename = "string.atf")
        let hipBytes = PacLib.extract orig hipEntry
        let abcBytes = PacLib.extract orig abcEntry
        let atfBytes = PacLib.extract orig atfEntry

        let atf = AtfBinary.parseBytes atfBytes
        let translated =
            atf.Records
            |> Array.map (fun r -> { r with Value = translateValue r.Value })
        let atf' = { atf with Records = translated }
        let atfBytes' = AtfBinary.buildBytes atf'
        res.Records <- translated.Length
        res.Chars <- translated |> Array.sumBy (fun r -> r.Value.Length)

        // 重打包（条目顺序/元数据沿用原 pac）
        let contents =
            orig.Entries
            |> Array.map (fun e ->
                if e.Filename = "string.atf" then atfBytes'
                elif e.Filename = "font.hip" then hipBytes
                elif e.Filename = "font.abc" then abcBytes
                else PacLib.extract orig e)
        let pac' = build orig contents

        // 回解校验
        let pac2 = PacLib.readBytes pac'
        let atf2 = AtfBinary.parseBytes (PacLib.extract pac2 (pac2.Entries |> Array.find (fun e -> e.Filename = "string.atf")))
        if atf2.Records.Length <> translated.Length then failwithf "记录数不符 %d != %d" atf2.Records.Length translated.Length
        for i in 0 .. translated.Length - 1 do
            if atf2.Records[i].Key <> translated[i].Key then failwithf "key[%d] 不符" i
            if atf2.Records[i].Value <> translated[i].Value then failwithf "value[%d] 回解不一致" i
        if PacLib.extract pac2 (pac2.Entries |> Array.find (fun e -> e.Filename = "font.hip")) <> hipBytes then failwith "font.hip 被改动"
        if PacLib.extract pac2 (pac2.Entries |> Array.find (fun e -> e.Filename = "font.abc")) <> abcBytes then failwith "font.abc 被改动"
        res.Ok <- true
    with e ->
        res.Error <- e.Message
    res

let args = fsi.CommandLineArgs |> Array.skip 1
match args with
| [|"run"|] ->
    Directory.CreateDirectory outDir |> ignore
    let pacs =
        Directory.GetFiles(Path.Combine(game, @"story\text\jpn"), "text_*.pac")
        |> Array.sort
    printfn "共 %d 个 text pac" pacs.Length
    let results = ResizeArray<string * PacResult>()
    let mutable totalChars = 0
    for p in pacs do
        let r = regressOne p
        results.Add(Path.GetFileName p, r)
        if r.Ok then totalChars <- totalChars + r.Chars
        else eprintfn "FAIL %s: %s" (Path.GetFileName p) r.Error
    let report =
        {| pacCount = pacs.Length
           okCount = results |> Seq.filter (fun (_, r) -> r.Ok) |> Seq.length
           totalRecords = results |> Seq.sumBy (fun (_, r) -> r.Records)
           totalChars = totalChars
           failures = results |> Seq.choose (fun (n, r) -> if r.Ok then None else Some {| pac = n; error = r.Error |}) |> Seq.toArray |}
    let opts = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
    File.WriteAllText(Path.Combine(outDir, "report.json"), JsonSerializer.Serialize(report, opts))
    printfn "OK %d/%d，总记录 %d，总字符 %d，失败 %d" report.okCount report.pacCount report.totalRecords report.totalChars report.failures.Length
| [|"run"; pacPath|] ->
    let r = regressOne pacPath
    printfn "%s: %s (records=%d chars=%d %s)" (Path.GetFileName pacPath) (if r.Ok then "OK" else "FAIL") r.Records r.Chars r.Error
| _ ->
    eprintfn "用法: regression-pipeline.fsx run [pac路径]"
    exit 2
