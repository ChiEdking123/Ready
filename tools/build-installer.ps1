<#
.SYNOPSIS
    构建 Ready! 的 Windows 安装包（.exe）。

.DESCRIPTION
    一条命令走完：dotnet publish（自包含）→ 准备语言包 → ISCC 编译。
    产物落在 artifacts\installer\Ready-<版本>-win-x64-setup.exe。

    为什么自包含：目标机不用预装 .NET 10，双击就能用。
    代价是安装包约 40-50MB（lzma2 压过之后）。

.PARAMETER Configuration
    构建配置，默认 Release。

.PARAMETER Runtime
    目标运行时，默认 win-x64。目前只支持 x64。

.PARAMETER SkipPublish
    跳过 dotnet publish，直接拿现有的 artifacts\installer\publish 编译。
    只改安装脚本时用这个，能省一两分钟。

.EXAMPLE
    pwsh -File tools\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$installerDir = Join-Path $repoRoot 'artifacts\installer'
$publishDir = Join-Path $installerDir 'publish'
$langDir = Join-Path $installerDir 'lang'
$issFile = Join-Path $PSScriptRoot 'Ready.iss'

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# 版本号只认 Directory.Build.props，避免安装脚本和程序集版本各说各话。
function Get-AppVersion {
    $props = Join-Path $repoRoot 'Directory.Build.props'
    [xml]$xml = Get-Content -LiteralPath $props -Raw
    # 文件里有多个 PropertyGroup，逐个找，别假设只有一个。
    $version = $xml.Project.PropertyGroup |
        ForEach-Object { if ($_.Version) { $_.Version } } |
        Select-Object -First 1
    if (-not $version) { throw 'Directory.Build.props 里没有 Version。' }
    return ([string]$version).Trim()
}

function Find-Iscc {
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    if ($programFilesX86) {
        $candidates += (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe')
    }

    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) { return $path }
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw @'
没找到 Inno Setup 6 的编译器 ISCC.exe。

安装方式（任选其一）：
  winget install --id JRSoftware.InnoSetup -e
  https://jrsoftware.org/isdl.php
'@
}

# 简体中文语言包不在 Inno 安装包里，也不该进本仓库（翻译文本另有许可），
# 所以按需下载并缓存到 artifacts\installer\lang。
function Get-ChineseLanguageFile {
    $target = Join-Path $langDir 'ChineseSimplified.isl'
    if (Test-Path -LiteralPath $target) {
        $cached = Get-Content -LiteralPath $target -Raw
        if ($cached -match 'LanguageName=' -and $cached -notmatch '<!DOCTYPE') { return $target }
        Remove-Item -LiteralPath $target -Force
    }

    New-Item -ItemType Directory -Force -Path $langDir | Out-Null

    $sources = @(
        'https://raw.githubusercontent.com/jrsoftware/issrc/refs/heads/main/Files/Languages/ChineseSimplified.isl',
        'https://gh-proxy.com/https://raw.githubusercontent.com/jrsoftware/issrc/refs/heads/main/Files/Languages/ChineseSimplified.isl',
        'https://github.moeyy.xyz/https://raw.githubusercontent.com/jrsoftware/issrc/refs/heads/main/Files/Languages/ChineseSimplified.isl'
    )

    foreach ($url in $sources) {
        try {
            curl.exe -sS -L --max-time 25 -o $target $url 2>$null | Out-Null
            if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Length -gt 4000)) {
                $text = Get-Content -LiteralPath $target -Raw
                if ($text -match 'LanguageName=' -and $text -notmatch '<!DOCTYPE') {
                    return $target
                }
            }
        } catch {
            Write-Verbose "语言包源失败：$url"
        }
    }

    throw "简体中文语言包下载失败。手动放到 $target 后重试（来源：https://jrsoftware.org/files/istrans/）。"
}

$version = Get-AppVersion
$iscc = Find-Iscc

Write-Step "Ready! $version"

if (-not $SkipPublish) {
    Write-Step 'dotnet publish（自包含 win-x64）'
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    $project = Join-Path $repoRoot 'src\Ready.WinForms\Ready.WinForms.csproj'
    & dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishReadyToRun=false `
        -p:DebugType=none `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（退出码 $LASTEXITCODE）。" }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'Ready.exe'))) {
    throw "$publishDir 里没有 Ready.exe，先跑一次不带 -SkipPublish 的构建。"
}

$langFile = Get-ChineseLanguageFile
$iconFile = Join-Path $repoRoot 'src\Ready.WinForms\Assets\appicon.ico'
$licenseFile = Join-Path $repoRoot 'LICENSE'

Write-Step 'ISCC 编译安装包'
& $iscc `
    "/DAppVersion=$version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    "/DIconFile=$iconFile" `
    "/DLicenseFile=$licenseFile" `
    "/DLangDir=$langDir" `
    $issFile

if ($LASTEXITCODE -ne 0) { throw "ISCC 编译失败（退出码 $LASTEXITCODE）。" }

$setup = Get-ChildItem -LiteralPath $installerDir -Filter 'Ready-*-setup.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $setup) { throw '编译成功但没找到安装包。' }

$sizeMb = [math]::Round($setup.Length / 1MB, 1)
Write-Host ''
Write-Host "安装包：$($setup.FullName)" -ForegroundColor Green
Write-Host "大小：$sizeMb MB"
