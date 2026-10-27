# 交接文档（2026-10-19 收工版）

> 本日专攻**标题菜单汉化**，从"玄学"打到定稿。本文记录到此刻的完整状态，供接手者无缝续接。
> 菜单相关的完整技术细节见 `docs/tables/ui-text-source.md` §13 与 `docs/re-notes.md`「定位方式」节。

## 一、项目当前状态

| 模块 | 状态 | 说明 |
|------|------|------|
| 文本解包/回包（pac） | ✅ 完成 | 2201 文件 roundtrip，SHA 全一致 |
| 对话文本 atf ⇄ JSON | ✅ 完成 | 双向无损已验证 |
| 事件脚本 bin ⇄ JSON | ✅ 完成 | 双向无损已验证 |
| 字库 abc/hip 读写 | ✅ 完成 | roundtrip 通过；中文新字形已追加 |
| TGA type9 编解码 | ✅ 完成 | Wu 量化 256 色 |
| HIP 专有格式 | ✅ 完成 | 1055 文件回归通过 |
| **对话译文** | ✅ 已验收 | 237 pac 中 **220 已译** / 17 未译 |
| **台词图（35 张）** | ✅ 已验收 | 等线字体，实机效果确认 |
| **标题菜单图（18 张）** | ✅ **定稿（2026-10-19）** | 六版迭代，位置/字号/描边全部实机验收 |
| **UI 文字图甄别** | ✅ **定论（2026-10-20）** | 852 张全部定级 A607/B22/C223（**无死资源**）；**文字 = 预渲染图**（名字写在 aex 里）。10/10 探针实机确认 → `docs/tables/ui-images-triage.md` §7 |
| **jpn_glyph 重建** | ⚠️ **三轮未定稿（已还原原版）** | 存档界面中文缺字形；黑字 + 字面大小不一未解决。方向见 `docs/tables/font-coverage.md` §7 |
| **译文行宽** | ✅ **已修完（2026-10-20）** | 650 行超宽 → 0（自动重排 251 + 人工改写 269），`check-line-width.fsx` 复查通过 |
| **ruby 注音核查** | ✅ **已修完（2026-10-20）** | 53 处全部改入 `work/atf-cn`，残留复扫 0 → `docs/tables/ruby-triage.md` §6 |
| 设置/系统菜单等其余 UI 重制 | ⬜ 下一会话展开 | 前置：探针实机复核（已装机） |
| Steam 成就 | ⬜ 不做 | Steam 后台文本，改 exe 无效 |

## 二、⚠️ 游戏目录现状（接手第一件事）

**2026-10-20：完整汉化已装机**（探针图已全部还原，AE 目录与 `work/backup/AE` 逐字节相同）。

| 位置 | 数量 | 当前状态 |
|------|------|----------|
| `data/story/text/jpn/*.pac` | 237 | ✅ **已装**（`work/patch-pac`，含内嵌中文字库 + 译文 + ruby 53 处修复） |
| `data/story/image/*.hip`（台词图） | 35 | ✅ **已装**（`work/patch-image`） |
| `data/story/image/title_menu_*` | 18 | ✅ 已装（`work/patch-menu`，第六版） |
| `data/AE/**` | — | ✅ 原版（探针已还原，逐字节校验） |
| `data/text/jpn/jpn_glyph.*`、`data/cmn/font.pac` | — | **原版**（jpn_glyph 重建三轮未定稿已还原，见 `font-coverage.md` §7） |

装机/还原命令：

```powershell
dotnet fsi scripts/font-rebuild.fsx all        # 字库重建 → work/patch-pac（已跑，237/237 OK）
dotnet fsi scripts/verify-patch.fsx            # 校验产物（237 OK / 0 FAIL）
dotnet fsi scripts/install-text-pac.fsx --apply   # 237 pac 装机（自动备份；--restore 还原）
dotnet fsi scripts/remake-telop.fsx install    # 35 张台词图装机
dotnet fsi scripts/install-title-menu.fsx      # 18 张菜单图（幂等）
dotnet fsi scripts/audit-backup.fsx            # 盘点游戏目录被改文件（只读）
```

