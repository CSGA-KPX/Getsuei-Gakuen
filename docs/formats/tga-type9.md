# TGA type9 变体（预渲染台词图 / UI 图）

`data\story\image\*.hip` 与 AE/UI pac 内嵌的 `.hip` 中，**非 `"HIP"` 开头**的文件都是
TGA type9（RLE 压缩 256 色索引图）。本文档记录编解码实现与逆向出的原版工具行为。

工具：

| 工具 | 用法 | 状态 |
|------|------|------|
| `scripts/tga.fsx` | 编解码库（TgaLib：decode/encode/encodeWith/encodeRleVariant） | ✅ roundtrip 全量验证 |
| `scripts/decode-tga.fsx` | `dotnet fsi decode-tga.fsx <in> <out.png>` | ✅ |
| `scripts/encode-tga.fsx` | `dotnet fsi encode-tga.fsx <in.png> <out.tga>`（PNG→256色量化→type9） | ✅ 端到端验证 |

## 1. 文件布局

```
偏移            结构                说明
────────────────────────────────────────────────────────
0x00            Header              18 字节
0x12            调色板              cmapLen × 4 字节，BGRA 顺序
...             RLE 索引数据
文件尾-26       页脚                见 §4
```

Header（18B）：`idLen, cmapType=1, imgType=9, cmapStart(u16), cmapLen(u16), cmapBits=32,
xOrigin(u16), yOrigin(u16), w(u16), h(u16), depth=8, desc`。
本游戏全部实测：`cmapStart=0, cmapLen=256, cmapBits=32, depth=8, desc=0x08`（bottom-up，每像素 8 属性位）。

## 2. RLE 包格式与原版包化算法（✅ 逆向验证）

- header byte ≥ 0x80：重复包，`count = hb - 0x7F`，后跟 1 字节索引；
- header byte < 0x80：原始包，`count = hb + 1`，后跟 count 字节索引。

原版工具包化规则（由 00_0-01_01_001.hip / ed_c2_image02.hip 包序列逐包比对逆向）：

1. 包起点处 run ≥ 2 → 发重复包（run 上限 128）；
2. 否则累积原始包，**条件：下一像素 != 包内最后一个像素**（等价于在下一个 run 的
   第二个像素处截断，run 的首像素并入原始包）或满 128；
3. 存在**两个变体**，唯一差别在数据末尾：
   - 变体 A（`encodeRleVariant false`，00_* 文字图工具链）：末尾 run 正常发重复包；
   - 变体 B（`endRunAsRaw=true`，ed_* 等另一版工具）：**末尾 run 恰好延伸到数据末尾时
     不发重复包**，拆成单像素原始包（ed_c2_image02 末尾 `250,250` → `W1 W1` 而非 `R2`）。

全量 410 个 image 目录文件 roundtrip：308 个变体 A 逐字节一致、102 个变体 B 逐字节一致、0 失败 ✅。
roundtrip 时两种变体都试（先 A 后 B）；**新制图一律用变体 A**。

## 3. 调色板与像素

- 调色板 256 × 4 字节，**BGRA** 顺序（R 在 +2，A 在 +3）。
- 索引按文件存储顺序存放；`desc bit5=0`（bottom-up）时首行是图像**底行**，解码需翻转。
- `encode-tga.fsx` 新图约定：调色板按出现顺序收集、不足 256 补 `BGRA=0000`，
  Wu 量化到 ≤256 色，bottom-up 写出。

## 4. 页脚（26 字节，✅ 已验证）

```
4B  扩展区偏移 = 0
4B  尺寸字段：画布原始尺寸的宽高分别向上取 2 的幂，u16 LE 各一
    （原版工具会对图裁剪；被裁剪过的图该字段保留裁剪前画布尺寸，如 018a_01：
     图 1224×256，字段 2048×1024。新图直接写 next-pow2(w), next-pow2(h)）
17B "TRUEVISION-XFILE."
1B  0x00
```

