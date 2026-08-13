#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Detect, download, update, and bundle lualsp.exe for the LuaHelper MCP server.
.DESCRIPTION
    Guarantees a lualsp/{rid}/lualsp.exe bundle exists on this machine.

    Detects every installed LuaHelper extension (VS Code, VS Code Insiders,
    Cursor, and any other yinfei.luahelper-* folder under the user profile),
    compares against the local bundle, downloads from the Marketplace when
    nothing is found, offers an update when a newer version is detected, and
    forms lualsp/{rid}/lualsp.exe + lualsp/version.json.

    The script is idempotent: when the bundle is already up to date it prints
    status and exits 0 without making changes.
.PARAMETER Rid
    Platform folder name inside the bundle (win-x64, linux-x64, osx-x64).
.PARAMETER OutputDir
    Where the bundle is formed. Defaults to lualsp/ at the repo root.
.PARAMETER Update
    Update to the latest found version without prompting (CI).
.PARAMETER Force
    Re-download/re-copy even if the bundle is up to date.
.PARAMETER SkipDownload
    Detect + compare only - no download, no writes (status check).
.EXAMPLE
    .\.github\tools\fetch-lualsp.ps1
    .\.github\tools\fetch-lualsp.ps1 -Update
    .\.github\tools\fetch-lualsp.ps1 -SkipDownload
#>

[CmdletBinding()]
param(
    [string]$Rid = "win-x64",
    [string]$OutputDir = "",
    [switch]$Update,
    [switch]$Force,
    [switch]$SkipDownload
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "lualsp"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

$exeName = if ($Rid -like "win*") { "lualsp.exe" } else { "lualsp" }
$bundleDir = Join-Path $OutputDir $Rid
$bundleExe = Join-Path $bundleDir $exeName
$manifestPath = Join-Path $OutputDir "version.json"

$marketplaceUrl = "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/yinfei/vsextensions/luahelper/latest/vspackage"

function ConvertTo-LuaHelperVersion {
    param([string]$Text)
    if (-not $Text) { return $null }
    $v = $null
    if ([version]::TryParse($Text, [ref]$v)) { return $v }
    $clean = $Text -replace '[^0-9.]', ''
    if ($clean -and [version]::TryParse($clean, [ref]$v)) { return $v }
    return $null
}

function Compare-LuaHelperVersions {
    param([string]$Left, [string]$Right)
    # Returns $true when Left is newer than Right.
    $lv = ConvertTo-LuaHelperVersion $Left
    $rv = ConvertTo-LuaHelperVersion $Right
    if ($null -ne $lv -and $null -ne $rv) { return $lv -gt $rv }
    if ($null -eq $lv -and $null -eq $rv) { return [string]::CompareOrdinal($Left, $Right) -gt 0 }
    # Unparseable version sorts as older.
    return $null -ne $lv
}

function Get-LuaHelperExtensionInfo {
    param([string]$Folder)
    $version = $null
    $pkgJson = Join-Path $Folder "package.json"
    if (Test-Path -LiteralPath $pkgJson) {
        try {
            $pkg = Get-Content -LiteralPath $pkgJson -Raw | ConvertFrom-Json
            $version = $pkg.version
        }
        catch {
            $version = $null
        }
    }
    if (-not $version) {
        $leaf = Split-Path $Folder -Leaf
        $dash = $leaf.LastIndexOf("-")
        if ($dash -ge 0) { $version = $leaf.Substring($dash + 1) }
    }
    $binary = Get-ChildItem -LiteralPath $Folder -Recurse -Filter $exeName -ErrorAction SilentlyContinue |
        Select-Object -First 1
    return [pscustomobject]@{
        Path      = $Folder
        Version   = $version
        Binary    = if ($binary) { $binary.FullName } else { $null }
        HasBinary = ($null -ne $binary)
    }
}

function Find-DetectedExtensions {
    $found = [System.Collections.Generic.List[object]]::new()
    $roots = @(
        (Join-Path $env:USERPROFILE ".vscode\extensions"),
        (Join-Path $env:USERPROFILE ".vscode-insiders\extensions"),
        (Join-Path $env:USERPROFILE ".cursor\extensions")
    )
    foreach ($root in $roots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Directory -Filter "yinfei.luahelper-*" -ErrorAction SilentlyContinue |
                ForEach-Object { $found.Add($_.FullName) }
        }
    }
    # Any other yinfei.luahelper-* folder under the user profile
    Get-ChildItem -LiteralPath $env:USERPROFILE -Directory -Filter "yinfei.luahelper-*" `
        -Recurse -Depth 6 -ErrorAction SilentlyContinue |
        ForEach-Object {
            if (-not $found.Contains($_.FullName)) { $found.Add($_.FullName) }
        }
    $found | ForEach-Object { Get-LuaHelperExtensionInfo $_ }
}

function Copy-FromVsix {
    param(
        [string]$VsixPath,
        [string]$ExtractDir
    )
    # Windows PowerShell 5.1's Expand-Archive only accepts .zip files.
    $zipPath = $VsixPath + ".zip"
    Copy-Item -LiteralPath $VsixPath -Destination $zipPath -Force
    Expand-Archive -LiteralPath $zipPath -DestinationPath $ExtractDir -Force
    $pkgJson = Join-Path $ExtractDir "extension\package.json"
    $version = $null
    if (Test-Path -LiteralPath $pkgJson) {
        try {
            $pkg = Get-Content -LiteralPath $pkgJson -Raw | ConvertFrom-Json
            $version = $pkg.version
        }
        catch { $version = $null }
    }
    $binary = Get-ChildItem -LiteralPath (Join-Path $ExtractDir "extension") -Recurse -Filter $exeName `
        -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $binary) {
        throw "lualsp binary not found in the downloaded VSIX."
    }
    return [pscustomobject]@{ Version = $version; Binary = $binary.FullName }
}

