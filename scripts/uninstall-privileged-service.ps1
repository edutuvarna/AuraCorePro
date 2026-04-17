#Requires -RunAsAdministrator
$ServiceName = "AuraCorePrivHelper"
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service not installed."
    exit 0
}
if ($existing.Status -eq "Running") { Stop-Service -Name $ServiceName -Force }
sc.exe delete $ServiceName | Out-Null
Write-Host "Uninstalled."
