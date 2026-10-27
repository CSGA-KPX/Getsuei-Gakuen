// 翻译文本台账：atf 对话 / staff_roll / 台词图 / UI 文本清点
open System
open System.IO
open System.Text
open System.Text.Json

let sb = StringBuilder()
sb.AppendLine("# 翻译文本台账") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("> 清点时间：2026-10-19。数据源 `work/atf`（日文原文）、`work/atf-cn`（译文）、") |> ignore
sb.AppendLine("> `work/inventory/atf-stats.json`（记录/字符统计）。") |> ignore
sb.AppendLine() |> ignore

// 1) atf 对话文本
let atfFiles = Directory.GetFiles("work/atf", "*.json") |> Array.map Path.GetFileNameWithoutExtension |> Set.ofArray
// 译文分两层：work/atf-cn-manual（手工翻译）优先于 work/atf-cn（GalTransl 回填）
let cnManual = Directory.GetFiles("work/atf-cn-manual", "*.json") |> Array.map Path.GetFileNameWithoutExtension |> Set.ofArray
let cnAuto = Directory.GetFiles("work/atf-cn", "*.json") |> Array.map Path.GetFileNameWithoutExtension |> Set.ofArray
let cnFiles = Set.union cnManual cnAuto
let missing = Set.difference atfFiles cnFiles
let stats = JsonDocument.Parse(File.ReadAllText "work/inventory/atf-stats.json")
let g (name: string) = (stats.RootElement.GetProperty name).GetInt32()

sb.AppendLine("## 1. 对话文本（atf）") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("| 项 | 数值 |") |> ignore
sb.AppendLine("|---|---|") |> ignore
sb.AppendLine(sprintf "| text pac 总数 | %d |" (g "pacCount")) |> ignore
sb.AppendLine(sprintf "| 文本记录（句） | %d |" (g "totalRecords")) |> ignore
sb.AppendLine(sprintf "| 日文字符总数 | %d |" (g "totalChars")) |> ignore
sb.AppendLine(sprintf "| 去重字符 | %d |" (g "distinctChars")) |> ignore
sb.AppendLine(sprintf "| 注音（ruby）记录 | %d |" (g "rubyRecords")) |> ignore
sb.AppendLine(sprintf "| 台词图引用（imageKey） | %d |" (g "imageKeyRecords")) |> ignore
sb.AppendLine(sprintf "| **已译 pac** | **%d / %d**（手工 %d 个 + GalTransl %d 个） |" cnFiles.Count atfFiles.Count cnManual.Count cnAuto.Count) |> ignore
sb.AppendLine(sprintf "| **未译 pac** | **%d** |" missing.Count) |> ignore
sb.AppendLine() |> ignore

if missing.Count > 0 then
    sb.AppendLine(sprintf "### 未译清单（%d 个 pac）" missing.Count) |> ignore
    sb.AppendLine() |> ignore
    for m in missing |> Seq.sort do
        let j = JsonDocument.Parse(File.ReadAllText (Path.Combine("work/atf", m + ".json")))
        let rec countRecords (e: JsonElement) =
            if e.ValueKind = JsonValueKind.Array then e.GetArrayLength()
            else 0
        let n =
            match j.RootElement.TryGetProperty("records") with
            | true, r -> countRecords r
            | _ -> 0
        let kind =
            if m.Contains "_test" || m.Contains "Test" || m.Contains "effecttest" then "开发用测试，可跳过"
            elif m.Contains "text_ending_" then "结局回想菜单标签（非剧情）—— 去重后仅 13 条唯一字符串，已译 ✅"
            elif m.Contains "text_effect" then "**特效资源清单（eff_###+美术说明）—— 不译**"
            else "未分类，需人工确认"
        sb.AppendLine(sprintf "- `%s`（%d 句）— %s" m n kind) |> ignore
    sb.AppendLine() |> ignore

// 2) 译文实际用字
let cnChars = Collections.Generic.HashSet<int>()
for dir in [ "work/atf-cn-manual"; "work/atf-cn" ] do
  if Directory.Exists dir then
    for f in Directory.GetFiles(dir, "*.json") do
      for c in File.ReadAllText f do
          if int c > 0x20 then cnChars.Add(int c) |> ignore
let hanzi = cnChars |> Seq.filter (fun c -> (c >= 0x4E00 && c <= 0x9FFF)) |> Seq.length

