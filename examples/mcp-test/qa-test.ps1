#Requires -Version 7.0

# LuaHelper MCP Server — Automated QA Smoke Test
#
# Exercises every API tool exposed by the luahelper MCP server against the
# sample project and reports pass/fail per section.
#
# Usage:
#   pwsh -NoLogo -NoProfile examples/mcp-test/qa-test.ps1
#
# The script starts the server from vscode-extension/, runs all checks,
# and prints a summary.  Exit code is 0 on full pass, 1 on any failure.
#
# After the run, restore the original luahelper.json if create_luahelper_json
# overwrote it (the script does NOT auto-restore):
#   git checkout -- examples/mcp-test/luahelper.json

param(
    [string]$ServerExe = "E:\Repository\luahelper-mcp\vscode-extension\LuaHelperMcpServer.exe",
    [string]$ProjectRoot = "E:\Repository\luahelper-mcp\examples\mcp-test"
)

if (-not (Test-Path $ServerExe)) {
    Write-Error "Server binary not found at $ServerExe`nBuild the project first."
    exit 1
}

$results = [System.Collections.Generic.List[hashtable]]::new()

function Record($Tool, $Args, [bool]$Success, $Output, $Section) {
    $results.Add(@{ Tool = $Tool; Args = $Args; Success = $Success; Output = $Output; Section = $Section })
    $icon = if ($Success) { "PASS" } else { "FAIL" }
    Write-Host "  [$icon] $Tool" -ForegroundColor $(if ($Success) { "Green" } else { "Red" })
    if (-not $Success) { Write-Host "    -> $Output" -ForegroundColor Red }
}

Write-Host "Starting MCP server from $ServerExe ..." -ForegroundColor Yellow
$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $ServerExe
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$proc = [System.Diagnostics.Process]::Start($psi)
$stdin = $proc.StandardInput; $stdout = $proc.StandardOutput

$nextId = 1
function SendAndReceive($method, $params, $timeoutMs = 30000) {
    $id = $nextId; $nextId++
    $req = @{ jsonrpc = "2.0"; id = $id; method = $method }
    if ($params -ne $null) { $req.params = $params }
    $stdin.WriteLine(($req | ConvertTo-Json -Depth 10 -Compress)); $stdin.Flush()
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if ($stdout.Peek() -ge 0) {
            $line = $stdout.ReadLine(); if ($line -eq $null) { break }
            try {
                $parsed = $line | ConvertFrom-Json
                if ($parsed.id -eq $id) {
                    if ($parsed.error) { return @{ Success = $false; Output = $parsed.error.message } }
                    if ($parsed.result) {
                        if ($parsed.result.content) {
                            return @{ Success = $true; Output = ($parsed.result.content | ForEach-Object { $_.text }) -join "`n" }
                        }
                        return @{ Success = $true; Output = $parsed.result }
                    }
                }
            } catch {}
        } else { Start-Sleep -Milliseconds 100 }
    }
    return @{ Success = $false; Output = "Timeout ($timeoutMs ms)" }
}

