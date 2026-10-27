# UI 文字图「现役 / 遗留」甄别（侦查阶段结论）

> 时间：**2026-10-20**（本文件只记录侦查结论与待验证项；重制工作下一会话展开）。
> **2026-10-20 第二轮已补做 procmon 全覆盖 + aex 名字表判据 + 探针装机 → 见 §7；
> §3 的 A~D 分布与 §4.2 的「Font00 实时渲染」猜测已被 §7 修正。**
> 输入：`work/inventory/ae-image-inventory-v2.json`（852 HIP）、`work/inventory/exe-resource-names.txt`、
> `work/inventory/aex-texture-names.txt`（§7 新增）、`work/inventory/procmon-accessed-paths-short.txt`
> （2026-10-18 抓包 + **2026-10-20 全覆盖抓包**合并）。
> 输出：`work/inventory/ui-images-verdict.tsv`（每个文件一行判定，5 列：verdict/pack/size/file/aex）。
> 复现：`dotnet fsi scripts/exe-resource-names.fsx` → `dotnet fsi scripts/aex-names.fsx`
> → `dotnet fsi scripts/procmon-pac-access.fsx` → `dotnet fsi scripts/ui-image-triage.fsx`

## 1. 一句话结论

**静态证据已经能砍掉一大半工作量**：`data/AE` 852 张 HIP 全部落在 A~D 四级（无一张落入「完全无引用」），
但**判「现役」的最强证据仍缺实机复核** —— 本次抓包只覆盖「标题画面 + 序章对话」，
系统菜单 / 设置 / 回想 / 开放之间 / 章节标题**一个都没进过**。
⇒ 下一会话第一步是**一次全覆盖的 Procmon 巡查**（见 §5），再用探针图定死图级归属。

## 2. 判据（三级 + 一个已证伪的反例）

| 级别 | 判据 | 强度 |
|---|---|---|
| ① 实机加载（procmon） | `data\AE\<dir>\<pac>` 在日志里出现过 | **铁证**（引擎确实读了该 pac） |
| ② exe 资源名字表 | 图名（含 `%02d` 通配）出现在 `Getsuei.exe` 的 ASCII 名字表里 | **强**（代码里有引用，但可能是死代码） |
| ③ pac 名命中 | 只有 pac 文件名在表里，图名没有 | 中（图名可能由代码拼接） |

**反例（必须记住）**：`title/UI_02_title.pac` 属于 ①（procmon 显示被加载）+ ②（`title_menu_00…71` 全在表里），
**但 2026-10-19 实机改图无效** —— 现役菜单是 `data/story/image/` 下的散装 `title_menu_01..09_on/off`。
⇒ 「①+②」仍可能是**死资源**（旧代码路径）。**图级归属只能靠探针图实测**。

## 3. 判定总览

`data/AE` 852 张 HIP（161 个 pac，递归展开双层嵌套）：

| 等级 | 张数 | 含义 |
|---|---|---|
| A 名字命中 + pac 实机加载 | 107 | 现役（个别 pac 内有死图，见 §4.3） |
| B pac 实机加载 | 171 | 现役（图名由代码拼接） |
| C 图名命中 exe 表 | 194 | 强候选（本次会话未进该界面） |
| D 仅 pac 名命中 | 380 | 候选（多为框线/底衬） |
| E/F 无任何引用 | **0** | —— |

## 4. 按 pac 的结论（只列含文字的 pac）

含文字图共 **372 张**（台账 `ae-ui-images.md` 的 ✅ 行）。

