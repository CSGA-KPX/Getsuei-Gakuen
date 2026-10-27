# 字库格式：hip 位图 + abc 索引

游戏的文本渲染链路：`string → char[] → glyph[] → 上屏`。`abc` 文件定义「char code → glyph 索引 → 字形矩形」，`hip` 文件是 glyph 位图集。
**abc 布局以 Getsuei.exe 运行时解析（sub_43F290）为准**（2026-10-18 IDA 反编译确认 ✅，
修正了本文档早期版本按 56B 连续记录解析的错误结论）。

实测对象：`data\text\jpn\jpn_glyph.abc/hip`（全局 UI 字库）、237 个 text pac 内嵌 `font.abc/hip`。
分析工具：`scripts/abc-analysis.fsx`（⚠️ 该工具仍是旧 56B 解析，仅适合字节级 roundtrip，
语义分析请用 `scripts/smoke-patch.fsx` 里的 `abcParse`）。

## 1. hip 位图文件

hip 有**两种子格式**，用前 3 个字节区分：

| 文件头 | 格式 | 处理方式 |
|--------|------|----------|
| `"HIP"` | Arc System Works 专有 HIP 格式 | 用 **hip-tool.fsx**（自研，已本地化）转换为 PNG |
| 其他 | **TGA** | 通用图像工具即可处理 |

（`jpn_glyph.hip` = HIP 专有格式，2048×2048；237 个 text pac 内嵌 font.hip 中 224 个为
TGA type9（256 色调色板 + RLE），13 个为 HIP 专有格式。`data\story\image\` 的台词 .hip
是 TGA type9 变体，见 [tga-type9](tga-type9.md)。）

### 1.5 图集调色板语义与字形排版（✅ 实机反馈修复，2026-10-19）

**TGA 子格式的调色板 = 白字 + 黑描边的双色表**（原版 text_10_1-01/font.hip 按**使用次数**重测）：

```
索引    内容                          实测像素数（字形像素共 15 万）
  0     (0,0,0,0)      背景透明         373806
 196    (0,0,0,255)    黑色描边          66131  ← 出现最多的字形条目
 211    (255,255,255,255) 白色字身        30881
197-210 (灰度,255)      字身抗锯齿渐变     2.7 万
  1-140 (0,0,0,alpha)  少量（边缘量化残留） ~1.5 万