try {
    # ---- Initialize ----
    Write-Host "Initializing MCP session..." -ForegroundColor Yellow
    $r = SendAndReceive "initialize" @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "qa-test"; version = "1.0.0" } } 10000
    if (-not $r.Success) { throw "Init failed: $($r.Output)" }
    Write-Host "  OK: $($r.Output.serverInfo.name) v$($r.Output.serverInfo.version)" -ForegroundColor Green
    $stdin.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}'); $stdin.Flush()
    Start-Sleep -Milliseconds 500

    # ==== SECTION 1: Version and capability probes ====
    Write-Host "`n=== 1. Version and capability probes ===" -ForegroundColor Cyan
    $r = SendAndReceive "tools/call" @{ name = "get_server_version"; arguments = @{} } 10000; Record "get_server_version" "{}" $r.Success $r.Output "1"
    $r = SendAndReceive "tools/call" @{ name = "get_luahelper_version"; arguments = @{} } 10000; Record "get_luahelper_version" "{}" $r.Success $r.Output "1"
    $r = SendAndReceive "tools/call" @{ name = "get_supported_checks"; arguments = @{} } 10000; Record "get_supported_checks" "{}" $r.Success $r.Output "1"

    # ==== SECTION 2: Single-file diagnostics ====
    Write-Host "`n=== 2. Single-file diagnostics ===" -ForegroundColor Cyan
    $files = @("broken.lua","config.lua","main.lua","src/utils.lua","src/player.lua","modules/inventory.lua","clean.lua")
    foreach ($f in $files) {
        $path = "$ProjectRoot\$($f -replace '/','\')"
        $label = $f -replace '.*/', ''
        $timeout = if ($f -eq "clean.lua") { 25000 } else { 20000 }
        if ($f -eq "clean.lua") { Write-Host "  (clean.lua may take ~10s due to lualsp timeout)" -ForegroundColor Yellow }
        $r = SendAndReceive "tools/call" @{ name = "check_lua_file"; arguments = @{ filePath = $path } } $timeout
        Record "check_lua_file" $label $r.Success $r.Output "2"
    }

    # ==== SECTION 3: Project-wide diagnostics ====
    Write-Host "`n=== 3. Project-wide diagnostics ===" -ForegroundColor Cyan
    $r = SendAndReceive "tools/call" @{ name = "check_lua_project"; arguments = @{ projectPath = "$ProjectRoot" } } 120000
    Record "check_lua_project" "$ProjectRoot" $r.Success $r.Output "3"

    # ==== SECTION 4: Configuration API ====
    Write-Host "`n=== 4. Configuration API ===" -ForegroundColor Cyan
    $r = SendAndReceive "tools/call" @{ name = "get_luahelper_config"; arguments = @{ projectPath = "$ProjectRoot" } } 15000; Record "get_luahelper_config" "(initial)" $r.Success $r.Output "4"
    $r = SendAndReceive "tools/call" @{ name = "create_luahelper_json"; arguments = @{ projectPath = "$ProjectRoot" } } 15000; Record "create_luahelper_json" "$ProjectRoot" $r.Success $r.Output "4"
    $r = SendAndReceive "tools/call" @{ name = "get_luahelper_config"; arguments = @{ projectPath = "$ProjectRoot" } } 15000; Record "get_luahelper_config" "(after create)" $r.Success $r.Output "4"

    # ==== SECTION 5: Error handling ====
    Write-Host "`n=== 5. Error handling ===" -ForegroundColor Cyan
    $r = SendAndReceive "tools/call" @{ name = "check_lua_file"; arguments = @{ filePath = "$ProjectRoot\nonexistent\missing.lua" } } 15000; Record "check_lua_file (bad path)" "nonexistent" $r.Success $r.Output "5"
    $r = SendAndReceive "tools/call" @{ name = "check_lua_project"; arguments = @{ projectPath = "$ProjectRoot\nonexistent_dir" } } 15000; Record "check_lua_project (bad dir)" "nonexistent_dir" $r.Success $r.Output "5"
    $r = SendAndReceive "tools/call" @{ name = "get_luahelper_config"; arguments = @{ projectPath = "$ProjectRoot\nonexistent_dir" } } 15000; Record "get_luahelper_config (bad dir)" "nonexistent_dir" $r.Success $r.Output "5"

    # ==== SECTION 6: Resources and prompts ====
    Write-Host "`n=== 6. Resources and prompts ===" -ForegroundColor Cyan
    $r = SendAndReceive "resources/read" @{ uri = "luahelper://config" } 10000; Record "resources/read (config)" "luahelper://config" $r.Success $r.Output "6"
    $r = SendAndReceive "resources/read" @{ uri = "luahelper://diagnostics/$ProjectRoot\clean.lua" } 15000; Record "resources/read (diagnostics)" "clean.lua" $r.Success $r.Output "6"
    $r = SendAndReceive "prompts/list" @{} 10000; Record "prompts/list" "" $r.Success $r.Output "6"

    # ---- Summary ----
    Write-Host "`n========================" -ForegroundColor Cyan
    Write-Host "       S U M M A R Y" -ForegroundColor Cyan
    Write-Host "========================" -ForegroundColor Cyan
    $sections = @{}
    foreach ($r in $results) {
        $s = if ($r.Section) { $r.Section } else { "?" }
        if (-not $sections.ContainsKey($s)) { $sections[$s] = @{ Total = 0; Pass = 0; Fail = 0 } }
        $sections[$s].Total++; if ($r.Success) { $sections[$s].Pass++ } else { $sections[$s].Fail++ }
    }
    $totalPass = 0; $totalFail = 0
    foreach ($s in ($sections.Keys | Sort-Object)) {
        $info = $sections[$s]; $status = if ($info.Fail -eq 0) { "PASS" } else { "FAIL" }
        Write-Host "  $s : $status ($($info.Pass)/$($info.Total))" -ForegroundColor $(if ($info.Fail -eq 0) { "Green" } else { "Red" })
        $totalPass += $info.Pass; $totalFail += $info.Fail
    }
    $overall = if ($totalFail -eq 0) { "PASS" } else { "FAIL" }
    Write-Host "-----------------------" -ForegroundColor Cyan
    Write-Host "OVERALL: $overall ($totalPass/$($totalPass+$totalFail))" -ForegroundColor $(if ($totalFail -eq 0) { "Green" } else { "Red" })
    if ($totalFail -gt 0) { exit 1 }

} catch {
    Write-Host "FATAL: $_" -ForegroundColor Red
    exit 1
} finally {
    try { $stdin.WriteLine('{"jsonrpc":"2.0","id":999,"method":"shutdown"}'); $stdin.Flush(); Start-Sleep -Milliseconds 500 } catch {}
    try { $stdin.WriteLine('{"jsonrpc":"2.0","method":"exit"}'); $stdin.Flush(); Start-Sleep -Milliseconds 500 } catch {}
    if (-not $proc.HasExited) { $proc.Kill() }
    $stdin.Dispose(); $stdout.Dispose(); $proc.Dispose()
}
