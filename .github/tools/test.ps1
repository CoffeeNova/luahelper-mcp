#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run tests for the LuaHelper MCP Server.
.DESCRIPTION
    Runs unit tests (fast) or all tests (unit + integration).
.PARAMETER IncludeIntegration
    If set, also runs integration tests (requires lualsp.exe).
.PARAMETER Filter
    Optional test filter (e.g., "FullyQualifiedName~LspClient").
.EXAMPLE
    .\.github\tools\test.ps1
    .\.github\tools\test.ps1 -IncludeIntegration
    .\.github\tools\test.ps1 -Filter "FullyQualifiedName~LspMessageReaderTests"
#>

param(
    [switch]$IncludeIntegration,
    [string]$Filter = ""
)

$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
Set-Location $root

# Unit tests
Write-Host "Running unit tests..." -ForegroundColor Cyan
if ($Filter) {
    dotnet test src/LuaHelperMcpServer.Tests.Unit --filter $Filter
}
else {
    dotnet test src/LuaHelperMcpServer.Tests.Unit
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Unit tests failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Unit tests passed." -ForegroundColor Green

# Integration tests (optional)
if ($IncludeIntegration) {
    Write-Host "`nRunning integration tests..." -ForegroundColor Cyan
    if ($Filter) {
        dotnet test src/LuaHelperMcpServer.Tests.Integration --filter $Filter
    }
    else {
        dotnet test src/LuaHelperMcpServer.Tests.Integration
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Integration tests failed." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Integration tests passed." -ForegroundColor Green
}

Write-Host "`nAll tests passed." -ForegroundColor Green
