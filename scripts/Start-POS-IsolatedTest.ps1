[CmdletBinding()]
param(
    [string] $SourceDatabasePath,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdditionalArgument
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\POS.Wpf\POS.Wpf.csproj'

if ([string]::IsNullOrWhiteSpace($SourceDatabasePath)) {
    $SourceDatabasePath = Join-Path $repositoryRoot 'data\pos-enterprise.db'
}

$source = Get-Item -LiteralPath ([IO.Path]::GetFullPath($SourceDatabasePath)) -ErrorAction Stop
if (-not $source.PSIsContainer -and
    (($source.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)) {
    # The source is validated by metadata only; its rows are never read.
}
else {
    throw 'The isolated-test source database must be a regular file.'
}

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'POS-Enterprise-IsolatedTest-' + $timestamp + '-' +
    ([Guid]::NewGuid().ToString('N')))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

$testDatabasePath = Join-Path $testRoot 'pos-enterprise-isolated.db'
[IO.File]::Copy($source.FullName, $testDatabasePath, $false)

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
            return $environment
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
        return $environment
    }

    $environment = $ProcessStartInfo.EnvironmentVariables
    if ($null -eq $environment) {
        throw 'The child process environment could not be initialized.'
    }
    return $environment
}

# These values exist only in the child process. The parent PowerShell
# environment is never assigned, restored or otherwise mutated.
$childEnvironment = Get-ChildEnvironment $processStartInfo
$childEnvironment['POS_RUNTIME_MODE'] = 'IsolatedTest'
$childEnvironment['Infrastructure__DatabasePath'] = $testDatabasePath

$process = [System.Diagnostics.Process]::Start($processStartInfo)
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