| pac | 含文字 | 等级 | 结论 |
|---|---|---|---|
| `adv/UI_00_key_help.pac` | 1 | A | ✅ 现役，按键说明图可重制 |
| `adv/UI_10_quick_save.pac` | 1 | A | ✅ 现役（QUICK SAVE） |
| `common/g_dialog.pac` | 37 | A | ✅ 现役；`g_dialog_botan_No/Yes_*` **命中**，但 `g_dialog_*_message`、`g_dialog_No/saisho/tuzuki` **未命中**（§4.2） |
| `option/options_screen.pac` | 42 | A | ✅ 现役；`option_mode_Description_01..10` **命中（是图）** ⇒ **设置界面可走图像重制**；`ST_modo_moji`/`ST_title_moji` 未命中 |
| `special/between_the_open.pac` | **165** | C | 强候选（开放之间的图鉴/图鉴标题/概率/角色名）；本次未进界面，**需巡查** |
| `system_menu/system_menu.pac` | 34 | C | ⚠️ **框线命中、文字全未命中**（`system-menu_moji`/`ST_title_moji`/`ST_modo_moji`）→ 疑似「文字走 Font00 实时渲染」 |
| `backlog/back_log.pac` | 12 | C | `title_face_00..` 命中；`back_log_title*`/`back_log_title_moji_light` 未命中 → 同上可疑 |
| `adv/chapter_title_*.pac` ×20 | 40 | D | pac 名 + exe 里 **60 条** `chapter_title_date_X-Y_in/loop/out` 命中 ⇒ **现役强候选**，旧「打补丁无效」应是被错路径误导，需重验 |
| `common/UI_29_nowloading.pac` | 6 | B | pac 实机加载；`nowloading_word` 图名未命中（代码拼接）→ 现役，可重制 |
| `title/UI_02_title.pac` | 34 | A（假） | ❌ **已知遗留**（§2 反例），其中的 `title_menu_00..71` / `title_start*` 不要动 |
| `common/UI_99_fade.pac`、`adv/choices.pac` | 0 | C/D | 无文字，跳过 |

### 4.1 命名不一致（重要线索）

exe 名字表与 pac 内文件名的差异，暴露了「新版 vs 旧版」两套命名：

| pac 内文件名 | exe 名字表 | 判断 |
|---|---|---|
| `Title-base.hip` | `Title_base.hip` | pac 内是旧命名 → 可疑 |
| `g_dialog_saisho_off` / `g_dialog_tuzuki_off` | `g_dialog_botan3_saisho_off` / `botan3_tuzuki_off` | 旧版无 `botan3_` |
| `g_dialog_kou-scenario_message` | `g_dialog_kou_scenario_in/loop/out` | 旧版用 `_message`（静态图），新版用 `_in/loop/out`（动画序列） |
| `BGM_modo_off` / `Message_modo_off` | `BGM_modo_text01_off` / `Message_modo_text01_off` | 旧版无编号 |

### 4.2 一个必须解释的现象

**「文字图」大多不在 exe 名字表里，而「框/线/底衬/图标」几乎都在。**
若属实，则系统菜单 / 回想界面的文字是 **Font00 实时渲染**（假名字形够用：250 字形含 ASCII+假名+「字」「漢」），
那就意味着：**汉化系统菜单不是改图，而是「扩 Font00 + 找到文本源」** —— 而文本源至今未定位
（exe 的 UTF-16 区只有 Steam 成就；SJIS 扫描只有零星命中；ADVScript 里 0 条日文）。
⚠️ 这是下一会话的**核心未知**，直接决定系统菜单能不能汉化。

> **❌ 2026-10-20 第二轮：上述「Font00 实时渲染」推断已被推翻。**
> 名字不在 exe 里是因为**名字写在各 pac 内嵌的 `.aex`（ArcSys UI 定义）里**，是 ASCII 单字节串，
> 形如 `TEX\system-menu_moji_01.png`。aex 内**无任何日文/全角文本**（逐字节验证 ✅），
> 只有纹理名与布局浮点数据。⇒ 文字是**预渲染图**，能改图汉化。详见 §7。

## 5. 待实机复验（下一会话第一件事）

### 5.1 一次全覆盖巡查（判 pac 级现役）

1. `cmd /c scripts\ida-mcp-run.cmd` 不需要；改用 **Procmon** 开过滤
   `Process Name is Getsuei.exe` + `Path contains Getsuei Gakuen`，开始捕获；
2. 启动游戏，按顺序做：**标题画面停留 → 系统菜单（逐项进出）→ 设置（音/画/键位各页）→
   存档 → 读档 → 回想（翻两页）→ 开放之间（有解锁项的话）→ 新游戏读到第 1 章日期卡**；
3. 存 Capture，导出 XML 覆盖 `work/procmon/Logfile.xml`，跑
   `dotnet fsi scripts/procmon-pac-access.fsx` → `work/inventory/procmon-accessed-paths-short.txt`；
4. 重跑 `dotnet fsi scripts/ui-image-triage.fsx` → 看哪些 pac 从 C/D 升到 A/B。

### 5.2 探针图（判图级归属，只需 6~7 张）

把目标图涂「品红 + 白色编号」，回写嵌套 pac（做法同 `scripts/smoke-patch.fsx hipimg`，
下轮需扩成批量），进对应界面看是否变色：