**原版基准在 `work/pristine/`**（Steam 校验后复制的只读副本）：
`story-text-jpn`(237) / `story-image`(410) / `text-jpn`(3)，共 ~228 MB。
**任何"原图/原文件"测量一律从这里取**，绝不从游戏目录取 —— 那里随时被自己的产物覆盖。
（`work/backup/story/text/jpn` 的 237 个备份已验证与 `work/pristine` 逐字节相同 ✅）

## 三、还原到原版的方案（留档）

```
Steam 库 → 右键「月英学園 -kou-」→ 属性 → 已安装文件 → 验证文件完整性
```

只重下被改动的文件，比重下 3.75 GB 快得多，也能恢复字库这种状态判不清的项。
还原后**立刻**把 `data/story/text/jpn`、`data/story/image`、`data/text/jpn` 复制进 `work/pristine/`。

## 四、菜单「玄学」破案全记录（六轮才收敛）

### A. `sub_5073B0` 参数错位（推翻旧结论）

`a4` 是 `__int64` 占两栈槽，调用点按 17 个 4 字节压栈（`add esp, 44h` 实锤）。
逐条 push 还原后：`a5=1, a6=128, **a7=0**, a8=a9=1.0f` ⇒ `if (a7)` **不执行**，缩放确实 1.0。
旧文档「a7=128 居中 → X/Y 是图片中心」是 Hex-Rays 错位误读，**作废**。

### B. `sub_53B400` + `title_menu_00…71` 是旧版遗留

它的坐标表（X 恒 512、Y=320/400/480/560）对应 `data/AE/title/UI_02_title.pac` 那套资源，
与现役菜单无关。两个名字池的指针表（`0x11D92BC`、`0x11DDA8BC`）在 exe 里**都没有静态引用**
（字节级搜索确认为 0），**现役绘制代码路径至今未定位** —— 但不影响制图。

### C. 渲染映射（实机整屏截图反解）

- **缩放 = 4/3**：UI 坐标空间 960×540 → 屏幕 1280×720；
- 画布**宽不是 16 的倍数会整体偏位**（136 ⇒ 偏左 16 屏幕 px、168 ⇒ 偏右 6、40 ⇒ 偏左 5）；
- 垂直行位置按画布高流式累加。

### D. on/off 定位规则（标尺图实验，终局）

用 `scripts/menu-ruler.fsx` 生成刻度图（边框 + 四角色块 + 16px 刻度 + 中心十字）装机读数：

| 标尺 | 画布 | 屏幕画布左上角（UI） |
|---|---|---|
| `_06_off` | 256×64 | (37, 349) |
| `_07_on` | 256×64 | (37, 349) |
| `_06_on` | 48×40 | (141, 365) |

**⇒ 引擎给每项开"槽位"，尺寸由 off 图决定；on 图按「原版 on 尺寸」居中放进槽位 ——
与我们实际给多大的纹理无关。**

- on 图画布左边缘恒在 UI x≈142，给它 256×64 也照样按 48×40 算 → **整块右溢 105 UI px**
  （第 5 版的翻车原因：「浩」被画到屏幕 x≈340）；
- on 画布顶恒为「槽位顶 + 16」（理论 +12）→ **内容比槽位中心低 4 UI px**，
  这就是从第一轮起反复出现的「选中时下沉几像素」。

⇒ **on 图的画布尺寸必须保持原版（48×40 / 40×40 / 64×40），内容在画布内上移 4px 补偿。**

### 与图片无关、别再动的东西

- 「返回」紧挨上一条是**游戏行为**（章节未全解锁时可见项变少），不要为它改图。

## 五、菜单图定稿参数（第六版，实机验收 ✅）

| 档 | 范围 | 字芯高 | 描边每边 |
|---|---|---|---|
| 主菜单 | 01~04 从头开始/继续游戏/设置/开放之间 | **34** | 1.5（pen 3） |
| 子菜单 | 05~09 序章/浩/匡/抗/返回 | **22** | off 3 / on 2 |

- **画布尺寸**：宽取 16 的倍数（`_03` 168→160、`_04` 136→160）；**高一律保持原版**；
  **on 图尺寸一律保持原版**（见 §四.D）。
