iCUE2HA - Corsair iCUE in Home Assistant, over MQTT
===================================================

WHAT THIS DOES
  Publishes every Corsair iCUE device to Home Assistant over MQTT as an RGB light
  with its own color and brightness, and lets Home Assistant switch the iCUE
  lighting profile, for instance when a game starts.

BEFORE YOU START
  1. iCUE is installed and running.
     In its settings, SDK page, turn on BOTH "iCUE SDK" and "Game SDK".
  2. You have an MQTT broker, usually the Mosquitto broker add-on of Home
     Assistant, and the MQTT integration is set up there.
     Note down its IP address, and the user and password if it needs them.
  3. For the lighting profiles only: export them from iCUE into
     C:\ProgramData\Corsair\CUE5\GameSdkEffects\profiles with only
     "Lighting Effects" checked, and list them in priorities.cfg next to them,
     one "Name=priority" line per profile. You can do this later.

INSTALLING
  Double-click install.cmd and answer the questions. It asks for administrator
  rights, installs into C:\Program Files\iCUE2HA and starts the service.

  To switch profiles, the installer offers to download CgSDK.x64_2015.dll from
  the Open CUE Service repository (https://github.com/Legion2/open-cue-service).
  That library is supplied by Corsair under its own terms and cannot be shipped
  with this program, so it is fetched separately and its content is checked.
  Say no and the lights still work; only the profiles will be missing.

UPDATING
  Extract the new archive and run install.cmd again. Your settings and the
  Game SDK library are kept.

REMOVING
  Run uninstall.ps1 from an administrator PowerShell, or:
    sc.exe stop icue2mqttService
    sc.exe delete icue2mqttService

IF SOMETHING DOES NOT WORK
  Open the Event Viewer, then Applications and Services Logs > icue2mqttLog,
  or run this in PowerShell:
    Get-WinEvent -LogName icue2mqttLog -MaxEvents 30 | Sort-Object TimeCreated | Format-List TimeCreated,Message

  You should see "Connected to MQTT broker", your profile names, "Game SDK
  connected", then one "Publishing device ..." line per device.

  Nothing works while iCUE is closed. Do not run open-cue-service or
  open-cue-cli at the same time: this program drives the Game SDK itself.

MORE
  https://github.com/Relyld/iCUE2HA
