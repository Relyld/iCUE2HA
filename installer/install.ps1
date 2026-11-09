<#
.SYNOPSIS
    Installs or updates iCUE2HA as a Windows service.

.DESCRIPTION
    Run install.cmd (or this script) from the folder extracted from the release archive.
    It copies the program, writes properties.json, optionally fetches the Corsair Game SDK
    library needed for the iCUE profiles, then creates and starts the service.
    Existing settings are kept when updating.

.PARAMETER Destination
    Folder the service runs from. Default: C:\Program Files\iCUE2HA

.PARAMETER ServiceName
    Name of the Windows service. Default: icue2mqttService

.PARAMETER MqttUrl
    IP address or host name of the MQTT broker. Asked for when not given.

.PARAMETER MqttUser, MqttPassword
    Broker credentials, when it needs them.

.PARAMETER MqttPort
    Broker port, when it is not the default one.

.PARAMETER GameSdk
    yes downloads the Corsair Game SDK library without asking, no skips it.
    Default: ask, unless -Unattended.

.PARAMETER Unattended
    Never ask anything: use the values given on the command line and keep the rest.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -MqttUrl 192.168.1.20 -MqttUser hass -MqttPassword secret
    .\install.ps1 -Unattended -MqttUrl 192.168.1.20 -GameSdk yes
#>
[CmdletBinding()]
param(
    [string]$Destination = "$env:ProgramFiles\iCUE2HA",
    [string]$ServiceName = 'icue2mqttService',
    [string]$MqttUrl = '',
    [string]$MqttUser = '',
    [string]$MqttPassword = '',
    [string]$MqttPort = '',
    [ValidateSet('ask', 'yes', 'no')]
    [string]$GameSdk = 'ask',
    [switch]$Unattended
)

$ErrorActionPreference = 'Stop'

# The Corsair Game SDK library is not redistributed with this project: it is supplied by Corsair
# under its own terms. The Open CUE Service ships a copy, and the hash below is checked after
# downloading so that an unexpected file is never installed.
$GameSdkDllName = 'CgSDK.x64_2015.dll'
$GameSdkUrl     = 'https://raw.githubusercontent.com/Legion2/open-cue-service/main/CgSDK.x64_2015.dll'
$GameSdkSha256  = 'FFE5F7961EC0770D505283DE21ABAC0BC3D4B6442E6BDC7192DF710E25E6FE10'

