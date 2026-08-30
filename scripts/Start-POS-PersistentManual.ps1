[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdditionalArgument
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executablePath = Join-Path $repositoryRoot (
    'src\POS.Wpf\bin\Release\net10.0-windows\POS.Enterprise.exe')

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "POS Enterprise Release output was not found: $executablePath"
}

$localApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
    throw 'The current user LocalApplicationData path could not be resolved.'
}

$profileRoot = Join-Path $localApplicationData 'POS Enterprise\ManualAcceptance'
$databasePath = Join-Path $profileRoot 'pos-enterprise-manual.db'

function Test-ExistingPathChainHasReparsePoint {
    param([Parameter(Mandatory = $true)][string] $Path)

    $current = New-Object IO.DirectoryInfo ([IO.Path]::GetFullPath($Path))
    while ($null -ne $current) {
        if ($current.Exists -and
            (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            return $true
        }
        $current = $current.Parent
    }
    return $false
}

if (Test-Path -LiteralPath $profileRoot) {
    $profileItem = Get-Item -LiteralPath $profileRoot -Force
    if (-not $profileItem.PSIsContainer -or
        (($profileItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Persistent manual profile is not a safe directory: $profileRoot"
    }
}
else {
    New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
}

if (Test-ExistingPathChainHasReparsePoint $profileRoot) {
    throw "Persistent manual profile contains a reparse point: $profileRoot"
}

if (Test-Path -LiteralPath $databasePath) {
    $databaseItem = Get-Item -LiteralPath $databasePath -Force
    if ($databaseItem.PSIsContainer -or
        (($databaseItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Persistent manual database is not a safe regular file: $databasePath"
    }
}

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][string] $Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

$arguments = @()
if ($null -ne $AdditionalArgument) {
    foreach ($argument in $AdditionalArgument) {
        $arguments += ConvertTo-ProcessArgument $argument
    }
}

$processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$processStartInfo.FileName = $executablePath
$processStartInfo.Arguments = $arguments -join ' '
$processStartInfo.WorkingDirectory = $repositoryRoot
$processStartInfo.UseShellExecute = $false

function Get-ChildEnvironment {
    param([Parameter(Mandatory = $true)]
        [System.Diagnostics.ProcessStartInfo] $ProcessStartInfo)

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

    return (, $ProcessStartInfo.EnvironmentVariables)
}

$childEnvironment = Get-ChildEnvironment $processStartInfo
$childEnvironment['POS_RUNTIME_MODE'] = 'IsolatedTest'
$childEnvironment['Infrastructure__DatabasePath'] = [IO.Path]::GetFullPath($databasePath)

Write-Host "Persistent manual database: $([IO.Path]::GetFullPath($databasePath))"
Write-Host 'The application will create or migrate this file once and reuse it on later runs.'
Write-Host 'Single-instance protection remains enforced by the application for this database.'

$process = [System.Diagnostics.Process]::Start($processStartInfo)
try {
    $process.WaitForExit()
    $exitCode = $process.ExitCode
}
finally {
    $process.Dispose()
}

exit $exitCode
