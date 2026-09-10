param(
    [string]$TargetDirectory,
    [string]$PackagePath
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

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-RequiresAdministrator([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $protectedDirectories = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ } |
        ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd('\') }

    return @($protectedDirectories | Where-Object {
        $fullPath -eq $_ -or $fullPath.StartsWith("$_\", [StringComparison]::OrdinalIgnoreCase)
    }).Count -gt 0
}

function Invoke-ElevatedDeployment([string]$Target, [string]$Package) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -TargetDirectory `"$Target`" -PackagePath `"$Package`""
    Write-Host 'Administrator permission is required for the deployment directory. Waiting for UAC approval...'
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Fail "Elevated deployment failed with exit code $($process.ExitCode)."
    }
}

function Confirm-Deployment([string]$Target, [string[]]$SourceFiles) {
    foreach ($sourceFile in $SourceFiles) {
        $destinationFile = Join-Path $Target (Split-Path -Leaf $sourceFile)
        if (-not (Test-Path -LiteralPath $destinationFile -PathType Leaf)) {
            Fail "Deployment file is missing: $destinationFile"
        }

        $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            Fail "Deployment file does not match build output: $destinationFile"
        }

        Write-Host "Verified $(Split-Path -Leaf $sourceFile): $sourceHash"
    }
}

if (-not $TargetDirectory) {
    $TargetDirectory = Get-EnvValue (Join-Path $RootDir '.env') 'LOCAL_DEPLOY_DIR'
}

if ([string]::IsNullOrWhiteSpace($TargetDirectory)) {
    Fail 'Set LOCAL_DEPLOY_DIR in .env or pass -TargetDirectory.'
}

if (-not $PackagePath) {
    $vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        Fail 'Visual Studio Installer vswhere.exe was not found.'
    }

    $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $vsPath) {
        Fail 'MSBuild was not found. Install Visual Studio with .NET desktop development.'
    }

    $msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $RootDir 'WindowsVirtualDesktopHelper.sln') /t:Rebuild /p:Configuration=Release /p:Platform='Any CPU' /p:PostBuildEvent= /m
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
    $PackagePath = Join-Path $distDirectory $zipName
    Compress-Archive -LiteralPath $filesToPackage -DestinationPath $PackagePath
    Write-Host "Packaged $PackagePath"
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    Fail "Package is missing: $PackagePath"
}

$filesToDeploy = @(
    (Join-Path $RootDir 'Source\bin\Release\WindowsVirtualDesktopHelper.exe'),
    (Join-Path $RootDir 'Source\bin\Release\WindowsVirtualDesktopHelper.exe.config')
)

foreach ($file in $filesToDeploy) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        Fail "Deployment file is missing: $file"
    }
}

$resolvedTargetDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TargetDirectory)

if ((Test-RequiresAdministrator $resolvedTargetDirectory) -and -not (Test-IsAdministrator)) {
    Invoke-ElevatedDeployment $TargetDirectory $PackagePath
    Confirm-Deployment $resolvedTargetDirectory $filesToDeploy
    Write-Host "Deployed files to $resolvedTargetDirectory"
    exit
}

try {
    Get-Process -Name 'WindowsVirtualDesktopHelper' -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Force -Path $resolvedTargetDirectory | Out-Null

    foreach ($file in $filesToDeploy) {
        $destinationPath = Join-Path $resolvedTargetDirectory (Split-Path -Leaf $file)
        Remove-Item -LiteralPath $destinationPath -Force -ErrorAction SilentlyContinue
        Copy-Item -LiteralPath $file -Destination $destinationPath -Force
    }
} catch {
    if (-not (Test-IsAdministrator) -and $_.Exception.Message -match '访问被拒绝|Access is denied') {
        Invoke-ElevatedDeployment $TargetDirectory $PackagePath
    } else {
        throw
    }
}

Confirm-Deployment $resolvedTargetDirectory $filesToDeploy
Write-Host "Deployed files to $resolvedTargetDirectory"
