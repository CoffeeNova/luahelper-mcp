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
.PARAMETER Version
    Version to stamp into the server assembly and the extension manifest
    (e.g. "0.1.1"). When omitted, the csproj <Version> and package.json
    version are used as-is.
.EXAMPLE
    .\.github\tools\build-vsix.ps1
.EXAMPLE
    .\.github\tools\build-vsix.ps1 -Version 0.1.1
#>

param(
    [switch]$SkipFetch,

    [string]$Version
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

$versionArgs = @()
if ($Version) {
    $versionArgs = @("-p:Version=$Version")
    Write-Host "Stamping version: $Version" -ForegroundColor Yellow
}

Write-Host "== Step 2/4: Publish .NET MCP server ==" -ForegroundColor Cyan
dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained `
    -p:PublishAot=true -p:InvariantGlobalization=true @versionArgs -o vscode-extension
if ($LASTEXITCODE -ne 0) {
    Write-Host "AOT publish failed (NativeAOT prerequisites not installed?)." -ForegroundColor Yellow
    Write-Host "Falling back to self-contained publish..." -ForegroundColor Yellow
    dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained `
        -p:PublishAot=false -p:InvariantGlobalization=true @versionArgs -o vscode-extension
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "== Step 3/4: Copy lualsp.exe bundle ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Path "vscode-extension/lualsp/win-x64" -Force | Out-Null
Copy-Item "lualsp/win-x64/lualsp.exe" "vscode-extension/lualsp/win-x64/lualsp.exe" -Force
Copy-Item "lualsp/version.json" "vscode-extension/lualsp/version.json" -Force

Write-Host "== Step 4/4: Install dependencies and package extension ==" -ForegroundColor Cyan
$pkgPath = "vscode-extension/package.json"
$pkgBackup = $null
if ($Version) {
    $pkgBackup = Get-Content -LiteralPath $pkgPath -Raw
    $pkg = $pkgBackup | ConvertFrom-Json
    $pkg.version = $Version
    $pkg | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $pkgPath
}
Push-Location vscode-extension
try {
    $npmCmd = Get-Command npm -ErrorAction SilentlyContinue
    if (-not $npmCmd) {
        throw "npm not found on PATH - required to build the extension."
    }
    & $npmCmd.Source ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npx --yes @vscode/vsce package --allow-missing-repository
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
    if ($pkgBackup) {
        [System.IO.File]::WriteAllText(
            (Join-Path $root $pkgPath),
            $pkgBackup,
            [System.Text.UTF8Encoding]::new($false)
        )
    }
}
exit $LASTEXITCODE