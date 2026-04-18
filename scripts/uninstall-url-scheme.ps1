<#
  AuraCorePro — remove auracore:// URL scheme (HKCU).
#>
$ErrorActionPreference = "Stop"
$Root = "HKCU:\Software\Classes\auracore"

if (Test-Path $Root) {
    Remove-Item $Root -Recurse -Force
    Write-Host "auracore:// scheme unregistered."
} else {
    Write-Host "auracore:// not registered; nothing to do."
}
