# ATF 对话文本格式

**这是汉化的主战场。** 分析依据 `ATFText/Program.cs`（C# 参考实现）+ 对 `string.atf`（12039 字节）的逐字节验证。工具：`scripts/AtfJson/`（F# / .NET 10，atf ⇄ JSON 双向）。

## 1. 文件总体布局

整个文件由 5 个区段**紧密排列**（无间隙、无对齐填充），全部为**小端序**：

```
0x0000  FileHeader          16 字节（固定）
0x0010  ParamHeader × 4     每个 16 字节（描述后 4 个区段的位置/数量/大小）
0x0050  TextHeader   × N    每个 0x30 (48) 字节
        StringHeader × M    每个 0x10 (16) 字节（M = 2N + 尾部空条目数）
        String Pool         窄字符串（ASCII）池，存放 key
        WString Pool        宽字符串（UTF-16LE）池，存放 value
[EOF]                        WString Pool 结束即文件结束
```

`string.atf` 实测数据（N = TextNum = 95，M = 192 = 95×2 + 2）：

| 区段 | Offset | Num | Size | 说明 |
| ---- | ------ | --- | ---- | ---- |
| TextHeader | 0x50 | 95 | 0x11D0 | 95 × 0x30 |
| StringHeader | 0x1220 | 192 | 0xC00 | 192 × 0x10 |
| String Pool | 0x1E20 | 1813 | 0x715 | Num = Size = 字节数 |
| WString Pool | 0x2535 | 1257 | 0x9D2 | Num = UTF-16 码元数，Size = Num × 2 |

## 2. 各结构体定义

### 2.1 FileHeader（16 字节，偏移 0x00）

| 偏移 | 类型 | 字段 | 说明 |
| ---- | ---- | ---- | ---- |
| 0x00 | u32 | Type | 魔数 `0x00465441`，即字节序列 `"ATF\0"` |
| 0x04 | u32 | TextNum | 文本记录数 N（等于 TextHeader 条数） |
| 0x08 | u32 | — | 填充，恒为 0 |
| 0x0C | u32 | — | 填充，恒为 0 |

### 2.2 ParamHeader ×4（各 16 字节，偏移 0x10–0x4F）

描述 4 个数据区段，顺序固定：TextHeader → StringHeader → String Pool → WString Pool。

| 偏移 | 类型 | 字段 | 说明 |
| ---- | ---- | ---- | ---- |
| +0x0 | u32 | Offset | 区段起始文件偏移 |
| +0x4 | u32 | Num | 条目数（池区段含义见上表） |
| +0x8 | u32 | Size | 区段字节大小 |
| +0xC | u32 | — | 填充，恒为 0 |

### 2.3 TextHeader（各 0x30 字节）

| 偏移 | 类型 | 字段 | 说明 |
| ---- | ---- | ---- | ---- |
| +0x00 | u32 | NameIdx | key 对应的 StringHeader 索引（= 2i） |
| +0x04 | u32 | TextIdx | value 对应的 StringHeader 索引（= 2i+1） |
| +0x08 | u32 ×6 | TextParam | 6 个参数（本文件全部为 0，语义未知） |
| +0x20 | u32 ×4 | — | 填充，恒为 0 |

**观测结论**：NameIdx/TextIdx 严格按 `2i / 2i+1` 顺序配对（无字符串去重/共享），TextParam 与尾部填充全部为 0。

### 2.4 StringHeader（各 0x10 字节）

共 M = 2N + T 条（T 为尾部空条目数，本文件 T = 2）：

| 偏移 | 类型 | 字段 | 说明 |
| ---- | ---- | ---- | ---- |
| +0x0 | u32 | Top | 字符串在池中的起始位置（单位见下） |
| +0x4 | u32 | Len | 字符串长度（单位见下） |
| +0x8 | u32 | — | 填充，恒为 0 |
| +0xC | u32 | — | 填充，恒为 0 |

- **偶数索引** → String Pool（key，ASCII）：
  - `Top` 单位 = 字节；`Len` = 字节数 **含** `'\0'` 终止符（即 `key.Length + 1`）
  - 每条在池中占 `Len + 1` 字节：内容 + `'\0'` + 1 字节 0 填充（即每条恒占 `key.Length + 2` 字节）
- **奇数索引** → WString Pool（value，UTF-16LE）：
  - `Top` 单位 = **UTF-16 码元**（寻址时要 ×2）；`Len` = 码元数 **不含** 终止符
  - 每条在池中占 `(Len + 1) × 2` 字节：内容 + `0x0000` 终止符
- 文件末尾存在 T 条 `Top = 0, Len = 0` 的**空 StringHeader**，未被任何 TextHeader 引用，是原始编译工具留下的尾部填充，重建时必须补回才能保证字节一致。

## 3. 解析 / 编译流程

### 3.1 parse（ATF → JSON）