- **位置**：墨迹居中于画布；`_06~_09` 的 on 额外上移 4px。
- **alpha 墨迹高 ≠ 字高**：原图 `_01_off` 墨迹高 37 里字芯只有 24，其余全是描边。
- **GDI+ `Pen(width)` 是笔的直径**，要传「每边 × 2」。
- 宽度不够时**以左边缘为基准横向压缩**（换字体无效：7 款中文字体宽/高比都是 1.01~1.06）。

**六版迭代史**（避免重蹈）：
1. 居中 + 统一 32px + 画布不改 → 「开放之间」偏左 16px（136 非 16 倍数）；
2. 复刻原图墨迹位置 → 位置对但字号参差、on/off 跳位；
3. 居中 + 画布 16 倍数 + 统一 37px → **主菜单验收通过**，子菜单挤压；
4. 字芯 24/29 + 粗描边 → 不挤了，但 on/off 仍错位；
5. on 画布对齐 off（256×64）→ **翻车**：引擎按原版尺寸算槽位，「浩」右溢 105 UI px；
6. 回原版 on 尺寸 + 上移 4px + 主菜单 34/1.5、子菜单统一小字 22/3·2 → **验收通过**。

## 六、工具链速查
```powershell
dotnet fsi scripts/remake-title-menu.fsx      # 18 张菜单图 → work/patch-menu/（读 work/pristine/）
dotnet fsi scripts/install-title-menu.fsx     # 装机（自动备份 work/backup/story/image-menu/，SHA 校验）
dotnet fsi scripts/title-menu-compare.fsx     # 原图 vs 重制对比图 → work/menu-rebuild-compare.png
dotnet fsi scripts/menu-ruler.fsx             # 生成定位标尺图 → work/patch-menu-ruler/（诊断用）
dotnet fsi scripts/font-rebuild.fsx all       # 字库整体重建 + atf 注入
dotnet fsi scripts/verify-patch.fsx           # 237 项校验
dotnet fsi scripts/audit-backup.fsx           # 盘点游戏目录被改文件（只读）
```

**IDA 逆向**：`cmd /c scripts\ida-mcp-run.cmd`（后台，监听 127.0.0.1:8745），
`idb_open` 传 `work/ida/Getsuei.exe.i64` 复用分析缓存，客户端 `scripts/ida-client.ps1`。

**改完 `data/story/image/` 的文件必须完全退出游戏再进**，否则引擎沿用旧纹理，会看到"改了没生效"的假象。

## 七、下一步建议

1. **先把完整汉装回去**（§二），确认菜单 + 对话 + 台词图三者共存无冲突。
2. **设置/系统菜单**（`options_screen`，124 个文件）开工前，先用 `menu-ruler.fsx` 那套方法
   量出它是不是同一套槽位/锚点规则 —— 如果是，本节结论可直接套用。
3. 现役菜单绘制代码路径仍未定位（两张名字表都无静态引用）。若将来需要精确控制位置，
   可考虑从 `data/story/script/scr_00.pac`（标题画面脚本，加载顺序在其之后）或
   `sub_5680C0` 这类通用资源加载函数入手。
4. **别再靠试错调图片**。标尺图 + 整屏截图反解是唯一可靠路径，一轮就能定死规则。

---

## 八、2026-10-20 侦查轮结论（只侦查，未改任何资源）

本轮目标：把「UI 文字图到底哪些现役」和「ruby 处理是否合规」两件事查清楚，为重制工作铺路。
**没有修改游戏目录、没有生成任何补丁。** 详细文档：
[`tables/ui-images-triage.md`](tables/ui-images-triage.md)、[`tables/ruby-triage.md`](tables/ruby-triage.md)。

### 8.1 新工具（已归档，可复跑）

```powershell
dotnet fsi scripts/exe-resource-names.fsx    # 从 exe 提取 UI 资源名字表（2495 条）→ work/inventory/
dotnet fsi scripts/ui-image-triage.fsx       # AE 852 张 HIP × 名字表 × procmon → 定级
dotnet fsi scripts/procmon-pac-access.fsx    # 从 procmon 日志提取实际访问路径（已有）
```

### 8.2 UI 图甄别：静态已完成，动态待巡查

判据三级：**① procmon 实际加载 > ② exe 资源名字表 > ③ pac 名命中**，但有一个反例证明
「①+②」也可能是死资源（`UI_02_title.pac` 被加载、名字全在表里，实机却是遗留）。

