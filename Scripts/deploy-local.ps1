param(
    [string]$TargetDirectory
)

$ErrorActionPreference = 'Stop'
$RootDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Fail([string]$Message) {
    throw "[deploy-local] $Message"
}

function Get-EnvValue([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*' + [regex]::Escape($Name) + '\s*=\s*(.*)\s*$') {
            return $Matches[1].Trim().Trim('"').Trim("'")
        }
    }

    return $null
}

if (-not $TargetDirectory) {
    $TargetDirectory = Get-EnvValue (Join-Path $RootDir '.env') 'LOCAL_DEPLOY_DIR'
}

if ([string]::IsNullOrWhiteSpace($TargetDirectory)) {
    Fail 'Set LOCAL_DEPLOY_DIR in .env or pass -TargetDirectory.'
}

$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    Fail 'Visual Studio Installer vswhere.exe was not found.'
}

$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsPath) {
    Fail 'MSBuild was not found. Install Visual Studio with .NET desktop development.'
}

$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $RootDir 'WindowsVirtualDesktopHelper.sln') /p:Configuration=Release /p:Platform='Any CPU' /p:PostBuildEvent= /m
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$outputDirectory = Join-Path $RootDir 'Source\bin\Release'
$filesToPackage = @(
    (Join-Path $outputDirectory 'WindowsVirtualDesktopHelper.exe'),
    (Join-Path $outputDirectory 'WindowsVirtualDesktopHelper.exe.config')
)

foreach ($file in $filesToPackage) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        Fail "Build output is missing: $file"
    }
}

$distDirectory = Join-Path $RootDir 'dist'
New-Item -ItemType Directory -Force -Path $distDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$zipName = "WindowsVirtualDesktopHelper.Release.Unsigned.$stamp.zip"
$zipPath = Join-Path $distDirectory $zipName
Compress-Archive -LiteralPath $filesToPackage -DestinationPath $zipPath

$resolvedTargetDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TargetDirectory)
New-Item -ItemType Directory -Force -Path $resolvedTargetDirectory | Out-Null
Copy-Item -LiteralPath $zipPath -Destination (Join-Path $resolvedTargetDirectory $zipName) -Force

Write-Host "Packaged $zipPath"
Write-Host "Deployed $(Join-Path $resolvedTargetDirectory $zipName)"
