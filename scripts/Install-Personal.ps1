[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$MigrationToolPath,
    [Parameter(Mandatory)][string]$PreparedSettingsPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedPackageSha256,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$ExpectedSignerThumbprint,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$migrationTool = (Resolve-Path -LiteralPath $MigrationToolPath).Path
$preparedFile = (Resolve-Path -LiteralPath $PreparedSettingsPath).Path
$backupFolder = Split-Path -Parent $preparedFile
$originalFile = Join-Path $backupFolder 'original-settings.json'
$forkName = 'yaqub0r.AudioDeviceSwitcher'
$legacyName = '16084JoseTorres.AudioDeviceSwitcher'
$startupRoot = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData'

if ((Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash -ne $ExpectedPackageSha256) {
    throw 'The package hash differs from the reviewed build.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $packageFile
if ($signature.SignerCertificate.Thumbprint -ne $ExpectedSignerThumbprint) {
    throw 'The package signer differs from the reviewed certificate.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $entry = $zip.GetEntry('AppxManifest.xml')
    if (!$entry) { throw 'Package manifest missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
} finally { $zip.Dispose() }
if ($manifest.Package.Identity.Name -ne $forkName -or $manifest.Package.Identity.Publisher -ne 'CN=yaqub0r.AudioDeviceSwitcher') {
    throw 'This is not the separate personal package.'
}
if (Get-AppxPackage -Name $forkName) {
    throw 'The personal app is already installed. This first-install script will not overwrite its settings.'
}
$legacy = Get-AppxPackage -Name $legacyName
if (!$legacy) { throw 'The original Store app is missing; this migration requires it.' }
$prepared = Get-Content -LiteralPath $preparedFile -Raw | ConvertFrom-Json
if (!$prepared.Commands -or !(Test-Path -LiteralPath $originalFile)) { throw 'Prepared settings or original backup missing.' }
& $migrationTool verify-source $originalFile
if ($LASTEXITCODE -ne 0) { throw 'The migration backup is no longer current.' }
$legacyStartup = Join-Path $startupRoot "$($legacy.PackageFamilyName)\Startup"
$legacyState = (Get-ItemProperty -LiteralPath $legacyStartup -Name State -ErrorAction SilentlyContinue).State

[pscustomobject]@{
    Package = $packageFile
    Version = $manifest.Package.Identity.Version
    SignatureStatus = $signature.Status
    SignerThumbprint = $signature.SignerCertificate.Thumbprint
    MigratedCommands = $prepared.Commands.Count
    OriginalStartupState = $legacyState
    Apply = [bool]$Apply
} | Format-List

if (!$Apply) {
    Write-Output 'Preview only. No app, certificate trust, startup, or settings changes were made.'
    return
}
if ($signature.Status -ne 'Valid') {
    throw 'The reviewed signing certificate must be explicitly trusted before installation. This script does not change certificate trust.'
}

$recordPath = Join-Path $backupFolder ('installation-' + (Get-Date -Format 'yyyyMMddTHHmmssfff') + '.json')
$record = [ordered]@{
    PackageSha256 = $ExpectedPackageSha256
    SignerThumbprint = $ExpectedSignerThumbprint
    LegacyFamily = $legacy.PackageFamilyName
    LegacyStartupState = $legacyState
    ForkName = $forkName
    PreparedSettingsPath = $preparedFile
}
$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
Write-Output "Rollback record: $recordPath"

function Stop-PackageProcess($package) {
    if (!$package) { return }
    $prefix = $package.InstallLocation.TrimEnd('\') + '\'
    Get-Process -Name AudioDeviceSwitcher -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    } | Stop-Process
}

try {
    Add-AppxPackage -Path $packageFile
    $fork = Get-AppxPackage -Name $forkName
    if (!$fork) { throw 'Package registration did not complete.' }
    & $migrationTool import $preparedFile
    if ($LASTEXITCODE -ne 0) { throw 'Settings import failed.' }
    & $migrationTool verify $preparedFile
    if ($LASTEXITCODE -ne 0) { throw 'Settings verification failed.' }
    $forkStartup = Join-Path $startupRoot "$($fork.PackageFamilyName)\Startup"
    # Configure startup only after the separate app and all settings are verified.
    if ($null -ne $legacyState) {
        Set-ItemProperty -LiteralPath $legacyStartup -Name State -Type DWord -Value 1
    }
    New-Item -Path $forkStartup -Force | Out-Null
    $forkState = if ($prepared.RunAtStartup) { 2 } else { 0 }
    New-ItemProperty -LiteralPath $forkStartup -Name State -PropertyType DWord -Value $forkState -Force | Out-Null
    Stop-PackageProcess $legacy
    Write-Output "Installed and migrated $($fork.PackageFullName). The Store app and its settings are retained. Launch the personal app to begin using its hotkeys."
} catch {
    $fork = Get-AppxPackage -Name $forkName
    if ($fork) {
        Stop-PackageProcess $fork
        $forkStartup = Join-Path $startupRoot "$($fork.PackageFamilyName)\Startup"
        if (Test-Path -LiteralPath $forkStartup) { Set-ItemProperty -LiteralPath $forkStartup -Name State -Type DWord -Value 1 }
    }
    if ($null -ne $legacyState) { Set-ItemProperty -LiteralPath $legacyStartup -Name State -Type DWord -Value $legacyState }
    throw
}