定级结果（`data/AE` 852 张 HIP）：**A 107 / B 171 / C 194 / D 380 / 无 E·F**。
含文字的 372 张按 pac 归属：

| pac | 含文字 | 结论 |
|---|---|---|
| `special/between_the_open.pac` | 165 | 强候选（开放之间，未进过界面） |
| `option/options_screen.pac` | 42 | 现役；**`option_mode_Description_01..10` 命中 = 设置说明文字是图，可重制** |
| `adv/chapter_title_*.pac` ×20 | 40 | 现役强候选（exe 有 60 条 `chapter_title_date_X-Y_in/loop/out`） |
| `common/g_dialog.pac` | 37 | 现役；`botan_*` 命中，`g_dialog_*_message` 未命中 |
| `system_menu/system_menu.pac` | 34 | ⚠️ **框线命中、文字图全未命中**（`system-menu_moji`/`ST_title_moji`/`ST_modo_moji`） |
| `title/UI_02_title.pac` | 34 | ❌ 已知遗留（勿动） |
| `backlog/back_log.pac` | 12 | 候选；`back_log_title*` 未命中 |
| `common/UI_29_nowloading.pac` | 6 | 现役（pac 已加载） |

### 8.3 ⚠️ 本轮最重要的未知（下一会话的核心问题）

「**文字图**大多不在 exe 名字表里，而**框/线/底衬**几乎都在。」
最可能的解释：系统菜单 / 回想界面的文字是 **Font00 实时渲染**（假名够用，250 字形），
⇒ 汉化这两处不是改图，而是「**扩 Font00 + 找到文本源**」。
而文本源至今**未定位**（exe UTF-16 区只有 Steam 成就；SJIS 扫描零星命中；ADVScript 里 0 条日文）。
**系统菜单能不能汉化，取决于下一会话能否找到这个文本源。**

### 8.4 ruby 注音

译文 635 个 `<ruby>` 标签逐条分类：「只保留写 A 读 B 与着重号」的约定**执行到位**
（615 个合规）。真正要修的 **53 处**全部落在 `work/atf-cn`：

- **拼音注音 9 条**（`<ruby>能力<nengli/ruby>` 等，原文是 `ちから` 这类语义读法）→ 改语义中文注音；
- **基字 = 注音冗余 42 条**（`<ruby>暗影蜃楼<暗影蜃楼/ruby>`）→ 建议去 ruby；
- **長音符误作破折号 2 条**（`コピー` → `コピ—`）。

清单 `work/inventory/ruby-fix-list.tsv`；建议写 `scripts/fix-ruby.fsx`（表驱动，做法同 `gen-ending-cn.fsx`）。

### 8.5 下一会话任务清单（按依赖序，可直接开工）

1. **装机完整汉化 + 共存冒烟**（§二 四条命令；顺手抽查 ruby 观感，见 ruby-triage §4）。
2. **Procmon 全覆盖巡查**（triage §5.1）：标题 → 系统菜单 → 设置 → 存档 → 读档 → 回想 → 开放之间 → 第 1 章日期卡，
   一次会话抓完 → 重跑 `ui-image-triage.fsx`，C/D 级会大量升为 A/B。
3. **探针图验证**（triage §5.2，6~7 张）：定死「文字是图还是实时渲染」。这步决定后续整条路线。
4. 若 3 的结论是「图」→ 按 title_menu 流程批量重制（优先级：`options_screen` → `chapter_title` → `back_log` → `between_the_open`）；
   若是「实时渲染」→ 转去查 Font00 的文本源（`ivy_StringBank.lst` / `catalog_env.info` / exe 非字符串区）。
5. **ruby 53 处修复**（可与 UI 并行，纯数据）。⚠️ 顺序：确认 `galtransl-proc` 定版、**不再跑 `import`**
   → `fix-ruby.fsx` → `font-rebuild.fsx all`；否则 import 会把修好的 ruby 覆盖掉（见 ruby-triage §5）。
6. `staff_roll` / `text_voice_test` 人名 / 一键还原脚本 —— 用户判定为「看到 bug 再说」，暂不做。

---

## 九、2026-10-20 第二轮：未知项已破题（任务 2 / 3 / 5 完成）

