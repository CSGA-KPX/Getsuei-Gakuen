# 月英学園 -kou-（月英学园）汉化项目 — 工具链与研究成果

对 Steam 游戏 **《月英學園 -kou-》（Getsuei Gakuen -kou-）** 的中文本地化项目。
本仓库收录的是**工具链（F# / .NET 10 脚本）与逆向 / 格式研究成果文档**，
供有同样需求的人复用整条技术路线。

> ⚠️ **本仓库不包含任何游戏资源文件**（pac / hip / atf 等二进制均不入库），
> 也不包含翻译成品数据（译文 JSON、补丁产物都在 `.gitignore` 排除的 `work/` 目录中，未公开）。
> 所有工具运行时读取**你自己本地已安装的游戏文件**。

## 技术栈与仓库结构

- **语言 / 运行时**：F# + .NET 10（`dotnet --version` ≥ 10）。小工具为 `.fsx` 脚本（`dotnet fsi` 直接跑），
  重型工具（atf ⇄ JSON、ADVScript ⇄ JSON）为 `scripts/AtfJson`、`scripts/AdvScriptParser` 控制台项目。
- `docs/overview.md` — 项目总览与文本显示链路（**必读**）
- `docs/formats/` — 逆向定稿的格式文档：pac（打包）、atf（对话文本）、advscript（事件脚本）、
  abc-hip-font（字库索引+位图）、tga-type9（预渲染台词图）、hip（HIP 专有图像格式）、staff-roll（制作名单）
- `docs/tables/` — 各类清点台账（字库容量、文本清单、UI 图像甄别、ruby 注音等）
- `docs/re-notes.md` — 可执行文件文本渲染逆向笔记（IDA 定位手册）
- `scripts/` — 约 80 个工具脚本，覆盖解包 / 重打包 / 字库重建 / 图像重制 / 回归验证全流程
- `AGENTS.md`、`TODO.md`、`docs/handover.md` — 开发过程的原版记录（含历史踩坑）

## 核心机制一句话

文本显示链路为 `string → char[] → glyph 索引 → hip 位图字形 → 上屏`：
atf 里文本是 UTF-16LE（可直接写中文），但游戏按 char code 查 abc 映射、
再从 hip 位图集取字形——**原字库只有日文，中文字形必须扩充 abc/hip 字库**；
而 UI 文字大多是**预渲染图片**，改图即可汉化（已用探针图实机证实）。

## 快速开始

前置条件：

1. Windows + .NET 10 SDK；
2. 通过 Steam 安装的游戏本体（脚本对游戏目录只读，产物写入本仓库的 `work/`）；
3. **自备字体**（版权原因不入库，见下）。

然后：

```powershell
# 1. 把各脚本中的 <游戏安装目录> 替换为你的实际安装路径（批量替换即可）
# 2. 台词图 / 标题菜单重制需要华康新综艺字体：
#    将「华康新综艺w7.ttf」放到 font/ 下（或修改 remake-telop.fsx / remake-title-menu.fsx 里的路径）
#    ——对话字库与 UI 图使用 Windows 自带的 C:\Windows\Fonts\Deng.ttf（等线），无需准备

# 例：解包一个对话 pac
dotnet fsi scripts/pac-unpack.fsx "<游戏安装目录>\data\story\text\jpn\text_10_1-01.pac" -o work\text_10_1-01

# 例：atf 对话文本 ⇄ JSON
cd scripts/AtfJson && dotnet run -- parse -i <in.atf> -o <out.json>

# 例：格式回归验证（无需游戏改动，只读）
dotnet fsi scripts/roundtrip-all-pac.fsx    # pac 全量 roundtrip
dotnet fsi scripts/roundtrip-all-hip.fsx    # HIP 专有格式全量回归
```

各工具用法见 `AGENTS.md` 的「目录结构」与 `docs/overview.md` 的「工具索引」。

## 进度总览

### ✅ 已完成（均含实机验收或全量 roundtrip 验证）

**格式与工具链**（全部 `bin → 中间态 → bin` 逐字节 roundtrip 通过）

- pac 打包/解包器：2201 个文件全量 roundtrip 一致；
- atf 对话文本 ⇄ JSON：双向无损；237 个对话 pac（66535 条记录 / 约 109 万字）全量提取；
- ADVScript 事件脚本 ⇄ JSON：双向无损；
- abc 字库索引（含异构 Version 5 / Font00）：运行时语义已由 IDA 逆向确认，重建全量通过；
- hip 位图字库 + HIP 专有图像格式：自研读写工具链（移植 ArcSysLib 思路），1055 文件解码 + 回归通过；
- TGA type9 变体（预渲染台词图）编解码：410 文件 roundtrip 一致（含 RLE 双变体）。

**翻译**（GalTransl 作为外部工具使用，仅桥接数据格式）

