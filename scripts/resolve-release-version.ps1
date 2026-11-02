param([string]$Tag)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($Tag)) {
    $props = Join-Path (Split-Path $PSScriptRoot -Parent) 'Directory.Build.props'
    $version = [string]([xml](Get-Content -LiteralPath $props -Raw)).Project.PropertyGroup.Version
    $Tag = 'v' + $version
    # Preserve the initial v0.1 naming when the configured patch is zero.
    if ($version -cmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.0$') {
        $Tag = 'v' + $version.Substring(0, $version.Length - 2)
    }
}

$pattern = '^v(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)(?:\.(?<patch>0|[1-9][0-9]*))?(?:-(?<preview>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z'
$match = [regex]::Match($Tag, $pattern)
if (-not $match.Success) {
    throw "Invalid version tag '$Tag'. Use v0.2, v0.2.1, or v1.0.0-rc.1."
}
$major = $match.Groups['major'].Value
$minor = $match.Groups['minor'].Value
$patch = if ($match.Groups['patch'].Success) { $match.Groups['patch'].Value } else { '0' }
foreach ($part in @($major, $minor, $patch)) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65534) {
        throw 'Version components must be in the range 0..65534 for Windows assembly metadata.'
    }
}
$preview = $match.Groups['preview'].Value
foreach ($identifier in ($preview -split '\.')) {
    if ($identifier -match '^0[0-9]+$') { throw 'Numeric prerelease identifiers must not have leading zeroes.' }
}
$version = "$major.$minor.$patch"
if ($preview) { $version += '-' + $preview }
[pscustomobject]@{ Tag = $Tag; Version = $version; IsPrerelease = [bool]$preview }