本轮没往游戏目录装汉化补丁（对话/台词图/字库仍是原版），但**已把 `work/patch-pac` 重跑到最新**
（`font-rebuild.fsx all` 237/237 OK + `verify-patch.fsx` 237 OK，含 ruby 修复）。
另装了 7 张探针图到 `data/AE`（见 9.3，看完要 restore）。

### 9.1 核心未知已解决：「UI 文字是图，不是 Font00 实时渲染」✅

原 §8.3 的疑问是「文字图不在 exe 名字表里 ⇒ 可能是 Font00 实时渲染」。**这条推断已推翻**：

- UI 图的名字**写在各 pac 内嵌的 `.aex`（ArcSys UI 定义）里**，是 ASCII 单字节串
  （`TEX\system-menu_moji_01.png`），所以 exe 里搜不到；
- aex 内**没有任何日文/全角文本**（UTF-16LE + SJIS 双扫，命中的全是二进制噪声），只有纹理名与布局浮点；
- 探针以外的旁证：直接解码这些 HIP 就看到日文 —— `ST_modo_moji_01`=「セーブ」、
  `ST_title_moji`=「項目を選択して下さい」、`open_mode_moji_01`=「CG鑑賞」、
  `g_dialog_st_save_message`=「システムデータをセーブしています。…」、`back_log_title_moji01`=「バ」、
  `nowloading_word_1`=「読」。

⇒ **汉化系统菜单/设置/回想/开放之间全都走「改图」路线**，不用去找 Font00 的文本源。

### 9.2 procmon 全覆盖（任务 2）

用户按自己的顺序抓了一圈（标题 → 系统菜单 → 设置 → 存档/读档 → 回想 → 开放之间 → 第 1 章开头），
日志已归档为 `work/procmon/Logfile-20261020.xml`（旧的两份同日改名 `Logfile-20261018.xml` /
`Logfile_full-20261018.xml`）。`procmon-pac-access.fsx` 改成自动汇总 `Logfile-*.xml`。

新增被加载：`back_log.pac` / `system_menu.pac` / `between_the_open.pac` / `cg_01011_thumb.pac`。
⇒ `data/AE` 13 个 pac 全部实机加载过；定级从 A107/B171/C194/D380 变成 **A607/B22/C223**，
**无死资源**。

⚠️ 顺带否掉一条旧计划：**章节日期卡不是 `adv/chapter_title_*.pac`，而是散装 `data/story/image/day_*.hip`**
（`day_0..9` 数字 + `day_mon..sun` 曜日 + `day_underbar`/`day_white_bg`），由脚本 `DAY_TITLE` 命令驱动。
`chapter_title_*.pac`（20 个 pac / 220 张）判为遗留，从工作量里划掉。

### 9.3 探针图已装机并实机验证 ✅（任务 3 完成）

```powershell
dotnet fsi scripts/probe-images.fsx list      # 7 张表
dotnet fsi scripts/probe-images.fsx verify    # 查当前装机状态
dotnet fsi scripts/probe-images.fsx restore   # 还原（复核完务必跑）
```

**实机结果（2026-10-20 用户复核，10/10 全部确认）**：

| # | 颜色 | 界面/位置 | 结果 |
|---|---|---|---|
| 1 | 品红 | 系统菜单第 1 项「セーブ」 | ✅ 变色 |
| 2 | 青色 | 系统菜单标题「項目を選択して下さい」 | ✅ 变色 |
| 3 | 黄色 | 设置「BGM音量」高亮态 | ✅ 变色（`BGM_modo_text01_on`） |
| 4 | 绿色 | 回想标题首字「バ」 | ✅ 变色 |
| 5 | 蓝色 | 开放之间「CG鑑賞」 | ✅ 变色 |
| 6 | 橙色 | 系统数据保存提示 | ✅ 变色（出现在从选项/开放之间回主界面时；该图是**自动存档**提示，不是存档页面） |
| 7 | 紫色 | 加载画面「読」 | ✅ 变色 |
| 8 | 青绿 | 设置「BGM音量」普通态 | ✅ 变色（`BGM_modo_text01_off`） |
| 9 | 玫红 | 设置底部说明 | ✅ 变色（`option_mode_Description_01`） |
| 10 | 淡紫 | 设置顶部标题「オプション」 | ✅ 变色（`option_title_moji01`） |

