<#
  AuraCorePro — auracore:// URL scheme registration (HKCU, no admin).
  Writes per-user registry entries under HKCU\Software\Classes\auracore.

  Usage:
    .\install-url-scheme.ps1 [-BinaryPath <path>] [-WhatIf]
#>
param(
    [string]$BinaryPath = "$env:LOCALAPPDATA\Programs\AuraCorePro\AuraCore.exe",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$Scheme = "auracore"
$Root   = "HKCU:\Software\Classes\$Scheme"
$CmdKey = "$Root\shell\open\command"

if (-not (Test-Path $BinaryPath)) {
    Write-Warning "Binary not found at $BinaryPath."
    Write-Warning "Pass -BinaryPath '<actual-path>' if AuraCore is installed elsewhere."
    Write-Warning "Registration will still proceed, but links will fail until the binary is placed at the registered path."
}

Write-Host "Registering auracore:// -> $BinaryPath"

if ($WhatIf) {
    Write-Host "[WhatIf] Would write:"
    Write-Host "  $Root\(Default) = 'URL:AuraCore Protocol'"
    Write-Host "  $Root\URL Protocol = ''"
    Write-Host "  $CmdKey\(Default) = `"$BinaryPath`" `"%1`""
    exit 0
}

New-Item -Path $Root -Force | Out-Null
Set-ItemProperty -Path $Root -Name "(Default)" -Value "URL:AuraCore Protocol"
Set-ItemProperty -Path $Root -Name "URL Protocol" -Value ""

New-Item -Path $CmdKey -Force | Out-Null
Set-ItemProperty -Path $CmdKey -Name "(Default)" -Value "`"$BinaryPath`" `"%1`""

Write-Host "Registered successfully. Test with: Start-Process 'auracore://disk-health'"