1. 读 FileHeader：Type、TextNum，跳过 8 字节填充。
2. 读 4 个 ParamHeader，取得 4 个区段的 Offset/Num/Size。
3. 按 `ParamHeader[0]` 读 N 个 TextHeader（每个跳过后 16 字节填充）。
4. 按 `ParamHeader[1]` 读 M 个 StringHeader。
5. 对每条 TextHeader：
   - key = String Pool 中 `Offset + StringHeader[NameIdx].Top` 处的 `Len - 1` 个 ASCII 字节；
   - value = WString Pool 中 `Offset + StringHeader[TextIdx].Top × 2` 处的 `Len × 2` 个 UTF-16LE 字节；
   - `Len = 0` 时视为空串。
6. 一致性校验（不满足则报错，因为无法无损重建）：
   - `TextNum == ParamHeader[0].Num`；
   - `NameIdx == 2i` 且 `TextIdx == 2i+1`（无共享/去重布局）；
   - 未被引用的尾部 StringHeader 必须全部为空（Top = Len = 0），其数量记入 `trailingEmptyStringHeaders`。

### 3.2 build（JSON → ATF）

JSON 中**不保存**任何偏移/索引/长度，全部在编译时重建：

1. `N = records.Count`；key 编码为 ASCII（非 ASCII 报错），value 为 UTF-16LE。
2. 重建派生量：
   - `TextNum = N`
   - `StringHeader 数 = 2N + trailingEmptyStringHeaders`
   - `StringHeader[2i]   = { Top = strTop,  Len = key字节数 + 1 }`，`strTop += key字节数 + 2`
   - `StringHeader[2i+1] = { Top = wstrTop, Len = value码元数 }`，`wstrTop += value码元数 + 1`
   - `TextHeader[i] = { NameIdx = 2i, TextIdx = 2i+1, TextParam = record.textParam（缺省补 6 个 0）, 填充 ×16 }`
3. 按固定布局计算各区段 Offset：`0x50 → +0x30×N → +0x10×M → +name池字节数 → EOF`。
4. 顺序写出 FileHeader、ParamHeader ×4、TextHeader、StringHeader（末尾补 T 条空条目）、String Pool、WString Pool，所有填充位写 0。

## 4. JSON Schema

```jsonc
{
  "type": "ATF",                        // FileHeader.Type 魔数，缺省 "ATF"
  "trailingEmptyStringHeaders": 2,      // 尾部空 StringHeader 数，为 0 时可省略
  "records": [                          // 顺序即 TextHeader 顺序
    {
      "key": "NAME_00_0-01_01_001",     // ASCII，String Pool
      "value": "【　】",                 // UTF-16LE，WString Pool；可含 \n 等多行
      "textParam": [0, 0, 0, 0, 0, 0]   // 可选；全 0 时省略，写出时不足 6 个补 0
    }
  ]
}
```

- 修改 `value`、增删 `records` 都是安全的：所有偏移和长度都会自动重建。
- **汉化提示**：`value` 是 UTF-16LE，直接写中文即可通过 build；能否上屏取决于字库（见 abc-hip-font.md）。
- 输出编码使用 `UnsafeRelaxedJsonEscaping`，日文与 `<ruby>` 标签原样保留（全角空格 U+3000 仍会被转义为 `\u3000`，这是编码器行为，不影响往返正确性）。

## 5. 验证结果

- `string.atf → string.json → roundtrip.atf`：**12039 字节逐字节一致**。
- 编辑测试：修改 value（含日文/ruby 标签）、增删记录、多行 value、空 value，重建后重新解析结果与编辑内容一致。

## 6. 已知限制

- 仅支持 `NameIdx = 2i, TextIdx = 2i+1` 的顺序配对布局；若其他 ATF 文件使用字符串去重/共享，解析时会明确报错（可扩展为按索引引用池条目的通用模型）。
- key 必须是 ASCII；value 不支持孤立代理项等非法 UTF-16。
- TextParam 的语义未知（本文件全 0），仅做透传保留。

### 6.1 ⚠️ 显示约束：**不会自动折行，单行上限 24 全角字符**（2026-10-20 实机实测）

游戏把 `\r\n` 当作唯一换行手段，一行超过框宽就**直接溢出到边框外**（backlog 里能明显看到）。

标定方法（`text_10_1-03`）：

| 记录 | 文本 | 结果 |
|---|---|---|
| 10_1-03_01_044 日文 | `勿論、味噌汁も煮物も色がおかしい。というか赤い。`（24 全角） | 刚好放下 ✅ |
| 10_1-03_01_044 中文 | `当然，味噌汤和炖菜的颜色也很不对劲。或者说，是红的。`（26 全角） | 溢出 ❌ |
| 10_1-03_01_043 日文 | `白米、味噌汁、サバ味噌、サラダ、煮物、漬物……。`（24 全角） | 刚好放下 ✅ |

⇒ **阈值 24 全角字符**；`<ruby>基字<注音/ruby>` 的注音不占宽度，比较时要先剥掉标签只留基字。

检查工具：`dotnet fsi scripts/check-line-width.fsx --tsv`
→ `work/inventory/line-width-issues.tsv`（当前 **650 行**超宽，涉及 141 个 pac，最严重 `text_SP` 127 行）。

## 7. 用法

```powershell
cd scripts/AtfJson
dotnet run -- parse -i <input.atf>  [-o <output.json>]   # ATF -> JSON
dotnet run -- build -i <input.json> [-o <output.atf>]    # JSON -> ATF
# 省略 -o 时默认替换扩展名
```
