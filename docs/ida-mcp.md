# ida-pro-mcp 与 reverse-engineer-anything 技能使用经验

> 2026-10-18 验证通过 ✅。本文记录环境配置、已知坑、调用方式与 Getsuei.exe 分析基线。

## 组件关系

```
reverse-engineer-anything（Reasonix 技能）
  └─ 依赖 ida-pro-mcp：通过 MCP 协议驱动 IDA 分析二进制
       └─ idalib-mcp（headless IDALIB 模式，无需 GUI）
            └─ IDA Professional 9.2（C:\Program Files\IDA Professional 9.2）
```

- `ida_pro_mcp 2.0.0`：装在**用户 site-packages**（`%APPDATA%\Python\Python313\site-packages`），
  入口 `idalib-mcp.exe` 在 `%APPDATA%\Python\Python313\Scripts`。
- 本机 IDA Professional 9.2 已安装，IDALIB 与其配合工作。

## 启动方式

```powershell
cmd /c scripts\ida-mcp-run.cmd     # 后台运行
```

- 监听 `127.0.0.1:8745`，端点：`/mcp`（Streamable HTTP）、`/sse`（SSE）。
- 启动成功标志：`INFO:zeromcp.mcp:[MCP] Server started`。

## 已知坑 ⚠️→✅

### Reasonix 便携宿主重定向 APPDATA（最常见）

宿主启动脚本 `Reasonix-Portable.cmd` 会重定向：

```
APPDATA     → D:\@Resources\Reasonix-windows-amd64\AppData\Roaming
LOCALAPPDATA / TEMP / TMP → 同目录下
REASONIX_HOME → D:\@Resources\Reasonix-windows-amd64\data
```

Python 的 user site 路径由**运行时** `APPDATA` 推导，被重定向后
`import ida_pro_mcp` 报 `ModuleNotFoundError` —— 包其实装好了，只是找不到。

**修复**（已在 `scripts/ida-mcp-run.cmd` 内置）：

```bat
if defined REASONIX_HOME set "APPDATA=%USERPROFILE%\AppData\Roaming"
```

手动 pwsh 调用任何依赖用户级 Python 包的工具前，同样先还原 `$env:APPDATA`。

### idb 默认写在输入文件旁（违反游戏目录只读）

`idb_open` 传入 `.exe` 时，`.i64` 自动生成在**同目录**。游戏目录必须保持只读，
分析完成后应把 `.i64` 移到工作区。

**复用已验证 ✅**：`idb_open` 直接传 `.i64` 路径即可（6.7s 打开，分析缓存完整），
无需重新分析 exe。当前基线库：`work/ida/Getsuei.exe.i64`。

### Streamable HTTP 调用细节（已验证）

- 必须先 `initialize`，从响应头 `Mcp-Session-Id` 取会话 id，后续请求带上。
- 之后发 `notifications/initialized`（notification 无 id）。
- 响应可能是 `application/json` 也可能是 SSE（`data: ` 行），都要能解析。
- 业务调用统一走 `tools/call`，参数 `{name, arguments}`。
- **参数名以 `tools/list` 的 inputSchema 为准**，不要猜：例如反编译用 `addr` 不是 `address`。

## 常用工具（共 66 个，挑常用）

| 工具 | 用途 |
|---|---|
| `idb_open` / `idb_close` / `idb_list` | 打开/关闭/列出分析会话（database 参数传 session id） |
| `survey_binary` | 二进制总览（md5、段、函数/字符串统计） |
| `server_health` | 会话健康状态 |
| `decompile` | Hex-Rays 反编译（参数 `addr`） |
| `disasm` / `func_profile` / `basic_blocks` | 反汇编与函数结构 |
| `find_bytes` / `find_regex` / `search_text` / `get_string` | 字节/正则/文本/字符串搜索 |
| `xrefs_to` / `callees` / `callgraph` | 交叉引用与调用图 |
| `rename` / `set_comments` / `set_type` | 打标记（不改二进制） |
| `list_funcs` / `func_query` / `lookup_funcs` | 函数检索 |

## Getsuei.exe 分析基线（已验证 ✅）

- 32 位 PE，imagebase `0x400000`，image_size `0x15dc000`
- MD5 `20dc6ac1d07657cc52decae9ac281dd5`，SHA256 `71768b353bf56581512812e811aeadad3e8fbfe42db57a654d548260cdfa6b85`
- 8430 函数（已命名仅 44，库函数 1430）、51417 条字符串、4 段
- 段：`.text` 0x401000-0x5d6000 (rx)、`.idata`、`.rdata`、`.data` (rw)
- 入口 `start` @ 0x5aacc6；首次自动分析约 23s，Hex-Rays 可用
- 基线库：`work/ida/Getsuei.exe.i64`（直接传入即可复用）

## 标准工作流

1. `cmd /c scripts\ida-mcp-run.cmd`（后台）→ 等 `Server started`
2. MCP `initialize` → `notifications/initialized`
3. `tools/call idb_open` 传 `work/ida/Getsuei.exe.i64`
4. 用各工具分析；所有 `patch` 类工具**禁止**用于游戏原始文件
5. `idb_close` 关闭会话，再停服务

## 操作纪律

- **安装任何软件/包前必须先向用户确认**（用户明确要求）。
- 临时客户端脚本放 TEMP，用完即删，不进仓库。
- 分析游戏文件产生的中间产物（.i64、缓存）一律落 `work/ida/`。

## 2026-10-18 增补（字库/文本渲染逆向 ✅）

- 极简 Python 客户端（initialize → 会话 id 缓存文件 → tools/call）放
  `%TEMP%\ida_client.py`，用法 `python ida_client.py <tool> '<json>'`；
  **每个 tools/call 都必须带 `database=<session_id>`**（idb_open 返回的 session_id，
  不是 HTTP 会话 id），参数名以 tools/list 的 inputSchema 为准
  （本次实测：`idb_open.input_path`、`search_text.pattern`、`xrefs_to.addrs`、
  `decompile.addr`、`get_bytes.regions`、`disasm.max_instructions`）。
- 字体/文本渲染逆向结论（查找逻辑、三套字库系统、关键函数地址）已落
  `docs/formats/abc-hip-font.md` §5，此处不再重复。
- 虚拟调用遍历时 `xrefs_to` 只能看到 vtable 数据引用，找不到真实调用者；
  从「资源加载函数」往消费方向追比追虚函数调用更快。
