// 生成 AE/UI 文字图台账：docs/tables/ae-ui-images.md + JSON
#r "nuget: SixLabors.ImageSharp, 3.1.3"
#load "./Pac.fsx"
#load "./hip.fsx"

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open PacLib
open HipLib

let root = @"<游戏安装目录>\data\AE"

let rec descend (pac: PacFile) (names: string[]) =
    let e = pac.Entries |> Array.find (fun e -> e.Filename = names.[0])
    let d = PacLib.extract pac e
    if names.Length = 1 then d
    else
        let tmp = Path.Combine(Path.GetTempPath(), "gakuen-ledger.pac")
        File.WriteAllBytes(tmp, d)
        let r = descend (PacLib.read tmp) names.[1..]
        File.Delete tmp
        r

let readFile (packRel: string) (fileName: string) =
    let parts = packRel.Split('/')
    let outerPac = Path.Combine(root, parts.[0], parts.[1])
    descend (PacLib.read outerPac) (Array.append parts.[2..] [| fileName |])

/// (含文字?, 内容类别, 处理路线, 现役性, 备注)
let classify (pattern: string) =
    let pl = pattern.ToLowerInvariant()
    let has (s: string) = pl.Contains(s.ToLowerInvariant())
    if has "chapter_title_date" then
        true, "日期卡（月日+星期）", "重制图", "待复验⚠️", "2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径"
    elif has "effect_text" then
        false, "文字特效/装饰线（未检出文字）", "跳过", "—", "抽样见到的是横线光效，非文字"
    elif has "back_log_title_moji" then true, "回想标题行", "重制图", "未验证", ""
    elif has "title_face" then false, "标题字形遮罩（非独立文字图）", "跳过", "—", "解出近空白，疑为遮罩/alpha 资源"
    elif has "nowloading_word" then true, "读取中提示（読／読み込み中）", "重制图", "未验证", ""
    elif has "ending_message" || has "ending_start_prompt" then true, "对话框提示语", "重制图", "未验证", ""
    elif has "system-menu_moji" || has "ST_title_moji" || has "ST_modo_moji" || has "ST_mode_moji" then
        true, "系统菜单项（セーブ／サウンド／オプション等）", "重制图", "未验证", "⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制"
    elif has "open_mode_moji" then true, "图鉴/奖励界面标题项", "重制图", "未验证", ""
    elif has "mode_Description" || has "modo_text" || has "message_mode_text" then
        true, "设置项说明文字", "重制图", "未验证", ""
    elif has "open_title_moji" then true, "奖励/成就界面标题（含选项文字）", "重制图", "未验证", ""
    elif has "open_item_description" then
        if has "frame" then false, "空框（非文字）", "跳过", "—", ""
        else true, "图鉴条目名/描述", "重制图", "未验证", ""
    elif has "open_rate" then true, "百分比/数值文字", "重制图", "未验证", "「%」「0」等，可直接用字体重绘"
    elif has "number_difference" then true, "数值数字", "重制图", "未验证", ""
    elif has "achievement_rate_moji" then true, "「達成率」标签", "重制图", "未验证", ""
    elif has "bonus_scenario_text" then true, "奖励剧本标题", "重制图", "未验证", ""
    elif has "profile_complete" then true, "「プロフィール」提示", "重制图", "未验证", ""
    elif has "chara_name" then
        if has "frame" then false, "空框（非文字）", "跳过", "—", ""
        else true, "角色名", "重制图", "现役✅", "「遠山 浩」等 21 人；hip.md §3 已确认为现役"
    elif has "title_start" then true, "标题画面提示（PRESS ANY BUTTONS 等）", "重制图", "未验证", ""
    elif has "NEW_moji" || has "new_moji" then true, "「NEW!」标记", "重制图", "未验证", ""
    elif has "g_dialog" then
        if has "message" then true, "对话框确认/系统提示语（日文整句）", "重制图", "未验证", "「〇〇をプレイします。よろしいですか？」等 20+ 条"
        elif has "botan" then true, "对话框按钮文字（はい／いいえ／さいしょから／つづきから）", "重制图", "未验证", ""
        else false, "对话框部件（空框/装饰）", "跳过", "—", ""
    elif has "title_menu" then
        true, "标题菜单项（はじめから／つづきから／オプション／スペシャル／1-3章／戻る，各 普通+高亮 两态）", "重制图", "现役✅(待复验)", "2026-10-19 图像确认含菜单文字；旧「打补丁无效」结论疑因当时目录结构不同改错路径"
    elif has "title_logo" || has "copyright" || has "_logo" then
        false, "Logo／版权标识（不译）", "跳过", "—", ""
    elif has "key_help" then true, "按键操作说明图（含文字）", "重制图", "未验证", "1024×1024 大图，需放大确认文字内容"
    elif has "quick_save" || has "quick-load" then true, "「QUICK SAVE」等快捷提示", "重制图", "未验证", ""
    elif has "adv_auto" || has "adv_skip" then false, "AUTO/SKIP 按钮外形（无文字）", "跳过", "—", ""
    elif has "option_title_moji" then true, "设置界面标题文字", "重制图", "未验证", ""
    elif has "option_title_effect" then false, "光效装饰", "跳过", "—", ""
    elif has "Message_modo_skip" || has "Message_modo_text" then true, "设置项文字（既読／強制／文字速度）", "重制图", "未验证", ""
    elif has "profile_item" then true, "人物资料项（年齢／身長体重）", "重制图", "未验证", ""
    elif has "nowloading_anime" || has "common_black" || has "common_ray" || has "fade_black"
         || has "BG_noise" || has "Emblem" || has "onpu" || has "book_" || has "shelf" || has "Arrow" then
        false, "装饰/图标/纯色层（无文字）", "跳过", "—", ""
    elif has "icon" || has "frame" || has "scroll" || has "gauge" || has "bar" || has "line"
         || has "shadow" || has "thumb" || has "circle" || has "window" || has "light"
         || has "moon_move" || has "_bg" || has "base" then
        false, "界面部件/底衬（无文字）", "跳过", "—", ""
    else true, "（未归类）", "待判", "?", "⚠️ 需人工看图确认"

