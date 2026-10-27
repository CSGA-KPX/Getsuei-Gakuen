# ADVScript 事件脚本格式

分析对象：`00_0-01_File.bin`（月英学園，Arc System Works 系引擎的事件脚本）。工具：`scripts/AdvScriptParser/`（F# / .NET 10，bin ⇄ JSON 双向）。

格式参考自 `ADVScript` 项目（`ADVScript/ADVScript/CScriptData.cs`），但不依赖其 `Config/*.json` 中的游戏特定命令定义 —— 二进制中的 `StrFlag` 字段本身已标明每个参数是整数还是字符串索引，可完全通用化地转储。

## 1. 二进制文件布局

所有整数均为 **32 位小端序**（`int32` / `uint32`）。

```
偏移      结构                说明
──────────────────────────────────────────────────────────────────
0x00     SAdvScrHeader       脚本头，16 字节
0x10     ParamHeader[3]      3 个区块描述符，每个 16 字节
0x40     CommandList         命令数组，每条 0x40 (64) 字节
...      StringHeader[]      字符串表头数组，每条 16 字节
...      StringBuffer        字符串数据区（NUL 结尾的字符串流）
```

### 1.1 SAdvScrHeader（16 字节）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x0 | u32 | Type | 脚本类型魔数，4 字节 ASCII（如 `"EVT\0"` = `0x00545645`） |
| +0x4 | u32 | Version | 版本号（本文件为 `1000`） |
| +0x8 | u32 | CommandNum | 命令条数 |
| +0xC | u32 | Flag | 未知标志位（本文件为 `0x0001000E`） |

### 1.2 ParamHeader（每个 16 字节，共 3 个）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x0 | u32 | Offset | 区块在文件中的绝对偏移 |
| +0x4 | u32 | Num | 区块条目数 / 字节数 |
| +0x8 | 8 字节 | Padding | 填充，实测全 0 |

| 索引 | 区块 | Offset | Num |
|------|------|--------|-----|
| [0] | 命令区 | 恒为 `0x40` | 命令条数（== CommandNum） |
| [1] | 字符串表头区 | `0x40 + CommandNum * 0x40` | 字符串个数 |
| [2] | 字符串数据区 | `ParamHeader[1].Offset + 字符串个数 * 0x10` | 数据总字节数（含 NUL 结尾） |

`ParamHeader[2].Offset + ParamHeader[2].Num` == 文件总长（无尾部数据）。

### 1.3 SAdvScrCommand（每条 0x40 / 64 字节）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x00 | i32 | Command | **命令名在字符串表中的索引** |
| +0x04 | i32 × 14 | Arg[14] | 14 个参数槽，未使用的槽为 0 |
| +0x3C | i32 | StrFlag | 位标志：第 j 位为 1 ⇒ `Arg[j]` 是**字符串表索引**；为 0 ⇒ 普通整数 |

`StrFlag` 是通用化的关键：不需要外部命令定义，直接从二进制判断每个参数的类型。

### 1.4 SAdvScrStringHeader（每条 16 字节）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x0 | u32 | StringIdx | 字符串在数据区中的相对偏移 |
| +0x4 | u32 | Length | 字符串字节长度（**不含**结尾 NUL） |
| +0x8 | 8 字节 | Padding | 填充，实测全 0 |

### 1.5 StringBuffer

连续的字符串字节流，每个字符串后跟一个 `0x00` 结尾字节。第 0 个字符串固定为 `"dummy"`（占位符，索引 0 不被实际使用）。字符串按 **Shift-JIS (codepage 932)** 解码。

## 2. `00_0-01_File.bin` 实测数值

```
文件长度:          184373 字节
Type:              "EVT" (0x00545645)
Version:           1000
CommandNum:        2718
Flag:              0x0001000E
ParamHeader[0]:    Offset=0x40     Num=2718   (命令区)
ParamHeader[1]:    Offset=0x2A7C0  Num=414    (字符串表头区)
ParamHeader[2]:    Offset=0x2C1A0  Num=3733   (字符串数据区)
```

