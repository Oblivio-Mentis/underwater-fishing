param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$')]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$ExpectedAssemblySha256
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$raidRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$raidBuild = [IO.Path]::GetFullPath((Join-Path $raidRoot 'Builds/Windows'))
$raidAssemblyRelative = 'UnderwaterFishing_Data/Managed/Assembly-CSharp.dll'
$raidRequiredFiles = @(
    'UnderwaterFishing.exe',
    'UnityPlayer.dll',
    'UnderwaterFishing_Data/globalgamemanagers',
    'UnderwaterFishing_Data/boot.config',
    $raidAssemblyRelative,
    'UnderwaterFishing_Data/Plugins/x86_64/steam_api64.dll',
    'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll',
    'D3D12/D3D12Core.dll'
)
if (!(Test-Path -LiteralPath $raidBuild -PathType Container)) { throw "Build directory is missing: $raidBuild" }
foreach ($raidRequired in $raidRequiredFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $raidBuild $raidRequired) -PathType Leaf)) { throw "Required runtime file is missing: $raidRequired" }
}
$raidProjectSettings = Get-Content -LiteralPath (Join-Path $raidRoot 'ProjectSettings/ProjectSettings.asset') -Raw -Encoding UTF8
$raidVersionMatch = [regex]::Match($raidProjectSettings, '(?m)^\s*bundleVersion:\s*([^\r\n]+)')
if (!$raidVersionMatch.Success -or $raidVersionMatch.Groups[1].Value.Trim().Trim('"') -ne $Version) {
    throw 'The requested version does not match ProjectSettings.bundleVersion. Package the checkout used for the approved build.'
}
$raidAssemblyPath = Join-Path $raidBuild $raidAssemblyRelative
$raidAssemblyHash = (Get-FileHash -LiteralPath $raidAssemblyPath -Algorithm SHA256).Hash
if ($raidAssemblyHash -ne $ExpectedAssemblySha256) { throw 'Assembly-CSharp.dll does not match the approved build hash. Nothing was packaged.' }
$raidCommit = [string](& git -C $raidRoot rev-parse HEAD)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raidCommit)) { throw 'Could not read the packaging checkout commit.' }
$raidCommit = $raidCommit.Trim()
$raidGitStatus = @(& git -C $raidRoot status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Could not read the packaging checkout dirty status.' }
$raidDirty = $raidGitStatus.Count -gt 0

function Test-RaidExcludedName([string]$Name) {
    return $Name -like '*_DoNotShip' -or $Name -like '*_BackUpThisFolder_ButDontShipItWithYourGame*' -or $Name -ieq 'FishNet.SDK.Id'
}
function Get-RaidRuntimeFiles([IO.DirectoryInfo]$Directory) {
    foreach ($raidEntry in Get-ChildItem -LiteralPath $Directory.FullName -Force) {
        if (Test-RaidExcludedName $raidEntry.Name) { continue }
        if (($raidEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing a runtime link/reparse point: $($raidEntry.Name)" }
        if ($raidEntry.PSIsContainer) { Get-RaidRuntimeFiles $raidEntry }
        else { $raidEntry }
    }
}
$raidRuntimeFiles = @(
    foreach ($raidEntry in Get-ChildItem -LiteralPath $raidBuild -Force) {
        if (Test-RaidExcludedName $raidEntry.Name) { continue }
        if (($raidEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing a build link/reparse point: $($raidEntry.Name)" }
        if ($raidEntry.PSIsContainer) {
            if ($raidEntry.Name -in @('UnderwaterFishing_Data', 'MonoBleedingEdge', 'D3D12')) { Get-RaidRuntimeFiles $raidEntry }
        }
        elseif ($raidEntry.Extension -in @('.exe', '.dll') -or $raidEntry.Name -ieq 'steam_appid.txt') { $raidEntry }
    }
)
$raidPrefix = $raidBuild.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$raidSources = New-Object 'System.Collections.Generic.List[object]'
foreach ($raidFile in $raidRuntimeFiles) {
    if (!$raidFile.FullName.StartsWith($raidPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime source escaped Builds/Windows.' }
    $raidRelative = $raidFile.FullName.Substring($raidPrefix.Length).Replace('\', '/')
    if ($raidFile.Name -ieq 'steam_appid.txt' -and (Get-Content -LiteralPath $raidFile.FullName -Raw).Trim() -ne '480') {
        throw 'steam_appid.txt does not contain the approved development App ID 480.'
    }
    $raidSources.Add([pscustomobject]@{ Source = $raidFile.FullName; Relative = $raidRelative; Length = $raidFile.Length; WrittenUtc = $raidFile.LastWriteTimeUtc })
}
foreach ($raidRelative in @('docs/STEAM-TEST.md', 'docs/WINDOWS-PLAYTEST.md', 'tools/Verify-SteamRemote.ps1')) {
    $raidSourcePath = Join-Path $raidRoot $raidRelative
    if (!(Test-Path -LiteralPath $raidSourcePath -PathType Leaf)) { throw "Required playtest companion is missing: $raidRelative" }
    $raidFile = Get-Item -LiteralPath $raidSourcePath
    if (($raidFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing a companion link: $raidRelative" }
    $raidSources.Add([pscustomobject]@{ Source = $raidFile.FullName; Relative = $raidRelative; Length = $raidFile.Length; WrittenUtc = $raidFile.LastWriteTimeUtc })
}
foreach ($raidRequired in $raidRequiredFiles) {
    if (@($raidSources | Where-Object { $_.Relative -eq $raidRequired }).Count -ne 1) { throw "Required runtime file was excluded or duplicated: $raidRequired" }
}

$raidOutput = [IO.Path]::GetFullPath((Join-Path $raidRoot "Builds/Distribution/$Version"))
$raidDistributionPrefix = [IO.Path]::GetFullPath((Join-Path $raidRoot 'Builds/Distribution')).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (!$raidOutput.StartsWith($raidDistributionPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output escaped Builds/Distribution.' }
New-Item -ItemType Directory -Force -Path $raidOutput | Out-Null
$raidPackageRoot = "UnderwaterFishing-$Version-Windows"
$raidStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$raidDirtySuffix = if ($raidDirty) { '-dirty' } else { '' }
$raidZipName = "$raidPackageRoot-$($raidCommit.Substring(0, 12))$raidDirtySuffix-$raidStamp.zip"
$raidZipPath = Join-Path $raidOutput $raidZipName
$raidManifest = [ordered]@{
    schemaVersion = 1
    product = 'Underwater Fishing'
    version = $Version
    platform = 'Windows x86_64'
    scriptingBackend = 'Mono'
    buildType = 'development'
    development = $true
    steamAppId = 480
    steamAppIdPurpose = 'Approved development testing; Spacewar App ID, not this prototype store entry'
    packagedUtc = [DateTime]::UtcNow.ToString('o')
    source = [ordered]@{
        commit = $raidCommit
        dirty = $raidDirty
        scope = 'Checkout at packaging time; the approved player is identified by assemblySha256'
    }
    assemblyPath = $raidAssemblyRelative
    assemblySha256 = $raidAssemblyHash
    executableSha256 = (Get-FileHash -LiteralPath (Join-Path $raidBuild 'UnderwaterFishing.exe') -Algorithm SHA256).Hash
    playerFileCount = $raidRuntimeFiles.Count
    companionFileCount = 3
    uncompressedSourceBytes = [long](($raidSources | Measure-Object -Property Length -Sum).Sum)
    excludedNames = @('*_DoNotShip', '*_BackUpThisFolder_ButDontShipItWithYourGame*', 'FishNet.SDK.Id')
    sourceSelection = 'Root EXE/DLL and optional steam_appid.txt; Data, MonoBleedingEdge and D3D12 trees; no test logs/results'
    instructions = @('docs/WINDOWS-PLAYTEST.md', 'docs/STEAM-TEST.md')
    externalSteamAcceptance = 'Not established by packaging; see manual checklist and Verify-SteamRemote.ps1'
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$raidStream = [IO.File]::Open($raidZipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$raidZip = $null
try {
    $raidZip = New-Object IO.Compression.ZipArchive($raidStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    foreach ($raidSource in $raidSources | Sort-Object Relative) {
        $raidEntryName = $raidPackageRoot + '/' + $raidSource.Relative
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($raidZip, $raidSource.Source, $raidEntryName, [IO.Compression.CompressionLevel]::Optimal)
    }
    $raidManifestEntry = $raidZip.CreateEntry($raidPackageRoot + '/package-manifest.json', [IO.Compression.CompressionLevel]::Optimal)
    $raidManifestWriter = New-Object IO.StreamWriter($raidManifestEntry.Open(), (New-Object Text.UTF8Encoding($false)))
    try { $raidManifestWriter.Write(($raidManifest | ConvertTo-Json -Depth 6)) }
    finally { $raidManifestWriter.Dispose() }
}
finally {
    if ($null -ne $raidZip) { $raidZip.Dispose() }
    $raidStream.Dispose()
}

# No staging directory, deletions or moves. A failed/incomplete archive receives no checksum sidecar.
foreach ($raidSource in $raidSources) {
    $raidCurrent = Get-Item -LiteralPath $raidSource.Source
    if ($raidCurrent.Length -ne $raidSource.Length -or $raidCurrent.LastWriteTimeUtc -ne $raidSource.WrittenUtc) { throw 'An input changed during packaging; do not distribute this archive.' }
}
if ((Get-FileHash -LiteralPath $raidAssemblyPath -Algorithm SHA256).Hash -ne $raidAssemblyHash) { throw 'Assembly changed during packaging; do not distribute this archive.' }
$raidCheck = [IO.Compression.ZipFile]::OpenRead($raidZipPath)
try {
    if ($raidCheck.Entries.Count -ne $raidSources.Count + 1) { throw 'Unexpected archive entry count.' }
    foreach ($raidEntry in $raidCheck.Entries) {
        foreach ($raidSegment in $raidEntry.FullName.Split('/')) {
            if ((Test-RaidExcludedName $raidSegment) -or $raidSegment -eq '..') { throw 'Archive contains an excluded or unsafe entry.' }
        }
    }
    foreach ($raidRequired in ($raidRequiredFiles + @('docs/STEAM-TEST.md', 'docs/WINDOWS-PLAYTEST.md', 'tools/Verify-SteamRemote.ps1', 'package-manifest.json'))) {
        if ($null -eq $raidCheck.GetEntry($raidPackageRoot + '/' + $raidRequired)) { throw "Archive is missing $raidRequired" }
    }
    $raidPackedAssembly = $raidCheck.GetEntry($raidPackageRoot + '/' + $raidAssemblyRelative).Open()
    $raidSha = [Security.Cryptography.SHA256]::Create()
    try { $raidPackedHash = [BitConverter]::ToString($raidSha.ComputeHash($raidPackedAssembly)).Replace('-', '') }
    finally { $raidSha.Dispose(); $raidPackedAssembly.Dispose() }
    if ($raidPackedHash -ne $raidAssemblyHash) { throw 'The archived assembly hash differs from the approved player.' }
}
finally { $raidCheck.Dispose() }
$raidArchiveHash = (Get-FileHash -LiteralPath $raidZipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText($raidZipPath + '.sha256', $raidArchiveHash + '  ' + $raidZipName + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
Write-Output "Package: $raidZipPath"
Write-Output "ZIP SHA256: $raidArchiveHash"
Write-Output "Assembly SHA256: $raidAssemblyHash"
Write-Output "Commit: $raidCommit; dirty=$raidDirty; runtime files=$($raidRuntimeFiles.Count)"
