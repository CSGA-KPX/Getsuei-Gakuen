# PAC 打包文件格式

pac 是 Arc System Works 系引擎的打包格式。定义参考 [arcsys 的 pac.rs](https://github.com/super-continent/arcsys/blob/main/src/pac.rs)，并经本游戏 `text_*.pac` / `scr_*.pac` 实测验证。

所有整数均为 **32 位小端序**。

## 1. 文件布局

```
偏移            结构                    说明
──────────────────────────────────────────────────────
0x00            Header                  32 字节
0x20            FileInfo[FileCount]     每条 16 + StringSize 字节
DataStart       文件数据区              各文件的原始字节依次存放（每条目对齐 16）
```

### 1.1 Header（32 字节）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x00 | char[4] | FourCC | 魔数 |
| +0x04 | i32 | DataStart | 数据区起始偏移 = HeaderSize + 元数据大小（即 `0x20 + FileCount × (12 + StringSize)`） |
| +0x08 | i32 | TotalSize | pac 文件总大小 |
| +0x0C | i32 | FileCount | 包含的文件数 |
| +0x10 | i32 | PacStyle | 风格标志位（见下） |
| +0x14 | i32 | StringSize | 每个文件名字段的字节长度（由 Header 指定，不能硬编码） |
| +0x18 | u64 | Padding | 填充，实测全 0 |

### 1.2 PacStyle 标志位

| 值 | 名称 | 说明 |
|----|------|------|
| 0x00000000 | Normal | 标准 |
| 0x00000001 | AutoLenFilename | 文件名长度自动 |
| 0x00000002 | IdOnly | 仅 ID |
| 0x00000010 | PathCut | 路径截断 |
| 0x20000000 | LongName | 长文件名 |
| 0x40000000 | HashSort | 按哈希排序 |
| 0x80000000 | Version2 | 版本 2 |

### 1.3 FileInfo（每条 16 + StringSize 字节）

| 偏移 | 类型 | 字段 | 说明 |
|------|------|------|------|
| +0x00 | char[StringSize] | Filename | 文件名，`\0` 结尾，按 StringSize 读取后 TrimEnd('\0') |
| +StringSize+0x00 | i32 | FileId | 文件 ID |
| +StringSize+0x04 | i32 | OffsetStart | **相对 DataStart 的偏移**（绝对偏移 = `DataStart + OffsetStart`） |
| +StringSize+0x08 | i32 | Length | 文件字节数（可为 0） |
| +StringSize+0x0C | u32 | FileNameHash | 见 §1.4（非 Version2 风格时为 0） |

### 1.4 文件名哈希

`FileNameHash` 为 4 字节 u32（小端），**对文件名小写形式**计算：

```fsharp
let hashName (fileName: string) =
    Text.Encoding.UTF8.GetBytes(fileName.ToLowerInvariant())
    |> Array.fold (fun (h: uint32) c -> uint32 c + 137u * h) 0u
```

（✅ 实测：`00_0-01_File.bin` 条目存储的哈希 = `hashName("00_0-01_file.bin")`，即按小写计算；
非 Version2 风格 pac 该字段恒为 0，见 §3。是否被运行时校验待验证。）

### 1.4.1 ⚠️ 文件名是**混编码**的，重打包必须原字节回写（2026-10-20 实机踩坑）

`data/AE/option/options_screen.pac` 内层 `tex.pac` 里有 8 条名字**不是 ASCII**：

```
window_frame_Ｌ_light_01..08.hip   原字节 … 5F  82 6B  5F 6C 69 67 68 74 …
                                             ↑ 全角Ｌ = Shift-JIS 双字节 0x82 0x6B
```

（与半角 `window_frame_R_light_01..08.hip` 成对，是左右窗口边框。）

后果链（连踩两次，都是同一个坑的不同表现）：

| 做法 | 结果 |
|---|---|
| 用 `Encoding.UTF8.GetString` 解码再 `GetBytes` 写回 | `82 6B` 非法 UTF-8 → 落成 `EF BF BD 6B`（U+FFFD + 'k'）⇒ **名字被改写** ⇒ 引擎按名字/哈希查不到 ⇒ **进设置菜单闪退** |
| 按名字重算 `FileNameHash` | 同样对不上 ⇒ 闪退 |

⇒ 正确做法（`scripts/Pac.fsx` 已改 ✅）：
1. `PacEntry` 保存 **`FileNameBytes`（存档里的原始定长字节）**，`build` 原样写回，不做任何重新编码；
2. `PacEntry.FileName` 只是给人看的：解码顺序 = 严格 UTF-8（非法字节抛异常）→ 失败则 CP932/SJIS；
3. `FileNameHash` **沿用原条目的 4 字节**（`e.FileNameHash`），不重算。

对纯 ASCII 名，这三件事都等价（2201 pac roundtrip 逐字节一致，看不出问题）；
只有这种混编码条目才会暴露，而且**不能用 roundtrip 发现** —— 见 §2.1 末尾的教训。

## 2. 解包要点

- 文件数据绝对偏移 = `Header.DataStart + FileInfo.OffsetStart`，读取 `Length` 字节；`Length = 0` 时为空文件。
- pac 内文本类文件按名字识别：`*.atf`（对话文本）、`*.abc`（字库索引）、`*.hip`（字库位图）。
- **hip 有两种子格式**：提取前 3 字节判断——`"HIP"` 开头为 HIP 专有格式（自研 `scripts/hip-tool.fsx` 转 PNG）；否则是 **TGA**，通用工具即可处理。

### 2.1 ⚠️ 文件名表必须按「字节」读，不能用 `BinaryReader.ReadChars`

`ReadChars(n)` 的 `n` 是**字符数**，而 FileInfo 里的文件名占固定 `StringSize` **字节**。
遇到多字节名字（上面那 8 条 SJIS 全角）就错位：每条多消耗 2 字节 → 后续条目起点漂移 →
最后几条抛 `The output char buffer is too small... (encoding codepage 65001)`（2026-10-20 实测并修复）。

正确写法（`scripts/Pac.fsx` 已改 ✅）：

```fsharp
let nameRaw = br.ReadBytes header.StringSize          // 按字节读，绝不 ReadChars
{ Filename = decodeName nameRaw                       // 严格 UTF-8 → 失败则 CP932
  FileNameBytes = nameRaw                             // ← 原始字节留着回写用
  ... }
```

（CP932 需要 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`，`Pac.fsx` 顶部已注册。）

修复后 2201 个 FPAC 全量 roundtrip 逐字节一致 ✅（含该 pac —— 因为回写用的是原字节）。

### 2.2 ⚠️ roundtrip 全绿**证明不了**写回逻辑正确

roundtrip 是「解包 → 重打包 → 比原字节」，只检验**自洽**。上面两个 bug 都属于
「解析时把值改了」：读进来就错了，写回去当然"一致"，2201 个 pac 全绿也照样崩。
⇒ 判断写回是否正确，必须对**内容变化的版本**做一次「与原版逐字节 / 逐字段 diff」：

```powershell
dotnet fsi scripts/pac-diff.fsx --backup 'option/options_screen.pac'   # 结构差异必须为 0
```

`pac-diff` 会比对名字/FileId/Length/FileNameHash/内容 SHA 并递归内层 pac；
但注意**它用的是同一个解析器**，解析器有 bug 时它会假阴性 —— 所以还留了一手：
`scratch/bytediff.py` 那种纯字节对比（不经过任何解析）才是最终裁判。

## 3. 重打包（✅ 已实现并全量验证，2026-10-18）

正确写法（`scripts/Pac.fsx` 已改 ✅）：

```fsharp
Filename = (br.ReadBytes header.StringSize |> Encoding.UTF8.GetString).TrimEnd('\000')
FourCC   = br.ReadBytes 4 |> Encoding.ASCII.GetString
```

修复后 2201 个 FPAC 全量 roundtrip 仍逐字节一致（含该含 U+FFFD 名的 pac —— 因为
`UTF8.GetBytes("\uFFFD")` 正是那 3 个原字节 `EF BF BD`，往返无损）。

## 3. 重打包（✅ 已实现并全量验证，2026-10-18）

工具：`scripts/pac-pack.fsx`（核心逻辑在 `Pac.fsx` 的 `PacLib.build`）。

```powershell
dotnet fsi scripts/pac-pack.fsx verify <orig.pac>            # 内存中 解包→重打包 逐字节对比
dotnet fsi scripts/pac-pack.fsx pack <orig.pac> <filesdir> -o <out.pac>
```

实测确认的重打包约定（2201 个 FPAC 全量 roundtrip 逐字节一致 ✅）：

- 数据区每条目起始偏移**对齐 16 字节**，不足补 `0x00`；TotalSize 也对齐 16（尾部补 0）。
- **条目顺序 == OffsetStart 顺序**（FileInfo 按数据偏移排序存放）。
- `FileNameHash` = `hashFileName(文件名.ToLowerInvariant())`（对 ASCII 名成立）；
  **仅 PacStyle 含 `Version2` 时写入该值**，非 Version2（如 `cmn\Static.pac`，Style=AutoLenFilename）
  该 4 字节字段为全 `0x00`。
  ⚠️ 但**重打包时不要重算，直接沿用原条目的 4 字节**（见 §1.4.1：名字被替换成 U+FFFD 的条目，
  其哈希对应替换前的原名，重算会让引擎查不到而闪退）。
- 头部 FourCC/DataStart/TotalSize/FileCount/PacStyle/StringSize 按公式重建：
  `DataStart = 0x20 + FileCount × (16 + StringSize)`。
- 非 FPAC 容器（如 `sound\**\bgm_ex5.pac` 是 **WBND** Wave Bank，与 pac 格式无关）不适用本工具。

未知项：PacStyle 的 `IdOnly/PathCut/HashSort/LongName` 语义（本游戏所有 FPAC 均为
`AutoLenFilename[,Version2]` 或仅 `AutoLenFilename`，未见其它组合）；FileNameHash 是否被
运行时校验（写 0 是否安全未实测——目前按原样重建不冒险）。
