# Checks the installer script's fork identity and the rule that user data survives install and upgrade.
# It reads installer/BrimDeck.iss. It does not compile or run an installer.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$iss = Get-Content -Raw -LiteralPath (Join-Path $root 'installer/BrimDeck.iss')
function Assert($condition, $message) { if (!$condition) { throw "FAIL $message" }; Write-Output "PASS $message" }

Assert ($iss -match 'AppId "C4E8A1B7-6D24-4F93-8A15-2B7E9D0C3F61"') 'fork AppId is the default'
Assert ($iss -notmatch '#define AppId "9D9AD6ED-CF62-4527-A587-6DBE94A74B9E"') 'upstream AppId is not the default'
Assert ($iss -match '\{localappdata\}\\BrimDeck-Arm64') 'data folder is the fork folder'
Assert ($iss -match 'PayloadArchitecture') 'payload architecture is a define'
Assert ($iss -match 'AllowedArch "arm64"' -and $iss -match 'AllowedArch "x64os"') 'ARM64 and x64 installers refuse each other''s OS'
Assert ($iss -match 'SetupArchitecture=x64') 'setup stub stays x64'
Assert ($iss -match 'DeleteUserData and not HasParam\(''/UPGRADE''\)') 'uninstall deletes data only when asked and never on upgrade'
Assert ($iss -match 'BrimDeckArm64Sensors') 'uninstall looks for this fork''s sensor service'
Assert ($iss -notmatch 'Services\\BrimDeckSensors''') 'uninstall does not remove the upstream sensor service'
Write-Output 'Installer identity checks passed.'