⚠️ 未知：该尺寸字段是否被游戏读取（推测仅工具元数据，游戏按 w/h 字段渲染）；
少数文件（ed_* 系）RLE 区在 w*h 像素后无残留，部分其它文件曾有 `00 00` 终止包残留
——`TgaLib.decode` 将 RLE 与页脚之间的字节存入 `RleTrailing` 原样写回。

## 5. 头中其它字段

- `xOrigin / yOrigin`（u16 各一）：子图在原始画布中的位置（裁剪残留元数据），
  roundtrip 原样保留，新图写 0。
- `Id` 段（idLen > 0）：本游戏全部实测为 0。

## 6. 台词图（テロップ）重制（✅ 2026-10-19 完成，35 张）

`data\story\image\` 里含文字的两批共 **35 张**（不是早期估计的 36）：
`00_0-01_01_0*` 25 张（开场白）+ `10_1-06_01_0*` 10 张（10 章演出）。全部 1024×128、透明底。

**原版版式（实测 ✅）**：

| 项目 | 实测值 |
|------|--------|
| 填充 / 描边 | 白色填充 + 黑色描边（外扩约 3.5px，笔画笔宽 ~7） |
| 正文行带 | 行带 1 墨迹 y 20..63、行带 2 y 84..127（单行图居中于所属行带） |
| ruby 注音 | 小字在正文上方 y 1..21（`<ruby>` 句才有），中文译文无 ruby → 留空 |
| 字宽 | 日文等宽约 35.3px/字（001 行宽 355 / 10 字） |
| 每图显示几行 | 由**原图墨迹行段**决定（见下） |

**逐帧图的行结构不是 `_1`/`_2` 后缀能简单套用的**（原图有三种玩法）：
1. `_1` 只显示第一句、`_2` 只显示第二句（如 010/017/019/024/025/026）；
2. `_1` 只显示第一句、`_2` 把两句**合并成一行**重新排版（021/022）；
3. `_1` 同时显示两行、`_2` 只剩第二行（`10_1-06_01_035`）。

判定方法（`scripts/remake-telop.fsx` 实现）：把原图按行投影，用阈值（0.3×max 墨迹像素数）
切出**正文行段**（自动剔除 ruby 小字行）；1 段时再结合 `_1`/`_2` 后缀与墨迹宽度
（÷35.3 ≈ 字数）判断是「第一句 / 第二句 / 两句合并」。

**工具**：`scripts/remake-telop.fsx`（GDI+ `GraphicsPath` 描边：`DrawPath` 黑 pen 外扩 +
`FillPath` 白填充 → Wu 量化 → TGA type9）

```powershell
dotnet fsi scripts/remake-telop.fsx all       # → work/telop-png/*.png（预览）+ work/patch-image/*.hip
dotnet fsi scripts/remake-telop.fsx install   # 写回游戏目录（原文件自动备份到 work/backup/story/image）
```

文本源：`work/atf-cn`（译文），缺失时退回 `work/atf` 原文；中文行超宽时按比例缩字号
（留边 30px）。字体 `font/华康新综艺w7.ttf`（台词图是**美术大字**，用粗综艺体更醒目；
与对话字库现在用的华文细黑刻意区分开 —— 原版台词图本身也是手写圆体、与正文不同宗）。

## 7. 已知与未知

已确认 ✅：全部布局字段、RLE 双变体、页脚结构、410 文件 roundtrip 逐字节一致、
PNG→TGA→PNG 端到端（`00_0-01_01_001` 中文台词图重制链路已打通）；
**35 张台词图中文重制并已装机（2026-10-19），待实机确认**。

待确认 ⚠️：页脚尺寸字段与 origin 的游戏侧语义（改新图时不写是否安全——目前按原版
惯例写出）；`encode-tga.fsx` 的 Wu 量化对渐变/抗锯齿较多的图可能有轻微色偏（文字图
纯色描边场景无影响）。

