[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $SourceDatabasePath,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdditionalArgument
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\POS.Wpf\POS.Wpf.csproj'

$source = Get-Item -LiteralPath ([IO.Path]::GetFullPath($SourceDatabasePath)) -ErrorAction Stop
if (-not $source.PSIsContainer -and
    (($source.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)) {
    # The source is validated by metadata only; its rows are never read.
}
else {
    throw 'The isolated-test source database must be a regular file.'
}

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'POS.Wpf project was not found.'
}

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][string] $Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Resolve-WindowsAbsolutePath {
    param([Parameter(Mandatory = $true)][string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'The isolated-test database path must not be empty.'
    }

    $isDriveQualified = $Value -match '^[A-Za-z]:[\\/]'
    $isUnc = $Value -match '^[\\/]{2}[^\\/]+[\\/]+[^\\/]+'

    if (-not $isDriveQualified -and -not $isUnc) {
        throw 'The isolated-test database path must be an absolute Windows path.'
    }

    $fullPath = [IO.Path]::GetFullPath($Value)
    $pathRoot = [IO.Path]::GetPathRoot($fullPath)

    if ([string]::IsNullOrWhiteSpace($pathRoot)) {
        throw 'The isolated-test database path must have an absolute Windows root.'
    }

    return $fullPath
}

function Test-PathWithinBoundary {
    param(
        [Parameter(Mandatory = $true)][string] $Boundary,
        [Parameter(Mandatory = $true)][string] $Candidate
    )

    $normalizeBoundaryPath = {
        param([Parameter(Mandatory = $true)][string] $Path)

        $fullPath = Resolve-WindowsAbsolutePath $Path
        $root = [IO.Path]::GetPathRoot($fullPath)
        if ([string]::IsNullOrWhiteSpace($root)) {
            throw 'The boundary path must have an absolute Windows root.'
        }

        # Windows PowerShell 5.1 runs on a framework that does not expose
        # TrimEndingDirectorySeparator or GetRelativePath. Normalize both
        # separator forms, but preserve drive and UNC roots exactly.
        $normalized = $fullPath.Replace(
            [IO.Path]::AltDirectorySeparatorChar,
            [IO.Path]::DirectorySeparatorChar)
        while ($normalized.Length -gt $root.Length -and
            ($normalized.EndsWith([IO.Path]::DirectorySeparatorChar.ToString()) -or
             $normalized.EndsWith([IO.Path]::AltDirectorySeparatorChar.ToString()))) {
            $normalized = $normalized.Substring(0, $normalized.Length - 1)
        }
        return $normalized
    }

    $normalizedBoundary = & $normalizeBoundaryPath $Boundary
    $normalizedCandidate = & $normalizeBoundaryPath $Candidate
    if ([string]::Equals($normalizedBoundary, $normalizedCandidate,
            [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    # The separator-qualified boundary prevents sibling-prefix confusion
    # (for example, boundary-evil is not inside boundary).
    $childPrefix = $normalizedBoundary
    if (-not $childPrefix.EndsWith([IO.Path]::DirectorySeparatorChar.ToString()) -and
        -not $childPrefix.EndsWith([IO.Path]::AltDirectorySeparatorChar.ToString())) {
        $childPrefix += [IO.Path]::DirectorySeparatorChar
    }
    return $normalizedCandidate.StartsWith($childPrefix,
        [StringComparison]::OrdinalIgnoreCase)
}

function Test-ExistingPathChainHasReparsePoint {
    param([Parameter(Mandatory = $true)][string] $Path)

    $current = New-Object IO.DirectoryInfo (Resolve-WindowsAbsolutePath $Path)
    while ($null -ne $current) {
        if ($current.Exists -and
            (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            return $true
        }
        $current = $current.Parent
    }
    return $false
}

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'POS-Enterprise-IsolatedTest-' + $timestamp + '-' +
    ([Guid]::NewGuid().ToString('N')))
$testDatabasePath = Join-Path $testRoot 'pos-enterprise-isolated.db'

$canonicalRepositoryRoot = Resolve-WindowsAbsolutePath $repositoryRoot
$canonicalTestRoot = Resolve-WindowsAbsolutePath $testRoot
$canonicalTestDatabasePath = Resolve-WindowsAbsolutePath $testDatabasePath
$expectedAutomaticBackupRoot = Resolve-WindowsAbsolutePath (
    Join-Path $canonicalTestRoot 'automatic-backups')
$expectedAutomaticBackupStatePath = Resolve-WindowsAbsolutePath (
    Join-Path $expectedAutomaticBackupRoot 'automatic-backup-state.json')
$localApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)
$canonicalProductionAutomaticRoot = Resolve-WindowsAbsolutePath (
    Join-Path (Join-Path $localApplicationData 'POS Enterprise') 'automatic-backups')

if (Test-PathWithinBoundary $canonicalRepositoryRoot $canonicalTestRoot) {
    throw 'The isolated-test root must not be inside the repository.'
}
if (-not (Test-PathWithinBoundary $canonicalTestRoot $canonicalTestDatabasePath) -or
    -not (Test-PathWithinBoundary $canonicalTestRoot $expectedAutomaticBackupRoot) -or
    ([IO.Path]::GetDirectoryName($expectedAutomaticBackupRoot) -cne $canonicalTestRoot)) {
    throw 'The isolated automatic backup root failed its owned-boundary verification.'
}
if ($expectedAutomaticBackupRoot -ieq $canonicalProductionAutomaticRoot) {
    throw 'The isolated automatic backup root must not equal the production root.'
}
if (Test-ExistingPathChainHasReparsePoint $canonicalTestRoot) {
    throw 'The isolated-test boundary must not contain a reparse point.'
}

New-Item -ItemType Directory -Path $canonicalTestRoot | Out-Null
if (Test-ExistingPathChainHasReparsePoint $canonicalTestRoot) {
    throw 'The created isolated-test boundary must not be a reparse point.'
}
[IO.File]::Copy($source.FullName, $canonicalTestDatabasePath, $false)

$arguments = @(
    'run'
    '--project'
    (ConvertTo-ProcessArgument $projectPath)
    '--configuration'
    $Configuration
    '--no-restore'
    '--no-launch-profile'
)

if ($null -ne $AdditionalArgument) {
    foreach ($argument in $AdditionalArgument) {
        $arguments += ConvertTo-ProcessArgument $argument
    }
}

$processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$processStartInfo.FileName = 'dotnet'
$processStartInfo.Arguments = $arguments -join ' '
$processStartInfo.WorkingDirectory = $repositoryRoot
$processStartInfo.UseShellExecute = $false

function Get-ChildEnvironment {
    param([Parameter(Mandatory = $true)]
        [System.Diagnostics.ProcessStartInfo] $ProcessStartInfo)

    # Initialize the legacy collection first. Windows PowerShell 5.1 and
    # current .NET use this collection when creating the child environment.
    $null = $ProcessStartInfo.EnvironmentVariables
    $legacyField = $ProcessStartInfo.GetType().GetField(
        'environmentVariables',
        [Reflection.BindingFlags]'Instance,NonPublic')
    if ($null -ne $legacyField) {
        $environment = $legacyField.GetValue($ProcessStartInfo)
        if ($null -ne $environment) {
            foreach ($entry in [Environment]::GetEnvironmentVariables('Process').GetEnumerator()) {
                if (-not $environment.ContainsKey([string]$entry.Key)) {
                    $environment.Add([string]$entry.Key, [string]$entry.Value)
                }
            }
            return (, $environment)
        }
    }

    # PowerShell 7 can expose both PATH and Path. Build a case-insensitive
    # child dictionary once so ProcessStartInfo does not reject duplicates.
    $field = $ProcessStartInfo.GetType().GetField(
        'environment',
        [Reflection.BindingFlags]'Instance,NonPublic')
    if ($null -ne $field) {
        $environment = New-Object 'System.Collections.Generic.Dictionary[string,string]' (
            [StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in [Environment]::GetEnvironmentVariables('Process').GetEnumerator()) {
            if (-not $environment.ContainsKey([string]$entry.Key)) {
                $environment.Add([string]$entry.Key, [string]$entry.Value)
            }
        }
        $field.SetValue($ProcessStartInfo, $environment)
        return (, $environment)
    }

    $environment = $ProcessStartInfo.EnvironmentVariables
    if ($null -eq $environment) {
        throw 'The child process environment could not be initialized.'
    }
    return (, $environment)
}

# These values exist only in the child process. The parent PowerShell
# environment is never assigned, restored or otherwise mutated.
$childEnvironment = Get-ChildEnvironment $processStartInfo

if ($null -eq $childEnvironment -or
    $childEnvironment -is [Array]) {
    throw 'The child process environment must be a single non-array collection.'
}

$childEnvironment['POS_RUNTIME_MODE'] = 'IsolatedTest'
$childEnvironment['Infrastructure__DatabasePath'] = $canonicalTestDatabasePath

if ($childEnvironment['POS_RUNTIME_MODE'] -cne 'IsolatedTest' -or
    $childEnvironment['Infrastructure__DatabasePath'] -cne $canonicalTestDatabasePath) {
    throw 'The child process environment failed its isolated-test verification.'
}

if ((Resolve-WindowsAbsolutePath (
        [string]$childEnvironment['Infrastructure__DatabasePath'])) -cne
        $canonicalTestDatabasePath) {
    throw 'The child process database path failed its isolated-test verification.'
}

$process = [System.Diagnostics.Process]::Start($processStartInfo)
Write-Host "Isolated test root: $canonicalTestRoot"
Write-Host "Isolated database path: $canonicalTestDatabasePath"
Write-Host "Expected automatic backup root: $expectedAutomaticBackupRoot"
Write-Host "Expected automatic backup state path: $expectedAutomaticBackupStatePath"
Write-Host "Child process ID: $($process.Id)"
try {
    $process.WaitForExit()
    $exitCode = $process.ExitCode
}
finally {
    $process.Dispose()
}

# The isolated copy is intentionally retained for inspection/recovery.
Write-Host 'Isolated test database snapshot retained after child exit.'
exit $exitCode