⇒ **UI 文字 = 预渲染图，10/10 实证**。汉化这些界面就是改图，不用找 Font00 文本源。

顺带查明：`options_screen.pac` 里的 `ST_modo_moji_*` / `ST_title_moji` 等 `ST_*` 系列是
**从 system_menu 复制的遗留品，不在此界面显示**（3 号探针最初就装在它们身上，无反应）。
设置界面真正要重制的是 `option_title_moji0X` / `option_window_text_0X` / `BGM_modo_text0X` /
`Message_modo_text0X` / `option_mode_Description_0X` —— 完整清单见
`docs/tables/ui-images-triage.md` §7.6。

探针已全部 `restore`，游戏目录与 `work/backup/AE` **逐字节相同**（6 个 pac 全部校验过）。

**记住：改完 AE pac 必须完全退出游戏再进。**

### 9.4 ruby 53 处已修（任务 5）

`scripts/fix-ruby.fsx` 按 `work/inventory/ruby-fix-list.tsv` 改了 `work/atf-cn`（38 文件 / 53 处，
0 未命中，幂等；残留复扫 0）。细节见 `docs/tables/ruby-triage.md` §6。

随后已跑完下游并验证：`font-rebuild.fsx all`（237/237 OK）→ `verify-patch.fsx`（237 OK / 0 FAIL），
并且解包抽查 `work/patch-pac/text_11_2-07_er.pac` 与 `text_10_4-09.pac` 确认
「终焉的轮舞曲」「<ruby>生命<命/ruby>」已进产物。

**装机完成（2026-10-20）**：`install-text-pac.fsx --apply`（237 pac，新写脚本，备份+逐文件 SHA）→
`remake-telop.fsx install`（35 台词图）→ `install-title-menu.fsx`（18 菜单图）。
`audit-backup.fsx` 盘点游戏目录共 **291 个文件被改**，与预期一致。

### 9.4.1 冒烟发现的两个问题：**行宽 ✅ 已修完 / jpn_glyph ⚠️ 三轮未定稿**

**问题 1：存档界面槽位详情乱码**（不是排版，是字库缺字）

存档界面用 `jpn_glyph`（UI 全局字库，2860 字形，只有日文汉字 + 假名），
**没有简体字**：`顺 U+987A / 欢 / 买 / 后 / 边 / 还 / 过 / 已 / 经 …` mapping 全为 0
（日文形 `順 U+9806` 有）。存档文件本身的文本是干净中文（`savedata.bin` 里 UTF-16 明文），
所以纯是字形问题；密集小字是 ruby 注音里的中文字，缺得更彻底。

⇒ 新工具 `scripts/font-rebuild-jpn.fsx` **重建 jpn_glyph**：
保留原字形 2543 + 丢弃 315 + **新增 1686 汉字**（Deng.ttf 渲染 32×39），
图集 2048×2720 行内变宽打包，编码 **Indexed8/RawRepeat**（原版 Gray16）。
实测 hip **2.85 MB**（上限 3.6 MB，79%）、abc 368 KB（上限 600 KB，61%）✅。
详见 `docs/tables/font-coverage.md` §6.4。

> ⚠️ **后续三轮实机都没过关，最终已还原原版**：黑字（Indexed8 不管怎么配调色板都渲染成黑色）
> + 中文字面大小不一（逐字拉伸到 39 高所致）。当前 `jpn_glyph.*` 与 `work/pristine` 一致。
> 第四次的方向与坑见 **`docs/tables/font-coverage.md` §7**（Gray16 + 统一字号渲染 + 缩到 26×32 左右）。

**问题 2：backlog 部分行溢出边框**

游戏不自动折行，`\r\n` 是唯一换行手段，**单行上限 24 全角字符**
（用 `text_10_1-03` 日文 24 字刚放得下、中文 26 字溢出反解，见 `docs/formats/atf.md` §6.1）。

判据关键：**日文原文自己也超 24 全角的记录用的是更宽的框**（`text_SP` 特别语音原文 35~47 全角仍正常）
⇒ 只报「中文超宽、日文没超」的行，否则误伤 119 行。真实工作量 **650 → 269 行**。

