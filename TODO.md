# TODO.md — 月英学園 -kou- 汉化项目

## 阶段 0：资产盘点（短期）— ✅ 已完成（2026-10-18）

- [x] 全量解包盘点 `data\story\text\jpn\*.pac`（**237 个**，非 238）：
  全部为 FPAC/Version2 三件套 `font.hip + font.abc + string.atf`；
  内嵌 font.hip 224 个 TGA 子格式 + 13 个 HIP 专有格式。清单：`work/inventory/pac-inventory.json`
  （工具 `scratch/pac-inventory.fsx`）。
- [x] 确认 `jpn_glyph.hip` 子格式：**HIP 专有格式**（`48 49 50 00...` 头）。
- [x] ~~运行 `abc-analysis.fsx info` 实测 `jpn_glyph.abc`~~ **已完成**：
  EntryCount=65509 / Mappings 长 65512（覆盖全部 BMP 码位）/ GlyphCount=2860 / map值=glyph+1；
  CJK 区已被日文汉字占用（U+4E00→glyph 519）→ 改造路线以「复用+替换+追加」为主，见 abc-hip-font.md。
- [x] 确认**每处文本上屏使用的字库**（IDA 逆向 Getsuei.exe，详见 abc-hip-font.md §5）：
  - ADV 对话 → **pac 内嵌 font**（按章节裁剪的子集，456–数千 glyphs）⚠️ 待实机最终验证；
  - UI/系统文本 → 全局 `jpn_glyph.*`；系统菜单 → `cmn\font.pac` 的 Font00。
- [x] 调查 `scr_*.pac`（229 个）：命名与 text pac 一一对应，内部 `XX.bin`(655B 头) + `XX_File.bin`；
  `message` 命令按键引用 atf（如 `10_1-01_01_001`），**全量 SJIS 扫描确认 bin 中无玩家可见文本**
  （少量命中均为二进制数据巧合）。清单：`work/inventory/scr-inventory.json`。
- [x] `text_00_0-01.pac_` 确认为**更新前旧版备份**（三件套结构相同但字库/文本更小，SHA256 不同），排除。
- [x] 新发现文本位置：`data\text\staff_roll\*.lst`（15 个，CSV/Shift-JIS，玩家可见）、
  `data\text\ivy_StringBank.lst`（ASCII 标识符）、`data\cmn\font.pac`（Font00 菜单字库，pac 套 pac）。

## 阶段 1：提取与翻译工作流（中期）

- [x] 批量 atf → JSON（`scripts/atf-batch.fsx extract`，237/237 OK，产物 `work/atf/*.json`，
  每文件附带 parse→build 逐字节回归）✅ 2026-10-18
- [x] 文本量统计（`atf-batch.fsx stats` → `work/inventory/atf-stats.json`）：66535 记录 /
  109 万字符；`<ruby>` 标签仅 0.6%（429 条）；与 image 目录 key 重叠 18 条；
  textParam 全 0；去重字符 2490 ✅ 2026-10-18
- [x] staff_roll lst 的 CSV 参数语义梳理（推断版，`docs/formats/staff-roll.md`）✅ 2026-10-18
- [x] 建立术语表与翻译记忆 ✅ 2026-10-20 核查：**不需要独立术语表** —— 日文说话人名去重 186 个，
  同一日文名对应多个中文译名 **0 个**（209 个 pac 译名天然统一）。术语事实标准 = `work/atf-cn` 现有译文，
  将来改译名须全量替换（见 `docs/tables/text-inventory.md` §1）。
- [ ] 制定文本长度约束：[ ] 尚无明确结论。已知：字库格 34×49、atf 内手动 `\r\n` 换行、
  220 pac 实机验收无截断/溢出反馈。⇒ 系统 UI 图重制时再定（届时按原图行宽量）。
- [x] 存档/设置等其他含文本的位置排查 ✅ 结论：无额外可译文本（见阶段 3 同名条目）。

## 阶段 2：字库改造（核心难点，长期）

- [x] **实机验证对话字体归属**：✅ **A/B/C/D 全部 PASS（2026-10-18 实机确认）**，
  见 `docs/testing/smoke-test.md`。结论：内嵌字库可改可追加 / TGA·HIP 重编码均被接受 /
  jpn_glyph 有 3.6MB 硬上限。（~~菜单文字走 Font00 实时渲染~~ **2026-10-19 更正：菜单文字是预渲染图**。）
  副产物：**更新后大量 AE 文字图为遗留资源，需按「现役」重新甄别**（见阶段 2「UI 文字图重制」）。
