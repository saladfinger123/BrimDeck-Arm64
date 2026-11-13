# Installs one setup for the current user, or checks that a setup refuses this OS.
# It does not install a driver or a sensor service. User data is not deleted.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Setup,
    [ValidateSet('aa64', '8664')][string]$ExpectedMachine,
    [switch]$ExpectRefuse
)
$ErrorActionPreference = 'Stop'
$setupPath = (Resolve-Path -LiteralPath $Setup).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ("BrimDeck-Arm64-safe-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$log = Join-Path $root 'setup.log'
$process = Start-Process -FilePath $setupPath -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/CURRENTUSER', "/DIR=`"$root\\`"", "/LOG=`"$log`"") -PassThru -Wait
if ($ExpectRefuse) {
    if ($process.ExitCode -eq 0) { throw "The installer for the other architecture installed on this OS (exit 0). Log: $log" }
    Write-Output "PASS installer refused this OS with exit $($process.ExitCode)"
    return
}
if ($process.ExitCode -ne 0) { throw "Install failed with $($process.ExitCode). Log: $log" }
$installed = Get-ChildItem -LiteralPath $root -Recurse -Filter BrimDeck.exe | Select-Object -First 1
if (-not $installed) { throw "BrimDeck.exe was not installed under $root" }
$bytes = [IO.File]::ReadAllBytes($installed.FullName)
$lfanew = [BitConverter]::ToInt32($bytes, 0x3C)
$machine = [BitConverter]::ToUInt16($bytes, $lfanew + 4)
$expected = [Convert]::ToUInt16($ExpectedMachine, 16)
if ($machine -ne $expected) { throw "Installed BrimDeck.exe machine $('{0:X4}' -f $machine) is not $ExpectedMachine" }
Write-Output "PASS installed payload machine $ExpectedMachine"
$service = Get-Service -Name BrimDeckArm64Sensors -ErrorAction SilentlyContinue
if ($service) { throw 'The install registered the sensor service.' }
Write-Output 'PASS no sensor service was registered'
$sensorFolder = Join-Path $env:ProgramFiles 'BrimDeck Arm64 Sensors'
if (Test-Path -LiteralPath $sensorFolder) { throw 'The install created the sensor service folder.' }
Write-Output 'PASS no sensor service folder was created'
$data = Join-Path $env:LOCALAPPDATA 'BrimDeck-Arm64'
New-Item -ItemType Directory -Force -Path $data | Out-Null
$marker = Join-Path $data 'safe-install-marker.txt'
Set-Content -LiteralPath $marker -Value 'keep' -Encoding ascii
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C4E8A1B7-6D24-4F93-8A15-2B7E9D0C3F61}_is1'
$uninstall = [regex]::Match((Get-ItemProperty $uninstallKey).UninstallString, '^"([^"]+)"').Groups[1].Value
Start-Process -FilePath $uninstall -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES') -Wait
$deadline = [DateTime]::UtcNow.AddSeconds(90)
while ((Test-Path $uninstallKey) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (Test-Path $uninstallKey) { throw 'Uninstall did not finish.' }
if (-not (Test-Path -LiteralPath $marker)) { throw 'Uninstall deleted user data without being asked.' }
Write-Output 'PASS uninstall kept the user-data marker'
Remove-Item -LiteralPath $marker -Force
Write-Output 'Safe install checks passed.'
