[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallationRecord,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$record = Get-Content -LiteralPath $InstallationRecord -Raw | ConvertFrom-Json
if ($record.ForkName -ne 'yaqub0r.AudioDeviceSwitcher' -or $record.LegacyFamily -ne '16084JoseTorres.AudioDeviceSwitcher_dbg56r8e2ee38') {
    throw 'Unexpected app identities in rollback record.'
}
if (!$Apply) {
    Write-Output "Will stop the personal app, disable its startup, and restore Store startup state $($record.LegacyStartupState). Both apps and their settings will be retained. Use -Apply to proceed."
    return
}
$startupRoot = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData'
$fork = Get-AppxPackage -Name $record.ForkName
if ($fork) {
    $prefix = $fork.InstallLocation.TrimEnd('\') + '\'
    Get-Process -Name AudioDeviceSwitcher -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    } | Stop-Process
    $startup = Join-Path $startupRoot "$($fork.PackageFamilyName)\Startup"
    if (Test-Path -LiteralPath $startup) { Set-ItemProperty -LiteralPath $startup -Name State -Type DWord -Value 1 }
}
if ($null -ne $record.LegacyStartupState) {
    $startup = Join-Path $startupRoot "$($record.LegacyFamily)\Startup"
    Set-ItemProperty -LiteralPath $startup -Name State -Type DWord -Value $record.LegacyStartupState
}
Write-Output 'Store startup restored. Open the original Audio Device Switcher to resume using it. No apps or settings were deleted.'