- [x] ~~用 abc-analysis 摸清原字库~~ **已按运行时语义重解析**（abc-hip-font.md §2 重写 ✅
  2026-10-18：双表结构 rec24 float/rec32 像素、Version 分支、度量字段全部反编译确认；
  字形位图用 rec32 像素矩形直接从图集定位，不再需要 overlay 猜测）
- [x] 确定中文用字集 ✅ 2026-10-19：译文全量去重 **3139 字符**（汉字 2952 + 假名 20 + 标点/数字/拉丁 167），
  需求与容量对照见 `docs/tables/font-coverage.md` §2。
- [x] 汉字字形生成 ✅ 2026-10-19：`scripts/font-rebuild.fsx`（等线 Deng.ttf / GDI+ 全量重渲染、
  34×49 固定格、白字 + 只描外侧 2px 黑边 + (2,1) 投影），实机六轮迭代后验收。
- [x] 制定超集字库方案 —— **237 个 pac 内嵌字库全部替换完毕 + 实机验收 ✅**（2026-10-19）。
  - [x] 码位分配：按 Unicode 码位直接映射，无复用/偷槽位（Mappings 65512 槽够用，已逆向确认）。
  - [x] ~~Font00.abc 异构格式~~ ✅ 破案 = 标准 abc **Version 5**（IDA：`BB_CFont_SystemFont` 0x504A10
    在 Font00.hip 存在时调 `sub_43F290` 标准解析）：LineHeight=15 / EntryCount=0x6F22 /
    GlyphCount=250（ASCII+假名+字・漢）/ 无 rec32 表；AbcLib 已支持 ver5 roundtrip（239/239 ✅）。
    详见 abc-hip-font.md §2.6。
  - [x] **jpn_glyph 扩充** ✅ 2026-10-20 深夜定稿（v4）：Gray16 + 统一字号 + 需求集裁剪
    （`jpn_idlist.txt` 是 UI 固定文本表，897 字符只缺 1）+ 8 级灰度量化 → hip **3.40 MB / 94.5%**、
    abc 347 KB / 58%。已装机，**待实机验收**。详见 `docs/tables/font-coverage.md` §8。
- [x] **预渲染台词图处理** ✅ 35 张全部重制并实机验收（等线美术字，见 `remake-telop.fsx`）。
  - [x] 中文美术字版式（`docs/formats/tga-type9.md` + 原图墨迹行段反解）。
  - [x] 涉及范围**实测核定（2026-10-20）**：`work/pristine/story-image` 410 个文件里，
    **1024×128 台词图恰好 35 张**（`00_0-01_*` 26 + `10_1-06_*` 9），已全覆盖 ✅。
    ~~`ed_*` 108 张~~ **误判**：它们全是 544×352 的结局 CG 缩略图（`cg_*_ed` / `ed_*image*`），
    不含台词，不需要重制。
