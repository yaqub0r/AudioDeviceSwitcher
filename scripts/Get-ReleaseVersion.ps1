[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../src/AudioDeviceSwitcher (Package)/Package.appxmanifest')
)

$ErrorActionPreference = 'Stop'
if ($Tag -cnotmatch '^v([1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Use a stable version tag such as v1.2.0 (no leading zeroes or prerelease suffix).'
}
$numbers = @($Matches[1], $Matches[2], $Matches[3])
foreach ($number in $numbers) {
    if ([long]$number -gt 65535) { throw 'MSIX version components must not exceed 65535.' }
}
$version = ($numbers -join '.') + '.0'
[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
if ($manifest.Package.Identity.Name -ne 'yaqub0r.AudioDeviceSwitcher' -or
    $manifest.Package.Identity.Publisher -ne 'CN=yaqub0r.AudioDeviceSwitcher') {
    throw 'Unexpected package identity or publisher.'
}
if ($manifest.Package.Identity.Version -ne $version) {
    throw "Tag $Tag requires manifest version $version; update and review the manifest before tagging."
}
Write-Output $version
