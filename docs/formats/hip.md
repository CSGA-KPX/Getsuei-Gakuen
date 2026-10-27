# HIP 专有格式（Arc System Works 纹理容器）

`hip` 有两种子格式，用前 3 字节区分：`"HIP"` 开头为本专有格式，否则是 [TGA type9](tga-type9.md)。
本文档记录专有格式的工具链结论（2026-10-18 初版；同日工具链本地化重写）。

## 1. 现状（✅ 工具链已本地化，GeoArcSysAIOCLITool 已移除）

**自研 HIP 读写工具链**（移植自 `external/external-src/ArcSysLib-master` 源码，逻辑逐行对应）：

- `scripts/hip.fsx` — 纯字节层读写库（`decode`/`encode`/`segsDecompress`），不依赖 ImageSharp；
- `scripts/hip-tool.fsx` — HIP⇄PNG CLI（ImageSharp）：
  - `decode <in.hip> -o <out.png>`：解码为 RGBA PNG；
  - `encode <in.png> -o <out.hip> [--format 32bpp|indexed] [--encoding raw|rawrepeat] [--palette-from <ref.hip>]`：
    默认 32bpp Raw（与旧外部工具行为一致，实机验证过）；
  - `batch-decode <dir|pac> [-o <outdir>]`：**直接扫目录/pac（含嵌套 pac，depth ≤3）批量出 PNG**，
    静默覆盖、无交互询问（对照旧外部工具的最大坑：输出已存在弹 Console.ReadKey 崩溃）；
  - `info <in.hip>`：头信息。

验证结论（2026-10-18，本地 vs 已移除的 GeoArcSysAIOCLITool 交叉验证）：

- **解码**：全量 **1055/1055 成功**（外部工具当年 1053/1055；多出的 2 个是 42B「空壳」，实为
  16×16 纯色占位图 `1D1D1DFF`，本地正确解码，外部工具存 PNG 时失败）。
- **像素级一致性**：抽样 37 个（覆盖全部 3 种 格式×编码 组合）与外部工具 PNG **逐像素一致**；
  本地编码产物交外部工具解回 12/12 一致（编码器交叉验证）。
- **roundtrip 回归**：`scripts/roundtrip-all-hip.fsx` 全量 **1055 文件**：Raw 严格逐字节一致；
  RawRepeat 2108/2109 一致 + 1 SKIP（见 §1.1）。
- 移植但本游戏 **0 文件使用**的代码路径（留作兼容）：SEGS 压缩块、Key、RawSignRepeat、
  8bpp 索引解码、layered 图层头、大端。

### 1.1 RawRepeat 截断陷阱（⚠️ 实战必读）

解码器循环上界是 `已消费字节 < 图幅裸字节数`（源实现如此，游戏内建解码器大概率同款）：
**当 RawRepeat 编码流 ≥ 裸图幅时，尾部像素会被截断为 0**。`saveui/slot_base.hip`
（128×64 Argb32，32800B ≈ 裸大小）即因此尾部像素本为截断零，属格式固有行为而非解码错误。

实战推论：

- 编码 **优先用 Raw（0x8）**——流长恰好等于图幅，不触发截断；
- RawRepeat 只用于**游程多的图**（编码流明显短于裸数据，如字库/纯色底图）；
  `hip-tool encode --encoding rawrepeat` 检测到流 ≥ 裸数据会打印警告。

### 1.2 jpn_glyph.hip 实为 Gray16（新发现 ✅）

`data\text\jpn\jpn_glyph.hip` = **2048×2048、16bpp 灰度、RawRepeat**（2.6MB）——
之前以为的「压缩」只是游程打包。全游戏 1055 个 HIP 的 格式×编码 分布：

| 组合 | 数量 | 说明 |
|------|------|------|
| Gray16 + RawRepeat | 13 | jpn_glyph + pac 内嵌 font（字形灰度掩膜） |
| Argb32 + RawRepeat | 963 | UI 图主力 |
| Argb32 + encoding 0 | 79 | encoding 字节 0（非 0x8），按 RawColors 无游程解码（外部工具 default 分支同款） |

**全游戏无 8bpp 索引 HIP**。`encode --format indexed`（Octree 量化 ≤256 色 + 首次出现序调色板）
是本地新增能力：jpn_glyph 量化 14 色后 RawRepeat 仅 **1.7MB < 3.6MB 上限**——
jpn_glyph 字库扩充的体积瓶颈有了解法 ⚠️ 但「8bpp 索引替换 Gray16 是否被游戏接受」未实机验证，
且量化有损字形灰阶，用前需实机对比。

## 2. 头结构（已验证 ✅，移植自 ArcSysLib 源码）

```
+0x00  "HIP\0"
+0x04  u32  0x125（=293；首字节为 0 → 整文件大端，本游戏全部小端）
+0x08  u32  FileLength（回填总长）
+0x0C  u32  ColorRange（调色板色数；0 + 8bpp = MissingPalette → 色0透明其余黑）
+0x10  i32  CanvasWidth / +0x14 CanvasHeight
+0x18  u8   像素格式：0x1=8bpp索引 / 0x4=16bpp灰度 / 0x10=32bpp(BGRA字节序) / 0x40=16bpp RGB565
+0x19  u8   像素编码：0x8=Raw / 0x1=RawRepeat / 0x2=Key / 0x10=RawSignRepeat / 0x20=RawCanvas
+0x1A  u8   layeredImages（图层数，0=无）
+0x1B  u8   extraParams（0x20=RenderableLayers）
+0x1C  u32  layerHeaderSize；HeaderSize = 0x20 + layerHeaderSize
+0x20  [layerHeaderSize]  若 RenderableLayers 且 layeredImages≠0：i32 ImageW/H、OffsetX/Y + 余量
       索引格式：HeaderSize 起 ColorRange×4 字节调色板（BGRA）
       像素数据：调色板后；若遇 "segs" 魔数则为 SEGS 压缩块（Deflate 分块），解压后大端读取
```

