// 结局回想菜单标签：生成 11 个 ending pac 的译文（13 条唯一字符串，按术语表统一）
open System
use _ = System.Text.Encoding.RegisterProvider(Text.CodePagesEncodingProvider.Instance)
open System.IO
open System.Text.Json
open System.Text.Encodings.Web

let jopts = JsonSerializerOptions(Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

/// 原文 → 译文（术语与正文既定译名一致）
let table =
    dict [
        "英理ルート", "英理线"
        "大河ルート", "大河线"
        "雄喜ルート", "雄喜线"
        "ルルルート", "露露线"
        "さつきルート", "皋月线"
        "律ルート", "律线"
        "後堂ルート", "后堂线"
        "久遠院ルート", "久远院线"
        "２章クリア", "第二章通关"
        "３章クリア", "第三章通关"
        "おまけ", "番外"
        "おまけ１", "番外 1"
        "おまけ２", "番外 2"
        "次へ", "下一页"
    ]

let targets =
    [ "text_ending_ay"; "text_ending_c2"; "text_ending_er"; "text_ending_ex1"; "text_ending_ex2"
      "text_ending_ky"; "text_ending_rr"; "text_ending_rt"; "text_ending_st"; "text_ending_tg"; "text_ending_yk" ]

let mutable totalRecs = 0
let missing = Collections.Generic.HashSet<string>()

for name in targets do
    let srcPath = Path.Combine("work/atf", name + ".json")
    let doc = JsonDocument.Parse(File.ReadAllText srcPath)
    let recs = doc.RootElement.GetProperty("records").EnumerateArray() |> Array.ofSeq

    let sb = Text.StringBuilder()
    sb.AppendLine "{" |> ignore
    sb.AppendLine "  \"type\": \"ATF\"," |> ignore
    sb.AppendLine "  \"records\": [" |> ignore
    for i in 0 .. recs.Length - 1 do
        let r = recs.[i]
        let k = r.GetProperty("key").GetString()
        let v =
            match r.TryGetProperty("value") with
            | true, x -> x.GetString()
            | _ -> ""
        let cn =
            match table.TryGetValue v with
            | true, t -> t
            | _ ->
                missing.Add v |> ignore
                v
        sb.AppendLine "    {" |> ignore
        sb.AppendLine (sprintf "      \"key\": %s," (JsonSerializer.Serialize(k, jopts))) |> ignore
        sb.AppendLine (sprintf "      \"value\": %s" (JsonSerializer.Serialize(cn, jopts))) |> ignore
        sb.Append(if i = recs.Length - 1 then "    }" else "    },") .AppendLine() |> ignore
        totalRecs <- totalRecs + 1
    sb.AppendLine "  ]" |> ignore
    sb.AppendLine "}" |> ignore

    File.WriteAllText(Path.Combine("work/atf-cn", name + ".json"), sb.ToString(), Text.UTF8Encoding false)
    printfn "%-20s %d 条 → work/atf-cn/%s.json" name recs.Length name

printfn "\n合计 %d 条记录" totalRecs
if missing.Count > 0 then
    printfn "⚠️ 未在映射表中命中的原文（已原样保留）：%s" (missing |> String.concat " / ")
else
    printfn "✅ 全部原文均命中映射表"
