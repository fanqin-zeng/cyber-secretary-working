$ErrorActionPreference = 'Stop'
$resolve = Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\resolve-release-version.ps1'
$cases = @(
    @{ Tag = 'v0.1'; Version = '0.1.0'; Preview = $false },
    @{ Tag = 'v0.2'; Version = '0.2.0'; Preview = $false },
    @{ Tag = 'v0.2.1'; Version = '0.2.1'; Preview = $false },
    @{ Tag = 'v1.0.0-rc.1'; Version = '1.0.0-rc.1'; Preview = $true },
    @{ Tag = 'v0.2-beta'; Version = '0.2.0-beta'; Preview = $true }
)
foreach ($case in $cases) {
    $result = & $resolve -Tag $case.Tag
    if ($result.Tag -cne $case.Tag -or $result.Version -cne $case.Version -or $result.IsPrerelease -ne $case.Preview) {
        throw ('Unexpected version resolution: ' + $case.Tag)
    }
}
$invalid = @('0.2', 'V0.2', 'v01.2', 'v0.02', 'v0.2.01', 'v0.2.1.4', 'v0.2-', 'v0.2.1-rc.01', 'v65535.1', 'v0.2/other', 'v0.2;other', "v0.2`n")
foreach ($tag in $invalid) {
    $rejected = $false
    try { & $resolve -Tag $tag | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw ('Invalid tag was accepted: ' + $tag) }
}
Write-Output ('PASS: ' + ($cases.Count + $invalid.Count) + ' release version cases.')
