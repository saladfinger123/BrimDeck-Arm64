[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(-(alpha|beta|arm64)\.\d+)?$')][string]$Version,
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [string]$ReleaseNotes,
    [string]$OutputDirectory = 'artifacts/releases'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseNotes.ps1')
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
Push-Location $projectRoot
try {
    if (!$ReleaseNotes) { $ReleaseNotes = "releases/$Version.md" }
    $notesPath = (Resolve-Path -LiteralPath $ReleaseNotes).Path
    $outputPath = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $projectRoot $OutputDirectory }))
    $publishPath = Join-Path $projectRoot ".artifacts/release-build/$Version-$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $publishPath, $outputPath | Out-Null
    # Each version gets a clean output folder, so files of an earlier release never end up in this one.
    Get-ChildItem -LiteralPath $outputPath -File | Remove-Item
    $payload = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }
    $platform = if ($Runtime -eq 'win-arm64') { 'ARM64' } else { 'x64' }
    # The sensor service reads Intel MSRs and AMD SMN. It is published only for win-x64. An ARM64 app build does not
    # embed a service hash, so it will not download or install that service.
    $sensorsArgument = @()
    if ($Runtime -eq 'win-x64') {
        $sensorsPath = "$publishPath-sensors"
        dotnet publish src/BrimDeck.Sensors/BrimDeck.Sensors.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -p:Version=$Version -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true -o $sensorsPath
        if ($LASTEXITCODE -ne 0) { throw 'Sensor service publish failed.' }
        $sensors = Copy-Item -LiteralPath (Join-Path $sensorsPath 'BrimDeck.Sensors.exe') -Destination $outputPath -PassThru
        $sensorsSha256 = (Get-FileHash -LiteralPath $sensors.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $sensorsArgument = @("-p:SensorsSha256=$sensorsSha256")
    }
    dotnet publish src/BrimDeck/BrimDeck.csproj -c Release -r $Runtime -p:Platform=$platform --self-contained true -p:Version=$Version -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false @sensorsArgument -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    $iscc = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1')
    $outputName = "BrimDeck-Arm64-$Version-$Runtime-Setup"
    & $iscc -qp "-dAppVersion=$Version" "-dSourceDir=$publishPath" "-o$outputPath" "-dPayloadArchitecture=$payload" "-dOutputName=$outputName" installer/BrimDeck.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Get-Item -LiteralPath (Join-Path $outputPath "$outputName.exe")
    $portableName = "BrimDeck-Arm64-$Version-$Runtime-Portable.zip"
    $portable = New-PortablePackage -SourceDir $publishPath -Destination (Join-Path $outputPath $portableName)
    $installerHash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $portableHash = (Get-FileHash -LiteralPath $portable.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $package = [ordered]@{
        architecture = $Runtime
        file = $installer.Name
        size = $installer.Length
        sha256 = $installerHash
        portable = [ordered]@{
            file = $portable.Name
            size = $portable.Length
            sha256 = $portableHash
            architecture = $Runtime
        }
    }
    # file, size and sha256 stay the installer for this runtime. packages lists it too, so a combined manifest can
    # carry both runtimes and the app still selects by its own architecture.
    $manifest = [ordered]@{
        product = 'BrimDeck-Arm64'
        repository = 'saladfinger123/BrimDeck-Arm64'
        version = $Version
        notes = Read-ReleaseNotes $notesPath
        architecture = $Runtime
        file = $installer.Name
        size = $installer.Length
        sha256 = $installerHash
        portable = $package.portable
        packages = @($package)
    }
    [IO.File]::WriteAllText((Join-Path $outputPath 'update.json'), ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    Get-ChildItem -LiteralPath $outputPath -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    } | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Encoding utf8
    Set-Content -LiteralPath (Join-Path $outputPath 'publish-path.txt') -Value $publishPath -Encoding ascii
    Write-Output "Release artifacts: $outputPath"
    Write-Output "Published application for validation: $publishPath"
}
finally { Pop-Location }
