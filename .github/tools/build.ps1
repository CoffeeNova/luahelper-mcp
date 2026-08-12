#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build all projects in the LuaHelper MCP Server solution.
.DESCRIPTION
    Runs dotnet build for the entire solution. Supports Release/Debug configuration.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.EXAMPLE
    .\.github\tools\build.ps1
    .\.github\tools\build.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
Set-Location $root

Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
dotnet build -c $Configuration

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded." -ForegroundColor Green
}
else {
    Write-Host "Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}
