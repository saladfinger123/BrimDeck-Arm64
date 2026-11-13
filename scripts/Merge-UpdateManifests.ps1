# Builds one update.json from the per-runtime publish folders. The running app selects its own architecture.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$X64Directory,
    [Parameter(Mandatory)][string]$Arm64Directory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
function Read-Manifest($directory) {
    Get-Content -Raw -LiteralPath (Join-Path $directory 'update.json') | ConvertFrom-Json
}
$x64 = Read-Manifest $X64Directory
$arm = Read-Manifest $Arm64Directory
if ($x64.version -ne $arm.version -or $x64.product -ne 'BrimDeck-Arm64' -or $arm.product -ne 'BrimDeck-Arm64' -or $x64.repository -ne 'saladfinger123/BrimDeck-Arm64' -or $arm.repository -ne 'saladfinger123/BrimDeck-Arm64') {
    throw 'The two manifests are not the same fork release.'
}
if ($x64.architecture -ne 'win-x64' -or $arm.architecture -ne 'win-arm64') { throw 'Manifest architectures are not the expected pair.' }
foreach ($pair in @(@($X64Directory, $x64), @($Arm64Directory, $arm))) {
    $directory = $pair[0]; $manifest = $pair[1]
    foreach ($file in @($manifest.file, $manifest.portable.file)) {
        $path = Join-Path $directory $file
        if (!(Test-Path -LiteralPath $path)) { throw "Missing package $path" }
        $item = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $expected = if ($file -eq $manifest.file) { $manifest.sha256 } else { $manifest.portable.sha256 }
        $size = if ($file -eq $manifest.file) { $manifest.size } else { $manifest.portable.size }
        if ($item.Length -ne $size -or $hash -ne $expected) { throw "Hash or size mismatch for $file" }
        if ($file -notlike "*$($manifest.architecture)*") { throw "$file does not state $($manifest.architecture)" }
    }
}
foreach ($directory in @($X64Directory, $Arm64Directory)) {
    Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Extension -in '.exe', '.zip', '.json' -and $_.Name -ne 'update.json' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $OutputDirectory $_.Name)
    }
}
$packages = @($x64.packages) + @($arm.packages)
$manifest = [ordered]@{
    product = 'BrimDeck-Arm64'
    repository = 'saladfinger123/BrimDeck-Arm64'
    version = $arm.version
    notes = $arm.notes
    architecture = 'win-arm64'
    file = $arm.file
    size = $arm.size
    sha256 = $arm.sha256
    portable = $arm.portable
    packages = $packages
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'update.json'), ($manifest | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
} | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8
Write-Output "Merged release folder: $OutputDirectory"