```

⇒ **原版字库字形是「白字 + 约 2px 黑描边」，背景全透明**；游戏按标准 alpha 混合
（`out = rgb*a + dst*(1-a)`，颜色取调色板 RGB）。因此正确实现是让调色板承载两种颜色：

| 索引 | 调色板 | 图集写入 |
|------|--------|----------|
| 0 | `(0,0,0,0)` | 背景 |
| 1..127 | `(0,0,0, i*255/127)` 黑描边（alpha 渐变，抗锯齿用） | 描边覆盖率 |
| 128..255 | `(g,g,g,**255**)` 白字身，**不透明灰度** `g=(i-127)*255/128` | 字身覆盖率 ×2 亮度提升 |

`font-rebuild.fsx` 的 `composeIndex` 按「字身覆盖率 t / 膨胀描边覆盖率 o」编码
（`t>=16` 走白字、否则 `o>=16` 走黑边），外部描边由 `outlineMask(body)` 生成。

**五条实机调优结论（2026-10-19 第三~五轮）**：

1. **外部描边只描字形外侧**（`exteriorMask`：从图集边界 flood fill 出外部背景）。
   笔画之间的开放间隙若塞黑，小字号/缩放显示会糊成一团；只描外侧后字间透气。
2. **封闭内腔要描 2px**（不与画布边界连通的空隙，如「口」「目」的内框）。
   完全不描会显得字是"空心"的（实机反馈"口目内部没有描边"）；内腔是封闭的，填黑不会糊。
3. **黑边 alpha 要快速饱和**（`o*4`，o≥32 即纯黑）。按覆盖率线性给 alpha 会让描边外圈半透明，
   亮背景（路面/雪地）上看起来像"发虚的投影"而不是描边 —— 原版黑边绝大多数是 alpha=255 的实心黑。
4. **字身必须不透明灰度**（不是白色 + alpha）。游戏把 49px 行缩小显示时，半透明白会被背景稀释，
   实机表现为"字发灰/发蓝"（雪地/蓝天下尤其明显）—— 原版字身条目 idx197-211 同样是 `(灰度,255)`。
   另外 `TextRenderingHint` 用 `AntiAliasGridFit`，并对覆盖率做 ×2 亮度提升：GDI 抗锯齿下小字号
   笔画中心常只到 150~200，直接映射会让笔画发虚。
5. **原版黑边本来不对称**：实测四边为 **左 1-2 / 右 3-4 / 上 2 / 下 3**（白字 + 右下投影的立体感）。
   `outlineMask` 用「膨胀 2px ∪ 右下偏移 (2,1)」复刻，均匀描边会显得扁平。

⚠️ **调色板四次迭代（都实机验证过，别再回退）**：
- `(v,v,v,255)` 全部 → 背景 alpha=255 ⇒ **文字带黑色方块背景**；
- `(0,0,0,v)` 全部 → 字身 RGB=0 ⇒ **文字整体变黑**（像没渲染）；
- `(0,0,0,v)` + 字身白 alpha 渐变 ⇒ 缩小显示时被背景稀释 ⇒ **字发灰/发蓝**；
- ✅ 现行：0 透明 / 1-127 黑描边（alpha 渐变）/ 128-255 白字身（**不透明灰度**）。
- HIP 子格式（Gray16）单通道 = alpha 遮罩，只能白字（无描边），与原版 HIP 字库一致。

**字体选择（2026-10-19 共换两次）**：现用 `C:\Windows\Fonts\Deng.ttf`（**等线**）。
判据是**字重**（「世」墨迹像素）：原版 178 / 华文细黑 260 / **等线 339** / 微软雅黑 363 /
华康新综艺 568。华康太粗（加黑边后连成黑块 → 实机"挤/糊"），华文细黑实机"偏细"，
取等线折衷。覆盖率实测：译文 ∪ 假名 ∪ 全角标点 **3298 码位 0 空渲染**。
换字体只改 `FONT_PATH` 一行（`calibrate` 自动重算 size 与原点）。

**水平校准改为「bbox 居中」**：不同字体的字面宽度不同，按固定左边界会把右侧描边挤出格子
（被裁掉）；`calibrate` 现在把「漢」bbox 居中于 34px 格。

**破折号类码位需特判**：`—`(U+2014)/`─`(U+2500)/`━`(U+2501) 在字体里与「一」几乎同形，
`―`(U+2015) 更是异常（渲染成顶端 1px 细线），译文里 U+2014 出现 3485 次、U+2015 2058 次。
`font-rebuild.fsx` 对这四个码位**自绘全宽 2px 细横线**（`DASH_CPS`）：与「一」（粗短横）在
粗细和长度上都分得开。

**破折号步进 = 33（不是 34）**：游戏采样时 UV 每边内缩半像素（§2.1），步进 34 时连续破折号
之间仍会露 ~1px 缝（实机"差 1-2px"反馈）；改 33 让相邻破折号重叠 1px 才真正连续。
原日文字库的 advance 本身也不统一（`—`=36、`―`=39，都比格子 34 宽），所以必须强制改小。

**破折号的两处渲染细节（2026-10-19 第六轮，实机"描边有毛刺"反馈）**：
1. **不走 GDI**：`FillRectangle(0, 24.5f, 34, 2)` 的半像素坐标会被抗锯齿摊成 3 行不等亮
   （50%/100%/50%），经亮度提升后边缘参差；改成直接在 mask 上写 `y=24,25` 两行 255。
2. **不用右下投影**：`outlineMask` 的 (2,1) 偏移会让第二个破折号格子左边缘缺 1-2px 黑边，
   接缝处露缺口；破折号单独走**均匀 dilate(2)**，才严丝合缝。

**字形在 34×49 格内按统一基线绘制**（原版实测 rec32 像素矩形 + 墨迹 bbox，格内相对坐标）：

| 码位 | 原版 bbox | 说明 |
|------|-----------|------|
| 汉字（声/あ/日…） | (1..3, 9..11)-(29..31, 39..40) | 字身占满，底部留 9-10px 基线空间 |
| `一` | (3, 23)-(31, 26) | 位于字身中部 |
| `。` `、` | (3, 31)-(9, 38) | 位于字身左下角 |
| `A`（ASCII 大写） | (1, 11)-(26, 37) | 大写字高 27px（比汉字略矮） |

⚠️ 曾按「各字形 bbox 顶端对齐到 y=11」实现，导致 `一`/`。`/`、` 全被顶到格子上沿
（实机可见"简单字与标点顶端对齐"）。正确做法：**全字库共用同一绘制原点**（同一基线），
字号用「漢」bbox 高 29 校准，原点用「漢」bbox 落在 (3,11) 反推（实测 size=30、原点 (-3,11)）。

另：`EntryCount` 是**最大 char code（含端点）**，遍历映射表用 `0 .. EntryCount`；
用 `<` 会漏掉恰好等于 EntryCount 的码位（如 U+FF1F=65311 正是某 pac 的 EntryCount）。

## 2. abc 索引文件布局（✅ 运行时反编译确认，2026-10-18 修正）

全部小端序。**头部**：

```
偏移   类型   字段          说明
────────────────────────────────────────────────────────────
+0x00  u32    Version       0x41535720（字节 "20 57 53 41"，即 " WSA"带前导空格）；
                          也接受 5（旧版，无 32B 表）与 4（更旧，无映射表）
