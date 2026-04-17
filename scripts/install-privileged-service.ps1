#Requires -RunAsAdministrator
<#
  AuraCorePro Privileged Helper — installer
  Usage: .\install-privileged-service.ps1 [-BinaryPath <path>] [-WhatIf]
#>
param(
    [string]$BinaryPath = "$env:ProgramFiles\AuraCorePro\PrivHelper\AuraCore.PrivilegedService.exe",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$ServiceName  = "AuraCorePrivHelper"
$DisplayName  = "AuraCore Privileged Helper"
$Description  = "Executes whitelisted privileged commands for AuraCore Pro."

Write-Host "AuraCorePro Privileged Helper installer"
Write-Host "Binary: $BinaryPath"
Write-Host "Service: $ServiceName"
Write-Host ""

if (-not (Test-Path $BinaryPath)) {
    Write-Error "Binary not found at $BinaryPath. Publish AuraCore.PrivilegedService first."
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service already exists; stopping + removing before reinstall."
    if (-not $WhatIf) {
        if ($existing.Status -eq "Running") { Stop-Service -Name $ServiceName -Force }
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 1
    }
}

$binLine = "`"$BinaryPath`""
Write-Host "Creating service..."
if (-not $WhatIf) {
    sc.exe create $ServiceName binPath= $binLine start= auto DisplayName= $DisplayName | Out-Null
    sc.exe description $ServiceName $Description | Out-Null
    sc.exe failure     $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
    Start-Service -Name $ServiceName
}

Write-Host "Waiting for pipe availability..."
if (-not $WhatIf) {
    $timeout = [DateTime]::UtcNow.AddSeconds(5)
    $connected = $false
    while ([DateTime]::UtcNow -lt $timeout) {
        try {
            $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "AuraCorePro", [System.IO.Pipes.PipeDirection]::InOut)
            $pipe.Connect(200)
            $pipe.Dispose()
            $connected = $true
            break
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $connected) {
        Write-Error "Service started but pipe did not become available within 5 seconds."
        exit 2
    }
}

Write-Host "Install complete. Service is running."