function Install-FromMarketplace {
    param([string]$WorkDir)
    $vsixPath = Join-Path $WorkDir "luahelper.vsix"
    $attempt = 0
    while ($true) {
        $attempt++
        Write-Host "Downloading lualsp from the Marketplace VSIX (attempt $attempt)..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $marketplaceUrl -OutFile $vsixPath -UseBasicParsing `
            -UserAgent "luahelper-mcp-fetch" -TimeoutSec 300
        $ok = $false
        if (Test-Path -LiteralPath $vsixPath) {
            $header = [System.IO.File]::ReadAllBytes($vsixPath)[0..1]
            $ok = ($header[0] -eq 0x50 -and $header[1] -eq 0x4B)
        }
        if ($ok) { break }
        if ($attempt -ge 3) {
            throw "Marketplace download failed: the response is not a valid VSIX archive."
        }
        Write-Host "Downloaded file is not a valid VSIX archive; retrying..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
    return Copy-FromVsix -VsixPath $vsixPath -ExtractDir (Join-Path $WorkDir "vsix")
}

function Install-ViaCodeCli {
    param([string]$WorkDir)
    $codeCmd = Get-Command code -ErrorAction SilentlyContinue
    if (-not $codeCmd) { throw "VS Code CLI (code) not found on PATH." }
    $extDir = Join-Path $WorkDir "extensions"
    New-Item -ItemType Directory -Path $extDir -Force | Out-Null
    Write-Host "Installing yinfei.luahelper via code CLI..." -ForegroundColor Cyan
    & $codeCmd.Source --install-extension yinfei.luahelper --extensions-dir $extDir --force | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "code --install-extension failed (exit $LASTEXITCODE)." }
    $folder = Get-ChildItem -LiteralPath $extDir -Directory -Filter "yinfei.luahelper-*" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $folder) { throw "Extension installed but no yinfei.luahelper-* folder found." }
    $info = Get-LuaHelperExtensionInfo $folder.FullName
    if (-not $info.HasBinary) { throw "Installed extension contains no lualsp binary." }
    return [pscustomobject]@{ Version = $info.Version; Binary = $info.Binary }
}

