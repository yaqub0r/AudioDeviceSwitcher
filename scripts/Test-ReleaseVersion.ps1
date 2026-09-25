$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('ads-release-' + [guid]::NewGuid() + '.xml')
try {
    '<Package><Identity Name="yaqub0r.AudioDeviceSwitcher" Publisher="CN=yaqub0r.AudioDeviceSwitcher" Version="1.2.3.0" /></Package>' | Set-Content -LiteralPath $fixture
    $validator = Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1'
    if ((& $validator -Tag 'v1.2.3' -ManifestPath $fixture) -ne '1.2.3.0') { throw 'Valid release rejected.' }
    foreach ($tag in @('v1.2.4', 'v01.2.3', 'v1.2.3-rc1', 'v1.2.3.0', '1.2.3', 'v0.2.3', 'v65536.2.3', 'v1.2.3;echo bad')) {
        $rejected = $false
        try { & $validator -Tag $tag -ManifestPath $fixture | Out-Null } catch { $rejected = $true }
        if (!$rejected) { throw "Invalid or mismatched tag accepted: $tag" }
    }
    '<Package><Identity Name="other.App" Publisher="CN=yaqub0r.AudioDeviceSwitcher" Version="1.2.3.0" /></Package>' | Set-Content -LiteralPath $fixture
    $rejected = $false
    try { & $validator -Tag 'v1.2.3' -ManifestPath $fixture | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw 'Wrong package identity accepted.' }
    Write-Output 'Release metadata checks passed (valid version, mismatch, malformed tags, range, identity).'
} finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
}
