# AE / UI 文字图台账

> 自动生成：`scripts/ae-ledger.fsx`（前置 `scripts/ae-rescan.fsx` 重扫 `data\AE` 全部 161 个 pac，递归展开嵌套 pac）。
> 原始清单：`work/inventory/ae-image-inventory-v2.json`（1011 个最终资源文件，852 HIP / 159 other）。
> 「含文字」列由文件名模式 + 抽样解码目视判定；⚠️ 表示需实机或逐张复核。
>
> ⚠️ **2026-10-20 起、「现役/遗留」列请以 [`ui-images-triage.md`](ui-images-triage.md) 为准** ——
> 那里用 exe 资源名字表 + procmon 对所有 852 张 HIP 做了 A/B/C/D 定级，
> 本表的「现役✅/遗留」栏是早期按文件名推断的，未逐个核实。

| # | 包（外层 pac / 内层 pac） | 文件模式 | 张数 | 尺寸 | 含文字 | 内容 | 处理路线 | 备注 |
|---|---|---|---|---|---|---|---|---|
| 1 | `adv/UI_00_key_help.pac/tex.pac` | `key_help.hip` | 1 | 1024×1024 | ✅ | 按键操作说明图（含文字） | 重制图 | 未验证 | 1024×1024 大图，需放大确认文字内容 |
| 2 | `adv/UI_07_adv_skipauto.pac/tex.pac` | `adv_auto_#.hip` | 3 | 128×128 | — | AUTO/SKIP 按钮外形（无文字） | 跳过 | — |  |
| 3 | `adv/UI_07_adv_skipauto.pac/tex.pac` | `adv_skip_a_#.hip` | 3 | 128×128 | — | AUTO/SKIP 按钮外形（无文字） | 跳过 | — |  |
| 4 | `adv/UI_07_adv_skipauto.pac/tex.pac` | `adv_skip_b_#.hip` | 3 | 128×128 | — | AUTO/SKIP 按钮外形（无文字） | 跳过 | — |  |
| 5 | `adv/UI_10_quick_save.pac/tex.pac` | `quick_save.hip` | 1 | 128×128 | ✅ | 「QUICK SAVE」等快捷提示 | 重制图 | 未验证 |  |
| 6 | `adv/chapter_title_1_10.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 7 | `adv/chapter_title_1_10.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 8 | `adv/chapter_title_1_10.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 9 | `adv/chapter_title_1_11.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 10 | `adv/chapter_title_1_11.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 11 | `adv/chapter_title_1_11.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 12 | `adv/chapter_title_1_12.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 13 | `adv/chapter_title_1_12.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 14 | `adv/chapter_title_1_12.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 15 | `adv/chapter_title_1_13.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 16 | `adv/chapter_title_1_13.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 17 | `adv/chapter_title_1_13.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 18 | `adv/chapter_title_1_16.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 19 | `adv/chapter_title_1_16.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 20 | `adv/chapter_title_1_16.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 21 | `adv/chapter_title_1_17.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 22 | `adv/chapter_title_1_17.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 23 | `adv/chapter_title_1_17.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 24 | `adv/chapter_title_1_30.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 25 | `adv/chapter_title_1_30.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 26 | `adv/chapter_title_1_30.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 27 | `adv/chapter_title_1_31.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 28 | `adv/chapter_title_1_31.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 29 | `adv/chapter_title_1_31.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 30 | `adv/chapter_title_1_9.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 31 | `adv/chapter_title_1_9.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 32 | `adv/chapter_title_1_9.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 33 | `adv/chapter_title_2_1.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 34 | `adv/chapter_title_2_1.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 35 | `adv/chapter_title_2_1.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 36 | `adv/chapter_title_2_10.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 37 | `adv/chapter_title_2_10.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 38 | `adv/chapter_title_2_10.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 39 | `adv/chapter_title_2_11.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 40 | `adv/chapter_title_2_11.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 41 | `adv/chapter_title_2_11.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 42 | `adv/chapter_title_2_12.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 43 | `adv/chapter_title_2_12.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 44 | `adv/chapter_title_2_12.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 45 | `adv/chapter_title_2_13.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 46 | `adv/chapter_title_2_13.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 47 | `adv/chapter_title_2_13.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 48 | `adv/chapter_title_2_14.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 49 | `adv/chapter_title_2_14.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 50 | `adv/chapter_title_2_14.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 51 | `adv/chapter_title_2_15.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 52 | `adv/chapter_title_2_15.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 53 | `adv/chapter_title_2_15.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 54 | `adv/chapter_title_2_16.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 55 | `adv/chapter_title_2_16.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 56 | `adv/chapter_title_2_16.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 57 | `adv/chapter_title_2_17.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 58 | `adv/chapter_title_2_17.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 59 | `adv/chapter_title_2_17.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 60 | `adv/chapter_title_2_18.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 61 | `adv/chapter_title_2_18.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 62 | `adv/chapter_title_2_18.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 63 | `adv/chapter_title_2_3.pac/tex.pac` | `chapter_title_date_#-#_A.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 64 | `adv/chapter_title_2_3.pac/tex.pac` | `chapter_title_date_#-#_B.hip` | 1 | 1024×256 | ✅ | 日期卡（月日+星期） | 重制图 | 待复验⚠️ | 2026-10-18 实机打补丁无变化，但与 title_menu 同批结论，同样可能是改错路径 |
| 65 | `adv/chapter_title_2_3.pac/tex.pac` | `effect_text_#_#.hip` | 9 | 1024×256 | — | 文字特效/装饰线（未检出文字） | 跳过 | — | 抽样见到的是横线光效，非文字 |
| 66 | `adv/choices.pac/tex.pac` | `choices_icon#.hip` | 2 | 64×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 67 | `adv/feed_mode_text.pac/tex.pac` | `moon_move_#.hip` | 13 | 128×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 68 | `adv/feed_mode_text.pac/tex.pac` | `outer_circle_#.hip` | 16 | 128×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 69 | `adv/feed_mode_text.pac/tex.pac` | `outer_circle_shitaji_#.hip` | 1 | 128×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 70 | `backlog/back_log.pac/tex.pac` | `Icon_#.hip` | 4 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 71 | `backlog/back_log.pac/tex.pac` | `ST_Light_line_#.hip` | 1 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 72 | `backlog/back_log.pac/tex.pac` | `ST_Operating_Instructions_base.hip` | 1 | 1024×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 73 | `backlog/back_log.pac/tex.pac` | `ST_title_line#.hip` | 2 | 1024×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 74 | `backlog/back_log.pac/tex.pac` | `Title-base.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 75 | `backlog/back_log.pac/tex.pac` | `back_log_frame_light_#.hip` | 4 | 1024×256 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 76 | `backlog/back_log.pac/tex.pac` | `back_log_shadow.hip` | 1 | 1024×256 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 77 | `backlog/back_log.pac/tex.pac` | `back_log_title_moji#.hip` | 5 | 64×32 | ✅ | 回想标题行 | 重制图 | 未验证 |  |
| 78 | `backlog/back_log.pac/tex.pac` | `back_log_title_moji_light_#.hip` | 7 | 256×32 | ✅ | 回想标题行 | 重制图 | 未验证 |  |
| 79 | `backlog/back_log.pac/tex.pac` | `feed_icon_page.hip` | 1 | 32×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 80 | `backlog/back_log.pac/tex.pac` | `icon_#.hip` | 1 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 81 | `backlog/back_log.pac/tex.pac` | `log_window_selection.hip` | 1 | 1024×256 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 82 | `backlog/back_log.pac/tex.pac` | `log_window_unselected.hip` | 1 | 1024×256 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 83 | `backlog/back_log.pac/tex.pac` | `scene_back_icon.hip` | 1 | 64×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 84 | `backlog/back_log.pac/tex.pac` | `scroll_bar_A#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 85 | `backlog/back_log.pac/tex.pac` | `scroll_bar_B#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 86 | `backlog/back_log.pac/tex.pac` | `scroll_bar_button_A#.hip` | 3 | 64×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 87 | `backlog/back_log.pac/tex.pac` | `scroll_bar_button_B#.hip` | 3 | 64×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 88 | `backlog/back_log.pac/tex.pac` | `title_face_#.hip` | 41 | 256×256 | — | 标题字形遮罩（非独立文字图） | 跳过 | — | 解出近空白，疑为遮罩/alpha 资源 |
| 89 | `backlog/back_log.pac/tex.pac` | `voice_play_icon.hip` | 1 | 64×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 90 | `backlog/back_log.pac/tex.pac` | `voice_play_icon_#.hip` | 1 | 64×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 91 | `cg_thumb/cg_01011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 92 | `cg_thumb/cg_01021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 93 | `cg_thumb/cg_01031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 94 | `cg_thumb/cg_11011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 95 | `cg_thumb/cg_11021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 96 | `cg_thumb/cg_11031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 97 | `cg_thumb/cg_11041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 98 | `cg_thumb/cg_11051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 99 | `cg_thumb/cg_11061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 100 | `cg_thumb/cg_11081_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 101 | `cg_thumb/cg_11091_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 102 | `cg_thumb/cg_12011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 103 | `cg_thumb/cg_12021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 104 | `cg_thumb/cg_12031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 105 | `cg_thumb/cg_12041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 106 | `cg_thumb/cg_12051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 107 | `cg_thumb/cg_12061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 108 | `cg_thumb/cg_13011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 109 | `cg_thumb/cg_13021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 110 | `cg_thumb/cg_13031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 111 | `cg_thumb/cg_13041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 112 | `cg_thumb/cg_13051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 113 | `cg_thumb/cg_13061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 114 | `cg_thumb/cg_13071_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 115 | `cg_thumb/cg_13081_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 116 | `cg_thumb/cg_13091_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 117 | `cg_thumb/cg_14011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 118 | `cg_thumb/cg_14021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 119 | `cg_thumb/cg_14031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 120 | `cg_thumb/cg_14041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 121 | `cg_thumb/cg_14051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 122 | `cg_thumb/cg_14061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 123 | `cg_thumb/cg_14071_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 124 | `cg_thumb/cg_14081_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 125 | `cg_thumb/cg_15011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 126 | `cg_thumb/cg_15021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 127 | `cg_thumb/cg_15031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 128 | `cg_thumb/cg_15041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 129 | `cg_thumb/cg_16011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 130 | `cg_thumb/cg_16021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 131 | `cg_thumb/cg_17021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 132 | `cg_thumb/cg_17031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 133 | `cg_thumb/cg_21011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 134 | `cg_thumb/cg_21021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 135 | `cg_thumb/cg_21031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 136 | `cg_thumb/cg_21041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 137 | `cg_thumb/cg_21051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 138 | `cg_thumb/cg_31011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 139 | `cg_thumb/cg_31021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 140 | `cg_thumb/cg_31031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 141 | `cg_thumb/cg_31041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 142 | `cg_thumb/cg_31051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 143 | `cg_thumb/cg_31061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 144 | `cg_thumb/cg_31071_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 145 | `cg_thumb/cg_31081_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 146 | `cg_thumb/cg_31091_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 147 | `cg_thumb/cg_31101_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 148 | `cg_thumb/cg_31111_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 149 | `cg_thumb/cg_41011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 150 | `cg_thumb/cg_41021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 151 | `cg_thumb/cg_41031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 152 | `cg_thumb/cg_41051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 153 | `cg_thumb/cg_41061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 154 | `cg_thumb/cg_41071_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 155 | `cg_thumb/cg_41081_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 156 | `cg_thumb/cg_41082_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 157 | `cg_thumb/cg_41091_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 158 | `cg_thumb/cg_41101_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 159 | `cg_thumb/cg_51021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 160 | `cg_thumb/cg_51031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 161 | `cg_thumb/cg_51041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 162 | `cg_thumb/cg_61021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 163 | `cg_thumb/cg_61031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 164 | `cg_thumb/cg_61041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 165 | `cg_thumb/cg_61051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 166 | `cg_thumb/cg_61061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 167 | `cg_thumb/cg_61071_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 168 | `cg_thumb/cg_71011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 169 | `cg_thumb/cg_71021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 170 | `cg_thumb/cg_71031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 171 | `cg_thumb/cg_71041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 172 | `cg_thumb/cg_71051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 173 | `cg_thumb/cg_71061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 174 | `cg_thumb/cg_81011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 175 | `cg_thumb/cg_81021_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 176 | `cg_thumb/cg_81031_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 177 | `cg_thumb/cg_81041_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 178 | `cg_thumb/cg_81051_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 179 | `cg_thumb/cg_81061_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 180 | `cg_thumb/cg_91011_thumb.pac` | `cg_#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 181 | `cg_thumb/cg_ex011_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 182 | `cg_thumb/cg_ex021_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 183 | `cg_thumb/cg_ex031_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 184 | `cg_thumb/cg_ex061a_thumb.pac` | `cg_ex#a_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 185 | `cg_thumb/cg_ex071_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 186 | `cg_thumb/cg_ex081_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 187 | `cg_thumb/cg_ex091_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 188 | `cg_thumb/cg_ex101_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 189 | `cg_thumb/cg_ex111_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 190 | `cg_thumb/cg_ex121_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 191 | `cg_thumb/cg_ex131_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 192 | `cg_thumb/cg_ex141_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 193 | `cg_thumb/cg_ex151_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 194 | `cg_thumb/cg_ex161_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 195 | `cg_thumb/cg_ex171_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 196 | `cg_thumb/cg_ex181_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 197 | `cg_thumb/cg_ex191_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 198 | `cg_thumb/cg_ex201_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 199 | `cg_thumb/cg_ex211_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 200 | `cg_thumb/cg_ex221_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 201 | `cg_thumb/cg_ex231_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 202 | `cg_thumb/cg_ex241_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 203 | `cg_thumb/cg_ex251_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 204 | `cg_thumb/cg_ex261_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 205 | `cg_thumb/cg_ex271_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 206 | `cg_thumb/cg_ex281_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 207 | `cg_thumb/cg_ex291_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 208 | `cg_thumb/cg_ex301_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 209 | `cg_thumb/cg_ex311_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 210 | `cg_thumb/cg_ex321_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 211 | `cg_thumb/cg_ex331_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 212 | `cg_thumb/cg_ex341_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 213 | `cg_thumb/cg_ex351_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 214 | `cg_thumb/cg_ex400_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 215 | `cg_thumb/cg_ex401_thumb.pac` | `cg_ex#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 216 | `cg_thumb/cg_exsce1_thumb.pac` | `cg_exsce#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 217 | `cg_thumb/cg_exsce2_thumb.pac` | `cg_exsce#_thumb.hip` | 1 | ? | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 218 | `common/UI_29_nowloading.pac/tex.pac` | `common_black.hip` | 1 | 256×256 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 219 | `common/UI_29_nowloading.pac/tex.pac` | `nowloading_anime.hip` | 1 | 512×512 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 220 | `common/UI_29_nowloading.pac/tex.pac` | `nowloading_word_#.hip` | 6 | 64×64 | ✅ | 读取中提示（読／読み込み中） | 重制图 | 未验证 |  |
| 221 | `common/UI_99_fade.pac/tex.pac` | `fade_black.hip` | 1 | 256×256 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 222 | `common/base_bg.pac/tex.pac` | `BG_noise_#.hip` | 1 | 1024×1024 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 223 | `common/base_bg.pac/tex.pac` | `BG_noise_alpha.hip` | 1 | 128×64 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 224 | `common/base_bg.pac/tex.pac` | `ST_BG_Emblem.hip` | 1 | 1024×1024 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 225 | `common/base_bg.pac/tex.pac` | `ST_BG_base.hip` | 1 | 16×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 226 | `common/g_dialog.pac/tex.pac` | `ST_Light_line_#.hip` | 1 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 227 | `common/g_dialog.pac/tex.pac` | `g_dialog_#_ending_message.hip` | 10 | 512×64 | ✅ | 对话框提示语 | 重制图 | 未验证 |  |
| 228 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_No_off.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 229 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_No_on.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 230 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_saisho_off.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 231 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_saisho_on.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 232 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_tuzuki_off.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 233 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan#_tuzuki_on.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 234 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan_No_off.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 235 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan_No_on.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 236 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan_Yes_off.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 237 | `common/g_dialog.pac/tex.pac` | `g_dialog_botan_Yes_on.hip` | 1 | 512×128 | ✅ | 对话框按钮文字（はい／いいえ／さいしょから／つづきから） | 重制图 | 未验证 |  |
| 238 | `common/g_dialog.pac/tex.pac` | `g_dialog_go-scenario_message.hip` | 1 | 1024×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 239 | `common/g_dialog.pac/tex.pac` | `g_dialog_hiroshi-scenario_message.hip` | 1 | 1024×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 240 | `common/g_dialog.pac/tex.pac` | `g_dialog_kito_scenario_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 241 | `common/g_dialog.pac/tex.pac` | `g_dialog_kou-scenario_message.hip` | 1 | 1024×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 242 | `common/g_dialog.pac/tex.pac` | `g_dialog_kyuse_scenario_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 243 | `common/g_dialog.pac/tex.pac` | `g_dialog_mafia_scenario_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 244 | `common/g_dialog.pac/tex.pac` | `g_dialog_optino_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 245 | `common/g_dialog.pac/tex.pac` | `g_dialog_quick-load_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 246 | `common/g_dialog.pac/tex.pac` | `g_dialog_return_scene_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 247 | `common/g_dialog.pac/tex.pac` | `g_dialog_senketsu_scenario_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 248 | `common/g_dialog.pac/tex.pac` | `g_dialog_st_endinginfo_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 249 | `common/g_dialog.pac/tex.pac` | `g_dialog_st_road_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 250 | `common/g_dialog.pac/tex.pac` | `g_dialog_st_save_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 251 | `common/g_dialog.pac/tex.pac` | `g_dialog_st_savedatacheck_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 252 | `common/g_dialog.pac/tex.pac` | `g_dialog_st_slotcheck_message.hip` | 1 | 512×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 253 | `common/g_dialog.pac/tex.pac` | `g_dialog_start_prologue_message.hip` | 1 | 1024×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 254 | `common/g_dialog.pac/tex.pac` | `g_dialog_titlre_return_message.hip` | 1 | 1024×64 | ✅ | 对话框确认/系统提示语（日文整句） | 重制图 | 未验证 | 「〇〇をプレイします。よろしいですか？」等 20+ 条 |
| 255 | `common/g_dialog.pac/tex.pac` | `g_dialog_warning_light_#.hip` | 5 | 1024×256 | — | 对话框部件（空框/装饰） | 跳过 | — |  |
| 256 | `common/g_dialog.pac/tex.pac` | `g_dialog_window#.hip` | 3 | 1024×128 | — | 对话框部件（空框/装饰） | 跳过 | — |  |
| 257 | `common/g_dialog.pac/tex.pac` | `g_dialog_window_light_#.hip` | 7 | 1024×256 | — | 对话框部件（空框/装饰） | 跳过 | — |  |
| 258 | `option/options_screen.pac/tex.pac` | `BGM_modo_text#_off.hip` | 3 | 256×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 259 | `option/options_screen.pac/tex.pac` | `BGM_modo_text#_on.hip` | 3 | 256×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 260 | `option/options_screen.pac/tex.pac` | `Icon_#.hip` | 6 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 261 | `option/options_screen.pac/tex.pac` | `Message_modo_skip#_off.hip` | 2 | 128×64 | ✅ | 设置项文字（既読／強制／文字速度） | 重制图 | 未验证 |  |
| 262 | `option/options_screen.pac/tex.pac` | `Message_modo_skip#_on.hip` | 2 | 128×64 | ✅ | 设置项文字（既読／強制／文字速度） | 重制图 | 未验证 |  |
| 263 | `option/options_screen.pac/tex.pac` | `Message_modo_text#_off.hip` | 3 | 256×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 264 | `option/options_screen.pac/tex.pac` | `Message_modo_text#_on.hip` | 3 | 256×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 265 | `option/options_screen.pac/tex.pac` | `ST_Light_line_#.hip` | 7 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 266 | `option/options_screen.pac/tex.pac` | `ST_Operating_Instructions_base.hip` | 1 | 1024×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 267 | `option/options_screen.pac/tex.pac` | `ST_Selection_mode_Description_base.hip` | 1 | 1024×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 268 | `option/options_screen.pac/tex.pac` | `ST_mode_Description_#.hip` | 1 | 512×32 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 269 | `option/options_screen.pac/tex.pac` | `ST_modo_frame_off.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 270 | `option/options_screen.pac/tex.pac` | `ST_modo_frame_off_shadow.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 271 | `option/options_screen.pac/tex.pac` | `ST_modo_frame_on.hip` | 1 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 272 | `option/options_screen.pac/tex.pac` | `ST_modo_moji_#.hip` | 6 | 128×64 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 273 | `option/options_screen.pac/tex.pac` | `ST_title_base_Red.hip` | 1 | 512×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 274 | `option/options_screen.pac/tex.pac` | `ST_title_line#.hip` | 1 | 512×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 275 | `option/options_screen.pac/tex.pac` | `ST_title_moji.hip` | 1 | 256×32 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 276 | `option/options_screen.pac/tex.pac` | `Title-base.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 277 | `option/options_screen.pac/tex.pac` | `g_dialog_window_light_#.hip` | 1 | 1024×256 | — | 对话框部件（空框/装饰） | 跳过 | — |  |
| 278 | `option/options_screen.pac/tex.pac` | `gauge#_off.hip` | 10 | 32×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 279 | `option/options_screen.pac/tex.pac` | `gauge#_on.hip` | 10 | 32×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 280 | `option/options_screen.pac/tex.pac` | `gauge_arrow_off#.hip` | 2 | 64×128 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 281 | `option/options_screen.pac/tex.pac` | `gauge_arrow_on.hip` | 1 | 64×128 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 282 | `option/options_screen.pac/tex.pac` | `gauge_frame_off.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 283 | `option/options_screen.pac/tex.pac` | `gauge_frame_on.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 284 | `option/options_screen.pac/tex.pac` | `gauge_off.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 285 | `option/options_screen.pac/tex.pac` | `gauge_on.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 286 | `option/options_screen.pac/tex.pac` | `icon_#.hip` | 1 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 287 | `option/options_screen.pac/tex.pac` | `mute_icon_off_display.hip` | 1 | 128×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 288 | `option/options_screen.pac/tex.pac` | `mute_icon_off_hide.hip` | 1 | 128×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 289 | `option/options_screen.pac/tex.pac` | `mute_icon_on_display.hip` | 1 | 128×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 290 | `option/options_screen.pac/tex.pac` | `mute_icon_on_hide.hip` | 1 | 128×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 291 | `option/options_screen.pac/tex.pac` | `option_Selection_mode_Description_base.hip` | 1 | 1024×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 292 | `option/options_screen.pac/tex.pac` | `option_mode_Description_#.hip` | 7 | 512×32 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 293 | `option/options_screen.pac/tex.pac` | `option_mode_Description_#_off.hip` | 3 | 512×32 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 294 | `option/options_screen.pac/tex.pac` | `option_mode_Description_#_on.hip` | 3 | 512×32 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 295 | `option/options_screen.pac/tex.pac` | `option_title_effect_#.hip` | 7 | 256×32 | — | 光效装饰 | 跳过 | — |  |
| 296 | `option/options_screen.pac/tex.pac` | `option_title_moji#.hip` | 3 | 256×32 | ✅ | 设置界面标题文字 | 重制图 | 未验证 |  |
| 297 | `option/options_screen.pac/tex.pac` | `option_window_frame_off.hip` | 1 | 1024×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 298 | `option/options_screen.pac/tex.pac` | `option_window_frame_on.hip` | 1 | 1024×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 299 | `option/options_screen.pac/tex.pac` | `option_window_text_#_off.hip` | 2 | 256×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 300 | `option/options_screen.pac/tex.pac` | `option_window_text_#_on.hip` | 2 | 256×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 301 | `option/options_screen.pac/tex.pac` | `window_frame_R_light_#.hip` | 8 | 1024×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 302 | `option/options_screen.pac/tex.pac` | `window_frame_�k_light_#.hip` | 8 | 1024×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 303 | `special/between_the_open.pac/tex.pac` | `BGM_frame_#.hip` | 3 | 64×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 304 | `special/between_the_open.pac/tex.pac` | `CG_frame.hip` | 1 | 512×256 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 305 | `special/between_the_open.pac/tex.pac` | `Icon_#.hip` | 12 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 306 | `special/between_the_open.pac/tex.pac` | `L-Arrow_A.hip` | 1 | 128×64 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 307 | `special/between_the_open.pac/tex.pac` | `L-Arrow_B.hip` | 1 | 128×64 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 308 | `special/between_the_open.pac/tex.pac` | `NEW_moji.hip` | 1 | 128×32 | ✅ | 「NEW!」标记 | 重制图 | 未验证 |  |
| 309 | `special/between_the_open.pac/tex.pac` | `R-Arrow_A.hip` | 1 | 128×64 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 310 | `special/between_the_open.pac/tex.pac` | `R-Arrow_B.hip` | 1 | 128×64 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 311 | `special/between_the_open.pac/tex.pac` | `ST_Light_line_#.hip` | 7 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 312 | `special/between_the_open.pac/tex.pac` | `ST_Operating_Instructions_base.hip` | 1 | 1024×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 313 | `special/between_the_open.pac/tex.pac` | `ST_Selection_mode_Description_base#.hip` | 1 | 1024×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 314 | `special/between_the_open.pac/tex.pac` | `ST_Selection_mode_Description_base.hip` | 1 | 1024×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 315 | `special/between_the_open.pac/tex.pac` | `ST_modo_frame_off.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 316 | `special/between_the_open.pac/tex.pac` | `ST_modo_frame_off_shadow.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 317 | `special/between_the_open.pac/tex.pac` | `ST_modo_frame_on.hip` | 1 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 318 | `special/between_the_open.pac/tex.pac` | `ST_title_base_Red.hip` | 1 | 512×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 319 | `special/between_the_open.pac/tex.pac` | `ST_title_line#.hip` | 1 | 512×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 320 | `special/between_the_open.pac/tex.pac` | `ST_title_moji.hip` | 1 | 256×32 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 321 | `special/between_the_open.pac/tex.pac` | `Title-base.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 322 | `special/between_the_open.pac/tex.pac` | `achievement_rate_moji.hip` | 1 | 128×64 | ✅ | 「達成率」标签 | 重制图 | 未验证 |  |
| 323 | `special/between_the_open.pac/tex.pac` | `bonus_scenario_text_kitou.hip` | 1 | 256×64 | ✅ | 奖励剧本标题 | 重制图 | 未验证 |  |
| 324 | `special/between_the_open.pac/tex.pac` | `bonus_scenario_text_mafia.hip` | 1 | 256×64 | ✅ | 奖励剧本标题 | 重制图 | 未验证 |  |
| 325 | `special/between_the_open.pac/tex.pac` | `book_o.hip` | 1 | 1024×1024 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 326 | `special/between_the_open.pac/tex.pac` | `book_s.hip` | 1 | 512×1024 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 327 | `special/between_the_open.pac/tex.pac` | `chara_name_#.hip` | 21 | 256×64 | ✅ | 角色名 | 重制图 | 现役✅ | 「遠山 浩」等 21 人；hip.md §3 已确认为现役 |
| 328 | `special/between_the_open.pac/tex.pac` | `chara_name_frame.hip` | 1 | 512×128 | — | 空框（非文字） | 跳过 | — |  |
| 329 | `special/between_the_open.pac/tex.pac` | `icon_#.hip` | 1 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 330 | `special/between_the_open.pac/tex.pac` | `number_difference#.hip` | 10 | 32×64 | ✅ | 数值数字 | 重制图 | 未验证 |  |
| 331 | `special/between_the_open.pac/tex.pac` | `number_difference_.hip` | 1 | 32×64 | ✅ | 数值数字 | 重制图 | 未验证 |  |
| 332 | `special/between_the_open.pac/tex.pac` | `onpu_mark.hip` | 1 | 128×32 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 333 | `special/between_the_open.pac/tex.pac` | `open_item_description_#.hip` | 10 | 32×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 334 | `special/between_the_open.pac/tex.pac` | `open_item_description_BGM.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 335 | `special/between_the_open.pac/tex.pac` | `open_item_description_CG.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 336 | `special/between_the_open.pac/tex.pac` | `open_item_description_achievement.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 337 | `special/between_the_open.pac/tex.pac` | `open_item_description_bonus_scenario.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 338 | `special/between_the_open.pac/tex.pac` | `open_item_description_ending.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 339 | `special/between_the_open.pac/tex.pac` | `open_item_description_hatena.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 340 | `special/between_the_open.pac/tex.pac` | `open_item_description_profile.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 341 | `special/between_the_open.pac/tex.pac` | `open_item_description_return_title.hip` | 1 | 512×32 | ✅ | 图鉴条目名/描述 | 重制图 | 未验证 |  |
| 342 | `special/between_the_open.pac/tex.pac` | `open_mode_moji_#.hip` | 8 | 256×64 | ✅ | 图鉴/奖励界面标题项 | 重制图 | 未验证 |  |
| 343 | `special/between_the_open.pac/tex.pac` | `open_rate_#.hip` | 10 | 32×64 | ✅ | 百分比/数值文字 | 重制图 | 未验证 | 「%」「0」等，可直接用字体重绘 |
| 344 | `special/between_the_open.pac/tex.pac` | `open_rate_%.hip` | 1 | 32×64 | ✅ | 百分比/数值文字 | 重制图 | 未验证 | 「%」「0」等，可直接用字体重绘 |
| 345 | `special/between_the_open.pac/tex.pac` | `open_rate_moji.hip` | 1 | 128×64 | ✅ | 百分比/数值文字 | 重制图 | 未验证 | 「%」「0」等，可直接用字体重绘 |
| 346 | `special/between_the_open.pac/tex.pac` | `open_title_moji_#.hip` | 7 | 256×32 | ✅ | 奖励/成就界面标题（含选项文字） | 重制图 | 未验证 |  |
| 347 | `special/between_the_open.pac/tex.pac` | `open_title_moji_#_effect_#.hip` | 48 | 256×32 | ✅ | 奖励/成就界面标题（含选项文字） | 重制图 | 未验证 |  |
| 348 | `special/between_the_open.pac/tex.pac` | `open_title_moji_effect_#.hip` | 9 | 256×32 | ✅ | 奖励/成就界面标题（含选项文字） | 重制图 | 未验证 |  |
| 349 | `special/between_the_open.pac/tex.pac` | `profile_BG_book.hip` | 1 | 1024×1024 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 350 | `special/between_the_open.pac/tex.pac` | `profile_complete_moji.hip` | 1 | 256×64 | ✅ | 「プロフィール」提示 | 重制图 | 未验证 |  |
| 351 | `special/between_the_open.pac/tex.pac` | `profile_item#.hip` | 1 | 256×64 | ✅ | 人物资料项（年齢／身長体重） | 重制图 | 未验证 |  |
| 352 | `special/between_the_open.pac/tex.pac` | `profile_item#_off.hip` | 11 | 256×64 | ✅ | 人物资料项（年齢／身長体重） | 重制图 | 未验证 |  |
| 353 | `special/between_the_open.pac/tex.pac` | `profile_item#_on.hip` | 11 | 256×64 | ✅ | 人物资料项（年齢／身長体重） | 重制图 | 未验证 |  |
| 354 | `special/between_the_open.pac/tex.pac` | `scroll_bar_A#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 355 | `special/between_the_open.pac/tex.pac` | `scroll_bar_B#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 356 | `special/between_the_open.pac/tex.pac` | `scroll_bar_button_A#.hip` | 3 | 64×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 357 | `special/between_the_open.pac/tex.pac` | `scroll_bar_button_B#.hip` | 3 | 64×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 358 | `special/between_the_open.pac/tex.pac` | `scroll_bar_shot_A#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 359 | `special/between_the_open.pac/tex.pac` | `scroll_bar_shot_B#.hip` | 1 | 64×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 360 | `special/between_the_open.pac/tex.pac` | `shelf.hip` | 1 | 2048×1024 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 361 | `system_menu/system_menu.pac/tex.pac` | `Icon_#.hip` | 2 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 362 | `system_menu/system_menu.pac/tex.pac` | `ST_BG_base.hip` | 1 | 16×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 363 | `system_menu/system_menu.pac/tex.pac` | `ST_Light_line_#.hip` | 7 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 364 | `system_menu/system_menu.pac/tex.pac` | `ST_Operating_Instructions_base.hip` | 1 | 1024×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 365 | `system_menu/system_menu.pac/tex.pac` | `ST_Selection_mode_Description_base.hip` | 1 | 1024×64 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 366 | `system_menu/system_menu.pac/tex.pac` | `ST_mode_Description_#.hip` | 7 | 256×32 | ✅ | 设置项说明文字 | 重制图 | 未验证 |  |
| 367 | `system_menu/system_menu.pac/tex.pac` | `ST_modo_frame_off.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 368 | `system_menu/system_menu.pac/tex.pac` | `ST_modo_frame_off_shadow.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 369 | `system_menu/system_menu.pac/tex.pac` | `ST_modo_frame_on.hip` | 1 | 512×128 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 370 | `system_menu/system_menu.pac/tex.pac` | `ST_modo_moji_#.hip` | 8 | 128×64 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 371 | `system_menu/system_menu.pac/tex.pac` | `ST_title_base_Red.hip` | 1 | 512×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 372 | `system_menu/system_menu.pac/tex.pac` | `ST_title_line#.hip` | 2 | 1024×16 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 373 | `system_menu/system_menu.pac/tex.pac` | `ST_title_moji.hip` | 1 | 256×32 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 374 | `system_menu/system_menu.pac/tex.pac` | `ST_title_moji_#.hip` | 8 | 32×32 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 375 | `system_menu/system_menu.pac/tex.pac` | `Title-base.hip` | 1 | 512×64 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 376 | `system_menu/system_menu.pac/tex.pac` | `icon_#.hip` | 1 | 128×32 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 377 | `system_menu/system_menu.pac/tex.pac` | `system-menu_moji_#.hip` | 9 | 256×64 | ✅ | 系统菜单项（セーブ／サウンド／オプション等） | 重制图 | 未验证 | ⚠️ 菜单文字主路径走 Font00 实时渲染，须实机确认该图是否仍被绘制 |
| 378 | `title/UI_02_title.pac/tex.pac` | `Hangame_logo.hip` | 1 | 2048×1024 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 379 | `title/UI_02_title.pac/tex.pac` | `arc_logo.hip` | 1 | 2048×1024 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 380 | `title/UI_02_title.pac/tex.pac` | `common_black.hip` | 1 | 256×256 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 381 | `title/UI_02_title.pac/tex.pac` | `common_ray.hip` | 1 | 512×16 | — | 装饰/图标/纯色层（无文字） | 跳过 | — |  |
| 382 | `title/UI_02_title.pac/tex.pac` | `ivy_logo.hip` | 1 | 2048×1024 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 383 | `title/UI_02_title.pac/tex.pac` | `nhn_logo.hip` | 1 | 2048×1024 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 384 | `title/UI_02_title.pac/tex.pac` | `title_bg_a.hip` | 1 | 2048×1024 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 385 | `title/UI_02_title.pac/tex.pac` | `title_bg_a_#.hip` | 1 | 2048×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 386 | `title/UI_02_title.pac/tex.pac` | `title_bg_b.hip` | 1 | 2048×1024 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 387 | `title/UI_02_title.pac/tex.pac` | `title_bg_b_#.hip` | 1 | 2048×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 388 | `title/UI_02_title.pac/tex.pac` | `title_bg_c.hip` | 1 | 2048×1024 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 389 | `title/UI_02_title.pac/tex.pac` | `title_bg_c_#.hip` | 1 | 2048×512 | — | 界面部件/底衬（无文字） | 跳过 | — |  |
| 390 | `title/UI_02_title.pac/tex.pac` | `title_copyright.hip` | 1 | 512×32 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 391 | `title/UI_02_title.pac/tex.pac` | `title_logo.hip` | 1 | 1024×256 | — | Logo／版权标识（不译） | 跳过 | — |  |
| 392 | `title/UI_02_title.pac/tex.pac` | `title_menu_#.hip` | 16 | 256×64 | ✅ | 标题菜单项（はじめから／つづきから／オプション／スペシャル／1-3章／戻る，各 普通+高亮 两态） | 重制图 | 现役✅(待复验) | 2026-10-19 图像确认含菜单文字；旧「打补丁无效」结论疑因当时目录结构不同改错路径 |
| 393 | `title/UI_02_title.pac/tex.pac` | `title_start.hip` | 1 | 1024×64 | ✅ | 标题画面提示（PRESS ANY BUTTONS 等） | 重制图 | 未验证 |  |
| 394 | `title/UI_02_title.pac/tex.pac` | `title_start_#.hip` | 15 | 64×64 | ✅ | 标题画面提示（PRESS ANY BUTTONS 等） | 重制图 | 未验证 |  |
| 395 | `title/UI_02_title.pac/tex.pac` | `title_start_a.hip` | 1 | 1024×32 | ✅ | 标题画面提示（PRESS ANY BUTTONS 等） | 重制图 | 未验证 |  |
| 396 | `title/UI_02_title.pac/tex.pac` | `title_start_b.hip` | 1 | 1024×32 | ✅ | 标题画面提示（PRESS ANY BUTTONS 等） | 重制图 | 未验证 |  |

**合计**：396 个模式，1011 个文件；判为含文字 372 个、非文字 607 个。

## 处理路线说明

- **重制图**：`hip-tool.fsx decode` 解出 PNG → 按原版式重绘中文 → `encode` 回 HIP → `pac-pack` 回写嵌套 pac。
- **跳过**：解出无文字（光效/空框/遮罩），不需要翻译。
- **待判**：需逐张看图确认。

## 已知风险

1. 文档已记录「更新后大量文字 UI 改实时渲染」——`chapter_title_date_*`、`title_menu_*` 打补丁后
   游戏无变化（实锤遗留）。**台账里的『现役/遗留』必须靠实机验证，不能只看文件存在**。
2. 菜单类（`system-menu_moji`/`ST_title_moji`）文字本体走 Font00 实时渲染，图可能只是底衬。
3. 角色名（`chara_name_*`）已确认现役（见 hip.md §3）。
