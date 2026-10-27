// 用 Steam 官方成就清单(docs/data/steam-achievements.ja.txt)对账 exe 提取结果：
// 分离出「Steam 成就文本」与「其他游戏内 UI 文本」，后者才是真正值得汉化的。
open System
open System.IO
open System.Text
open System.Text.Json
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

// ---- 1. 解析 Steam 成就清单 ----
let lines =
    File.ReadAllLines("docs/data/steam-achievements.ja.txt", Encoding.UTF8)
    |> Array.map (fun s -> s.Trim())
let blocks =
    lines
    |> Array.fold (fun (acc: ResizeArray<string[]>, cur: ResizeArray<string>) line ->
        if line = "" then
            if cur.Count > 0 then acc.Add(cur.ToArray()); acc, ResizeArray()
            else acc, cur
        else
            cur.Add line
            acc, cur) (ResizeArray(), ResizeArray())
    |> fun (acc, cur) -> (if cur.Count > 0 then acc.Add(cur.ToArray()) else ()); acc

let achievements =
    blocks
    |> Seq.choose (fun b ->
        if b.Length >= 4 && b.[3].StartsWith "ACHIEVEMENT_" then
            let name = b.[0]
            let desc = b.[1].Replace("Hidden achievement: ", "").Replace("Hidden achievement:", "")
            Some(name, desc, b.[3])
        else None)
    |> Array.ofSeq

printfn "Steam 成就 %d 个（= %d 条文本：名称+描述）" achievements.Length (achievements.Length * 2)

// ---- 2. 读 exe 提取结果 ----
let j = JsonDocument.Parse(File.ReadAllText "work/inventory/exe-ui-strings.json")
let extracted =
    j.RootElement.GetProperty("strings").EnumerateArray()
    |> Seq.map (fun e -> e.GetProperty("offset").GetString(), e.GetProperty("text").GetString())
    |> Array.ofSeq
printfn "exe 提取 %d 条\n" extracted.Length

let achievementTexts =
    achievements |> Seq.collect (fun (n, d, _) -> [ n; d ]) |> Set.ofSeq
let extractedSet = extracted |> Seq.map snd |> Set.ofSeq

// ---- 3. 对账 ----
let hit = extracted |> Array.filter (fun (_, t) -> achievementTexts.Contains t)
let other = extracted |> Array.filter (fun (_, t) -> not (achievementTexts.Contains t))
let missing = achievementTexts |> Set.filter (fun t -> not (extractedSet.Contains t))

printfn "| 分组 | 条数 |"
printfn "|---|---|"
printfn "| Steam 成就文本（名称+描述） | %d |" achievementTexts.Count
printfn "| ├ exe 中已提取到的 | %d |" hit.Length
printfn "| └ 未被提取到的（提取器遗漏） | %d |" missing.Count
printfn "| **非成就的其他 exe 文本** | **%d** |" other.Length

printfn "\n===== 其他 exe 文本（非成就，值得关注）%d 条 =====" other.Length
for (off, t) in other do
    printfn "  %s  %s" off t

printfn "\n===== Steam 成就中未被提取到的 %d 条（提取器遗漏，需人工补）=====" missing.Count
for t in missing |> Seq.sort do
    printfn "  %s" t