处理：`reflow-atf.fsx` 自动重排 251 条（同记录内挪字、不删字、收尾标点不留行首）+
`apply-line-fixes.fsx` 人工改写 269 行（6 批，校验每行 ≤24 全角 + ruby 标签序列不变）。
复查 **0 行超宽** ✅。

### 9.5 修掉的核心库 bug：pac 条目名是**混编码**的（闪退真因）

探针 3 两次让设置菜单闪退，根因不在图，而在 **pac 条目名**：

```
window_frame_Ｌ_light_01..08.hip     全角Ｌ = Shift-JIS 双字节 0x82 0x6B
（半角 window_frame_R_light_01..08.hip 是它的镜像，左右窗口边框）
```

- **(a) 读**：`BinaryReader.ReadChars(n)` 的 n 是字符数，遇到非 ASCII 名错位 → 抛异常（已修）；
- **(b) 写**：用 UTF-8 解码再编码回写 → `82 6B` 变成 `EF BF BD 6B`（U+FFFD+'k'）⇒ **名字被改写**
  ⇒ 引擎查不到 ⇒ 闪退（已修：`PacEntry.FileNameBytes` 原字节回写 + 解码顺序「严格 UTF-8 → CP932」）。

修复后：原版备份 roundtrip 8/8、游戏 data 全量 2201 OK；台账里名字正确显示为
`window_frame_Ｌ_light_0X`；**等长探针**（`apply 3 --pad`，尾部补 0 保持 4677 字节）装上后
纯字节 diff **只剩 1 段 4669 字节 = 目标图数据区**，名字/偏移/哈希全未变。

**两条重要教训**：
1. **roundtrip 全绿证明不了写回逻辑正确** —— 它只验证「读回来的值→写回去」自洽；
   解析时把值改掉（本 bug 就是），2201 个 ASCII 名 pac 全绿照样崩。
2. **解析器有 bug 时，基于它的 diff 会假阴性** —— `pac-diff` 比的是解析后字段，两边用同一个坏解析器
   → 名字都显示 U+FFFD → "结构一致"。最终裁判是**纯字节对比**，新增 `scripts/pac-bytediff.fsx`。

工具：`scripts/pac-diff.fsx`（逐字段）+ `scripts/pac-bytediff.fsx`（纯字节）；
`probe-images.fsx apply --pad`（等长探针，排除长度/偏移变量）。

### 9.6 下一会话任务清单（按依赖序）
0. ~~复测探针 3~~ ✅ **10/10 全部确认（2026-10-20）**，探针已全量 restore；
0.5 ~~装完整汉化~~ ✅ 已装（237 pac + 35 台词图 + 18 菜单图）；
1. **jpn_glyph 第四次尝试**（本轮三次未定稿，已还原原版）—— 详见 `docs/tables/font-coverage.md` **§7**：
   - **黑字 ⇒ 必须用 Gray16**（Indexed8 怎么配调色板都渲染成黑色），但 4632 字 = 4.6MB > 3.6MB 上限
     ⇒ 需把字形缩到 26×32 左右（约 3.17MB）或减少字数；
   - **中文字面大小不一 ⇒ 改统一字号渲染**（照 `font-rebuild.fsx` 的 `calibrate()`），
     不要像现在这样逐字拉伸到 39 高（笔画少的字被放大）；
   - 用户建议：整库用 Deng.ttf 统一重渲染（连原日文字形一起），一致性最好；
   - 砍字数有坑：存档界面固定文本（セーブ/スロットを選択してください/総プレイ時間）的字符
     不在译文/脚本/exe 字符串里，无法静态穷举（已踩，缺 63 个）；
   - 工具 `scripts/font-rebuild-jpn.fsx` 已就绪（含 mapping 0-based 修正），改渲染与尺寸即可复用。
2. **按图重制 UI 文字**：优先级 `system_menu`（34 张，用户原本要的下一步）→ `options_screen`（42）→
   `back_log`（12）→ `between_the_open`（165，量大，先做 `open_title_moji_*`/`open_mode_moji_*`）；
   规则直接套 §四/§五（原版尺寸、on 图尺寸不变、宽 16 倍数、缩放 4/3）；
   **每次改完 pac 跑 `scripts/pac-diff.fsx --backup <相对路径>` 确认只改了该改的**；