+0x04  f32    LineHeight    行高像素：内嵌字库 49.0、jpn_glyph 39.0
                          （旧文档误读为"字号 0x44=68"——那是本 f32 的字节而已）
+0x08  f32    UNK2          内嵌=3.0、jpn_glyph=0.0，语义未知
+0x0C  f32    UNK3          内嵌=4.0、jpn_glyph=0.0，语义未知
+0x10  f32    UNK4          内嵌=42.0、jpn_glyph=39.0(=LineHeight)，语义未知
+0x14  u16    EntryCount    最大 char code（GetGlyphIndex 越界判断用）
+0x16  u16[]  Mappings      长度 EntryCount+1；下标 = char code，值 = glyph 索引，
                          0 = 无字形（渲染空）。映射指针 = 文件+0x16（charcode 0/1 槽
                          即 +0x16/+0x18 的零值头）
+..    u32    GlyphCount    glyph 记录数（含 index 0 空白位；实测映射值域 1..GlyphCount-1）
+..    rec24  GlyphCount × 24B 记录，见 §2.1
+..    rec32  GlyphCount × 32B 记录，见 §2.2（仅 Version " WSA" 存在；
          Version 5 无此表 → 渲染走 rec24 float 路径；Version 4 无映射表，
          glyphIdx = charcode - 首个码位）