| # | 探针文件 | 界面 | 看什么 |
|---|---|---|---|
| 1 | `system_menu.pac/tex.pac` → `system-menu_moji_01.hip` | 系统菜单 | 第 1 项是否变品红 ⇒ 图 vs 实时渲染 |
| 2 | 同上 → `ST_title_moji.hip` | 系统菜单 | 界面标题行 |
| 3 | `options_screen.pac/tex.pac` → `ST_modo_moji_01.hip` | 设置 | 设置项名（对照 `option_mode_Description_01` 的说明文字） |
| 4 | `back_log.pac/tex.pac` → `back_log_title_moji_light_01.hip` | 回想 | 回想标题行 |
| 5 | `chapter_title_1_10.pac/tex.pac` → `chapter_title_date_1-10_A.hip` | 第 1 章开头 | 日期卡（重验旧结论） |
| 6 | `UI_29_nowloading.pac/tex.pac` → `nowloading_word_0.hip` | 任意加载画面 | NOW LOADING 字样 |
| 7 | `g_dialog.pac/tex.pac` → `g_dialog_st_save_message.hip` | 存档确认 | 对话框文字 |

### 5.3 判定完成后怎么用

- 探针变色 ⇒ 该组图**现役**，按 title_menu 的流程重制（原版尺寸、16 倍数宽、on/off 槽位规则见 `docs/handover.md` §四/§五）；
- 探针不变色 ⇒ 该组图**遗留**，从台账划掉，转去查文本源（§4.2）。

## 6. 已知风险

1. **不要用「pac 被加载」当图在用的证据**（title_menu 反例）。
2. 探针图会进游戏目录，改前必须备份（`smoke-patch.fsx` 已自动备份到 `work/backup/`）。
3. 改 `data/story/image/` 或 AE 下的文件后**必须完全退出游戏再进**，否则引擎沿用旧纹理。

---

## 7. 2026-10-20 第二轮：procmon 全覆盖 + aex 判据 + 探针装机

### 7.1 procmon 抓包已覆盖全部目标界面 ✅

新日志 `work/procmon/Logfile-20261020.xml`（36.8 MB，用户按自己的顺序跑了一圈：标题 → 系统菜单 →
设置 → 存档/读档 → 回想 → 开放之间 → 第 1 章开头）。旧日志同日改名成 `Logfile-20261018.xml`
（`Logfile_full-20261018.xml` 是同一会话的超集，脚本默认跳过）。

`scripts/procmon-pac-access.fsx` 已改成**自动汇总 `work/procmon/Logfile-*.xml`**（可传参指定），
输出仍是 `work/inventory/procmon-accessed-paths-short.txt`（615 条去重路径）。

**新增加载的 AE pac（相对 10-18）**：`backlog/back_log.pac`、`system_menu/system_menu.pac`、
`special/between_the_open.pac`、`cg_thumb/cg_01011_thumb.pac`。
⇒ `data/AE` 下 **13 个 pac 全部实机加载过**，只剩 `adv/chapter_title_*.pac`（20 个）与 `adv/choices.pac` 未加载。

### 7.2 关键发现：UI 图的名字写在 `.aex` 里，不是在 exe 里 ✅

`data/AE` 的 161 个 pac 里有 32 个内嵌 `aex.pac/*.aex`（4 字节魔数 `1XEA` = 反序 "AEX1"，内含反序标签
`JORP`/`REYL` = PROJ/LYER）。用 ASCII 单字节扫这些 aex，得到 **1663 条纹理名引用 / 1104 个唯一图名**，
形如 `TEX\system-menu_moji_01.png`、`back_log_title_moji_light_01.png`。

- aex 内**没有任何日文/全角文本**（UTF-16LE 与 SJIS 双向扫过，命中的全是二进制噪声）；
- ⇒ **§4.2 的「文字走 Font00 实时渲染」不成立**，文字图就是图，能按 `title_menu` 那套流程重制。

新工具 `scripts/aex-names.fsx` → `work/inventory/aex-texture-names.txt`，已作为判据并入
`scripts/ui-image-triage.fsx`（verdict.tsv 第 5 列 `aex` 标记名字来自 aex）。

### 7.3 判定总览（加入 aex 判据后）

852 张 HIP：