# ---- relaunch as administrator -------------------------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                 '-Destination', "`"$Destination`"", '-ServiceName', "`"$ServiceName`"", '-GameSdk', $GameSdk)
    foreach ($pair in @{ MqttUrl = $MqttUrl; MqttUser = $MqttUser; MqttPassword = $MqttPassword; MqttPort = $MqttPort }.GetEnumerator()) {
        if ($pair.Value) { $argList += @("-$($pair.Key)", "`"$($pair.Value)`"") }
    }
    if ($Unattended) { $argList += '-Unattended' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    exit
}

function Write-Step([string]$text) { Write-Host "==> $text" -ForegroundColor Cyan }

$failed = $false
try {
    $sourceDir = Join-Path $PSScriptRoot 'app'
    if (-not (Test-Path (Join-Path $sourceDir 'icue2mqtt.exe'))) {
        throw "app\icue2mqtt.exe not found next to this script. Extract the whole release archive and run install.cmd from there."
    }

    $Destination = $Destination.TrimEnd('\')
    $destFull  = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
    $driveRoot = [IO.Path]::GetPathRoot($destFull).TrimEnd('\')
    if ($destFull -eq $driveRoot) { throw "Refusing to install into the drive root '$destFull'." }
    if ($PSScriptRoot.TrimEnd('\') -eq $destFull) { throw "Install somewhere else than the extracted archive itself." }

    # ---- stop the service while the files are replaced ---------------------------------------
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Write-Step "Stopping $ServiceName"
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    Get-Process -Name 'icue2mqtt' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # ---- copy the program, keeping the settings and the Game SDK library ---------------------
    Write-Step "Installing into $destFull"
    if (-not (Test-Path $destFull)) { New-Item -ItemType Directory -Path $destFull -Force | Out-Null }

    $propsPath = Join-Path $destFull 'properties.json'
    $existing = $null
    if (Test-Path $propsPath) {
        try { $existing = Get-Content $propsPath -Raw | ConvertFrom-Json }
        catch { Write-Warning "The existing properties.json could not be read, it will be replaced." }
    }

    Copy-Item -Path (Join-Path $sourceDir '*') -Destination $destFull -Recurse -Force

    # ---- settings -----------------------------------------------------------------------------
    $settings = [ordered]@{
        logPath             = "$destFull\servicelog.txt"
        mqttUrl             = ''
        mqttCredentialsUser = ''
        mqttCredentialsPwd  = ''
        profilesDirectory   = 'profiles'
        gameSdkEffectsPath  = ''
        autoManualMode      = $true
    }
    if ($existing) {
        foreach ($property in $existing.PSObject.Properties) { $settings[$property.Name] = $property.Value }
    }
    if ($MqttUrl)      { $settings['mqttUrl'] = $MqttUrl }
    if ($MqttUser)     { $settings['mqttCredentialsUser'] = $MqttUser }
    if ($MqttPassword) { $settings['mqttCredentialsPwd'] = $MqttPassword }
    if ($MqttPort)     { $settings['mqttOverridePort'] = $MqttPort }

    if (-not $settings['mqttUrl'] -and -not $Unattended) {
        Write-Host ''
        Write-Host 'Address of your MQTT broker, for instance 192.168.1.20 (this is usually your Home Assistant machine).'
        $settings['mqttUrl'] = (Read-Host 'MQTT broker').Trim()
        if (-not $settings['mqttCredentialsUser']) {
            $user = (Read-Host 'MQTT user (leave empty if your broker allows anonymous access)').Trim()
            if ($user) {
                $settings['mqttCredentialsUser'] = $user
                $secret = Read-Host 'MQTT password' -AsSecureString
                $settings['mqttCredentialsPwd'] = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
            }
        }
    }
    if (-not $settings['mqttUrl']) {
        Write-Warning "No MQTT broker set. Edit $propsPath before the service can do anything."
    }
    ($settings | ConvertTo-Json) | Set-Content -Path $propsPath -Encoding UTF8
    Write-Host "Settings written to $propsPath"

    # ---- Corsair Game SDK library, for the iCUE profiles only --------------------------------
    $dllPath = Join-Path $destFull $GameSdkDllName
    if (Test-Path $dllPath) {
        Write-Host "$GameSdkDllName already installed."
    } else {
        $wanted = $GameSdk
        if ($wanted -eq 'ask') {
            if ($Unattended) {
                $wanted = 'no'
            } else {
                Write-Host ''
                Write-Host "Switching iCUE profiles from Home Assistant needs $GameSdkDllName, which is supplied by"
                Write-Host 'Corsair and cannot be shipped with this program. It can be downloaded from the Open CUE'
                Write-Host 'Service repository (https://github.com/Legion2/open-cue-service), and its content is checked.'
                Write-Host 'The lights work without it; only the profiles need it.'
                $answer = (Read-Host "Download it now? [Y/n]").Trim()
                $wanted = if ($answer -eq '' -or $answer -match '^[yYoO]') { 'yes' } else { 'no' }
            }
        }
        if ($wanted -eq 'yes') {
            Write-Step "Downloading $GameSdkDllName"
            $temp = Join-Path ([IO.Path]::GetTempPath()) "$GameSdkDllName.download"
            try {
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -Uri $GameSdkUrl -OutFile $temp -UseBasicParsing
                $hash = (Get-FileHash -Path $temp -Algorithm SHA256).Hash
                if ($hash -ne $GameSdkSha256) {
                    Remove-Item $temp -Force
                    Write-Warning "The downloaded file is not the expected one (SHA256 $hash), it was discarded."
                    Write-Host "  Get $GameSdkDllName yourself and put it in $destFull"
                } else {
                    Move-Item $temp $dllPath -Force
                    Write-Host "$GameSdkDllName installed and verified."
                }
            } catch {
                Write-Warning "Download failed: $($_.Exception.Message)"
                Write-Host "  Get $GameSdkDllName yourself and put it in $destFull"
            }
        } else {
            Write-Host "Skipped. The lights will work, the iCUE profiles will not."
            Write-Host "  To add it later, put $GameSdkDllName in $destFull and restart the service."
        }
    }

    # ---- service --------------------------------------------------------------------------------
    $exePath = Join-Path $destFull 'icue2mqtt.exe'
    if (-not $service) {
        Write-Step "Creating the service $ServiceName"
        $result = sc.exe create $ServiceName binPath= "`"$exePath`"" start= delayed-auto DisplayName= "iCUE2HA"
        if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed: $result" }
        sc.exe description $ServiceName "Publishes Corsair iCUE devices and profiles to Home Assistant over MQTT." | Out-Null
    } else {
        sc.exe config $ServiceName binPath= "`"$exePath`"" start= delayed-auto | Out-Null
    }

    $startedAt = Get-Date
    Write-Step "Starting $ServiceName"
    Start-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    Write-Host "$ServiceName is running." -ForegroundColor Green

    Start-Sleep -Seconds 5
    try {
        $events = Get-WinEvent -LogName 'icue2mqttLog' -MaxEvents 25 -ErrorAction Stop |
            Where-Object { $_.TimeCreated -ge $startedAt } | Sort-Object TimeCreated
        if ($events) {
            Write-Step 'Service log'
            foreach ($entry in $events) {
                Write-Host ("{0:HH:mm:ss}  {1}" -f $entry.TimeCreated, ($entry.Message -split "`r?`n")[0])
            }
        }
    } catch { }

    Write-Host ''
    Write-Host 'Done.' -ForegroundColor Green
    Write-Host "  Program   : $destFull"
    Write-Host "  Settings  : $propsPath"
    Write-Host "  Log       : Event Viewer > Applications and Services Logs > icue2mqttLog"
    Write-Host ''
    Write-Host 'In Home Assistant, the devices appear under Settings > Devices & services > MQTT.'
    Write-Host 'Make sure iCUE is running, with the iCUE SDK and the Game SDK enabled in its settings.'
} catch {
    Write-Host ''
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $failed = $true
}

Write-Host ''
Read-Host 'Press Enter to close'
if ($failed) { exit 1 }
