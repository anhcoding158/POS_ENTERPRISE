[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdditionalArgument
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\POS.Wpf\POS.Wpf.csproj'

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
    'Release'
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

# Remove only the two runtime-control variables in the child. The parent
# PowerShell environment is never changed.
$childEnvironment = Get-ChildEnvironment $processStartInfo
[void]$childEnvironment.Remove('Infrastructure__DatabasePath')
[void]$childEnvironment.Remove('POS_RUNTIME_MODE')

$process = [System.Diagnostics.Process]::Start($processStartInfo)
try {
    $process.WaitForExit()
    $exitCode = $process.ExitCode
}
finally {
    $process.Dispose()
}

exit $exitCode
