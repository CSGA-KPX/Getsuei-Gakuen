module AtfJson.Program

open System
open System.IO

let private usage () =
    eprintfn "用法:"
    eprintfn "  AtfJson parse -i <input.atf>  [-o <output.json>]   将二进制 ATF 转储为 JSON"
    eprintfn "  AtfJson build -i <input.json> [-o <output.atf>]    将 JSON 编译回二进制 ATF"

let private parseArgs (argv: string[]) =
    let rec loop (i: int) (input: string option) (output: string option) =
        if i >= argv.Length then (input, output)
        else
            match argv[i] with
            | "-i" | "--input" when i + 1 < argv.Length -> loop (i + 2) (Some argv[i + 1]) output
            | "-o" | "--output" when i + 1 < argv.Length -> loop (i + 2) input (Some argv[i + 1])
            | other ->
                eprintfn "未知参数: %s" other
                usage ()
                exit 2
    loop 1 None None

[<EntryPoint>]
let main argv =
    if argv.Length < 1 then
        usage ()
        2
    else
        let mode = argv[0]
        let input, output = parseArgs argv

        match input with
        | None ->
            eprintfn "缺少必需参数 -i/--input"
            usage ()
            2
        | Some input ->
            if not (File.Exists input) then
                eprintfn "输入文件不存在: %s" input
                1
            else
                match mode with
                | "parse" ->
                    let output = defaultArg output (Path.ChangeExtension(input, ".json"))
                    input |> AtfBinary.parse |> fun atf -> AtfJsonIO.writeJson atf output
                    printfn "已解析 %s -> %s" input output
                    0
                | "build" ->
                    let output = defaultArg output (Path.ChangeExtension(input, ".atf"))
                    input |> AtfJsonIO.readJson |> fun atf -> AtfBinary.build atf output
                    printfn "已编译 %s -> %s" input output
                    0
                | other ->
                    eprintfn "未知命令: %s（应为 parse 或 build）" other
                    usage ()
                    2
