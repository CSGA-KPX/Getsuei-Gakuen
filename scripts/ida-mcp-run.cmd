@echo off
rem Reasonix 便携宿主会重定向 APPDATA，导致 Python 找不到 user site-packages（ida_pro_mcp）。
rem 检测到宿主环境（REASONIX_HOME）时还原 APPDATA；普通终端中该条件不生效。
if defined REASONIX_HOME set "APPDATA=%USERPROFILE%\AppData\Roaming"
uv run idalib-mcp --host 127.0.0.1 --port 8745
