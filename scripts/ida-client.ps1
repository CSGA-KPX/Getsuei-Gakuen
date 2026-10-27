# ida-client.ps1 — ida-pro-mcp 极简客户端（Streamable HTTP）
#
# 用法：
#   pwsh -File scripts\ida-client.ps1 -List                  # 列出全部工具
#   pwsh -File scripts\ida-client.ps1 -Tool idb_open  -Json '{"input_path":"Z:\\...\\Getsuei.exe.i64"}'
#   pwsh -File scripts\ida-client.ps1 -Tool decompile -Json '{"addr":"0x510050","database":"<idb_session>"}'
#
# 约定：
#   - 服务需先启动：cmd /c scripts\ida-mcp-run.cmd（后台，等 ~12s）
#   - HTTP 会话 id 缓存在 %TEMP%\gakuen_ida_http_session.txt，失效会自动重建
#   - 业务调用的 database 参数是 **idb_open 返回的 session_id**（如 b88bf701），不是 HTTP 会话 id
param(
    [string]$Tool,
    [string]$Json = '{}',
    [switch]$List,
    [switch]$Reset
)
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:8745/mcp'
$headers = @{ Accept = 'application/json, text/event-stream' }
$sessionFile = Join-Path $env:TEMP 'gakuen_ida_http_session.txt'

function ParseResp($resp) {
    $ct = [string]$resp.Headers['Content-Type']
    $body = $resp.Content
    if ($ct -match 'event-stream') {
        $lines = $body -split "`n" | Where-Object { $_ -match '^data: ' }
        $body = ($lines | ForEach-Object { $_ -replace '^data: ', '' }) -join "`n"
    }
    return $body
}

function New-Session {
    $init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"gakuen-ida-client","version":"1"}}}'
    $r = Invoke-WebRequest -Uri $base -Method Post -Body $init -ContentType 'application/json' -Headers $headers -TimeoutSec 60
    $sid = $r.Headers['Mcp-Session-Id']
    if ($sid -is [array]) { $sid = $sid[0] }
    Set-Content -Path $sessionFile -Value $sid -NoNewline
    $h2 = $headers.Clone(); $h2['Mcp-Session-Id'] = $sid
    Invoke-WebRequest -Uri $base -Method Post -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' `
        -ContentType 'application/json' -Headers $h2 -TimeoutSec 60 | Out-Null
    Write-Host "[new http session] $sid" -ForegroundColor DarkGray
    return $sid
}

if ($Reset -and (Test-Path $sessionFile)) { Remove-Item $sessionFile -Force }
$sid = if (Test-Path $sessionFile) { (Get-Content $sessionFile -Raw).Trim() } else { $null }
if (-not $sid) { $sid = New-Session }

$payload =
    if ($List) {
        '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    } else {
        if (-not $Tool) { throw "必须提供 -Tool 或 -List" }
        '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"' + $Tool + '","arguments":' + $Json + '}}'
    }

$h = $headers.Clone(); $h['Mcp-Session-Id'] = $sid
try {
    $r2 = Invoke-WebRequest -Uri $base -Method Post -Body $payload -ContentType 'application/json' -Headers $h -TimeoutSec 300
} catch {
    # 会话过期（服务重启）→ 重建一次
    Write-Host "[session expired, reconnecting]" -ForegroundColor DarkYellow
    $sid = New-Session
    $h['Mcp-Session-Id'] = $sid
    $r2 = Invoke-WebRequest -Uri $base -Method Post -Body $payload -ContentType 'application/json' -Headers $h -TimeoutSec 300
}
ParseResp $r2
