# GalTransl 翻译流水线（atf-json ⇄ GalTransl JSON）

工具：`scripts/galtransl.fsx`（F# / .NET 10，`dotnet fsi` 直接运行）。
本文档记录 ATF 文本与 GalTransl name-message JSON 的双向桥接规则与验证状态。

## 1. 用法

```powershell
# atf-json → GalTransl JSON（送翻）
dotnet fsi scripts/galtransl.fsx export
#   默认 work/atf → work/galtransl-raw；[-atf <dir>] [-o <dir>] 可改

# GalTransl 译文合并回 atf-json（翻完后）
dotnet fsi scripts/galtransl.fsx import
#   默认读 work/galtransl-proc + work/atf → 写 work/atf-cn
#   之后 work/atf-cn 的 JSON 用 AtfJson build → pac-pack 回包

# 恒等回归：export 产物原样 import，合并结果须与原始 atf-json 逐记录一致
dotnet fsi scripts/galtransl.fsx verify
```

端到端流程：
'`work/atf` ─export→ `work/galtransl-raw` ─(GalTransl 翻译)→ `work/galtransl-proc`
─import→ `work/atf-cn` ─AtfJson build→ atf ─pac-pack→ pac 补丁。

### ⚠️ 译文分两层，不要混（2026-10-19 约定）

| 目录 | 来源 | 谁写 |
|------|------|------|
| `work/atf-cn-manual/` | **手工翻译**（术语表性质、句数少、无需上下文的文本：菜单标签/结局回想/成就名等） | 人写，脚本 `gen-*.fsx` 生成 |
| `work/atf-cn/` | **GalTransl 回填**（大量剧情文本，机翻+校对） | `galtransl.fsx import` 写 |

`font-rebuild.fsx` 按 **manual 优先、atf-cn 次之** 的顺序查找译文（`atfCnDirs`），
所以手工译文放进 `work/atf-cn-manual/` 就自动生效，且**不会被 `galtransl.fsx import` 覆盖**。
遇到两层都有同一 pac 时 manual 胜出。'

## 2. 文本纳入规则（2026-10-18 与用户确认 ✅ 剧情+test 全部纳入，目录数据排除）

| key 类别 | 处理 | 依据 |
| -------- | ---- | ---- |
| `NAME_` 前缀 | 人名 → 导出到 `name` 字段 | 其后第 1 条消息为该人说话，仅挂那一条 |
| 数字开头（`00_`~`40_`） | 消息文本 | 剧情主文本（约 2.6 万条） |
| `RGN_` / `SP_` / `FontCheckScript_` | 消息文本 | test/SP 文件里的真实对话（约 470 条） |
| `ending_c3_` | 消息文本 | 结局对话（海滩场景）；区别于大写 `ENDING_`（路线标签，排除） |
| `VOICE_TEST_` | 消息文本 | 语音测试条目标签（「英理サンプルボイス１」类） |
| `chara_test_` / `chara_test_sub2/3/4_` | 消息文本 | 角色查看器 test 模式菜单句 |
| `chara_test_sub1_` | **排除** | 立绘姿势/表情目录（280 条），NAME 是条目名≠说话人 |
| `CHID_` 及其它（`effect*` / `ENDING_` / `cg_` / `bg_test_` / `CNT_` 等） | 不导出，import 时原样保留 | 指令参数、图鉴/特效名等目录数据 |
| 纯 name-only 文件（无任何消息 key） | 写**空数组 `[]` 占位**，不送翻 | 目录类文件防御（如 chara_test_sub1）；占位保证 raw 与 atf 目录一一对应，**下游按文件清单工作，缺文件会报错**（export 产出 237=237）；import 端自动 SKIP |

关键事实（全量扫描 66535 条记录 ✅）：
- NAME 与消息之间可能隔着 `CHID_` 等指令 key —— 挂起的人名一直有效到
  下一条**消息 key** 为止；连续 NAME / 文件尾 NAME 不会出现（防御性支持：导出为
  只有 `name` 的对象）。
- 人名值三种形态：`【名】`（14623）、`【名】$后缀`（177，如 `$spy`）/ `【名】&后缀`（2）、
  无括号（380，全部在 text_SP/VOICE_TEST）。`$`/`&` 控制后缀**意义不明**（推测为立绘/声线
  指示），不送翻、不依赖译者保留 —— 由 export/import 确定性剥离/还原。
- `【　】`（全角空格）= 无名占位：导出为无 `name` 字段的一般消息，import 时忽略
  译者误填的名字并告警，保持占位不变。
- 消息可含 `<ruby>` 标签与 `\r\n`，原样透传（14 条消息含 `「」`，也原样透传）。