- [ ] **UI 文字图重制**（现余最大工作量）：~~先甄别~~ ✅ **甄别定论（2026-10-20 第二轮）**，
  结论与方法见 `docs/tables/ui-images-triage.md` §7：
  - ✅ **已重制装机 174 张**（2026-10-20 深夜，工具 `scripts/remake-ui-text.fsx`）：
    `system_menu` 24（菜单项+标题+说明）、`options_screen` 31（含 `_on` 高亮辉光；`option_title_moji01` 因背景滚动动画保留原版）、
    `back_log` 5（标题逐字）、`g_dialog` 37（结局/剧本/系统提示 27 + 胶囊按钮 10）。
    4 个 pac `pac-diff` 结构一致。**待实机验收**。做法与五个坑见同文档 §8。
  - [x] `between_the_open` **76 张已重制**（标题/模式名/角色名 20/个人资料项/达成率·开放率红字/右下角模板标签/按键提示条）；其余是数字、`???`、动画帧、美术字，按台账 §8.4 跳过。
  - ❌ 不做：`UI_29_nowloading` 艺术字（用户要求）、`system-menu_moji_01..09` 模糊动画帧、
    `back_log_title_moji_light_*` 光斑层、`title/UI_02_title.pac`（遗留）。
  - AE 852 张 HIP 全部定级 **A 607 / B 22 / C 223 / 无 D·E·F**（procmon 全覆盖 + aex 名字表）；
  - ✅ **核心未知已破题**：UI 图的名字写在各 pac 内嵌的 `.aex` 里（ASCII 串 `TEX\xxx.png`），
    不在 exe 里 ⇒ 「文字是 Font00 实时渲染」的猜测**推翻**，文字就是预渲染图，可改图汉化；
    新工具 `scripts/aex-names.fsx`（1104 个唯一图名）；
  - 含文字 372 张主体：`between_the_open`(165，现役)、`options_screen`(42)、`g_dialog`(37)、
    `system_menu`(34)、`back_log`(12)、`UI_29_nowloading`(6)；
  - ❌ **划掉 `adv/chapter_title_*.pac`（20 pac / 220 张）**：日期卡实际是散装
    `data/story/image/day_*.hip`（数字 + 月火水木金土日），由脚本 `DAY_TITLE` 驱动；
  - **已知遗留**：`title/UI_02_title.pac` 内 34 张（实机验证改图无效）；
  - 7 张探针图**实机已验证 10/10 全部确认**（`scripts/probe-images.fsx`）：系统菜单项/标题、
    回想标题、开放之间模式名、系统数据保存提示、NOW LOADING、设置页标题/项名 on·off/底部说明
    全部变色 ⇒ **文字是预渲染图，路线定案**。设置界面重制清单见 `ui-images-triage.md` §7.6
    （注意 `ST_*` 系列是遗留，别动）。
- [x] abc 重打包：`AbcLib.buildBytes`，jpn_glyph + 237 内嵌 font.abc 全量 roundtrip 逐字节一致 ✅ 2026-10-18
- [x] **HIP 专有格式工具链**：~~GeoArcSysAIOCLITool 可解码 1053/1055~~
  **✅ 已本地化（同日）**：自研 `scripts/hip.fsx`（移植 ArcSysLib 源码）+ `hip-tool.fsx`
  （decode/encode/batch-decode/info，encode 支持 32bpp/indexed + --palette-from）。
  全量 1055/1055 解码、抽样 37 与旧工具像素级一致、roundtrip 回归 2109 OK + 1 SKIP
  （RawRepeat 截断区，源实现固有行为，见 hip.md §1.1）。外部工具已移除。
  - [x] ~~HIP→PNG→HIP 实机验证（工具再编码产物游戏是否接受）~~
    ✅ 实机补丁已装机改打标题画面嵌套图（UI_02_title.pac → tex.pac → title_menu_00.hip
    「はじめから」叠品红框 X，43.7KB→65.6KB 裸 RGBA），实机接受。
    ⚠️ 教训：jpn_glyph.hip 直打会触发装载缓冲断言（**hip < 3.6MB、abc < 600KB 硬上限**，
    sub_510050 逆向确认），裸 RGBA 超限；解法（**尚未实机验证，当前也不需要**）：jpn_glyph 实为
    Gray16，`hip-tool encode --format indexed` 量化 14 色 RawRepeat 仅 1.7MB < 3.6MB（hip.md §1.2）。
  - [x] ~~自研 HIP 读写器（长期；解析代码在 ArcSysLib.dll 闭源）~~
    **✅ 已完成（同日）**：源码在 external-src/ArcSysLib-master 中找到并移植，见上条。