- 对话译文 220 / 237 个 pac 完成（其余 17 个为开发测试文本等，判定不译）；
- 术语一致性核查通过（186 个日文说话人名，一对多译名 = 0）；
- 行宽修复完成：游戏单行上限 24 全角、不自动折行——自动重排 251 条 + 人工改写 269 行，复查 0 超宽；
- ruby 注音 635 个标签逐条判定，53 处修复并验证。

**字库**（汉化核心难点）

- 237 个 pac 内嵌字库全部替换为中文超集字库（按 Unicode 码位直接映射），实机验收通过；
- 全局 UI 字库 jpn_glyph v4 重建（Gray16 + 统一字号 + 需求集裁剪），hip 3.40 MB / 上限 94.5%，已装机；
- 中文字形生成管线（GDI+ 统一渲染、34×49 固定格、白字黑边 + 投影）。

**图像**

- 35 张预渲染台词图中文重制（白字黑边美术字，TGA type9 回写），实机验收通过；
- 18 张标题菜单图（9 项 × on/off 两态，六轮迭代定稿：画布宽须 16 倍数、on 图尺寸必须保持原版等
  引擎槽位规则均已实测定论）；
- 174 张 UI 文字图重制（系统菜单 24 / 设置 31 / 回想 5 / 通用对话框 37 / 开放之间 76），已装机；
- 实机验证 A/B/C/D 四组补丁 + 10/10 探针图全部 PASS（证实 UI 文字是预渲染图而非实时渲染）。

**回归**

- 全量回归流水线：237/237 pac「伪翻译 → 重打包 → 回解」逐条校验一致。

### ⬜ 未完成

- **最后一轮实机验收**（2026-10-20 装机后待确认，需完全退出游戏再进）：
  对话字库 1px 细线是否消失、存档界面中文是否正常、174 张 UI 图的文案/字号/位置；
- `between_the_open`（开放之间）剩余图像的逐张核对（动画帧 / 数字 / `???` 等按台账跳过，
  含文字的 76 张已重制）；
- `staff_roll` 制作人员名单未译（CSV 字段语义仍属推断，见 `docs/formats/staff-roll.md`）；
- `text_voice_test` 中说话人名「マスター」未译（2 条，开发残留）；
- 一键还原脚本未写（低优先级——Steam「验证文件完整性」即可还原）；
- 技术债：abc rec32 尾 16B 语义、ADVScript Flag 字段语义、TGA 变体 B 中间包行为等
  （清单见 `TODO.md`「技术债」）。

### ❌ 无法完成 / 决定不做

- **Steam 成就界面文本**：文本存储在 Steam 后台而非游戏文件，修改 exe 无效——技术上无解；
- **字库容量硬上限**：引擎装载缓冲断言限制 `jpn_glyph.hip < 3.6 MB、abc < 600KB`
  （`sub_510050` 逆向确认）。中文需求集与容量冲突时只能裁剪——v4 丢弃了 695 个 CJK 日文汉字
  （漏字仅显示空白不崩溃，清单当时存于 `work/inventory/glyph-drop-list.txt`）；
- **Font00 扩充**：破案后确认菜单文字全是预渲染图，Font00 字库无需扩充（且其 289 格上限极小）；
- **`title/UI_02_title.pac` 内 34 张图**：旧版遗留资源，实机验证改图无效，无法生效；
- **`UI_29_nowloading` 艺术字、`system-menu_moji_*` 模糊动画帧、光斑层**：用户判定不做；
- **游戏不自动折行**：单行 24 全角字符是硬上限，长句只能靠人工断行（已建立工具链应对）。

## 字体说明（自备）

| 用途 | 字体 | 来源 |
|------|------|------|
| 对话字库、UI 文字图、jpn_glyph | 等线 `Deng.ttf` | Windows 自带（`C:\Windows\Fonts\`） |
| 台词图、标题菜单图 | 华康新综艺 W7（`华康新综艺w7.ttf`） | **商业字体，需自行购买/获取**，放到 `font/` 下 |

字体版权归原厂商所有，本仓库不以任何形式再分发字体文件。

## 法律与安全声明

- 本仓库不包含、也不协助获取游戏资源文件；所有工具仅读取使用者本地合法安装的游戏。
- 游戏《月英學園 -kou-》及其全部资源的版权归原著作权人与发行商所有，本项目与官方无关，仅供学习研究。
- 本项目未附带担保，见 [LICENSE](LICENSE)（Unlicense，公有领域）。
- 代码与文档已做公开化处理：移除本机绝对路径（以 `<游戏安装目录>` 占位）、移除编辑器/代理配置、
  不含任何密钥或个人信息，并经扫描确认。

## 致谢

- [ArcSysLib](https://github.com/TheKins/ArcSysLib) / GeoArcSys 系列工具：HIP/PAC 格式参考
  （本仓库 `scripts/hip.fsx` 为据此思路重写的 F# 实现）；
- [GalTransl](https://github.com/GalTransl/GalTransl)：翻译流程外部工具（未引用其代码，
  仅链接见 `docs/galtransl/README.md`）；
- [Rea](https://github.com/morluto/rea)：逆向引擎。