```

字节级 roundtrip（`AbcLib.parseBytes/buildBytes`）对全部门类文件逐字节一致 ✅
（旧 56B 连续解析恰好在字节层面等价：56 = 24+32，glyph 数据区偏移也吻合）。

### 2.1 rec24（24B × GlyphCount）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x00 | f32 | x1 | 字形矩形 UV（归一化 0..1，按纹理宽/高） |
| +0x04 | f32 | y1 | |
| +0x08 | f32 | x2 | |
| +0x0C | f32 | y2 | |
| +0x10 | u16 | 左撑/前距 | 测宽：步进 = 本值 + +0x14 值（比例模式，0x10000 标志时只用 +0x18） |
| +0x12 | u16 | 宽 | 等宽模式步进（0x10000 标志） |
| +0x14 | u16 | 右撑/advance | 与 +0x10 相加为比例模式步进 |
| +0x16 | u16 | 0 | 恒 0 |

渲染时 UV 会内缩半像素：`u1 = x1 + 0.5/W`、`u2 = x2 - 0.5/W`（H 同理，W/H 为纹理尺寸）。

### 2.2 rec32（32B × GlyphCount，仅 " WSA" 版）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x00 | i32 | x1 | **像素矩形（注意顺序是 x1,x2,y1,y2，与 rec24 的 x1,y1,x2,y2 不同！）** |
| +0x04 | i32 | x2 | |
| +0x08 | i32 | y1 | |
| +0x0C | i32 | y2 | |
| +0x10 | 16B | 未知 | 实测非全 0；修改/新增字形时**克隆同字体既有记录的尾 16B** 最安全 |

**渲染路径（sub_43F750 确认）**：rec32 表存在时像素矩形优先生效（UV = px ÷ 纹理尺寸，
覆盖 rec24 float）；否则用 rec24 float UV。两套值在文件里冗余存储、数值一致
（已抽验 '0'/'A'/こ/の/一，rec24 UV×纹理尺寸 == rec32 像素，逐点吻合 ✅）。

### 2.5 `jpn_glyph.abc` 实测数据（✅ 运行时语义重测，2026-10-18）

```
Version=0x41535720  LineHeight=39.0  UNK2/UNK3=0.0  UNK4=39.0
EntryCount=65509  GlyphCount=2860  映射非0项=2859（值域 1..2859，index 0 空白）
纹理 2048×2048（rec32 像素坐标上限证实）
```

- **码位空间足够**：Mappings 覆盖整个 BMP，中文码位（U+4E00+）按下标直接映射没有容量问题。
- **CJK 区已被日文汉字占用**（如 U+4E00「一」→ glyph 某索引）——日文字形与中文规范字形有差异，
  汉化时可选择复用（省空间）或替换（字形更正）。需要全文本用字 ∩ 已有映射 的覆盖分析。
- 追加 glyph 的做法（smoke-patch.fsx `adv` 已实现并待实机验证）：
  `Mappings[新码位] = GlyphCount`；`GlyphCount += 1`；rec24/rec32 表尾各追加一条
  （克隆既有同宽字形、改矩形指向图集空白区）；hip 图集空白处绘制位图。

### 2.6 `Font00.abc` = abc **Version 5**（✅ 已解析，2026-10-18 破案）

`data\cmn\font.pac` → 嵌套 `abc.pac` → `Font00.abc`（62928 字节）曾被认为是异构格式，
经 IDA 反编译 `BB_CFont_SystemFont` 初始化（sub_504A10）确认：**它就是标准 abc 的
Version 5 分支**（§5.0 SetFontData 接受 ver 4/5/" WSA" 三版本），早期判断
「float+UV atlas 异构结构」是把 ver5 布局误读（`0x6F22` 其实是 EntryCount=28450，
即码位上限 U+6F22「漢」）。

实测字段：

```
Version=5  LineHeight=15.0  UNK2=1.0  UNK3=1.0  UNK4=13.0
EntryCount=28450  Mappings 长 28451（=EntryCount+1）
GlyphCount=250（独立 u32 字段，含 index0 空白位；非0映射 249，值域 1..249）
布局校验：28 + 2×28450 + 4 + 24×250 = 62928 ✅ 恰好到文件尾
```

- **与 " WSA" 版差异仅两点**：无 rec32 表（渲染走 rec24 float UV 路径）；
  GlyphCount 是显式字段且含空白位 glyph0。
- 映射覆盖：ASCII 90 码位 + 平假名 83 + 片假名 76 + 汉字仅「字」「漢」2 个
  ——菜单文字本体靠这套小字库实时渲染（标题主菜单毛笔体实机已证，见 §5.2 注）。
- `Font00.hip` = HIP 专有格式，**256×256 图集**（GeoArcSys 可解 ✅），
  行高 16px（=1/16 纹理高，与 glyph 行 UV 吻合）；图集右下区域尚有空位可绘新字形。
- **扩充路径**（写回已内存验证 ✅）：EntryCount 是 u16（上限 65535），
  中文码位超出 0x6F22 时只需「提高 EntryCount + Mappings 补零 + 表尾追加 rec24」；
  本路径**无装载尺寸断言**（"size over" 只在 cons_LocalizeText，§5.2）。
- `AbcLib.parseBytes/buildBytes` 已支持 ver5 roundtrip（`scripts/abc.fsx`，
  全量回归 `roundtrip-all-abc.fsx` 239/239 ✅，含 Font00.abc）。
  ver4 仍无文件实例（运行时支持直减模式，未验证）。

## 3. 与文本显示的对应关系

```
atf value (UTF-16LE) → char code → mapping[charCode]（非0?）→ glyphIdx
  → rec24[glyphIdx]（float UV + 度量）/ rec32[glyphIdx]（像素矩形，优先）
  → 从 hip 位图裁剪 → 上屏
