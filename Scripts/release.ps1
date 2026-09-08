param(
    [Parameter(Position = 0)]
    [string]$Version,
    [string]$Branch,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$RootDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$AssemblyInfoPath = Join-Path $RootDir 'Source\AssemblyInfo.cs'

function Fail([string]$Message) {
    Write-Error "[release] $Message"
    exit 1
}

function Invoke-Git([string[]]$Arguments) {
    & git -C $RootDir @Arguments
    if ($LASTEXITCODE -ne 0) {
        Fail "git $($Arguments -join ' ') failed."
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail 'Missing required command: git'
}

$assemblyInfo = Get-Content -LiteralPath $AssemblyInfoPath -Raw
if ($assemblyInfo -notmatch 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.0"\)') {
    Fail 'Could not read the current version from Source/AssemblyInfo.cs.'
}

$currentVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
$suggestedVersion = "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)"

if (-not $Version) {
    $inputVersion = Read-Host "Release version [default: $suggestedVersion]"
    $Version = if ($inputVersion.Trim()) { $inputVersion.Trim() } else { $suggestedVersion }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Fail 'Version must look like 2.1.1.'
}

if (-not $Branch) {
    $inputBranch = Read-Host 'Release branch [default: main]'
    $Branch = if ($inputBranch.Trim()) { $inputBranch.Trim() } else { 'main' }
}

$remotes = @(git -C $RootDir remote)
if ($remotes.Count -eq 0) {
    Fail 'No git remotes configured.'
}

$dirty = git -C $RootDir status --porcelain
if ($dirty) {
    Write-Host '[release] Working tree has changes. They will be included in the release commit:'
    Invoke-Git @('status', '--short')
}

$tag = "v$Version"
if (git -C $RootDir tag --list $tag) {
    Fail "Git tag $tag already exists locally."
}

foreach ($remote in $remotes) {
    if (git -C $RootDir ls-remote --tags $remote "refs/tags/$tag") {
        Fail "Git tag $tag already exists on remote '$remote'."
    }
}

Write-Host "[release] Preparing Windows Virtual Desktop Helper $tag"
Write-Host "[release] Current version: $currentVersion"
Write-Host "[release] Branch: $Branch"
Write-Host "[release] Remotes: $($remotes -join ', ')"

$updatedAssemblyInfo = $assemblyInfo `
    -replace 'AssemblyVersion\("[0-9]+\.[0-9]+\.[0-9]+\.0"\)', "AssemblyVersion(`"$Version.0`")" `
    -replace 'AssemblyFileVersion\("[0-9]+\.[0-9]+\.[0-9]+\.0"\)', "AssemblyFileVersion(`"$Version.0`")"

if ($updatedAssemblyInfo -eq $assemblyInfo) {
    Fail 'Failed to update the assembly version.'
}

[System.IO.File]::WriteAllText($AssemblyInfoPath, $updatedAssemblyInfo, [System.Text.UTF8Encoding]::new($false))

Invoke-Git @('add', '-A')
if (-not (git -C $RootDir diff --cached --name-only)) {
    Fail 'No staged changes found for the release commit.'
}

Invoke-Git @('commit', '-m', "release: $tag")
Invoke-Git @('tag', '-a', $tag, '-m', "Release $tag")
Write-Host "[release] Created commit and tag $tag"

if ($Push) {
    foreach ($remote in $remotes) {
        Write-Host "[release] Pushing to $remote"
        Invoke-Git @('push', $remote, $Branch)
        Invoke-Git @('push', $remote, $tag)
    }
} else {
    Write-Host '[release] Push skipped. Next commands:'
    foreach ($remote in $remotes) {
        Write-Host "  git push $remote $Branch"
        Write-Host "  git push $remote $tag"
    }
}
