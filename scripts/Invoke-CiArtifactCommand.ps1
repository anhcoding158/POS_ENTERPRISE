[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        'Restore',
        'BuildRelease',
        'TestRelease',
        'VulnerabilityReport',
        'QualityGate',
        'PublishWinX64',
        'FailureProbe')]
    [string]$Step,

    [string]$RepositoryRoot,

    [string]$ArtifactRoot,

    [string]$LogPathOverride
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot))
{
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
else
{
    $RepositoryRoot = (Resolve-Path $RepositoryRoot).Path
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot))
{
    $ArtifactRoot = [System.IO.Path]::Combine($RepositoryRoot, '_ci_artifacts')
}

$ArtifactRoot = [System.IO.Path]::GetFullPath($ArtifactRoot)
$expectedArtifactRoot = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($RepositoryRoot, '_ci_artifacts'))

if ($ArtifactRoot -ne $expectedArtifactRoot)
{
    throw "Artifact root must be exactly '$expectedArtifactRoot'."
}

$solutionPath = [System.IO.Path]::Combine($RepositoryRoot, 'POS.Enterprise.slnx')
$qualityGatePath = [System.IO.Path]::Combine($RepositoryRoot, 'scripts', 'Test-QualityGate.ps1')
$publishProjectPath = [System.IO.Path]::Combine($RepositoryRoot, 'src', 'POS.Wpf', 'POS.Wpf.csproj')
$publishOutputPath = [System.IO.Path]::Combine(
    $ArtifactRoot, 'publish', 'POS.Wpf', 'win-x64')

$logPath = $null
$outputPath = $null
$executablePath = $null
$arguments = @()

switch ($Step)
{
    'Restore'
    {
        $logPath = [System.IO.Path]::Combine($ArtifactRoot, 'logs', 'restore.log')
        $executablePath = 'dotnet.exe'
        $arguments = @(
            'restore',
            $solutionPath,
            '--verbosity',
            'minimal',
            '-p:RestoreBuildInParallel=false',
            '-p:NuGetAudit=false')
    }

    'BuildRelease'
    {
        $logPath = [System.IO.Path]::Combine($ArtifactRoot, 'logs', 'build-release.log')
        $executablePath = 'dotnet.exe'
        $arguments = @(
            'build',
            $solutionPath,
            '-c',
            'Release',
            '--no-restore',
            '-m:1',
            '-nr:false',
            '-p:BuildInParallel=false')
    }

    'TestRelease'
    {
        $executablePath = 'dotnet.exe'
        $arguments = @(
            'test',
            $solutionPath,
            '-c',
            'Release',
            '--no-build',
            '--no-restore',
            '-m:1',
            '-nr:false',
            '-p:BuildInParallel=false',
            '--logger',
            'trx;LogFileName=POS.Architecture.Tests.trx',
            '--results-directory',
            [System.IO.Path]::Combine($ArtifactRoot, 'test-results'))
    }

    'VulnerabilityReport'
    {
        $outputPath = [System.IO.Path]::Combine(
            $ArtifactRoot, 'reports', 'vulnerability', 'vulnerabilities.json')
        $executablePath = 'dotnet.exe'
        $arguments = @(
            'package',
            'list',
            '--project',
            $solutionPath,
            '--vulnerable',
            '--include-transitive',
            '--format',
            'json',
            '--output-version',
            '1',
            '--no-restore')
    }

    'QualityGate'
    {
        $logPath = [System.IO.Path]::Combine($ArtifactRoot, 'logs', 'quality-gate.log')
        $executablePath = 'powershell.exe'
        $arguments = @(
            '-NoProfile',
            '-ExecutionPolicy',
            'Bypass',
            '-File',
            $qualityGatePath)
    }

    'PublishWinX64'
    {
        $logPath = [System.IO.Path]::Combine($ArtifactRoot, 'logs', 'publish-win-x64.log')
        $executablePath = 'dotnet.exe'
        $arguments = @(
            'publish',
            $publishProjectPath,
            '-c',
            'Release',
            '-f',
            'net10.0-windows',
            '-r',
            'win-x64',
            '--self-contained',
            'false',
            '-p:PublishSingleFile=false',
            '-p:PublishReadyToRun=false',
            '-p:DebugSymbols=false',
            '-p:DebugType=None',
            '-o',
            $publishOutputPath)
    }

    'FailureProbe'
    {
        $executablePath = 'cmd.exe'
        $arguments = @('/c', 'exit', '23')
        if ([string]::IsNullOrWhiteSpace($LogPathOverride))
        {
            $logPath = [System.IO.Path]::Combine($ArtifactRoot, 'logs', 'failure-probe.log')
        }
        else
        {
            $logPath = [System.IO.Path]::GetFullPath($LogPathOverride)
        }
    }
}

function Write-Utf8NoBomFile
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content)

    $parent = Split-Path -Parent $LiteralPath
    if (-not (Test-Path -LiteralPath $parent))
    {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($LiteralPath, $Content, $utf8NoBom)
}

if ($null -ne $logPath)
{
    Write-Utf8NoBomFile -LiteralPath $logPath -Content (
        "Step=$Step`r`nCommand={0} {1}`r`n" -f $executablePath, ($arguments -join ' '))
}

if ($null -ne $outputPath)
{
    Write-Utf8NoBomFile -LiteralPath $outputPath -Content ''
}

$processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$processStartInfo.FileName = $executablePath
$processStartInfo.UseShellExecute = $false
$processStartInfo.CreateNoWindow = $true
$processStartInfo.RedirectStandardOutput = $true
$processStartInfo.RedirectStandardError = $true

if ($processStartInfo.PSObject.Properties.Name -contains 'ArgumentList')
{
    foreach ($argument in $arguments)
    {
        [void]$processStartInfo.ArgumentList.Add([string]$argument)
    }
}
else
{
    $quotedArguments = foreach ($argument in $arguments)
    {
        $argumentText = [string]$argument
        if ($argumentText -notmatch '[\s"]')
        {
            $argumentText
            continue
        }

        $escapedArgument = $argumentText `
            -replace '(\\*)"', '$1$1\"' `
            -replace '(\\+)$', '$1$1'
        '"' + $escapedArgument + '"'
    }

    $processStartInfo.Arguments = $quotedArguments -join ' '
}

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $processStartInfo
[void]$process.Start()

$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
$combinedOutput = $stdout
if (-not [string]::IsNullOrEmpty($stderr))
{
    $combinedOutput += $stderr
}

if ($null -ne $logPath)
{
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::AppendAllText($logPath, $combinedOutput, $utf8NoBom)
}

if (-not [string]::IsNullOrEmpty($combinedOutput))
{
    Write-Host $combinedOutput -NoNewline
}

if ($null -ne $outputPath)
{
    Write-Utf8NoBomFile -LiteralPath $outputPath -Content $stdout
}

$exitCode = $process.ExitCode
if ($exitCode -ne 0)
{
    exit $exitCode
}
