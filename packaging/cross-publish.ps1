# =============================================================================
# AuraCore Pro — Cross-Platform Publisher (runs on Windows)
# Publishes self-contained binaries for Windows, Linux, macOS
# Usage: .\cross-publish.ps1 [-Version "1.8.0"] [-Platforms "all"]
# =============================================================================
param(
    [string]$Version = "1.8.0",
    [string]$Platforms = "all"  # all, win, linux, macos
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$Project = "$RepoRoot\src\UI\AuraCore.UI.Avalonia\AuraCore.UI.Avalonia.csproj"
$OutDir = "$ScriptDir\dist"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  AuraCore Pro Cross-Platform Publisher v$Version" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Clean
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$targets = @()
if ($Platforms -eq "all" -or $Platforms -eq "win") {
    $targets += @{ RID = "win-x64"; Name = "Windows x64"; Ext = ".exe" }
}
if ($Platforms -eq "all" -or $Platforms -eq "linux") {
    $targets += @{ RID = "linux-x64"; Name = "Linux x64"; Ext = "" }
}
if ($Platforms -eq "all" -or $Platforms -eq "macos") {
    $targets += @{ RID = "osx-x64"; Name = "macOS Intel"; Ext = "" }
    $targets += @{ RID = "osx-arm64"; Name = "macOS ARM64"; Ext = "" }
}

$step = 1
$total = $targets.Count

foreach ($target in $targets) {
    $rid = $target.RID
    $name = $target.Name
    $publishDir = "$OutDir\publish-$rid"
    
    Write-Host "[$step/$total] Publishing for $name ($rid)..." -ForegroundColor Yellow
    
    dotnet publish $Project `
        --nologo `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -o $publishDir
    
    $fileCount = (Get-ChildItem $publishDir -Recurse -File).Count
    Write-Host "    Published $fileCount files" -ForegroundColor Green
    
    # Create zip archive
    $zipName = "AuraCorePro-$Version-$rid.zip"
    $zipPath = "$OutDir\$zipName"
    
    Write-Host "    Creating $zipName..."
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
    $sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "    Created: $zipName ($sizeMB MB)" -ForegroundColor Green
    
    # Cleanup publish dir to save space
    Remove-Item $publishDir -Recurse -Force
    
    $step++
}

# Summary
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Cross-Platform Build Complete!" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Output directory: $OutDir" -ForegroundColor White
Write-Host ""

Get-ChildItem "$OutDir\*.zip" | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name) ($sizeMB MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "  Distribution:" -ForegroundColor Yellow
Write-Host "    Windows: Extract zip, run AuraCore.Pro.exe"
Write-Host "    Linux:   Extract zip, chmod +x AuraCore.Pro, ./AuraCore.Pro"
Write-Host "             OR use build-linux.sh on Linux for .deb + AppImage"
Write-Host "    macOS:   Extract zip, use build-macos.sh on macOS for .app bundle"
Write-Host "             OR right-click Open to bypass Gatekeeper"
Write-Host ""