- [x] **台词图机制破案（2026-10-18 实机验证）**：message key 若在 `data\story\image\` 有同名 .hip
  （TGA type9），中央大字模式**只显示图片、不查 atf**（删图实测=空白，回退不存在）⇒ 重制图是唯一路线。
- [x] 自写 TGA type9 编码器（`scripts/encode-tga.fsx` + `tga.fsx` 库；
    PNG→256色量化→RLE，包化算法复刻原版工具含双变体）✅ 2026-10-18 全量 roundtrip：
    410 文件 308 变体A + 102 变体B 逐字节一致，PNG→TGA→PNG 端到端验证。
  - [x] ✅ 实机补丁已装机（00_0-01_01_001.hip 叠加品红框+X 重编码），实机接受。
- [x] ~~**UI 文字图重制（2026-10-18 新排查）**：498 张文字图候选~~
  **数据已过时**：旧清单只扫一层嵌套 pac，2026-10-19 用 `scripts/ae-rescan.fsx`（递归展开）
  重扫得 **396 个模式 / 1011 个文件（含文字 372）**，见 `docs/tables/ae-ui-images.md`。
- [x] ~~实机验证：单条文本替换为中文，观察上屏效果~~ ✅ 2026-10-18 实机通过
  （text_00_0-01.pac：こ 品红块 + 追加「汉」+ 005 句插入），观察结论见 `docs/testing/smoke-test.md`。

## 阶段 3：打包与回归（长期）

- [x] **实现 pac 打包器**（`scripts/pac-pack.fsx` + `PacLib.build`）：对齐 16、FileNameHash
  小写+仅 Version2 写入、条目按偏移排序——**2201 个 FPAC 全量 roundtrip 逐字节一致**
  （唯一跳过：`sound\**\bgm_ex5.pac` 是 WBND Wave Bank，非 pac 格式）✅ 2026-10-18
- [x] **全量回归流水线**（`scripts/regression-pipeline.fsx run`，2026-10-18）✅：
  237/237 pac 通过（66535 记录 / 109.4 万字符伪翻译后回解逐条校验一致，字库组件字节不变），
  报告 `work/regression/report.json`。流水线为内存中「解包→伪翻译→重打包→回解校验」，
  不进游戏目录；游戏启动冒烟已由实机验证 A/B/C/D 覆盖。
- [ ] 备份/还原机制：**部分完成** —— 各 install 脚本装前自动备份到 `work/backup/`（`audit-backup.fsx`
  可 SHA 盘点游戏目录被改文件），但**没有一键还原脚本**；目前还原靠 Steam「验证文件完整性」。
  ⇒ 若要补，只需按 `work/backup` 的目录镜像复制回游戏目录（低优先级）。
- [x] 存档/设置等其他含文本的位置排查（2026-10-18）✅：
  - `usual\UI_usual_*.pac`、`IvyEff\IvyEff.pac`：192B 空壳（FileCount=0），无内容；
  - `cmn\catalog_env.info`：引擎配置（语音数等），SJIS 注释，无玩家可见文本；
  - `saveload\*.bin`：存档数据（二进制度/场景状态），文本走渲染链路不入档；
  - `saveui\saveui.pac`（13.8MB）：存档 UI 图集，内含 HIP 专有格式图，已列入 ae-image-inventory
    待逐张确认是否含文字；
  - `AE\**\*.pac`（167 个）：UI 图大本营，**396 个模式 / 1011 个文件（含文字 372）**已盘点
    （`scripts/ae-rescan.fsx` 递归展开双层嵌套 pac，见 `docs/tables/ae-ui-images.md`）。

## 技术债

- [ ] abc rec32 尾 16B 语义未明 —— 新增/修改字形时克隆同字体既有值绕过
  （smoke-patch.fsx 做法）；字库重建 237/237 + 实机验收已通过，风险已大幅降低，仅在出现
  「个别字形位置/大小异常」时再回头查。rec24 +8/+12（3.0/4.0）语义未知，推测与 ruby 注音相关。
  （旧条目「abc Chunk1 24B 语义未明」已随 abc-hip-font.md §2 重写解决 ✅ 2026-10-18）
- [ ] `scripts/abc-analysis.fsx` / `abc-overlay.fsx` 仍是旧 56B 连续解析，仅适合字节级 roundtrip；
  语义分析用 `scripts/smoke-patch.fsx` 中的 `abcParse`（或将来抽出共享库）。
- [ ] ATF 仅支持 (2i, 2i+1) 顺序配对布局，遇到去重/共享文件需扩展模型。
- [ ] ADVScript 的 Flag 字段与参数语义需要游戏知识，目前仅透传。
- [ ] 考虑把 AtfJson / AdvScriptParser 合并为一个 `GakuenTools` 项目（共享 roundtrip 测试基建）。
- [ ] `scripts/atf-batch.fsx` 以 #load 方式复用 AtfJson 项目源码文件（.fsx 不能引项目），
  Atf.fs 改动后需注意两份编译路径（dotnet build + fsi）都验证。
- [ ] TGA 变体 B（endRunAsRaw）只逆向了末尾行为，若未来发现中间包差异需重查包序列。

## 翻译质量收尾（2026-10-20 核查结论，尚待处理）
- [x] **译文覆盖率核查**（`work/atf` ⇄ `work/atf-cn` 逐 key 比对，数据见 `docs/tables/text-inventory.md` §1）：
  209 个 pac / 65267 记录，**空译文 0、缺 key 0**；真正未翻的消息正文只有 2 条
  （`text_test` 的 `CNT_01_001/002`，开发测试项，不译）。
- [x] **术语一致性核查**：日文说话人名去重 186 个，**一对多 = 0** ✅ ⇒ 不需要独立术语表。
- [x] **校对轮次** ✅ 2026-10-20 用户确认：GalTransl 侧**已做过 proofread**，`work/galtransl-proc`
  就是**输出版本**（文件里只有 `message`/`src_msg`/`atf-json-key` 是因为导入侧只取最终译文，
  不代表「没校对」）。⇒ 不安排二次校对；ruby 那 53 处属格式规整，不是翻译问题。
- [x] **ruby 保真逐条判定** ✅ 2026-10-20：635 个标签全部归类（写汉字读假名 7 / 写汉字读汉字 200 /
  着重号 396 / 空注音间隔点 21 / 其他 11），「只保留写 A 读 B + 着重号」的约定执行到位。
  待修 **53 处**（拼音注音 9 + 基字=注音冗余 42 + 長音符误作破折号 2），清单
  `work/inventory/ruby-fix-list.tsv`，判定与建议见 `docs/tables/ruby-triage.md`。
  - [x] 写 `scripts/fix-ruby.fsx`（表驱动，做法同 `gen-ending-cn.fsx`）✅ 2026-10-20：
    **53/53 全部命中**（38 文件），幂等；残留复扫（基字=注音 / 拼音 / 注音内 U+2014）全为 0；
    `AtfJson build` 试编译通过。记录见 `docs/tables/ruby-triage.md` §6。
    ⚠️ **不要重跑 `galtransl.fsx import`**：它会用 `work/galtransl-proc` 重写 `work/atf-cn`，把修复覆盖掉
    （真重跑了就再跑一次 `fix-ruby.fsx`，幂等）。
    - [x] 进游戏前的构建：`font-rebuild.fsx all`（237/237 OK）+ `verify-patch.fsx`（237 OK / 0 FAIL），
      解包抽查 `patch-pac` 确认修复已进产物 ✅ 2026-10-20。**已装机**。
- [x] **扩 jpn_glyph（存档界面动态文本）** ✅ **2026-10-20 深夜 v4 定稿并装机**：
  存档界面的槽位详情用 UI 全局字库 `jpn_glyph`，原版没有简体字 ⇒ 中文缺字形。
  前三轮（Indexed8 黑字 / 逐字拉伸导致字面大小不一 / 体积超限）的根因与 v4 做法见
  `docs/tables/font-coverage.md` **§6–§8**：
  - ✅ **必须 Gray16 + RawRepeat**（全游戏 1055 个 HIP 里没有一个 8bpp 索引文件，索引路径没被测过）；
  - ✅ **统一字号 + 统一原点**（「漢」墨迹高 = 29，字身落框内 (1,5)-(31,34)）；
  - ✅ **需求集裁剪**：`jpn_idlist.txt`（UI 固定文本表，897 字符只缺 1）∪ 译文 ∪ 脚本 ∪ ASCII
    = 3702 码位；保留 2164（非 CJK 全留）/ 新增 1685 / 丢弃 695 个 CJK 日文汉字
    （清单 `work/inventory/glyph-drop-list.txt`，漏字只是空白不崩）；
  - ✅ 体积：hip **3,403,679 B（94.5%）**、abc 346,646 B（58%），`--levels 8` 灰度量化；
  - ✅ 离线自检工具 `scripts/preview-font.fsx`（按引擎语义排版，查 mapping 偏移/缺字/字面大小）；
  - **待实机验收**（完全退出游戏再进，看存档界面中文是否正常）。
- [x] **⚠️ 修 650 行超宽译文（冒烟发现）** ✅ **2026-10-20 全部修完（0 行超宽）**：
  游戏不自动折行、单行上限 **24 全角字符**，超出直接溢出 backlog 边框。
  - ✅ 立判据：**日文原文也超 24 全角的记录用更宽的框**（`text_SP` 特别语音原文 35~47 全角仍正常显示）
    ⇒ 只报「中文超宽、日文没超」的行（否则误伤 119 行）。最终真实工作量 **650 → 269 行**。
  - ✅ 新工具：`scripts/check-line-width.fsx --tsv`（台账）、`scripts/reflow-atf.fsx`
    （同记录内重排行宽，自动 **251 条**，不删字、收尾标点不断行）、
    `scripts/apply-line-fixes.fsx`（人工改写应用器，校验「每行 ≤24 全角 + ruby 标签序列不变」）。
  - ✅ 人工改写 **269 行**（6 批，`work/inventory/line-fix-batch{1..6}.tsv`），
    覆盖第 1~4 章 + 番外 + 特别语音，`check-line-width` 复查 **0 行超宽**。
  - ✅ 已重跑 `font-rebuild.fsx all`（237/237）+ `verify-patch`（237 OK）+ `install-text-pac --apply` 装机。
- [ ] ~~`text_voice_test` 说话人名「マスター」未译（2 条）~~ 用户判定：**看到 bug 再说，暂不做**。
- [ ] ~~`staff_roll`（12 个 `.lst`）未译~~ 用户判定：**看到 bug 再说，暂不做**。
  （字段语义仍属推断，见 `docs/formats/staff-roll.md`；真要译先确认字段含义。）

## 当前状态（2026-10-20 清点）

已完成：阶段 0 全部；阶段 1 全部（含术语一致性核查）；阶段 2 的「用字集 / 字形生成 /
237 pac 超集字库 / 台词图 35 张」全部完成并实机验收；阶段 3 pac 打包器 + 文本位置排查 +
**全量回归流水线（237/237 ✅）**；abc 格式运行时语义全解析；实机验证 A/B/C/D 全部 PASS；
**对话译文 220/237（17 个判定不译）**、**台词图 35 张**、**标题菜单图 18 张** 均已定稿。

⚠️ **游戏目录实测（2026-10-20 装机后）—— 完整汉化已在位**：

| 位置 | 实测 | 说明 |
|---|---|---|
| `data/story/text/jpn/*.pac` | 237 个**已装**（= `work/patch-pac`，含内嵌中文字库〔**列间隔 1px** 版〕、译文、ruby 53 处修复） | ✅ |
| `data/story/image/*.hip` | 18 张菜单图 + 35 张台词图已装，其余 357 张原版 | ✅ |
| `data/AE/**` | **98 张 UI 文字图已重制**（system_menu 24 / options_screen 32 / back_log 5 / g_dialog 37），其余原版 | 待验收 |
| `data/text/jpn/jpn_glyph.*` | **v4 已装**（Gray16，hip 3.40 MB / 94.5%） | 待验收 |
| `data/cmn/font.pac` | 原版（菜单字库不用扩，UI 文字都是图） | — |
| `work/patch-pac/` | **最新**（237 pac + build.log） | 随时可重装 |
| 游戏目录被改文件总数 | **292**（237 pac + 18 菜单 + 35 台词 + 1 其它）+ 4 个 AE pac + 2 字库 | `audit-backup.fsx` 盘点 |

下一步建议：
0. ~~复测探针 3~~ ✅ **10/10 全部确认**，探针已 restore；
1. ~~装机完整汉化~~ ✅ 237 pac + 35 台词图 + 18 菜单图；
2. **实机验收本轮三件事**（都需**完全退出游戏**再进）：对话字库 1px 细线是否消失、
   存档界面中文是否正常、98 张 UI 图的文案/字号/位置；
3. UI 剩余：`between_the_open` **165 张**（开放之间），做法见
   `docs/tables/ui-images-triage.md` §8（表驱动加条目即可）；
   ~~`adv/chapter_title_*`(220)~~ 已划掉（日期卡走散装 `day_*.hip`）；
   每次改完 pac 跑 `scripts/pac-diff.fsx --backup <相对路径>`（逐字段）；
   仍不放心或怀疑解析器有 bug 时跑 `scripts/pac-bytediff.fsx --backup <相对路径>`（纯字节，最终裁判）；
4. ~~Procmon 全覆盖巡查~~ ✅ 已完成（`Logfile-20261020.xml`）；
5. ~~探针图定死路线~~ ✅ 已定论（aex 名字表证文字是图）；
6. ~~ruby 53 处修复~~ ✅ 已装机验证；
7. 暂不做（用户判定「看到 bug 再说」）：`staff_roll`、`text_voice_test` 人名、一键还原脚本、Font00 扩充。

---

## 2026-10-19 进展补记（此前 git 版本缺这一节，恢复时补回）

- [x] **对话译文验收**：237 pac 中 **220 已译** / 17 未译（台账 `docs/tables/text-inventory.md`）
- [x] **台词图 35 张**已验收（等线字体，实机确认效果）
- [x] **菜单图真实位置定位**：`data/story/image/title_menu_01..09_on/off.hip`
  （散装文件，扩展名 `.hip` 但内容是 TGA）；`UI_02_title.pac` 里那套是**旧版遗留**
- [x] **标题菜单图重制**：9 项 × 2 态 = 18 张。译法：从头开始／继续游戏／设置／
  开放之间／序章／浩／匡／抗／返回。红条 `title_menu_mark_01/02` 无文字，保持原样
- [x] **IDA 逆向菜单绘制逻辑**：`sub_53B400` 固定坐标表（X 恒 512、Y 320/400/480/560）、
  缩放恒 1.0、`unk_11A3674`=480（详见 `docs/re-notes.md`）

## ✅ 阶段 X：菜单调试收尾（2026-10-19 收官，详见 `docs/handover.md`）

- [x] **还原游戏到原版** ✅ Steam「验证文件完整性」跑完，实机解图确认回到日文原版。
- [x] **建 `work/pristine/` 永久只读基准** ✅ `story-text-jpn`(237) / `story-image`(410) /
  `text-jpn`(3)，共 ~228 MB。**此后"原图/原文件"一律从这里取**，绝不从游戏目录量
  （上轮尺寸漂移 160→164→154 的根因）。
- [x] **`sub_5073B0` 参数映射破案** ✅ `a4` 是 `__int64` 占两栈槽，调用点按 17 个 4 字节压栈
  （`add esp, 44h` 实锤）。逐条 push 还原得 **`a7=0`（不居中）、`a8=a9=1.0f`**。
  ⇒ 「X/Y 是图片中心」的旧结论作废；`sub_53B400` + `title_menu_00…71` 属**旧版遗留资源**。
- [x] **原稿墨迹本来就不居中** ✅ 18 张实测 dx ∈ [−3.5, +2.0]、dy ∈ [−1.0, +4.0]。
- [x] **渲染映射量定** ✅ 缩放 **4/3**（UI 960×540 → 屏幕 1280×720）；画布**宽必须 16 的倍数**
  （136→偏左 16px、168→偏右 6px、40→偏左 5px）；垂直按画布高流式累加。
- [x] **标尺图实验（终局）** ✅ `scripts/menu-ruler.fsx` 刻度图装机读数：
  `_06_off`(256×64) 与 `_07_on`(256×64) 屏幕画布左上角**都是 (37, 349)**；
  `_06_on`(48×40) 居中有位置但**垂直低 4 UI px**。
  ⇒ 引擎按**原版 on 尺寸算死槽位**（给它 256×64 也按 48×40 算 → 右溢 105 UI px），
  **on 画布尺寸绝对不能改**（第 5 版翻车原因）。
- [x] **第六版定稿 + 实机验收** ✅ **用户确认「终于不偏移」「看着不错」**：
  主菜单 01~04 字芯 34 / 描边 1.5；子菜单 05~09 统一小字 字芯 22 / 描边 3(off)·2(on)；
  06~09 的 on 内容上移 4px 补偿。产物墨迹中心全居中，装机 SHA 18/18。
- [ ] **装机完整汉化**（下次开工第一件事）：Steam 还原后目前**只装了 18 张菜单图**，
  237 pac / 35 台词图 / 字库都还是原版 → 跑 `font-rebuild.fsx all` → `verify-patch.fsx`
  → `remake-telop.fsx install`（命令见 `docs/handover.md` §二）。
- [ ] 子菜单「返回」紧挨是**游戏行为**（章节未全解锁时可见项变少），**不要为它改图**。
- [ ] 设置/系统菜单开工前，先用标尺图方法量出它是否同一套槽位规则（`docs/handover.md` §七）。