| 等级 | 旧（10-18 日志 + exe 表） | 新（10-20 日志 + aex 表） | 说明 |
|---|---|---|---|
| A 名字命中 + pac 加载 | 107 | **607** | 铁证现役（其中 660 张的名字来自 aex） |
| B 仅 pac 加载 | 171 | 22 | 现役，名字仍未知（`UI_99_fade` 等） |
| C 名字命中但 pac 未加载 | 194 | 223 | 220 张 = `chapter_title_*` 20 个 pac，2 张 `choices`，1 张 `UI_99_fade` |
| D 仅 pac 名命中 | 380 | 0 | —— |
| E/F | 0 | 0 | **无死资源** |

⚠️ A 里含已知假阳性：`title/UI_02_title.pac` 48 张（§2 反例，勿动）。

### 7.4 按 pac 的结论（含文字的 pac）

| pac | 含文字 | 结论 |
|---|---|---|
| `system_menu/system_menu.pac` | 34 | ✅ **现役**（pac 加载 + aex 引用 `ST_modo_moji_01..08`/`ST_title_moji*`/`system-menu_moji_01..09`）→ 可重制 |
| `option/options_screen.pac` | 42 | ✅ 现役；`ST_modo_moji_01` 原图实测是「セーブ」，`option_mode_Description_*` 是说明文字 |
| `backlog/back_log.pac` | 12 | ✅ 现役；`back_log_title_moji01..05` 是逐字图（`_moji01` = 「バ」），`_light_01..07` 是辉光层 |
| `special/between_the_open.pac` | 165 | ✅ 现役（进过界面）；`open_mode_moji_01` 实测是「CG鑑賞」，`open_title_moji_*`/`chara_name_*`/`open_rate_*` 均可重制 |
| `common/g_dialog.pac` | 37 | ✅ 现役；`g_dialog_st_save_message` 实测是「システムデータをセーブしています。…」 |
| `adv/chapter_title_*.pac` ×20 | 40 | ❌ **判为遗留**（见 §7.5） |
| `title/UI_02_title.pac` | 34 | ❌ 已知遗留（勿动） |
| `common/UI_29_nowloading.pac` | 6 | ✅ 现役；`nowloading_word_1..6` 是逐字图（`word_1` = 「読」） |

### 7.5 ⚠️ 章节日期卡不是 `chapter_title_*.pac`，而是散装 `day_*.hip`

10-20 日志里第 1 章开头（`scr_10_1-01` / `text_10_1-01` 都加载了）同时加载了：

```
data\story\image\day_0..day_9.hip      ← 数字 0-9
data\story\image\day_mon/sun/….hip     ← 月火水木金土日
data\story\image\day_underbar.hip / day_white_bg.hip / day_brank_bg.hip
```

而 **20 个 `adv/chapter_title_*.pac` 一个都没被加载**。脚本串里也有 `DAY_TITLE` 命令与 `day_id` 变量
（`work/inventory/script-sjis-scan` 的产出）⇒ 日期卡由脚本 `DAY_TITLE` 驱动、用散装 `day_*.hip`
（TGA type9，48×64 / 56×64 / 64×64）拼装。

⇒ 原 §5.2 探针 #5「改 `chapter_title_1_10.pac` 验日期卡」**作废**；`chapter_title_*.pac`（220 张）
从工作量里划掉。若要汉化日期显示，改的是 `day_mon..sun` 这 7 张（可考虑改成「一…日」，但中文玩家
读「月火水木金土日」无障碍，优先级低）。

### 7.6 探针图已装机并实机验证 ✅（2026-10-20，用户复核）

`scripts/probe-images.fsx`（新工具）：把目标 HIP 涂成**不透明纯色**（保持尺寸/格式/偏移不变），
回写「tex.pac → 外层 pac」两层包，自动备份到 `work/backup/AE/`，并做「重编码后是纯色 + 尺寸未变 +
pac 回包后可回读」三重自检。

