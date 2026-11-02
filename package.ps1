param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$repository = $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $repository 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
# GitHub uses the requested v0.1 tag; assembly versions use 0.1.0.
$tag = 'v' + (($version -split '\.')[0..1] -join '.')
$packageName = 'CyberSecretary-' + $tag + '-win-x64'
$staging = Join-Path $repository ('artifacts\package\' + $packageName)
$destination = Join-Path $repository ('release\' + $tag)

& (Join-Path $repository 'build.ps1') -SkipTests:$SkipTests -OutputDirectory $staging
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$exe = Join-Path $destination ($packageName + '.exe')
$archive = Join-Path $destination ($packageName + '.zip')
Copy-Item -LiteralPath (Join-Path $staging '计划栅格.exe') -Destination $exe -Force
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal -Force
$checksums = @($exe, $archive) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
}
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
Write-Output ('发布附件：' + $destination)