function Write-Manifest {
    param(
        [string]$Version,
        [string]$Source,
        [string]$Sha256
    )
    $manifest = [ordered]@{
        version   = $Version
        rid       = $Rid
        source    = $Source
        sha256    = $Sha256
        fetchedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

# --- 1. Detect installed versions ---
$detected = @(Find-DetectedExtensions)
$sorted = @($detected | Sort-Object @{ Expression = { ConvertTo-LuaHelperVersion $_.Version }; Descending = $true })
$latest = $sorted | Where-Object { $_.HasBinary } | Select-Object -First 1
if (-not $latest) { $latest = $sorted | Select-Object -First 1 }

# --- 2. Check the local bundle ---
$bundleVersion = $null
$bundleSha = $null
if (Test-Path -LiteralPath $manifestPath) {
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $bundleVersion = $manifest.version
        $bundleSha = $manifest.sha256
    }
    catch {
        Write-Host "Warning: could not parse $manifestPath - treating bundle as absent." -ForegroundColor Yellow
        $bundleVersion = $null
    }
}
$bundleExists = Test-Path -LiteralPath $bundleExe

# --- 3. Report detected versions ---
Write-Host ""
Write-Host "== Detected LuaHelper extensions on this machine ==" -ForegroundColor Cyan
if ($detected.Count -eq 0) {
    Write-Host "  (none found)"
}
else {
    foreach ($e in $sorted) {
        $binary = if ($e.HasBinary) { "binary: yes" } else { "binary: NO" }
        $version = if ($e.Version) { $e.Version } else { "(unknown)" }
        Write-Host "  $version  $binary  $($e.Path)"
    }
}
Write-Host "== Local bundle ==" -ForegroundColor Cyan
if ($bundleExists) {
    Write-Host "  $bundleExe (version: $bundleVersion)"
}
else {
    Write-Host "  no bundle at $bundleExe"
}

# --- 4. Decide what to do ---
$action = "none"
if ($Force) {
    $action = "reinstall"
}
elseif (-not $bundleExists) {
    $action = "install"
}
elseif ($latest -and $latest.Version -and (Compare-LuaHelperVersions $latest.Version $bundleVersion)) {
    $action = "update"
}

if ($action -eq "update" -and -not $Update) {
    try {
        $answer = Read-Host "Update lualsp $bundleVersion -> $($latest.Version)? [Y/n]"
        if ($answer -match "^n") { $action = "none" }
    }
    catch {
        Write-Host "Non-interactive shell detected; skipping update. Use -Update to update without prompting." -ForegroundColor Yellow
        $action = "none"
    }
}

if ($SkipDownload) {
    Write-Host ""
    Write-Host "Status check (-SkipDownload): no changes made." -ForegroundColor Yellow
    switch ($action) {
        "none"      { Write-Host "  Bundle is up to date (version $bundleVersion)." -ForegroundColor Green }
        "install"   { Write-Host "  Bundle missing - a provisioning run would install lualsp." -ForegroundColor Yellow }
        "update"    { Write-Host "  Update available: $bundleVersion -> $($latest.Version)." -ForegroundColor Yellow }
        "reinstall" { Write-Host "  -Force requested - a provisioning run would re-copy lualsp." -ForegroundColor Yellow }
    }
    exit 0
}

if ($action -eq "none") {
    Write-Host ""
    Write-Host "Bundle is up to date (version $bundleVersion). Nothing to do." -ForegroundColor Green
    exit 0
}

# --- 5. Obtain the lualsp binary ---
$workDir = Join-Path $env:TEMP "luahelper-fetch-$Rid"
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$sourceDesc = ""
$chosenVersion = $null
$binaryPath = $null

try {
    if ($latest -and $latest.HasBinary) {
        $binaryPath = $latest.Binary
        $chosenVersion = $latest.Version
        $sourceDesc = "vscode-extension:$($latest.Path)"
        Write-Host "Using lualsp from detected extension $($latest.Path)" -ForegroundColor Cyan
    }
    else {
        try {
            $dl = Install-FromMarketplace $workDir
            $binaryPath = $dl.Binary
            $chosenVersion = $dl.Version
            $sourceDesc = "vscode-marketplace"
        }
        catch {
            Write-Host "Marketplace download failed: $($_.Exception.Message)" -ForegroundColor Yellow
            try {
                $cli = Install-ViaCodeCli $workDir
                $binaryPath = $cli.Binary
                $chosenVersion = $cli.Version
                $sourceDesc = "code-cli"
            }
            catch {
                Write-Host "code CLI fallback failed: $($_.Exception.Message)" -ForegroundColor Yellow
                if ($detected.Count -gt 0) {
                    Write-Host "A LuaHelper extension was detected but contains no lualsp binary." -ForegroundColor Yellow
                    Write-Host "Reinstall the LuaHelper extension manually, then re-run this script." -ForegroundColor Yellow
                }
                throw "Could not obtain lualsp. Install the LuaHelper VS Code extension manually (yinfei.luahelper) or download it from the Marketplace, then re-run this script."
            }
        }
    }

    # --- 6. Form the bundle ---
    New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null
    Copy-Item -LiteralPath $binaryPath -Destination $bundleExe -Force
    $sha = (Get-FileHash -LiteralPath $bundleExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $chosenVersion) { $chosenVersion = $bundleVersion }
    Write-Manifest -Version $chosenVersion -Source $sourceDesc -Sha256 $sha

    Write-Host ""
    Write-Host "== Bundle formed ==" -ForegroundColor Green
    Write-Host "  binary:  $bundleExe"
    Write-Host "  version: $chosenVersion"
    Write-Host "  source:  $sourceDesc"
    Write-Host "  sha256:  $sha"
    Write-Host "  manifest: $manifestPath"
}
finally {
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
}

exit 0