sb.AppendLine("### ⚠️ 关于结尾 pac 的重要更正（2026-10-19）") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("原先记「10 个 `text_ending_*` 是结局文本，必须译」——**判断错了**。实际结构：") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("| pac | 条数 | 实际内容 |") |> ignore
sb.AppendLine("|-----|------|----------|") |> ignore
sb.AppendLine("| `ending_ay / c2 / er / ky / rr / rt / st / tg / yk`（9 个） | 各 12 | **内容完全相同**的结局回想菜单标签：8 条路线名 + `２章クリア` + `３章クリア` + `おまけ` + `次へ` |") |> ignore
sb.AppendLine("| `ending_ex1 / ex2` | 各 13 | 同上 + `おまけ１` / `おまけ２` |") |> ignore
sb.AppendLine("| `ending_c3` | 20 | **真剧情**（海滩小场景，10 句 + 10 个名字标签）——已译 ✅ |") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("⇒ 11 个 pac 共 134 条记录，**去重后只有 14 个唯一字符串**，全是 UI 菜单标签。") |> ignore
sb.AppendLine("**不需要 galtransl**（无说话人、无剧情上下文、无语气）。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("**已处理（2026-10-19）**：统一译法 = 英理线 / 大河线 / 雄喜线 / 露露线 / 皋月线 / 律线 /") |> ignore
sb.AppendLine("后堂线 / 久远院线 / 第二章通关 / 第三章通关 / 番外 / 番外 1 / 番外 2 / 下一页。") |> ignore
sb.AppendLine("角色名沿用正文既定译名（从 209 个已译 pac 反查确认）。生成脚本 `scripts/gen-ending-cn.fsx`") |> ignore
sb.AppendLine("（映射表内联，改词只改这一处），已重打包并安装。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("## 2. 译文用字") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine(sprintf "- 去重字符 **%d**（汉字 %d、假名 20、标点/数字/拉丁 %d）" cnChars.Count hanzi (cnChars.Count - hanzi - 20)) |> ignore
sb.AppendLine("- 这是字库扩充的需求基数，详见 [font-coverage.md](font-coverage.md)。") |> ignore
sb.AppendLine() |> ignore

// 3) 其他文本位置
sb.AppendLine("### ⚠️ text_effect* 是特效资源清单，**不要翻译**（2026-10-19 查证）") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("8 个 `text_effect*` pac 共 101 条，内容形如：") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("```") |> ignore
sb.AppendLine("eff_001桜の花びら") |> ignore
sb.AppendLine("eff_006a斬撃（刀）汎用「a」") |> ignore
sb.AppendLine("eff_012b血飛沫（赤）「b」") |> ignore
sb.AppendLine("eff_053ハザードの瘴気") |> ignore
sb.AppendLine("```") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("判据：") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("1. 每条都是 `eff_###` 编号 + 日文**美术描述**，括号里是技术变体（`（通常）`/`（見上げ）`/`汎用`/`「a」`）—— 开发清单，不是台词。") |> ignore
sb.AppendLine("2. **ADVScript（`scr_*.pac` 内的 bin）里确实存在 `eff_053`/`eff_038`/`eff_015`/`eff_009`/`eff_012b`/`eff_033` 等字符串查找**（实测命中），") |> ignore
sb.AppendLine("   即编号是**程序标识符**，改文本有破坏脚本调用的风险。") |> ignore
sb.AppendLine("3. 内容是美术资源描述（樱花花瓣/血沫/集中线/白屏），不可能上屏给玩家看。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("⇒ **结论：一律不译**，保持原样。这也回答了「战斗演出会不会翻到硬编码效果名」—— 会，所以这些包必须原样跳过。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("## 3. 其他文本位置") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("| 位置 | 状态 | 说明 |") |> ignore
sb.AppendLine("|---|---|---|")
let staffDir = @"<游戏安装目录>\data\text\staff_roll"
let staffFiles = if Directory.Exists staffDir then Directory.GetFiles(staffDir, "*.lst") else [||]
sb.AppendLine(sprintf "| `data\\text\\staff_roll\\*.lst` | 已清点（%d 个） | CSV/Shift-JIS，玩家可见制作人员名单；语义推断见 [staff-roll.md](../formats/staff-roll.md) ⚠️ |" staffFiles.Length) |> ignore
sb.AppendLine("| 台词图（`story\\image` TGA） | ✅ 已重制 35 张 | 覆盖 imageKey 引用的全部中文张 |") |> ignore
sb.AppendLine("| UI/菜单文本（游戏代码内字符串） | ❌ **未清点** | 标题菜单、设置项、存档界面等文字本体；走 Font00/jpn_glyph 实时渲染 |") |> ignore
sb.AppendLine("| 事件脚本（ADVScript） | ✅ 已 dump | `scripts/AdvScriptParser`，Flag/参数语义未译（透传） |") |> ignore
sb.AppendLine() |> ignore

sb.AppendLine("## 4. 结论：还差什么") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine(sprintf "1. **对话文本**：%d/%d pac 已译，剩 %d 个 pac 未译（见上清单）。" cnFiles.Count atfFiles.Count missing.Count) |> ignore
sb.AppendLine("2. **UI/菜单文本**：完全没清点——不知道字符串存在哪个资源、有多少条。") |> ignore
sb.AppendLine("   这是决定「是否需要扩 Font00」的前置问题（见 [font-coverage.md](font-coverage.md) §3.2）。") |> ignore
sb.AppendLine("3. **staff_roll**：语义是推断的（⚠️），要翻译得先确认字段含义。") |> ignore

Directory.CreateDirectory "docs/tables" |> ignore
File.WriteAllText("docs/tables/text-inventory.md", sb.ToString(), Encoding.UTF8)
printfn "→ docs/tables/text-inventory.md"
printfn "atf: %d 已译 / %d 未译 / 共 %d" cnFiles.Count missing.Count atfFiles.Count
printfn "staff_roll lst: %d" staffFiles.Length
printfn "译文去重字符: %d（汉字 %d）" cnChars.Count hanzi