一致性验证全部通过（区块偏移连续、StringIdx 连续、NUL 结尾、填充为 0）。

## 3. JSON 表示设计

**设计原则：JSON 中只保留真实数据，一切可由内容推导的字段都在还原（编译）时重建**：

| 数据 | JSON 中的表示 | 还原时 |
|------|--------------|--------|
| `strings`（字符串表） | **省略**（可推导时） | 按编译器构造规则从命令重建（见下） |
| `paramHeaders`（offset/num） | **省略**（标准布局时） | 按 `0x40` 基址 + 命令数 + 字符串表整体重算 |
| `stringHeaders`（stringIdx/length） | **省略**（标准布局时） | 按字符串表顺序连续重算 |
| 命令名索引 | 只存 `"name"` 文本 | 在字符串表中查找（首次出现位置），找不到则追加 |
| 字符串参数索引 | 参数直接写成 JSON 字符串 | 同上，按文本解析索引 |
| `StrFlag` | **省略** | 由参数实际类型（字符串/整数）自动重建 |
| 末尾未使用的 0 参数 | **省略** | 补齐到 14 个 |
| `commandNum` | 保留（仅供阅读） | 按实际命令数重建 |

字符串表的推导规则与原编译器（`CParsedScript.Compile`）的构造顺序完全一致：

1. 表头固定为 `"dummy"`（索引 0 占位）；
2. 按命令顺序收集所有**字符串参数**（按槽位序，首次出现时追加，序数去重）；
3. 再按命令顺序收集所有**命令名**（首次出现时追加，序数去重）。

推论：**不再被任何命令引用的字符串会在重建时自动从表中消失**；反之新增引用会自动进入表中。若确实需要保留未引用字符串，`dump` 会自动输出 `strings` 字段兜底（数组顺序 = 表中顺序，无法无损编码的字符串写作 `{ "$hex": "..." }`）。非标准布局（`isStandardLayout` 检测）时输出 `paramHeaders` / `stringHeaders`（含 `paddingHex`）兜底；`nameIndex` / `strFlag` 同理。

实际转储结果（`ADVScript/2` 格式）：

```jsonc
{
  "format": "ADVScript/2",
  "encoding": "shift_jis",
  "header": { "type": "EVT", "typeRaw": 5527109, "version": 1000, "commandNum": 2718, "flag": 65550 },
  "commands": [
    { "name": "scene", "args": ["File_01"] },
    { "name": "param", "args": ["bg", "posx", 115] }
    // 字符串参数直接写文本；整数参数写数字
  ]
}
```

## 4. 验证结果

- `bin → 模型 → bin`：逐字节一致；
- `bin → json → bin`：SHA256 一致；
- **编辑测试**：将字符串参数 `"File_01"` 改为新字符串 `"NewScene"` 后还原 —— 字符串表自动重建（`File_01` 消失、`NewScene` 按首次出现序入表，总数仍为 414），所有偏移自动重算，产物自身 roundtrip 一致。

## 5. 已知限制

- 字符串编码固定为 Shift-JIS (932)。本文件为纯 ASCII，不受影响；若其他文件含无法无损编码的字符，以 `$hex` 兜底或产生警告。
- 命令参数除"整数 / 字符串索引"外的语义（定点小数、标志位等）需要游戏特定知识 —— 但参数值本身全部原样保留，不丢失信息。
- `Flag` 字段含义未知，原样保留。

## 6. 用法

```powershell
cd scripts/AdvScriptParser
dotnet run -- dump    <input.bin> [output.json]   # 转储 bin -> json
dotnet run -- restore <input.json> [output.bin]   # 还原 json -> bin
dotnet run -- verify  <input.bin>                 # 内存中 roundtrip 校验
dotnet run -- info    <input.bin>                 # 打印结构信息
```
