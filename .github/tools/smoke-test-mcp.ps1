#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke-test a published LuaHelper MCP server binary over stdio JSON-RPC.
.DESCRIPTION
    Spawns the server executable, performs a full MCP handshake
    (initialize -> notifications/initialized -> tools/list -> tools/call
    check_lua_file) and asserts the expected responses. Used by CI to verify
    the NativeAOT binary answers like a real MCP server.
.PARAMETER ServerPath
    Path to the published MCP server executable.
.PARAMETER ExpectedVersion
    When set, calls the get_server_version tool and asserts the server
    reports exactly this version (semver without a v prefix).
.EXAMPLE
    .\.github\tools\smoke-test-mcp.ps1 -ServerPath publish\win-x64\LuaHelperMcpServer.exe
.EXAMPLE
    .\.github\tools\smoke-test-mcp.ps1 -ServerPath publish\win-x64\LuaHelperMcpServer.exe -ExpectedVersion 0.1.0
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ServerPath,

    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ServerPath)) {
    throw "Server binary not found at $ServerPath"
}

$workDir = Join-Path $env:TEMP ("luahelper-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workDir | Out-Null
$luaFile = Join-Path $workDir "test.lua"
Set-Content -LiteralPath $luaFile -Value "---@type Frame`nlocal x = nil`n" -Encoding UTF8
$stderrFile = Join-Path $workDir "server-stderr.log"

try {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Resolve-Path -LiteralPath $ServerPath).Path
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stderrTask = $proc.StandardError.ReadToEndAsync()

    $stdout = $proc.StandardOutput
    $stdin = $proc.StandardInput

    function Send-Line {
        param([string]$Json)
        $stdin.WriteLine($Json)
        $stdin.Flush()
    }

    function Read-Response {
        param([string]$Label)
        $line = $stdout.ReadLine()
        if (-not $line) {
            throw "No $Label response received (server exited, code $($proc.ExitCode))."
        }
        return $line
    }

    Send-Line '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"luahelper-mcp-smoke","version":"1.0"}}}'
    $init = Read-Response "initialize"
    if ($init -notmatch '"result"') {
        throw "initialize failed: $init"
    }
    Write-Host "initialize: OK"

    Send-Line '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

    Send-Line '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    $tools = Read-Response "tools/list"
    if ($tools -notmatch '"check_lua_file"') {
        throw "tools/list did not expose check_lua_file: $tools"
    }
    Write-Host "tools/list: OK (check_lua_file present)"

    $escapedPath = $luaFile.Replace("\", "\\").Replace('"', '\"')
    Send-Line ('{{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{{"name":"check_lua_file","arguments":{{"filePath":"{0}"}}}}}}' -f $escapedPath)
    $call = Read-Response "tools/call"
    if ($call -match '"isError":\s*true') {
        throw "tools/call returned an error: $call"
    }
    if ($call -notmatch 'warning') {
        throw "tools/call did not return diagnostics: $call"
    }
    Write-Host "tools/call check_lua_file: OK"

    if ($ExpectedVersion) {
        Send-Line '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_server_version","arguments":{}}}'
        $ver = Read-Response "get_server_version"
        if ($ver -match '"isError":\s*true') {
            throw "get_server_version returned an error: $ver"
        }
        if ($ver -notmatch [regex]::Escape($ExpectedVersion)) {
            throw "get_server_version did not report version '$ExpectedVersion': $ver"
        }
        Write-Host "tools/call get_server_version: OK ($ExpectedVersion)"
    }

    try { $proc.Kill($true) } catch { $proc.Kill() }
    $proc.WaitForExit(10000) | Out-Null
    Write-Host ""
    Write-Host "MCP handshake smoke test PASSED." -ForegroundColor Green
    exit 0
}
finally {
    if ($proc -and -not $proc.HasExited) {
        try { $proc.Kill($true) } catch { $proc.Kill() }
    }
    if ($stderrTask -and -not $stderrTask.IsCompleted) {
        try { $stderrTask.Wait(2000) | Out-Null } catch { }
    }
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
}