#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish the LuaHelper MCP Server for distribution.
.DESCRIPTION
    Builds a self-contained AOT publish of the MCP server and copies lualsp.exe.
    Output goes to publish/ directory.
.PARAMETER Runtime
    Target runtime: win-x64 (default), linux-x64, osx-x64.
.PARAMETER Configuration
    Build configuration: Release (default) or Debug.
.EXAMPLE
    .\.github\tools\deploy.ps1
    .\.github\tools\deploy.ps1 -Runtime linux-x64
#>

param(
    [ValidateSet("win-x64", "linux-x64", "osx-x64")]
    [string]$Runtime = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$publishDir = "publish/$Runtime"
$lualspDir = "lualsp/$Runtime"

Write-Host "Publishing MCP server ($Configuration, $Runtime)..." -ForegroundColor Cyan

# Publish .NET server as self-contained AOT binary
dotnet publish src/LuaHelperMcpServer -c $Configuration -r $Runtime --self-contained `
    -p:PublishAot=true -p:InvariantGlobalization=true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Copy lualsp.exe if available
if (Test-Path $lualspDir) {
    Write-Host "Copying lualsp binaries from $lualspDir..." -ForegroundColor Cyan
    Copy-Item "$lualspDir/*" "$publishDir/lualsp/" -Recurse -Force
}
else {
    Write-Host "Warning: lualsp binaries not found at $lualspDir. Skipping." -ForegroundColor Yellow
}

Write-Host "Deploy complete. Output in: $publishDir" -ForegroundColor Green