| # | 颜色 | pac / 成员 | 界面 | 看什么 | **实机结果** |
|---|---|---|---|---|---|
| 1 | 品红 | `system_menu/ST_modo_moji_01.hip` | 系统菜单 | 第 1 项「セーブ」 | ✅ **变色**（品红块出现在第 1 项位置） |
| 2 | 青色 | `system_menu/ST_title_moji.hip` | 系统菜单 | 标题「項目を選択して下さい」 | ✅ **变色**（顶部青色长条） |
| 3 | 黄色 | `options_screen/BGM_modo_text01_on.hip` | 设置 | 「BGM音量」高亮态 | ✅ **变色**（黄块盖住「BGM音量」蓝字位置） |
| 4 | 绿色 | `back_log/back_log_title_moji01.hip` | 回想 | 标题首字「バ」 | ✅ **变色**（标题左侧绿块） |
| 5 | 蓝色 | `between_the_open/open_mode_moji_01.hip` | 开放之间 | 「CG鑑賞」 | ✅ **变色**（列表首行蓝条） |
| 6 | 橙色 | `g_dialog/g_dialog_st_save_message.hip` | 系统数据保存 | 「システムデータをセーブしています」 | ✅ **变色**，出现在「从选项/开放之间回主界面」时（该图是**自动存档**提示，不是存档页面） |
| 7 | 紫色 | `UI_29_nowloading/nowloading_word_1.hip` | 加载画面 | 「読」 | ✅ **变色**（环形加载动画上方紫块） |
| 8 | 青绿 | `options_screen/BGM_modo_text01_off.hip` | 设置 | 「BGM音量」普通态 | ✅ **变色**（焦点移到音量图标时，「BGM音量」位置变青绿） |
| 9 | 玫红 | `options_screen/option_mode_Description_01.hip` | 设置 | 底部说明「BGMの音量を変更します」 | ✅ **变色**（底部说明条整段变玫红） |
| 10 | 淡紫 | `options_screen/option_title_moji01.hip` | 设置 | 顶部标题「オプション」 | ✅ **变色**（左上角标题位置变淡紫） |

**结论：10/10 全部确认 —— UI 文字就是预渲染图**，按 `title_menu` 流程改图汉化，不需要动 Font00。
（覆盖 7 个界面：系统菜单 / 设置 / 回想 / 开放之间 / 系统数据保存 / 加载画面）

**设置界面的两条副结论（2026-10-20）**

- `options_screen.pac` 里的 `ST_modo_moji_01..06` / `ST_title_moji` / `ST_mode_Description_03`
  **是从 system_menu 复制过来的遗留品**（原图内容实测是「セーブ」「ロード」「項目を選択して下さい」
  「オプションウィンドウを開きます」），**不在此界面显示** —— 3 号旧目标就是它们之一（装上无变化，也不崩）。
- 设置界面真正用的是 `option_*` / `BGM_modo_text0X_*` / `Message_modo_text0X_*` /
  `option_mode_Description_*` 系列。原图内容逐张核对过：`option_title_moji01`「オプション」、
  `_02`「メッセージを設定します」、`_03`「サウンドを設定します」、`BGM_modo_text01_on`「BGM音量」蓝字、
  `_off` 白字、`option_mode_Description_01`「BGMの音量を変更します」。
  **重制时只做这些，别碰 ST_\* 系列。**

**设置界面重制清单（2026-10-20 逐张核对原图内容得出，重制时照这个做）**

| 角色 | 文件（均在 `option/options_screen.pac/tex.pac`） | 原图内容 |
|---|---|---|
| 顶部标题 | `option_title_moji01` / `_02` / `_03` | オプション / メッセージを設定します / サウンドを設定します |
| 页签 | `option_window_text_01_on/off`、`option_window_text_02_on/off` | サウンド / メッセージ（蓝=选中、灰=未选） |
| 声音页三项 | `BGM_modo_text01..03_on/off` | BGM音量 / SE音量 / ボイス音量 |
| 消息页三项 | `Message_modo_text01..03_on/off` | メッセージ速度 / オートリード速度 / スキップモード |
| 底部说明 | `option_mode_Description_01..10` | 「BGMの音量を変更します」等，随焦点变化 |
| 滑块/箭头/图标 | `gauge*` / `gauge_arrow*` / `mute_icon*` | 无文字，不必译 |
| 数值（50/40/80、既読/強制） | **实时渲染**（数字/状态值随设置变化） | 不要做图 |

⚠️ `ST_modo_moji_*` / `ST_title_moji` / `ST_mode_Description_03` / `ST_Light_line_*` / `Title-base`
等 `ST_*` 系列是**从 system_menu 或旧版复制过来的遗留**，重制时不要动。

查看任意 AE pac 内图片：`dotnet fsi scripts/ae-export.fsx option/options_screen.pac --all`
（PNG 落到 `work/ae-png/<pac 名>/`）。

命令：`dotnet fsi scripts/probe-images.fsx list|export|apply [编号] [--pad]|verify|restore`
（`export` 会把原图/探针图 PNG 写到 `work/probe-preview/`，已逐张确认原图内容就是日文文字图）。
⚠️ 复核完务必 `restore`（当前 2026-10-20 装的是 3/8/9/10 四张设置界面等长探针）。