## 3. 导出对象格式

```json
[
  {
    "name": "執行 櫂",          // 仅当说话者非空；已剥【】与控制后缀
    "message": "転校初日から…",
    "atf-json-key": "10_1-02_01_009"   // 锚定 atf record key
  },
  { "message": "一般消息…", "atf-json-key": "10_1-02_01_001" }
]
```

- `atf-json-key` 是 import 的首选匹配依据。**GalTransl 是否原样保留额外字段待实测**；
  若被丢弃，import 自动回退（见 §4）。
- 人名条目不单独成对象：NAME 的翻译跟着它名下那条消息走（译文的 `name` 字段）。

## 4. import 匹配模式（按文件自动选择）

| 优先级 | 形状 | 匹配方式 |
| ------ | ---- | -------- |
| ① | 全部对象带 `atf-json-key` | 按键精确匹配（重复 key 取首个并告警） |
| ② | 对象带 `post_jp`/`pre_jp`（transl_cache 形状） | 按 `post_jp`（回退 `pre_jp`）匹配原文；孤立人名按显示名匹配 |
| ③ | 纯 `name`/`message` | 位置对齐（要求两边对象数一致，否则告警） |

译文取值：`proofread_zh` → `pre_zh` → `message`；`name` 字段剥一层【】
（译者原样保留括号也能正确还原），若仍带原控制后缀则先剥掉再还原，防止重复。

合并策略：
- 译文为空 → 保留原文 + 告警；找不到匹配对象 → 保留原文 + 告警。
- 人名还原：带括号原名 → `【` + 译文 + `】` + 原后缀；无括号原名 → 译文 + 原后缀。
- 未导出 key（CHID_ 等）不参与合并，原样写出。

## 5. 验证状态（2026-10-18）

- `verify` 恒等回归：**209 文件 / 36094 对象全部通过**（export→import 零改动，
  records 逐记录一致 + 导出结构重放一致）✅
- 伪翻译实测 ✅：消息替换、译者保留【】的名字、空译文告警、`&hood` 后缀还原、
  与原名相同的 name（视为未翻译）均正确；ByKey / ByPreJp / Positional 三种模式各测一例；
  ending_c3 / voice_test 端到端写回验证通过 ✅
- 2026-10-18 修复（用户报告三文件）：`ending_c3_` 真对话此前被前缀白名单漏掉、
  `chara_test_sub1`（立绘目录）被误当说话人导出 name-only 垃圾 —— 现按上表规则修复，
  新增「纯 name-only 文件不导出」防御；export 重跑前自动清理旧产物。✅
- 待实测 ⚠️：GalTransl 对 `atf-json-key` 额外字段的保留行为（决定 import 走 ① 还是 ②/③）；
  GalTransl 对 `<ruby>` 标签的保留情况（被吃掉则需在缓存里修）。
- 规模：209/237 个 atf-json 导出（跳过目录文件 chara_test_sub1），共 36094 对象，
  其中 15075 带人名。

## 6. 字库重建与回包（2026-10-18 端到端跑通 ✅）

端到端流程补全（译文 → 可进游戏的 pac）：

```powershell
dotnet fsi scripts/galtransl.fsx import        # 译文 → work/atf-cn（atf-json）
dotnet fsi scripts/font-rebuild.fsx all        # atf-cn + 字库重建 → work/patch-pac/
dotnet fsi scripts/verify-patch.fsx            # 产物校验（rect/UV/atf/结构，237 项）
# 备份 + 安装（PowerShell，见 git 历史或重装时手动）：work/patch-pac → 游戏目录
```

**font-rebuild.fsx 方案**（核心决策均经侦查验证）：
- **字体**：`C:\Windows\Fonts\Deng.ttf`（**等线**，Windows 自带）。共换过两次：华康新综艺
  （「世」墨迹 568，太粗 → 加黑边后连成黑块）→ 华文细黑（260，实机偏细）→ 等线（**339**，折衷）。
  判据是**字重**而非风格：原版是细笔画黑体（178）。覆盖率实测 3298 码位（译文∪假名∪全角标点）
  0 空渲染。GDI+ 渲染（`TextRenderingHint.AntiAliasGridFit`，与 GeoArcSys 原工具链同源）。
- **垂直/水平校准**：`漢` bbox 高 29、顶边 y=11（基线与原版一致）；水平按 bbox 居中（不同字体
  字面宽不同，固定左边界会把右侧描边挤出格子）。
- **全量重渲染**：不搬运原像素，全部字形（原码位 + 新增简体）统一重渲染——风格统一、
  调色板统一（TGA = 白字 + 黑描边双色表，索引 1-127 黑边 / 128-255 白字 / 0 透明）、
  排版统一。渲染字号按「漢」bbox 高 29px 自动校准（实测 30.0px）。
