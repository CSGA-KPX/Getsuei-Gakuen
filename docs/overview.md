# 月英学園 -kou- 汉化项目 · 总览

## 目标

提取游戏文本与资源，完成中文翻译后重新打包进游戏。

游戏路径：`<游戏安装目录>\`

技术栈：**.NET 10 + F#**（工具脚本以 `.fsx` 为主，重型工具为 F# 控制台项目）。

## 文本显示链路（汉化的核心约束）

```
字符串(string) → char[] → glyph 索引[] → hip 位图字形 → 上屏
```

- 文本本身以 UTF-16LE 存储在 atf 文件中（`value` 字段），**改中文文本不需要改编码**；
- 但游戏按 char code 查 `abc` 映射表得到 glyph 索引，再从 `hip` 位图集取字形上屏；
- **原字库只覆盖日文（含 ASCII）**。要显示中文，必须向 `jpn_glyph.abc` / `jpn_glyph.hip` **扩充汉字字形**（详见 [abc-hip-font](formats/abc-hip-font.md)）。

## 关键文件位置

| 内容 | 路径 | 格式 |
|------|------|------|
| 对话文本（主） | `data\story\text\jpn\text_*.pac`（237 个） | pac 打包 → atf |
| 预渲染台词图 | `data\story\image\<atf key>.hip`（410 个，少数为文字图） | **TGA type9**（RLE 256 色索引，`scripts/decode-tga.fsx`/`encode-tga.fsx`） |
| 字库索引（全局/UI） | `data\text\jpn\jpn_glyph.abc` | abc（roundtrip ✅） |
| 字库位图（全局/UI） | `data\text\jpn\jpn_glyph.hip` | hip（HIP 专有格式，GeoArcSys 可解 ✅） |
| 系统菜单字库 | `data\cmn\font.pac` → 嵌套 `abc.pac`+`fullimg.pac` → Font00.abc/Font00.hip | **Font00.abc = abc Version 5**（✅ 已解析，256×256 HIP 图集，见 abc-hip-font.md §2.6） |
| 事件脚本 | `data\story\script\scr_*.pac`（229 个） | pac 打包 → ADVScript bin |
| 制作人员名单 | `data\text\staff_roll\staff_roll_*.lst`（12 个） | CSV（Shift-JIS，语义推断见 formats/staff-roll.md） |
| 字符串银行 | `data\text\ivy_StringBank.lst` | 纯 ASCII 每行一条（内部标识符） |
| **UI 文字图（2026-10-18 新排查）** | `data\AE\**\*.pac`（167 个，嵌套 tex.pac） | hip（**498 张文字图候选**，清单 `work/inventory/ae-image-inventory.json`） |

注意：`text_00_0-01.pac_`（下划线结尾）曾是**更新前的旧版备份**（内部同样是
font+font+string 三件套但字库/文本都更小），**2026-10-18 游戏更新时已随 Steam 校验消失**。
⚠️ `work/inventory/pac-inventory.json`（10:08 生成）是**更新前**的尺寸数据，
更新（10:57，2234+405 个文件）后部分 pac 尺寸已变（如 text_00_0-01 569792→362608）；
atf JSON（13:00 提取）与当前游戏文件一致。重新盘点前以实机文件为准。

**实测：每个对话 pac 内嵌一套字库**（237 个 pac 全部如此，uniform 三件套）：

```
text_10_1-01.pac (FPAC, AutoLenFilename|Version2)
├── font.hip    (168002 字节)  字库位图（TGA 或 HIP 子格式，224/13 分布）
├── font.abc    (142634 字节)  字库索引（UNK1 字号 0x44）
└── string.atf  (8474 字节)    对话文本
```

**字库使用关系已由 IDA 逆向确认**（详见 [abc-hip-font](formats/abc-hip-font.md) 第 5 节）：
对话渲染用 **pac 内嵌 font**（且是按章节裁剪的用字子集，456–数千 glyphs 不等），
UI/系统文本用全局 `jpn_glyph.*`，系统菜单用 `cmn/font.pac` 的 Font00。
⇒ 汉化字库要改 **237 个 pac 内嵌 font + jpn_glyph + Font00 三处**，
方案上做一套中文超集字库批量替换各 pac 内嵌字库。

**⚠️ 预渲染台词图优先机制（2026-10-18 破案 ✅，2026-10-19 完成中文重制）**：message 的 key 若在
`data\story\image\` 存在同名 `.hip` 文件，**显示预渲染图片而非 atf 文本**
（atf 里那句只是源数据备份）。已证：开场白 `00_0-01_01_001`–`004` 是 1024×128 美术字幕图
（描边大字+ruby 注音）；005 起无图才走 atf+font 实时渲染。image 目录 410 个文件中
**含文字图共 35 张**：`00_0-01_01_0*` 25 + `10_1-06_01_0*` 10；`day_*` 是日期数字（无需译）。
删图回退已实测=空白（大字模式不查 atf），因此重制图是唯一路线 ——
`scripts/remake-telop.fsx` 已按原版版式（白字黑边/行带/ruby 留空）重制 35 张并装机。
详见 [tga-type9.md §6](formats/tga-type9.md)、排查记录 [re-notes.md](re-notes.md)。

## 格式文档索引

| 文档 | 说明 |
|------|------|
| [formats/pac.md](formats/pac.md) | pac 打包文件（**打包器已验证**，2201 文件 roundtrip ✅） |
| [formats/atf.md](formats/atf.md) | atf 对话文本（**汉化主战场**，已验证双向无损） |
| [formats/advscript.md](formats/advscript.md) | ADVScript 事件脚本 bin（已验证双向无损） |
| [formats/abc-hip-font.md](formats/abc-hip-font.md) | 字库 hip 位图 + abc 索引（abc roundtrip ✅ 含 ver5/Font00；含 §5 运行时逆向结论） |
| [formats/tga-type9.md](formats/tga-type9.md) | 预渲染台词图 TGA type9 变体（**编解码全通**，410 文件 roundtrip ✅） |
| [formats/hip.md](formats/hip.md) | HIP 专有格式工具链（GeoArcSys 解/编码 ✅，UI 文字图分布） |
| [formats/staff-roll.md](formats/staff-roll.md) | staff_roll 制作名单 CSV（语义推断 ⚠️） |
| [tables/ae-ui-images.md](tables/ae-ui-images.md) | **台账**：AE/UI 图像 1011 个文件，含文字判定 + 现役/遗留 |
| [tables/font-coverage.md](tables/font-coverage.md) | **台账**：三套字库容量 vs 译文用字（2952 汉字），两个硬约束 |
| [tables/text-inventory.md](tables/text-inventory.md) | **台账**：翻译文本 209/237 已译 + 未译分类 + UI 文本未清点 |
| [tables/ui-text-source.md](tables/ui-text-source.md) | **UI/菜单文本定位**：exe 内日文 = Steam 成就（64/66 对账通过）；菜单项来源未解；3.6MB 上限/Font00 图集 IDA 实锤 |
| [re-notes.md](re-notes.md) | Getsuei.exe 文本渲染逆向笔记：函数地址/类名/伪代码/追踪技巧（IDA 定位手册） |

## 工具索引（scripts/）

| 工具 | 用法 | 状态 |
|------|------|------|
| `scripts/AdvScriptParser/` | bin ⇄ JSON 事件脚本 | ✅ 已验证 roundtrip 逐字节一致 |
| `scripts/AtfJson/` | atf ⇄ JSON 对话文本 | ✅ 已验证 roundtrip 逐字节一致 |
| `scripts/atf-batch.fsx` | `extract` 批量 atf→JSON（237✅）/ `stats` 文本量统计 | ✅ |
| `scripts/pac-unpack.fsx` | pac 解包（提取全部或按扩展名过滤） | ✅ 可用 |
| `scripts/pac-pack.fsx` | `verify` 内存 roundtrip / `pack` 以原 pac 为清单重打包 | ✅ 2201 文件全量验证 |
| `scripts/pac-search.fsx` | 在游戏目录中按字节模式搜索文件 | ✅ 可用 |
| `scripts/Pac.fsx` | pac 读写库（`read`/`readBytes`/`build`） | ✅ |
| `scripts/tga.fsx` | TGA type9 编解码库（TgaLib，含 RLE 双变体） | ✅ 410 文件 roundtrip |
| `scripts/decode-tga.fsx` | TGA type9 → PNG | ✅ |
| `scripts/encode-tga.fsx` | PNG → TGA type9（Wu 量化 256 色） | ✅ 端到端验证 |
| `scripts/remake-telop.fsx` | `all` 台词图中文重制（35 张 → `work/patch-image`）/ `install` 写回游戏（自动备份） | ✅ 2026-10-19 已装机待实机确认 |
| `scripts/abc.fsx` | abc 解析/重建库（`parseBytes`/`buildBytes`/`verify`） | ✅ 238 文件 roundtrip |
| `scripts/abc-analysis.fsx` | abc 分析（info/dump/glyphs/mapping） | ⚠️ 分析工具，格式部分字段语义未确认 |
| `scripts/abc-overlay.fsx` | 字形矩形叠加 PNG（需 ImageSharp nuget） | ⚠️ 需配合 GeoArcSys 转出的 PNG |
| `scripts/roundtrip-all-*.fsx` | 全量回归：pac / abc / TGA（image 目录） | ✅ 各全量通过 |
| `scripts/hip-tool.fsx` + `hip.fsx` | HIP⇄PNG 转换（自研，已本地化；decode/encode/batch-decode/info） | ✅ 全量 1055 解码 + roundtrip 回归；见 formats/hip.md |
| `scripts/roundtrip-all-hip.fsx` | 全量 HIP roundtrip 回归 | ✅ 1055 文件 |
| `scripts/smoke-patch.fsx` | 实机验证补丁生成（adv=字形替换+追加 / image=台词图重编码 / hip=本地 hip-tool 重编码），自动备份 | ✅ 已装机待实机观察 |
| `scripts/ida-mcp-run.cmd` | 启动 IDA MCP（默认监听 127.0.0.1:8745，可用 --port 改） | 需要本机 IDA/uv 环境 |

## 通用约定（继承自过往项目，新工具必须遵守）

1. **JSON 中只存真实数据**：偏移、索引、表头、长度等一切可由内容推导的字段一律在还原（build/restore）时重建，不允许出现需要手工维护的 magic number。
2. **无损往返优先**：任何解析器先保证 `bin → json → bin` 逐字节一致（用 `verify`/SHA256 验证），再谈编辑功能。
3. **字符串编码**：ADVScript 字符串池为 Shift-JIS (codepage 932)；ATF 的 key 为 ASCII、value 为 UTF-16LE。无法无损编码的字符串以 `{ "$hex": "..." }` 兜底。
4. **历史存档**：`prev-projects/` 为原始未整理项目（含可参考的 C# 实现与样例文件），已被本 docs/ 与 scripts/ 吸收，日常开发不要往里写新东西。