```

- 日文字库覆盖：ASCII + 日文假名/汉字（Shift-JIS 体系字符的 Unicode 码位）。
- **中文汉字的 char code（U+4E00–U+9FFF 等）不在原映射表中** —— 这就是汉化必须改造字库的原因。

## 4. 已知与未知 / 汉化改造路径

已确认：

- ✅ abc 整体布局（运行时 sub_43F290 反编译 + 图集目检双向验证）。
- ✅ rec24/rec32 双表结构与字段（rec32 = 像素 x1,x2,y1,y2；rec24 = UV + u16 度量）。
- ✅ hip 双格式判别方式（HIP 魔数 vs TGA）。

待确认（TODO）：

- ⚠️ rec32 尾 16B 语义（克隆既有值可绕过；实机若 B 测试失败优先怀疑这里）。
- ⚠️ rec24 +8/+12 两个 f32 语义（旧文档记 4040/8040 即 3.0/4.0；推测 ruby 参数）。
- ⚠️ 全文本用字 ∩ 已有 CJK 映射的覆盖分析。
- ✅ Version 5 实例已找到：`cmn\font.pac` 的 Font00.abc（§2.6，AbcLib roundtrip ✅）；
  Version 4 仍无文件实例。

## 5. 运行时行为（IDA 逆向 Getsuei.exe 确认，2026-10-18 ✅）

### 5.0 abc 装载与版本分支（sub_43F290，AA_CFont_Tool::SetFontData）

```c
int SetFontData(font, buf) {
    u32 ver = *(u32*)buf;
    if (ver == 4) {                        // 旧格式：无映射表、无 32B 表
        font->lineH   = *(u32*)(buf+4);    // +28
        font->minChar = *(u16*)(buf+20);   // +32
        font->maxChar = *(u16*)(buf+22);   // +34
        font->count   = maxChar - minChar + 1;   // +36
        font->rec24   = buf + 28;          // +44
        // rec32 (+48) 保持 NULL → float 路径；无映射 → 直减模式
    } else if (ver == 5 || ver == 0x41535720) {
        font->lineH   = (int)*(f32*)(buf+4);     // 49.0/39.0 → int
        font->maxChar = *(u16*)(buf+20);         // EntryCount
        font->mapping = buf + 22;                // +40
        font->count   = *(u32*)(buf + 24 + 2*EntryCount);  // +36
        font->minChar = EntryCount - count + 1;  // +32
        font->rec24   = buf + 24 + 2*EntryCount + 4;       // +44
        font->rec32   = rec24 + 24*count;        // +48，仅 ver==0x41535720 设置！
    } else return 0;   // 装载失败
}
```

渲染时纹理尺寸 W/H 存于 font+132/+136（f32，来自 hip 头），rec 下标直接用映射原值
（0 = 空字形渲染空白，越界码位也回退 0——**改坏一个字形/映射不会崩**，实验安全）。

### 5.1 字形查找与渲染（sub_43F440 GetGlyphIndex / sub_43F750 GetGlyphInfo）

```c
int GetGlyphIndex(font, charcode) {
    if (charcode > *(u16*)(font + 34))   // EntryCount
        return 0;                         // 超出范围 → glyph 0（空白回退）
    if (*(font + 40))                     // 映射表存在（ver 5/" WSA"）
        return *(i16*)(*(font + 40) + 2 * charcode);
    if (charcode >= *(u16*)(font + 32))   // 无映射表（ver 4）：直接相减
        return charcode - *(u16*)(font + 32);
    return 0;
}
```

GetGlyphInfo 输出：UV 矩形（rec24 float 内缩半像素，或 rec32 像素 ÷ 纹理尺寸覆盖）、
步进度量（rec24 +16/+18/+20 三处 u16）。步进计算（sub_43F4A0 测宽确认）：
比例模式 `advance = spacing + u16[+16] + u16[+20]`；等宽模式（0x10000 标志）
`advance = spacing + u16[+18]`。

### 5.2 三套并存的字库系统（对话字体的归属依据「按章节子集」强推断，待实机验证）

| 系统 | 类/源文件 | 字库来源 | 用途 |
|------|-----------|----------|------|
| ADV 对话 | AA_CText / AA_CFont_Tool（AdvCtrTask） | **每个 text pac 内嵌的 font.abc/font.hip** | 对话文本（atf bank 按 pac 挂载注册） |
| UI/系统文本 | cons_LocalizeText（BBSrc/Scene/Consumer） | **全局 `data\text\jpn\jpn_glyph.abc/hip`** | 系统/UI 文本；**装载缓冲固定大小（sub_510050）：hip < 3,600,000B、abc < 600,000B，`>=` 即断言 "Localize ... size over"**；扩充字库须在此约束内 |
| 菜单/标题 | BB_CFont_SystemFont | `data\cmn\font.pac` → 嵌套 `abc.pac`(Font00.abc) + `fullimg.pac`(Font00.hip)，另有 D3DXCreateFontIndirectA GDI 字体 | 系统菜单 |

关键证据：

- 每个 text pac 内嵌 font 是该脚本用字的**子集**（如 text_00_0-01 仅 250 glyphs，
  全局 jpn_glyph 有 2860；hip 尺寸随文本量 26KB–1.6MB 波动）——按章节裁字库
  只有「运行时使用内嵌字体」才说得通。
- AA_CFont_Tool 支持最多 4 套子字库（this+29..32 指针，ruby 注音用，`0x440390`
  按 `char-49` 选库），正文/ruby 各有独立 rec24 与纹理高度（this+7 / this+12..15）。
- `data\cmn\font.pac` 确实存在（140KB），系统初始化时（sub_4C9E90）从 `data/cmn/` 加载。
- 逆向关键函数地址（基线库 `work/ida/Getsuei.exe.i64`）：
  - `0x43F290` SetFontData（abc 解析，§5.0）；`0x43F440` 码位→glyph；`0x43F750` GetGlyphInfo
  - `0x43F4A0`/`0x43FFB0` 文本测宽（后者带 ruby 分支）；`0x43FF30` ruby 选库
  - `0x510190` cons_LocalizeText 载入 `data/text/<lang>/<lang>_glyph.{hip,abc}` 与 `_idlist.txt`
  - `0x456680` AdvCtrTask 按 `data/story/text/jpn/text_<名>.pac` 挂载（最多 10 个）
  - `0x459E00` 遍历挂载 pac 条目注册纹理与文本 bank；`0x48CA00` 建立 ADV 文本 bank 表
  - `0x504A10` BB_CFont_SystemFont 初始化（判别 Font00.hip 存在与否选路径；
    hip 存在时调 sub_43F290 用标准 abc 语义解析 Font00.abc——ver5 实锤依据，见 §2.6）

### 5.3 对汉化方案的影响

- **不能只改全局 jpn_glyph**：对话字体是每个 pac 内嵌的子集字库。
  可行做法：做一套覆盖全部译文用字的「中文超集字库」，替换每个 pac 内嵌的
  font.abc/font.hip（237 个 pac 都要改，但替换逻辑相同、可批量，`scripts/smoke-patch.fsx` 即原型）。
  pac 内嵌字库没有观测到大小上限断言（「size over」断言只在 cons_LocalizeText 路径）。
- jpn_glyph（UI）与 Font00（菜单）也要扩充；前者是 HIP 专有格式（GeoArcSys 工具链 ✅），
  后者是 §2.6 abc Version 5（已解析 ✅，AbcLib 直接支持，本路径无尺寸断言）。
- 追加 glyph 只需同步增长 Mappings/GlyphCount/rec24/rec32 四处 + hip 位图，
  **不需要 exe patch**（BMP 码位全在 EntryCount 范围内）。
- 行高 LineHeight（49/39px）决定图集行距；新字形按此高度绘制。

可行的改造路线（按侵入性排序）：

1. **复用**：CJK 区内与中文同形的字形直接复用；原文不用且码位空闲的槽位可分配给生僻字。
2. **替换字形**：对「码位已映射但字形中日不同」的汉字，在 hip 中重绘该 glyph 的位图（矩形尺寸不变最保险）。
3. **追加 glyph**：hip 图集空白/扩展区绘制新字形，`Mappings[中文码位] = 新索引`；
   克隆同宽字形记录改矩形，rec32 尾 16B 沿用克隆值。
