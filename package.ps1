param(
    [switch]$SkipTests,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$repository = $PSScriptRoot
$releaseVersion = & (Join-Path $repository 'scripts\resolve-release-version.ps1') -Tag $Tag
$tag = $releaseVersion.Tag
$packageName = 'CyberSecretray-Working-' + $tag + '-win-x64'
$staging = Join-Path $repository ('artifacts\package\' + $packageName)
$destination = Join-Path $repository ('release\' + $tag)

& (Join-Path $repository 'build.ps1') -SkipTests:$SkipTests -OutputDirectory $staging -Version $releaseVersion.Version
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$exe = Join-Path $destination ($packageName + '.exe')
$archive = Join-Path $destination ($packageName + '.zip')
Copy-Item -LiteralPath (Join-Path $staging 'CyberSecretray-Working.exe') -Destination $exe -Force
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal -Force
$checksums = @($exe, $archive) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
}
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
Write-Output ('发布附件：' + $destination)