let j = System.Text.Json.JsonDocument.Parse(File.ReadAllText "work/inventory/ae-image-inventory-v2.json")
let files =
    j.RootElement.GetProperty("files").EnumerateArray()
    |> Seq.map (fun e ->
        e.GetProperty("pack").GetString(), e.GetProperty("file").GetString(), e.GetProperty("size").GetInt32())
    |> Array.ofSeq

let groups =
    files
    |> Array.filter (fun (_, f, _) -> not (f.EndsWith(".aex")))
    |> Array.groupBy (fun (p, f, _) -> p, Regex.Replace(f, @"\d+", "#"))
    |> Array.sortBy (fun ((p, pat), _) -> p, pat)

let sb = StringBuilder()
let mutable n = 0
let mutable textCount = 0
let mutable skipCount = 0
sb.AppendLine("# AE / UI 文字图台账") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("> 自动生成：`scripts/ae-ledger.fsx`（前置 `scripts/ae-rescan.fsx` 重扫 `data\\AE` 全部 161 个 pac，递归展开嵌套 pac）。") |> ignore
sb.AppendLine("> 原始清单：`work/inventory/ae-image-inventory-v2.json`（1011 个最终资源文件，852 HIP / 159 other）。") |> ignore
sb.AppendLine("> 「含文字」列由文件名模式 + 抽样解码目视判定；⚠️ 表示需实机或逐张复核。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("| # | 包（外层 pac / 内层 pac） | 文件模式 | 张数 | 尺寸 | 含文字 | 内容 | 处理路线 | 备注 |")
sb.AppendLine("|---|---|---|---|---|---|---|---|---|") |> ignore

for ((pack, pattern), grp) in groups do
    n <- n + 1
    let (_, f0, _) = grp.[0]
    let dims =
        try
            let h = HipLib.decode (readFile pack f0)
            sprintf "%d×%d" h.ImageWidth h.ImageHeight
        with _ -> "?"
    let hasText, kind, route, cur, note = classify pattern
    if hasText then textCount <- textCount + grp.Length else skipCount <- skipCount + grp.Length
    sb.AppendLine(sprintf "| %d | `%s` | `%s` | %d | %s | %s | %s | %s | %s | %s |"
        n pack pattern grp.Length dims (if hasText then "✅" else "—") kind route cur note) |> ignore

sb.AppendLine() |> ignore
sb.AppendLine(sprintf "**合计**：%d 个模式，%d 个文件；判为含文字 %d 个、非文字 %d 个。"
    groups.Length files.Length textCount skipCount) |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("## 处理路线说明") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("- **重制图**：`hip-tool.fsx decode` 解出 PNG → 按原版式重绘中文 → `encode` 回 HIP → `pac-pack` 回写嵌套 pac。") |> ignore
sb.AppendLine("- **跳过**：解出无文字（光效/空框/遮罩），不需要翻译。") |> ignore
sb.AppendLine("- **待判**：需逐张看图确认。") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("## 已知风险") |> ignore
sb.AppendLine() |> ignore
sb.AppendLine("1. 文档已记录「更新后大量文字 UI 改实时渲染」——`chapter_title_date_*`、`title_menu_*` 打补丁后") |> ignore
sb.AppendLine("   游戏无变化（实锤遗留）。**台账里的『现役/遗留』必须靠实机验证，不能只看文件存在**。") |> ignore
sb.AppendLine("2. 菜单类（`system-menu_moji`/`ST_title_moji`）文字本体走 Font00 实时渲染，图可能只是底衬。") |> ignore
sb.AppendLine("3. 角色名（`chara_name_*`）已确认现役（见 hip.md §3）。") |> ignore

Directory.CreateDirectory "docs/tables" |> ignore
File.WriteAllText("docs/tables/ae-ui-images.md", sb.ToString(), Encoding.UTF8)
printfn "→ docs/tables/ae-ui-images.md（%d 模式 / %d 文件 / 含文字 %d / 非文字 %d）"
    groups.Length files.Length textCount skipCount