### 7.7 顺带修掉的两个核心库 bug（都跟 pac 文件名有关，均已实机验证）

**(a) 读：`BinaryReader.ReadChars(StringSize)` 的 n 是字符数、不是字节数** ——
遇到 `options_screen.pac` 内层 8 条**非 ASCII 名字**就错位并抛异常。已改成
`ReadBytes + 解码`（`docs/formats/pac.md` §2.1）。

**(b) 写：pac 条目名是混编码的，必须原字节回写** —— 那 8 条是
`window_frame_Ｌ_light_01..08.hip`，全角Ｌ = **Shift-JIS 双字节 `0x82 0x6B`**
（与半角 `window_frame_R_light_*` 成对，左右窗口边框）。
用 UTF-8 解码再写回会把它变成 `EF BF BD 6B`（U+FFFD + 'k'）⇒ **名字被改写** ⇒
引擎按名字/哈希查不到 ⇒ **进设置菜单闪退**（探针 3 两次装机实测）。

修法（`scripts/Pac.fsx` 已改 ✅）：
1. `PacEntry.FileNameBytes` 保存原始定长字节，`build` **原样写回**，不做任何重新编码；
2. `FileName` 只是给人看的：严格 UTF-8 → 失败则 CP932（需 `RegisterProvider`）；
3. `FileNameHash` 沿用原条目 4 字节，不重算。

修复后验证：
- 原版备份 `work/backup/AE` roundtrip **8/8 逐字节一致**；游戏 data 全量 **2201 OK / 0 FAIL**；
- 台账里名字已正确显示为 `window_frame_Ｌ_light_01..08`（旧版是 `window_frame_�k_light_*`）；
- 等长探针（`apply 3 --pad`）后，`scripts/pac-bytediff.fsx` 显示**只有 1 个差异段 0x140018..0x141255
  = 目标图数据区（4669 字节）**，文件名/偏移/哈希全未变。

**两条工程教训（重要）**

1. **roundtrip 全绿证明不了写回逻辑正确** —— 它只证明「读回来的值 → 写回去」自洽。
   上面两个 bug 都是「解析时把值改了」，2201 个 ASCII 名 pac 全绿也照样崩。
2. **解析器有 bug 时，基于它的 diff 会假阴性** —— `pac-diff` 比较的是解析后的字段，
   两边用同一个坏解析器 → `Filename` 都是 U+FFFD → "结构一致"。

---

## 8. 2026-10-20 深夜：UI 文字图重制实际开工（✅ 98 张已装机，待实机验收）

工具：**`scripts/remake-ui-text.fsx`**（表驱动，`list` / `preview` / `apply` / `restore`）+
配套的 `scripts/ui-inspect.fsx`（量 bbox）、`scripts/ui-contact-sheet.fsx`（拼对照图）、
`ae-export.fsx --rgba`（保 alpha 导出，判断"有框/无框"必须用这个）。

### 8.1 已完成（表在脚本里，`list` 可打印）

| pac | 张数 | 内容 |
|---|---|---|
| `system_menu/system_menu.pac` | 25 | 菜单项 8（存档/读取/设置/返回标题/返回/删除存档/前往开放之间/存档）+ 标题「请选择项目」+ 逐字 8 帧（系/统/菜/单 + 4 帧清空）+ 底部说明 7 + 按键条 `icon_16` |
| `option/options_screen.pac` | 31 | 标题 2 + 页签 4 + 音量/消息项 12 + 底部说明 13（含 `_on` 高亮态）。⚠️ `option_title_moji01` 不重制，见 §8.4 |
| `backlog/back_log.pac` | 5 | 标题逐字「对话记录」4 帧 + 末帧清空 |
| `common/g_dialog.pac` | 37 | 结局/剧本/系统提示 27 张（两行）+ 胶囊按钮 10 张（是/否/从头开始/继续游戏） |
| `special/between_the_open.pac` | 76 | 开放之间：标题 7（开放之间/CG鉴赏/BGM鉴赏/**角色资料**/客串剧本/结局/成就）+ 模式名 7 + 达成率/开放率红字 2 + 说明 2 + **角色名 20** + 个人资料项 10×2 态 + 右下角模板标签 5 + **按键提示条 13** |