- **垂直对齐（2026-10-19 修正）**：全字库共用**同一绘制原点**（同一基线），`一`/`。`/`、`
  按字体自身位置落位（原版：`一` 在格内 y23..26、`。` 在 y31..38）。⚠️ 早期实现按各字形
  bbox 顶端对齐 y=11，实机表现为简单字与标点被顶到行上沿 —— 已修复。
- **字形着色（2026-10-19 修正）**：原版字形 = **白字 + 黑描边**。现行做法 = 2px 描边且
  **只描字形外侧**（`exteriorMask` flood fill，避免笔画间隙被黑边塞满导致糊）+ **黑边 alpha 快速
  饱和**（实心黑，不发光发虚）+ **右下投影偏移 (2,1)**（原版黑边实测左1-2/右3-4/上2/下3，本就不对称）。
  `composeIndex` 双色编码。⚠️ 调色板三代的坑见 [abc-hip-font §1.5](../formats/abc-hip-font.md)。
  HIP 版（Gray16）单通道只能白字无描边，与原版 HIP 一致。
- **破折号特判**：`—`(U+2014)/`―`(U+2015)/`─`/`━` 在字体里与「一」同形（U+2015 还是畸形字形），
  统一自绘全宽 2px 细横线；并把它们的 rec24 步进强制改成 34（原值 36/39 > 格宽 → 断线）。
- **字形矩形统一 34×49**（与原版一致），定宽行排 60 列 × 41 行 = 2460 容量
  （最大 pac text_15_03_st 1851 字 ✅；2038 个 pac 全部装下，无扩画布风险）。
- **图集统一重建 2048×2048**：TGA pac → 双色调色板 type9 RLE；HIP pac → Gray16
  EncRawRepeat（u16 灰度 8→16 用 ×257 对齐原图满量程 65535，值本身即 alpha）。
- **abc 重建**：mapping 沿用 + 新增码位追加（值=GlyphCount+i）；rec24 UV 重算（rect/2048）、
  度量沿用原 glyph（语义未破解，smoke B 已实机验证克隆法）；rec32 rect 重算、尾 16B 沿用/克隆。
  mapping 区 = [MapOff, CountOff) 含尾部 2 字节未知字段，原样保留。
- **atf**：atf-cn 译文重建；无译文 pac 原样（parse→build 恒等）。
- 警告 474 映射空洞 + 193 空格空渲染均为预期；零缺字、零失败。

**验证状态**：重建 237/237 ✅；产物校验 237/237 ✅（结构自洽 / mapping 无越界 /
rect 无重叠全在界 / UV=rect / atf 记录数一致）；已备份（work/backup，236 新建 + 1 复用）
并安装至游戏目录。**实机上屏待用户测试** ⚠️（重点：中文显示、行高、人名括号、
test 模式菜单、HIP 格式 13 个 pac 的字体渲染）。还原：work/backup 复制回游戏目录。

**2026-10-19 第三轮（观感调优）**：① 描边改「只描字形外侧」（flood fill 排除字内空隙）；
② 破折号 rec24 步进强制 34（原 36/39 造成断线）。
**第四轮（字重与描边质感）**：③ 字体换 **华文细黑**（原版是细笔画黑体，华康新综艺粗一倍 →
实机"挤/糊"）；④ 黑边 alpha 快速饱和（原为线性渐变，亮背景下像发虚投影）；
⑤ 黑边加右下投影偏移 (2,1)（复刻原版实测的左1-2/右3-4/上2/下3）；⑥ 水平校准改「bbox 居中」，
避免字面宽的字体右侧描边被格子裁掉。
**第五轮**：⑦ 字体换 **等线**（华文细黑实机"偏细"；等线「世」墨迹 339 折衷）；
⑧ 字身改**不透明灰度**（原为白+alpha 渐变，游戏缩小显示时白色被背景稀释 → 实机"字发灰/发蓝"）；
⑨ 封闭内腔（口/目内框）描 2px（原为不描，实机"字像空心"）；⑩ 破折号步进 34→33（补 UV 半像素内缩）。
**第六轮**：⑪ 破折号改**整数像素直写**（GDI 半像素坐标被抗锯齿摊成 3 行不等亮 → 毛刺）；
⑫ 破折号**不用右下投影**（投影在接缝处让第二格左边缘缺 1-2px 黑边 → 毛刺），走均匀 dilate。
产物校验 237/237 ✅，已装机。**用户确认（第六轮后）**：字重/内腔/破折号连线均正常，
剩余"字偏灰"系游戏内渲染处理，非字库问题。
