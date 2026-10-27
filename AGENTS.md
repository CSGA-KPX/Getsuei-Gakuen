# AGENTS.md — 月英学園 -kou- 汉化项目

## 项目目标

提取 `<游戏安装目录>\` 的文本与资源，翻译为中文后重新打包。
**先读 [docs/overview.md](docs/overview.md)**，格式细节都在 `docs/formats/` 下。

## 硬约束

- 语言/运行时：**F# + .NET 10**（`dotnet --version` → 10.0.401）。小工具写 `.fsx`（`dotnet fsi`），复杂工具写控制台项目。
- 游戏路径只读；所有产物写到工作区内（解包输出、JSON、中间文件）。
- 修改游戏文件前先备份原文件（pac 级备份即可，如 `xxx.pac.bak`）。

## 目录结构

```
docs/
  overview.md              项目总览、文本显示链路、文件位置、通用约定（必读）
  ida-mcp.md               ida-pro-mcp 使用经验：环境坑、调用方式、Getsuei.exe 分析基线
  re-notes.md              Getsuei.exe 文本渲染逆向笔记：函数地址/类名/追踪技巧（IDA 定位手册）
  formats/
    pac.md                 pac 打包格式（打包已实现，2201 文件 roundtrip ✅）
    atf.md                 对话文本格式（汉化主战场，双向无损已验证）
    advscript.md           事件脚本格式（双向无损已验证）
    abc-hip-font.md        字库格式（abc roundtrip ✅；含 §5 运行时逆向、§2.6 Font00 异构格式）
    tga-type9.md           预渲染台词图 TGA type9（编解码全通，roundtrip ✅）
    hip.md                 HIP 专有格式（自研读写工具链已本地化 ✅，全量 1055 回归通过）+ UI 文字图分布
    staff-roll.md          制作人员名单 CSV（SJIS，语义推断 ⚠️）
  tables/                  **台账**（清点结果，人工/脚本共建，改动请同步重跑对应 scratch 脚本）
    ae-ui-images.md        AE/UI 图像台账：396 模式 / 1011 文件，含「是否含文字 + 现役/遗留」判定
    ui-images-triage.md    **UI 图甄别定论**：852 张 × (exe + aex 名字表) × procmon → A607/B22/C223；
                           §7 = 探针实证「文字是预渲染图」+ 各界面重制清单
    ruby-triage.md         **ruby 注音台账（2026-10-20）**：635 个标签逐条判定（写A读B/着重号）
                           + 53 处已修（§6 执行记录，scripts/fix-ruby.fsx）
    font-coverage.md       字库覆盖台账：三套字库容量 vs 译文用字需求 + 两个硬约束（jpn_glyph 3.6MB、Font00 289 格）
                           **§6/§7 = jpn_glyph 重建三轮踩坑与第四次方向（未定稿）**
    text-inventory.md      翻译文本台账：atf 209/237 已译 + 未译清单分类 + UI 文本未清点
    ui-text-source.md      **UI/菜单文本定位**：在 exe .rdata（UTF-16LE，0xD80000-0xDD0000）+ IDA 硬结论