合计 **174 张**。校验：5 个 pac 全部 `pac-diff --backup` **结构一致，差异仅在预期条目内容** ✅

### 8.2 做法（渲染链路）

1. 解码 HIP（Argb32，字节序 **BGRA**）→ RGBA；
2. 量**两组 bbox**：
   - `ink`（alpha ≥ 16，含辉光/抗锯齿）→ **清除范围**；
   - `body`（alpha ≥ 200 且不太暗）→ **定位基准**；
3. 清除区填**底色**（区域内像素按亮度取**中位数**）：透明底得到 `(0,0,0,0)`，
   胶囊按钮得到底色本身 ⇒ 框/底不会被清掉；
4. 用 Deng.ttf **统一字号**（「漢」墨迹高 = `BodyH`，字号由 `calibrate` 反算）渲染中文，
   水平+垂直居中于原文字 bbox；
5. 叠加顺序：on 态辉光 → 黑描边（dilate 2，gain 0.95）→ 加粗层（dilate 1，gain 0.92）→ 字身；
6. 编码回 Argb32 + RawRepeat，**画布尺寸/偏移一律不变**（引擎按原尺寸算槽位）。

### 8.3 五个必须知道的坑（都实测踩过）

1. **主色不能取均值**：原图是「白字 + 黑描边」，均值会把字拉成灰 ⇒ 取 alpha≥200 里**最亮 30%** 的均值（on 态字芯是白，边缘是蓝，全均值会偏暗偏蓝）。辉光色另取（alpha 16..150 里**饱和度最高**的像素）。
2. **on 态 bbox 被辉光撑大**（bbox 高 53 vs 字身 39）⇒ 用它定位会把字压低 4~7px。必须用 body bbox 定位、ink bbox 清除。
3. **不能用"alpha 取大者"覆盖**：按钮图底色不透明，字身抗锯齿边缘 alpha 比底色低 ⇒ 字只剩骨架、甚至完全空白。必须走**标准 alpha over 合成**。
4. **胶囊按钮的框不能清**：`*botan*` 图的 ink bbox 就是整个胶囊。做法 = 清除区**内缩 12px** + 中位数底色填充 + `brightOnly` 判据（只把亮像素当字身，否则半透明底会把 bbox 撑满）。
5. **逐字动画图中文更短**：原版「システムメニュー」8 帧 / 「バックログ」5 帧，
   中文「系统菜单」4 字 /「对话记录」4 字 ⇒ **多出来的帧写空串**（清成透明），动画表现为"打完就停"，自然。
6. **复合图只替换其中一半**（`splitMode`）：两类图不能整块清：
   - **按键提示条**（`Icon_*` / `icon_16`）：左边是手柄/鼠标小图标，右边是文字。
     图标与文字之间往往只有 2~3px 空隙，"找空白列"会失败（实测 4/6 张失败）⇒ 改用
     `splitAfterIcon`：**取第一段连续有内容的列**（= 图标）作为分界，比找空白稳。
     另外这类图**字色必须强制白色** —— 图上图标的彩色像素（红色按键）会被"最饱和取色"选中，
     把整句文字染成红色。
   - **右下角「标签 + ▮▮冊 / ▮▮冊 ▮▮%」模板**（`open_item_description_BGM/CG/bonus_scenario/ending/profile`）：
     右半是占位符模板，清掉就没了 ⇒ 只替换左半标签（`findSplit` 找标签后最宽的空白列）。

### 8.4 明确不做（用户要求 / 判断）

- **艺术字不重制**：`common/UI_29_nowloading.pac` 的 `nowloading_word_1..6`（「読」起头的环形加载艺术字）。
- **`option_title_moji01`（设置页顶部「オプション」）保留原版**（2026-10-20 用户定）：
  这张图背后有一层**滚动/切换的「オプション」动画**，前景换成 2 字中文会与背景日文叠在一起很难看；
  把动画也换中文则变成"2 个字来回蹦"，更糟。⇒ 删掉表里这条即可（脚本 `list` 里已没有它）。
- `system_menu` 的 `system-menu_moji_01..09`：**逐字滑入的模糊动画帧**（bbox 从 x=0 递增到 230），
  重制成本高、收益低，保留原样。同理开放之间的 `open_title_moji_0X_effect_01..08`。
