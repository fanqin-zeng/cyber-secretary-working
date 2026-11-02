param(
    [switch]$SkipTests,
    [string]$OutputDirectory = 'release',
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$repository = $PSScriptRoot
$localSdk = Join-Path $env:LOCALAPPDATA 'CyberSecretary\dotnet-sdk\dotnet.exe'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } elseif ($dotnetCommand) { $dotnetCommand.Source } else { throw '需要 .NET 10 SDK 才能从源码构建。成品 EXE 不需要 SDK 或运行时。' }
$versionOptions = @()
if ($Version) {
    $resolved = & (Join-Path $repository 'scripts\resolve-release-version.ps1') -Tag ('v' + $Version)
    $versionOptions = @('-p:Version=' + $resolved.Version)
}

Push-Location $repository
try {
    if (-not $SkipTests) {
        & $sdk run --project 'tests\CyberSecretary.Tests\CyberSecretary.Tests.csproj' -c Release @versionOptions
        if ($LASTEXITCODE -ne 0) { throw '规则与存储检查失败。' }
        & $sdk run --project 'tests\CyberSecretary.UiTests\CyberSecretary.UiTests.csproj' -c Release @versionOptions
        if ($LASTEXITCODE -ne 0) { throw '界面集成检查失败。' }
    }
    & $sdk publish 'src\CyberSecretary\CyberSecretary.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts\publish' @versionOptions
    if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    Copy-Item -LiteralPath 'artifacts\publish\CyberSecretary.exe' -Destination (Join-Path $OutputDirectory 'CyberSecretray-Working.exe') -Force
    Copy-Item -LiteralPath '使用说明.txt' -Destination (Join-Path $OutputDirectory '使用说明.txt') -Force
    Copy-Item -LiteralPath 'README.md','LICENSE','THIRD_PARTY_NOTICES.md' -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath 'src\CyberSecretary.Core\Calendars\LICENSE' -Destination (Join-Path $OutputDirectory '日历数据许可.txt') -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $OutputDirectory 'notices') | Out-Null
    Copy-Item -LiteralPath 'notices\dotnet-runtime-LICENSE.txt','notices\dotnet-runtime-THIRD-PARTY-NOTICES.txt','notices\wpf-LICENSE.txt','notices\holiday-cn-LICENSE.txt' -Destination (Join-Path $OutputDirectory 'notices') -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $OutputDirectory 'docs') | Out-Null
    Copy-Item -LiteralPath 'docs\screenshot.png' -Destination (Join-Path $OutputDirectory 'docs\screenshot.png') -Force
    $output = Get-Item -LiteralPath (Join-Path $OutputDirectory 'CyberSecretray-Working.exe')
    Write-Output ('已生成：' + $output.FullName + '（' + [math]::Round($output.Length / 1MB, 1) + ' MB）')
}
finally { Pop-Location }