解码细节（全部移植并全量回归 ✅）：

- RawRepeat：像素(Bpp) + u8 重复次数（0=跳过）；编码器单 run 上限 255（源实现 quirk 保留）。
- RawCanvas / extraParams==0：像素按画布尺寸解码，输出时再按图像尺寸从左上角裁
  （对齐 C# CreateBitmap+Clone 行为）。
- Key / RawSignRepeat：本游戏 0 使用，逻辑移植自源码未实测 ⚠️。

```powershell
# 解码
dotnet fsi scripts/hip-tool.fsx decode <in.hip> -o <out.png>
# 编码（默认 32bpp Raw）
dotnet fsi scripts/hip-tool.fsx encode <in.png> -o <out.hip>
# 批量：扫目录（递归 pac 内嵌 + 散装）
dotnet fsi scripts/hip-tool.fsx batch-decode "<游戏安装目录>\...\data\AE" -o work\hip-png
```

## 3. 游戏内 HIP 专有格式分布（2026-10-18 全量扫描）

| 位置 | 数量 | 内容 |
|------|------|------|
| `data\story\image\` | **410 个散装 `.hip`（扩展名 .hip，内容为 TGA）** | 台词图、标题菜单图、logo 等**现役 UI 图** |
| 237 个 text pac 内嵌 font.hip | 13 | 对话字库（其余 224 为 TGA） |
| `data\text\jpn\jpn_glyph.hip` | 1 | 全局 UI 字库（**装载缓冲 <3.6MB**，§1） |
| AE / saveui 嵌套图像 | 1041 | UI 图集中于此（见下） |

**更新后 AE 目录**：`adv`（chapter_title_*）/ `backlog` / `cg_thumb` / `common`
（base_bg、g_dialog 对话框按钮、nowloading、fade）/ `option` / `special`
（between_the_open：chara_name/book/CG_frame/Icon/BGM_frame 等 216 图）/
`system_menu` / `title`。

> ⚠️ 2026-10-18 记录（**2026-10-19 已推翻，见下**）：曾认为**更新后大量文字 UI 改为实时渲染**——标题主菜单（はじめから…）、
> 剧情日期卡（1月9日（月））打补丁后游戏无任何变化（当时判定为遗留资源：title_menu_*、
> chapter_title_date_*）。**498 张文字图候选（ae-image-inventory.json，更新前生成）需按
> 「是否仍被渲染」重新甄别**；已确认现役的图像类 UI：对话框按钮（g_dialog_botan_*）、
> 开场图像（between_the_open）、存档界面（saveui）、角色名（chara_name_*）、
> 选项/日志界面（options_screen/back_log）。当时认为菜单**文字**本体走 Font00/GDI 实时渲染。

> ✅ **2026-10-19 修正（推翻上面「实时渲染」的结论）**：
> `data\AE\title\UI_02_title.pac` → `tex.pac` 里的 `title_menu_*.hip` **确认就是菜单文字图**
> —— はじめから／つづきから／オプション／スペシャル／1章・2章・3章／戻る，每项**普通+高亮两态**，
> 尺寸 256×64；另有 `title_start*.hip` 是 PRESS ANY BUTTONS（含逐字母动画 15 张）。
> 当前目录已是**双层嵌套结构**（旧结构是 `AE\UI_02_title\tex.pac`），当年「打补丁无效」
> 很可能是**改错了旧路径**。**待实机复验**：把 `title_menu_00.hip` 涂品红看菜单第一项是否变色。
> 详见 [../tables/ui-text-source.md](../tables/ui-text-source.md) §5。
> 该批结论同时波及 `chapter_title_date_*`（40 张日期卡，台账已标「待复验⚠️」）。

> ⚠️ 注意 `usual\UI_usual_jpn.pac` / `UI_usual_cmn.pac`（更新前结构）为 192 字节空壳，
> 更新后已不存在。

## 4. 对汉化工作流的意义

1. **台词图重制**（`story\image` TGA）：用自研 [tga-type9](tga-type9.md) 工具链，已全通
   **且实机验证通过**（smoke-test C，品红边框上屏 ✅）。
2. **图像类 UI 重制**（现役：g_dialog 按钮、chara_name、saveui、between_the_open 等）：
   自研 hip-tool PNG⇄HIP + 嵌套 pac 回写；实机验证见 smoke-test D（待终验）。
3. **菜单/日期卡等文字本体**：~~实时渲染（Font00/GDI 路径）~~ → **2026-10-19 修正为预渲染图**（`title_menu_*.hip` 等），走图像重制路线；原记录保留供对照：
   [abc-hip-font](abc-hip-font.md) §2.6 Font00 异构格式解析，重制图路线不适用。
4. **字库扩充**（jpn_glyph.hip + 13 个 pac 内嵌 HIP font）：HIP→PNG 后按 abc 矩形
   定位字形位图，绘制中文 glyph 后 PNG→HIP 回写（`--palette-from` 可复用原调色板）。
   jpn_glyph 须控制在 3.6MB 以内（§1）；**体积解法见 §1.2 indexed 量化（⚠️ 待实机验证）**；
   内嵌 HIP font 无此限制（ADV 路径无尺寸断言）。