- `back_log_title_moji_light_01..07`：纯模糊光斑层（不含文字）。
- `title/UI_02_title.pac` 34 张（§2 已证伪的遗留）。
- 数值（50/40/80、既読/強制）是**实时渲染**，不做图。
- **开放之间里这类别动**（`remake-ui-text.fsx` 表里已有注释）：
  - `open_item_description_hatena` / `open_mode_moji_06` / `profile_item11_*`：内容是 `???`；
  - `open_item_description_0..9`、`open_rate_0..9`、`number_difference0..9`：纯数字；
  - `profile_item10`：箭头图标；`chara_name_21`：`マスター` 未译（保持与对话说话人名一致）；
  - `bonus_scenario_text_kitou/mafia`：「鮮血の蜃気楼」等**蓝色发光美术字**，同 loading 艺术字不重制。
- 说明：`open_item_description_BGM/CG/bonus_scenario/ending/profile` 与 `Icon_*`/`icon_16` 按键条
  已按 §8.3 第 6 条「只替换一半」的方式做完，不再属于跳过项。

### 8.6 第一轮实机反馈修掉的三个问题（2026-10-20 夜，✅ 已修并重装）

用户实机跑设置界面后的反馈与对应修法：

| 反馈 | 根因 | 修法 |
|---|---|---|
| 「声音设置」「音效音量」「声音」on 态等**有背景方框**（黑/蓝色块） | 清除区填底色用了**中位数**——2~4 个字的图里文字几乎占满清除区，中位数落在**文字**上 ⇒ 填出色块 | 纯文字图（`inset = 0`）**一律填全透明**；只有胶囊按钮才取底色，且改成**四角小块中位数**（胶囊内部有横纹、alpha 不均，取整区会偏淡） |
| 右下角说明文字有**奇怪的光晕**（不是所有选项都有） | 辉光按 `_on` 后缀一律加 —— 但 `option_mode_Description_02_on/_off` 的 on/off 指**开关状态**，不是高亮态 | 改成**按原图自身判断**：`ink bbox 面积 / body bbox 面积 > 1.25` 才算有辉光；再加 `BodyH ≥ 28` 限制（20px 小字加辉光必糊） |
| 说明文字**略显模糊** | 小字（20px）用了 2px 描边 + 高 gain | 小字描边降到 1px、gain 0.75 |
| on 态按钮字**发暗**（「是」「否」） | 字色取"最亮 30% 均值"，被辉光/半透明底拉暗 | 改**逐通道最大值**（单色文字时就是本色） |
| 「達成率」「開放率」红字被画成**白字** | 这两个是彩色美术字（**白字芯 + 红描边 + 红辉光**），逐通道最大取到白芯就丢了红色调 | 字色优先取**最饱和像素的颜色**（饱和度 > 100 时用饱和色，否则才走逐通道最大）——红字美术图这才对，纯白/浅蓝高亮态不受影响 |

备忘：对比图里偶尔看到的"暗块"是原图 **alpha=0 但 RGB 有值**的残留（`ae-export` 的 bg=40 合成后才显出来），
游戏里 `alpha=0` 不绘制，**不是图的问题** —— 这也是为什么 `ui-inspect.fsx` 必须看 alpha 而不是看 PNG。

### 8.5 待办

- ~~`special/between_the_open.pac` 165 张~~ ✅ **76 张已重制**（2026-10-20 深夜）：
  标题 / 模式名 / 角色名 20 / 个人资料项 / 达成率·开放率红字 / 右下角模板标签 5 / 按键提示条 13；
  其余是数字、`???`、模糊动画帧、美术字，按 §8.4 跳过。
- ~~`Icon_*` / `icon_16` 按键提示条~~ ✅ 已完成（`splitMode = SplitRight`，见 §8.3 第 6 条）。
- 实机验收后再微调字号/描边（表里 `BodyH` 按原图 bbox 高估，实机若偏大偏小改一个数就行）。

### 8.7 实机反馈第二轮（存档界面 ✅ 通过）

- 存档界面：**中文正常，大小差不多**；中日字体混用略显杂但可接受（原字库里的日文汉字按原样保留）。
- 对话字体：**1px 细线已消失**（`font-rebuild.fsx` 列间隔 1px 生效）✅
   最终裁判必须是**纯字节对比**：新增 `scripts/pac-bytediff.fsx`（不经过任何解析）。

**本轮还新增的保险**：`probe-images.fsx apply --pad` 让探针图**尾部补 0 与原文等长**，
从而 Length/OffsetStart 全不变 —— 诊断时能排除"长度/偏移重排"这个变量。