scripts/
  AtfJson/                 atf ⇄ JSON（dotnet run -- parse/build）
  AdvScriptParser/         bin ⇄ JSON（dotnet run -- dump/restore/verify/info）
  atf-batch.fsx            批量 atf→JSON 提取 / 文本量统计（extract/stats）
  gen-ending-cn.fsx        结局回想菜单标签手工翻译（13 条映射表内联）→ work/atf-cn-manual/
  galtransl.fsx            atf-json ⇄ GalTransl JSON 桥接（export/import/verify，'
                           纳入规则与匹配回退见 docs/galtransl/galtransl-pipeline.md）
  font-rebuild.fsx         字库整体重建：全量重渲染 2048² 图集（**等线** Deng.ttf，GDI+）+
                           中文新字形追加 + abc 重建 + atf 注入 → work/patch-pac/
                           （字形统一基线 + bbox 水平居中；TGA 图集"白字+2px 外描边（右下投影）"双色调色板；
                            破折号自绘细横线且步进强制 34；
                            **列间隔 1px**（`CELL_STRIDE`）：定宽格紧排时笔画宽的汉字墨迹会与相邻字形
                            连成 1px 竖线，代价是容量 2460 → 2378 格，仍够）
  remake-telop.fsx         预渲染台词图中文重制：atf 译文 → 白字黑边美术字（1024×128）
                           → TGA type9 → work/patch-image/；install 写回游戏（自动备份）
  Pac.fsx                  pac 读写库（#load 用：read/readBytes/build）
  pac-unpack.fsx           pac 解包（-o 输出目录，--only .abc,.hip）
  pac-pack.fsx             pac 重打包（verify 内存回归 / pack 目录回包）
  pac-search.fsx           目录内字节模式搜索（--hex）
  tga.fsx                  TGA type9 编解码库（#load 用，含 RLE 双变体）
  decode-tga.fsx           TGA type9 → PNG
  encode-tga.fsx           PNG → TGA type9（Wu 量化 256 色）
  abc.fsx                  abc 字节级解析/重建库（#load 用；⚠️ 旧 56B 布局，语义用 smoke-patch.fsx 的 abcParse）
  abc-analysis.fsx         abc 分析（info/dump/glyphs/mapping）⚠️ 同上旧布局
  abc-overlay.fsx          字形矩形叠加 PNG（需 ImageSharp nuget）⚠️ 同上旧布局
  roundtrip-all-pac.fsx    全量 pac roundtrip 回归
  roundtrip-all-abc.fsx    全量 abc roundtrip 回归
  roundtrip-all-tga.fsx    image 目录 TGA roundtrip 回归
  roundtrip-all-hip.fsx    全量 HIP roundtrip 回归（1055 文件：解码 + Raw/RawRepeat 重编码）
  ae-rescan.fsx            重扫 data\AE（**双层嵌套 pac 递归展开**）→ work/inventory/ae-image-inventory-v2.json
  ae-ledger.fsx            AE/UI 图像台账 → docs/tables/ae-ui-images.md（含文字判定 + 现役/遗留）
  font-rebuild-jpn.fsx     **jpn_glyph 重建（v4 定稿）**：UI 全局字库，存档界面等动态文本用它。
                           需求集 = `jpn_idlist.txt`（UI 固定文本表）∪ 译文 ∪ 脚本 ∪ ASCII；
                           保留原非 CJK 字形（按原框宽）+ 新增汉字（Deng.ttf 统一字号 32×39）；
                           **必须 Gray16 + RawRepeat**（Indexed8 实机变黑）；
                           `--levels N` 灰度量化（8 级 → 3.40 MB / 94.5%）、`--keep-all` 对照；
                           `--apply` 写 work/patch-jpn，`--install` 装机（备份 work/backup/text/jpn/）
  preview-font.fsx         **字库离线排版预览**：按引擎语义（mapping → rec32 → 步进）把文本渲染成 PNG，
                           装机前查「缺字形 / mapping 偏移一位 / 字面大小不一」
  font-ledger.fsx          字库头部摘要（EntryCount/GlyphCount/LineHeight）三套字库对比
  font-capacity.fsx        图集容量评估（格子数/已用/剩余）+ 译文用字需求
  text-ledger.fsx          翻译文本台账 → docs/tables/text-inventory.md（已译/未译 + 分类）
  exe-ui-strings.fsx       从 Getsuei.exe 提取 UTF-16LE 日文串（四层判据+聚类）→ work/inventory/exe-ui-strings.json
  exe-ui-crosscheck.fsx    与 docs/steam_achievements.txt 对账，分离「Steam 成就」与「其他 UI 文本」
  exe-resource-names.fsx   **从 exe 提取 UI 资源名字表**（2495 条，含 %02d 格式串）
                           → work/inventory/exe-resource-names.txt；甄别现役/遗留的第一判据
                           （取代早期 dump-resource-names.fsx 的两个固定偏移池）
  aex-names.fsx            **从 AE 各 pac 内嵌 .aex 提取纹理名表**（1663 条/1104 唯一图名）
                           → work/inventory/aex-texture-names.txt；UI 图名字其实写在这里
                           （不在 exe 里，这解释了「文字图不在 exe 表」的假象，2026-10-20 ✅）
  ui-image-triage.fsx      AE 852 张 HIP × (exe 名字表 + aex 名字表) × procmon
                           → A(名字+加载)/B(加载)/C(名字)/D(pac 名) → work/inventory/ui-images-verdict.tsv
                           （见 docs/tables/ui-images-triage.md §7；第 5 列 aex 标记名字来源）
  procmon-pac-access.fsx   汇总 work/procmon/Logfile-*.xml（跳过 *_full-*）→ 实机访问路径短名单
                           → work/inventory/procmon-accessed-paths-short.txt
  probe-images.fsx         **UI 文字图探针**：目标 HIP 涂纯色 → 回写 tex.pac/外层 pac（自动备份）
                           list/export/apply [编号] [--pad]/verify/restore
                           `--pad` = 尾部补 0 保持与原文件等长（排除 Length/偏移变量，诊断用）
                           → 2026-10-20 实机已验证 6/7（清单见 ui-images-triage §7.6）
  check-line-width.fsx     **译文行宽检查**：找出超过 24 全角（游戏单行上限）的行 → 溢出边框
                           只报「中文超宽、日文原文没超」的行（原文也超的用宽框，如 text_SP）
                           `--tsv` 写 work/inventory/line-width-issues.tsv（文本内换行已转义成 \n）
  reflow-atf.fsx           **行宽重排**：同一记录内把超宽行的字挪到下一行（行数不变），
                           收尾标点不断行、不删任何字符；`--apply` 才写 work/atf-cn
  apply-line-fixes.fsx     **人工改写应用器**：读 TSV（文件/key/新值），校验「每行 ≤24 全角 +
                           ruby 标签序列不变」后写入 work/atf-cn
  ae-export.fsx            **导出 AE pac 内 HIP 图 → PNG**（自动取 tex.pac；深灰底合成 alpha）
                           `<pac> <成员...>` / `--all` / `--filter 子串` → work/ae-png/<pac 名>/
                           重制 UI 文字图第一步：先看清每张图写的是什么
                           `--rgba` 保留原始 alpha（判断有无边框必须看 alpha，合成后的 PNG 看不出）
  remake-ui-text.fsx       **UI 文字图汉化重制（表驱动）**：`list` / `preview` / `apply` / `restore`
                           解码 HIP → 量 ink/body 双 bbox → 清区填底色 → Deng.ttf 统一字号画中文
                           → 回写 tex.pac/外层 pac（备份 work/backup/AE/）。画布尺寸一律不变。
                           坑见 docs/tables/ui-images-triage.md §8.3（over 合成 / on 态辉光 /
                           胶囊按钮内缩 / 主色取最亮 30%）
  ui-inspect.fsx           **量 UI 图文字 bbox / 边框**（alpha 视角），定字号与清除区的依据
  ui-contact-sheet.fsx     **UI 图拼版对照**（文件名 + 尺寸 + 原图），逐张核对时用
  pac-diff.fsx             **pac 逐字段 diff**（名字/FileId/Length/FileNameHash/内容 SHA，递归内层）
                           `--backup <相对 data\AE 的路径>` 直接对比 work/backup/AE 与游戏目录
                           改完 pac 必跑：结构差异 >0 即退出码非零
  pac-bytediff.fsx         **pac 纯字节 diff**（不经过任何解析器 → 解析器有 bug 时也不假阴性）
                           是 pac-diff 的最终裁判；诊断「到底改了哪些字节」用
  rasm.py                  快速反汇编 Getsuei.exe 指定 RVA（Python + capstone/pefile，秒级）
                           拿到崩溃地址后第一时间看代码；IDA MCP 起不来时的替代
  fix-ruby.fsx             按 work/inventory/ruby-fix-list.tsv 修 work/atf-cn 的 53 处 ruby
                           （P 拼音→语义注音 / D 長音符 / R 去冗余 ruby；幂等，--apply 才写）
