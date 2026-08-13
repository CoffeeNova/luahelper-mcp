#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build the LuaHelper MCP Server VS Code extension (.vsix).
.DESCRIPTION
    Ensures the lualsp bundle is up to date, publishes the .NET MCP server
    (NativeAOT when the platform linker is available, self-contained otherwise),
    copies lualsp.exe into the extension, and packages the .vsix with vsce.
.PARAMETER SkipFetch
    Skip the lualsp provisioning step (use an existing bundle).
.EXAMPLE
    .\.github\tools\build-vsix.ps1
#>

param(
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "== Step 1/4: Ensure lualsp.exe bundle ==" -ForegroundColor Cyan
if ($SkipFetch) {
    if (-not (Test-Path "lualsp/win-x64/lualsp.exe")) {
        Write-Host "Bundle missing at lualsp/win-x64/lualsp.exe and -SkipFetch was set." -ForegroundColor Red
        exit 1
    }
}
else {
    & .\.github\tools\fetch-lualsp.ps1 -Rid win-x64 -Update
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "== Step 2/4: Publish .NET MCP server ==" -ForegroundColor Cyan
dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained `
    -p:PublishAot=true -p:InvariantGlobalization=true -o vscode-extension
if ($LASTEXITCODE -ne 0) {
    Write-Host "AOT publish failed (NativeAOT prerequisites not installed?)." -ForegroundColor Yellow
    Write-Host "Falling back to self-contained publish..." -ForegroundColor Yellow
    dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained `
        -p:InvariantGlobalization=true -o vscode-extension
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "== Step 3/4: Copy lualsp.exe bundle ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Path "vscode-extension/lualsp/win-x64" -Force | Out-Null
Copy-Item "lualsp/win-x64/lualsp.exe" "vscode-extension/lualsp/win-x64/lualsp.exe" -Force
Copy-Item "lualsp/version.json" "vscode-extension/lualsp/version.json" -Force

Write-Host "== Step 4/4: Package VS Code extension ==" -ForegroundColor Cyan
Push-Location vscode-extension
try {
    npx --yes @vscode/vsce package --allow-missing-repository
}
finally {
    Pop-Location
}
exit $LASTEXITCODE