<#
.SYNOPSIS
    Removes the iCUE2HA service.

.PARAMETER Destination
    Folder the service runs from. Default: C:\Program Files\iCUE2HA

.PARAMETER ServiceName
    Name of the Windows service. Default: icue2mqttService

.PARAMETER KeepFiles
    Removes the service but leaves the installed folder, settings included.
#>
[CmdletBinding()]
param(
    [string]$Destination = "$env:ProgramFiles\iCUE2HA",
    [string]$ServiceName = 'icue2mqttService',
    [switch]$KeepFiles
)

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                 '-Destination', "`"$Destination`"", '-ServiceName', "`"$ServiceName`"")
    if ($KeepFiles) { $argList += '-KeepFiles' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    exit
}

try {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -ne 'Stopped') {
            Write-Host "Stopping $ServiceName"
            Stop-Service -Name $ServiceName -Force
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        sc.exe delete $ServiceName | Out-Null
        Write-Host "Service $ServiceName removed."
    } else {
        Write-Host "Service $ServiceName was not installed."
    }
    Get-Process -Name 'icue2mqtt' -ErrorAction SilentlyContinue | Stop-Process -Force

    if (-not $KeepFiles) {
        $destFull = [IO.Path]::GetFullPath($Destination.TrimEnd('\')).TrimEnd('\')
        $driveRoot = [IO.Path]::GetPathRoot($destFull).TrimEnd('\')
        if ($destFull -eq $driveRoot) { throw "Refusing to delete the drive root '$destFull'." }
        if (Test-Path $destFull) {
            Start-Sleep -Seconds 1
            Remove-Item $destFull -Recurse -Force
            Write-Host "$destFull deleted."
        }
    }

    Write-Host ''
    Write-Host 'The iCUE profiles and the iCUE settings were not touched.'
    Write-Host 'The entities stay in Home Assistant until you delete them there.'
} catch {
    Write-Host ''
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ''
Read-Host 'Press Enter to close'