'  ida-client.ps1           ida-pro-mcp 极简客户端（-List / -Tool / -Json，会话缓存+自动重连）
  script-text-scan.fsx     扫 scr_*.pac 内 ADVScript 的 SJIS 串（验证菜单文本是否在脚本里）
  exe-sjis-scan.fsx        扫 Getsuei.exe 的 SJIS 日文串（验证菜单文本是否硬编码）
'  title-image-check.fsx    导出 title/UI_02_title.pac 的旧版菜单图（已确认遗留，仅存档参考）
  remake-title-menu.fsx    标题菜单中文重制（9 项×2 态=18 张）→ work/patch-menu/
                           **读 work/pristine/story-image/**；墨迹居中 + 画布宽取 16 倍数
                           + 主菜单字芯 34/描边 1.5、子菜单 off 24/on 25
                           + **on 图画布尺寸必须保持原版**（引擎按原版尺寸算槽位，改了会右溢）
                           （实测：缩放 4/3、画布中心锚点、宽非 16 倍数会整体偏位）
  install-title-menu.fsx   菜单图装机（备份 work/backup/story/image-menu/，SHA 校验）
  install-text-pac.fsx     **237 个对话 pac 装机**（work/patch-pac → data\story\text\jpn；
                           首次覆盖前自动备份到 work/backup/story/text/jpn，逐文件 SHA 校验；
                           `--apply` 才写，`--restore` 从备份还原；不碰 jpn_glyph / Font00）
'  title-menu-compare.fsx   原图 vs 重制对比图 → work/menu-rebuild-compare.png
  audit-backup.fsx         SHA 盘点游戏目录被改文件（只读，用于还原前清点）
  menu-ruler.fsx           **菜单定位标尺图**：生成 256×64 刻度图（边框/四角色块/刻度/十字）
                           → work/patch-menu-ruler/，装机后从整屏截图反解画布位置与缩放
                           （2026-10-19 用它定论：槽位由 off 决定、on 须同尺寸）
  ae-image-scan.fsx        AE/UI 嵌套 pac 图像盘点（旧版，只扫一层，保留参考）
  hip.fsx                  HIP 专有格式读写库（#load 用：decode/encode/segsDecompress，
                           移植自 ArcSysLib 源码，纯字节层）
  hip-tool.fsx             HIP⇄PNG CLI（decode/encode/batch-decode/info；encode 支持
                           32bpp/indexed 与 --palette-from，替代已移除的 GeoArcSysAIOCLITool）
  smoke-patch.fsx          实机验证补丁生成（adv/image/hip/hipimg 四模式，自动备份 work/backup/）✅ 已完成使命
  regression-pipeline.fsx  全量回归流水线（run [pac]：伪翻译→重打包→回解校验，237/237 ✅）
  ida-mcp-run.cmd          IDA MCP 启动脚本
font/                      字体自备（版权原因不入库）：华康新综艺w7.ttf（台词图/标题菜单用），
                           放到本目录或改脚本里的路径；对话字库与 UI 图用 Windows 自带 Deng.ttf
TODO.md                    任务清单（开工前先看）
docs/handover.md           交接文档：项目状态、还原方案、遗留问题（§九 = 2026-10-20 两轮记录）
                            docs/handover-next-session.md = **给下一个会话的浓缩交接文本**
docs/testing/smoke-test.md 实机验证手册（4 项补丁的进游戏观察清单与还原方法）
```

## 核心技术事实（速查）

1. **文本链路**：`string → char[] → glyph[] → 上屏`。atf 里文本是 UTF-16LE（直接写中文 OK），
   但游戏按 char code 查 abc 映射取 hip 字形 —— **中文字形不在原字库，必须扩充 abc/hip**。
2. **编码**：ADVScript 字符串池 = Shift-JIS (932)；ATF key = ASCII、value = UTF-16LE；pac 文件名 = UTF-8。
3. **JSON 原则**：只存真实数据；偏移/索引/表头一律还原时重建；新工具必须自带 roundtrip 验证
   （`bin → json → bin` 逐字节一致，参考 AdvScriptParser 的 `verify` 命令）。
'4. **hip 双格式**：前 3 字节 `"HIP"` → 专有格式（自研 `scripts/hip.fsx`/`hip-tool.fsx` 读写 ✅）；否则 TGA。
   ⚠️ `data\story\image\` 下 410 个 `.hip` **全部是 TGA 内容**（扩展名骗人），含台词图与标题菜单图。
6. **资源可能在「散装文件」里，不只在 pac 里**。判断某图是否现役，看 `work/procmon/Logfile-<日期>.xml`
   （Getsuei.exe 实际访问的路径），比静态搜 pac 可靠得多。游戏根目录 `assert.txt` 是断言日志。
   ⚠️ UI 图的名字**不写在 exe 里**，而在各 pac 内嵌的 `.aex`（ASCII 串 `TEX\xxx.png`）——
   查名字用 `scripts/aex-names.fsx`，别只查 exe 表（2026-10-20 ✅）。
   **UI 的固定文本也不在 exe**，而在 `data/text/jpn/jpn_idlist.txt`（UTF-16LE，ID→文本，
   533 行日文：BGM 曲名 / END 名 / 声优表 / 制作人员 / 界面提示）。字库需求集必须含它
   （2026-10-20 ✅，jpn_glyph 重建的关键依据）。
7. **pac 文件名表按「字节」读**：`BinaryReader.ReadChars(n)` 的 n 是字符数，遇多字节名会错位。
   用 `ReadBytes(StringSize)`，解码顺序「严格 UTF-8 → 失败则 CP932」。
8. **pac 条目名是混编码的（ASCII + 偶发 Shift-JIS），写回必须用原始字节**：
   `PacEntry.FileNameBytes` 原样写入，`FileNameHash` 也沿用原值。
   实例：`options_screen.pac` 内层 `window_frame_Ｌ_light_01..08`（0x82 0x6B = 全角Ｌ），
   用 UTF-8 重新编码会变成 U+FFFD ⇒ 进设置菜单闪退（2026-10-20 实机踩坑）。
9. **改完 pac 的验收顺序**：`pac-diff.fsx --backup <路径>`（逐字段）→ 仍不放心就
   `pac-bytediff.fsx --backup <路径>`（纯字节，不经过解析器，是最终裁判）。
   roundtrip 全绿**证明不了**写回正确 —— 它只验证"读回来→写回去"自洽。'
5. **pac 数据偏移** = `Header.DataStart + FileInfo.OffsetStart`；文件名长度由 `Header.StringSize` 指定。
6. **原版基准在 `work/pristine/`**（Steam 校验后复制的只读副本：`story-text-jpn` / `story-image` /
   `text-jpn`）。任何「原图/原文件」测量一律从这里取，**绝不从游戏目录取** —— 那里随时被自己的产物覆盖，
   上轮菜单尺寸漂移（160→164→154）就是这么来的。

## 常用命令

```powershell
# 解包对话文本 pac
dotnet fsi scripts/pac-unpack.fsx "<游戏安装目录>\data\story\text\jpn\text_10_1-01.pac" -o work\text_10_1-01

# atf -> json -> atf（改完 json 后 build 回 atf）
cd scripts/AtfJson
dotnet run -- parse -i <in.atf> -o <out.json>
dotnet run -- build -i <in.json> -o <out.atf>

# 事件脚本验证
cd scripts/AdvScriptParser
dotnet run -- verify <in.bin>

# abc 字库分析
dotnet fsi scripts/abc-analysis.fsx "<游戏安装目录>\data\text\jpn\jpn_glyph.abc" info

# HIP 专有格式 ⇄ PNG（decode / encode / batch-decode / info）
dotnet fsi scripts/hip-tool.fsx decode <in.hip> -o <out.png>
dotnet fsi scripts/hip-tool.fsx encode <in.png> -o <out.hip> [--format 32bpp|indexed] [--encoding raw|rawrepeat]
dotnet fsi scripts/hip-tool.fsx batch-decode "<游戏安装目录>\data\AE" -o work\hip-png   # 递归 pac 内嵌 + 散装

# 字库重建（237 pac → work/patch-pac）与预渲染台词图重制（35 张 → work/patch-image）
dotnet fsi scripts/font-rebuild.fsx all
dotnet fsi scripts/verify-patch.fsx
dotnet fsi scripts/remake-telop.fsx all        # 台词图渲染 + 预览 PNG
dotnet fsi scripts/remake-telop.fsx install    # 写回游戏（原文件自动备份 work/backup/story/image）
```

## 工作约定

- 文档与代码注释用简体中文；代码标识符/路径/命令保持原文。
- 逆向结论写进 `docs/formats/`，标注「已验证 ✅ / 推测 ⚠️」，不要把推测写成事实。
- 探索性脚本放临时目录或在工作区根下 `scratch/`，完成后清理或归档，不留半成品。
- 大型多轮任务（如全量提取、字库改造）先更新 TODO.md 拆解步骤，再逐步执行。
- **经验落盘**：每次调试通一条工具链/踩过一个环境坑/验证过一个结论后，必须把可复用
  经验写进 `docs/` 下对应文档（格式类→`docs/formats/`，工具与环境类→`docs/ida-mcp.md`
  或新建专题文档），标注「已验证 ✅ / 推测 ⚠️」。经验只留在对话里等于丢失；
  后续会话只从文档继承，不重蹈调试过程。文档同步更新本文件的目录结构。

# 编码原则

- Keep it simple, stupid.
- Entities should not be multiplied unnecessarily.
- Vibe coding 八荣八耻：
  - 以瞎猜接口为耻，以认真查询为荣。
  - 以模糊执行为耻，以寻求确认为荣。
  - 以臆想业务为耻，以人类确认为荣。
  - 以创造接口为耻，以复用现有为荣。
  - 以跳过验证为耻，以主动测试为荣。
  - 以破坏架构为耻，以遵循规范为荣。
  - 以假装理解为耻，以诚实无知为荣。
  - 以盲目修改为耻，以谨慎重构为荣。
