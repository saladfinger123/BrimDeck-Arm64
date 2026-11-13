# Installs one setup for the current user, or checks that a setup refuses this OS.
# It does not install a driver or a sensor service. User data is not deleted.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Setup,
    [ValidateSet('aa64', '8664')][string]$ExpectedMachine,
    [switch]$ExpectRefuse,
    [string]$ModuleReport
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
if ($ModuleReport) {
    $running = Start-Process -FilePath $installed.FullName -WorkingDirectory $installed.DirectoryName -PassThru
    Start-Sleep -Seconds 8
    if ($running.HasExited) { throw "BrimDeck.exe exited before its modules could be read (code $($running.ExitCode))." }
    $running.Refresh()
    $loaded = @()
    foreach ($module in $running.Modules) {
        $machineName = 'unreadable'
        $machineValue = $null
        try {
            $moduleBytes = [IO.File]::ReadAllBytes($module.FileName)
            if ($moduleBytes.Length -gt 0x40 -and $moduleBytes[0] -eq 0x4D -and $moduleBytes[1] -eq 0x5A) {
                $moduleLfanew = [BitConverter]::ToInt32($moduleBytes, 0x3C)
                $machineValue = [BitConverter]::ToUInt16($moduleBytes, $moduleLfanew + 4)
                $machineName = '{0:X4}' -f $machineValue
            }
        } catch { $machineName = 'locked' }
        $loaded += [ordered]@{ path = $module.FileName; machine = $machineName }
    }
    Stop-Process -Id $running.Id -Force
    $inApp = @($loaded | Where-Object { $_.path.StartsWith($installed.DirectoryName, [StringComparison]::OrdinalIgnoreCase) -and $_.machine -notin @($ExpectedMachine.ToUpperInvariant(), '014C', 'locked', 'unreadable') })
    [IO.File]::WriteAllText($ModuleReport, ($loaded | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
    if ($inApp.Count -gt 0) { throw "Installed files loaded with an unexpected machine: $($inApp.path -join ', ')" }
    Write-Output "PASS loaded $($loaded.Count) modules; report $ModuleReport"
}
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