3. 对话字库 1px 细线打磨（可选）：`font-rebuild.fsx` 渲染字号缩 1~2px 给格子留边；
4. `day_mon..sun` 是否改中文曜日（`月火水` → `一二三`）—— 低优先级，玩家可读。

---

## 十、2026-10-20 深夜第三轮：三件事全部落地（待实机验收）

### 10.1 jpn_glyph v4 ✅ 定稿并装机（详见 `docs/tables/font-coverage.md` §8）

三个根因逐条对齐，都已有明确结论（不是推测）：

| 症状 | 结论 |
|---|---|
| Indexed8 字变黑 | **必须 Gray16**。全游戏 1055 个 HIP 里**没有一个 8bpp 索引文件**，引擎的索引取色路径从没被使用过 ⇒ 调色板怎么配都没用 |
| 中文字面大小不一 | **统一字号 + 统一原点**（「漢」墨迹高 29，字身落框内 (1,5)-(31,34)），不再逐字拉伸 |
| 体积超限 | **需求集裁剪**（关键：`jpn_idlist.txt` 就是 UI 固定文本表，897 字符只缺 1 个字形）+ 8 级灰度量化 |

实测：hip **3,403,679 B（94.5%）**、abc 346,646 B（58%）、图集 2048×2301、3849 字形
（保留 2164 / 新增 1685 / 丢弃 695 个 CJK 日文汉字，清单 `work/inventory/glyph-drop-list.txt`）。

离线验收工具：`scripts/preview-font.fsx`（按引擎语义排版成 PNG，mapping 偏移/缺字一眼可见）。

### 10.2 对话字库 1px 细线 ✅ 已重做并装机

`font-rebuild.fsx` 的图集**列间隔 1px**（`CELL_STRIDE = 35`）：定宽格紧排时笔画宽的汉字墨迹
贴到格子边界，与相邻字形连成一条 1px 竖线。代价是列数 60 → 58、容量 2460 → 2378（当前最大
单 pac 1854 格，仍有余量）。237/237 重建 + `verify-patch` 237 OK + 已装机。

### 10.3 UI 文字图重制 ✅ 98 张已装机（详见 `docs/tables/ui-images-triage.md` §8）

新工具 **`scripts/remake-ui-text.fsx`**（表驱动 `list`/`preview`/`apply`/`restore`）：
解码 HIP → 量 ink/body 双 bbox → 清区填底色（中位数）→ Deng.ttf 统一字号画中文 →
黑描边 + `_on` 态辉光 → 回写 tex.pac/外层 pac。**画布尺寸一律不变**（引擎按原尺寸算槽位）。

已完成：`system_menu` 25 / `options_screen` 31 / `back_log` 5 / `g_dialog` 37 / `between_the_open` 76 = **174 张**。
5 个 pac 全部 `pac-diff` 结构一致。第一轮实机反馈的三个问题（背景方框 / 说明文字怪光晕 /
小字发糊）已修并重装；`option_title_moji01`（顶部「オプション」）因背后有滚动动画，按用户决定**保留原版**。
右下角「标签 + ▮▮冊/▮▮%」模板图只替换左半标签、按键提示条只替换右侧文字（`splitMode`），剩下的数字 / `???` / 模糊动画帧 / 美术字按 §8.4 跳过。

五个必须知道的坑（都在同文档 §8.3）：主色取最亮 30%（不能取均值）、on 态用 body bbox 定位、
**必须 alpha over 合成**（取大者会让按钮图上的字变空白）、胶囊按钮清除区内缩 12px、
逐字动画图中文更短时多出的帧写空。

不做：`UI_29_nowloading` 艺术字（用户要求）、`system-menu_moji_*` 模糊动画帧、light 光斑层。

### 10.4 下一会话

1. **实机验收**（三件事：1px 细线 / 存档界面中文 / 98 张 UI 图），需完全退出游戏再进；
2. `between_the_open` 165 张（开放之间），往 `remake-ui-text.fsx` 表里加条目即可；
3. 若验收发现问题：字号改表里 `BodyH` 一个数、缺字用 `--keep-all` 或把码位加进需求集重跑。
