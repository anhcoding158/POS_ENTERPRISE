param(
    [switch]$SkipEfCheck
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory =
    Split-Path `
        -Parent `
        $MyInvocation.MyCommand.Path

$repositoryRoot =
    (
        Resolve-Path `
            (Join-Path $scriptDirectory "..")
    ).Path

$solutionPath =
    Join-Path `
        $repositoryRoot `
        "POS.Enterprise.slnx"

$configuration =
    "Release"

$infrastructureProjectPath =
    Join-Path `
        $repositoryRoot `
        "src\POS.Infrastructure\POS.Infrastructure.csproj"

$startupProjectPath =
    Join-Path `
        $repositoryRoot `
        "src\POS.Wpf\POS.Wpf.csproj"

Set-Location $repositoryRoot

function Invoke-DotNetStep
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host `
        "==================================================" `
        -ForegroundColor DarkGray

    Write-Host `
        $Title `
        -ForegroundColor Cyan

    Write-Host `
        "dotnet $($Arguments -join ' ')" `
        -ForegroundColor DarkGray

    & dotnet @Arguments

    $exitCode =
        $LASTEXITCODE

    if ($exitCode -ne 0)
    {
        throw `
            "$Title failed with exit code $exitCode."
    }
}

Write-Host ""
Write-Host `
    "POS Enterprise Quality Gate" `
    -ForegroundColor Green

Write-Host `
    "Repository: $repositoryRoot" `
    -ForegroundColor DarkGray

Write-Host `
    "Solution: $solutionPath" `
    -ForegroundColor DarkGray

Write-Host `
    "Configuration: $configuration" `
    -ForegroundColor DarkGray

Invoke-DotNetStep `
    -Title "1. Restore solution" `
    -Arguments @(
        "restore",
        $solutionPath,
        "--verbosity",
        "minimal",
        "-p:RestoreBuildInParallel=false"
    )

Invoke-DotNetStep `
    -Title "2. Build solution" `
    -Arguments @(
        "build",
        $solutionPath,
        "--configuration",
        $configuration,
        "--no-restore",
        "-m:1",
        "-nr:false",
        "-p:BuildInParallel=false"
    )

Invoke-DotNetStep `
    -Title "3. Run automated tests" `
    -Arguments @(
        "test",
        $solutionPath,
        "--configuration",
        $configuration,
        "--no-build",
        "--no-restore",
        "-m:1",
        "-nr:false",
        "-p:BuildInParallel=false"
    )

Write-Host ""
Write-Host `
    "==================================================" `
    -ForegroundColor DarkGray

Write-Host `
    "4. Scan vulnerable packages" `
    -ForegroundColor Cyan

$vulnerabilityLines =
    & dotnet `
        list `
        $solutionPath `
        package `
        --vulnerable `
        --include-transitive `
        2>&1

$vulnerabilityExitCode =
    $LASTEXITCODE

$vulnerabilityOutput =
    $vulnerabilityLines |
    Out-String

Write-Host $vulnerabilityOutput

if ($vulnerabilityExitCode -ne 0)
{
    throw `
        "Package vulnerability scan failed with " +
        "exit code $vulnerabilityExitCode."
}

if (
    $vulnerabilityOutput -match
    "(?i)\b(GHSA-[A-Za-z0-9-]+|CVE-\d{4}-\d+)\b"
)
{
    throw `
        "A vulnerable package was detected. " +
        "The quality gate cannot pass."
}

if (-not $SkipEfCheck)
{
    Invoke-DotNetStep `
        -Title "5. Restore local tools" `
        -Arguments @(
            "tool",
            "restore"
        )

    Invoke-DotNetStep `
        -Title "6. Check pending EF model changes" `
        -Arguments @(
            "ef",
            "migrations",
            "has-pending-model-changes",
            "--configuration",
            $configuration,
            "--project",
            $infrastructureProjectPath,
            "--startup-project",
            $startupProjectPath
        )
}

Write-Host ""
Write-Host `
    "==================================================" `
    -ForegroundColor DarkGray

Write-Host `
    "7. Check Git whitespace" `
    -ForegroundColor Cyan

& git diff --check

$gitDiffExitCode =
    $LASTEXITCODE

if ($gitDiffExitCode -ne 0)
{
    throw `
        "git diff --check detected whitespace errors."
}

Write-Host ""
Write-Host `
    "8. Git status" `
    -ForegroundColor Cyan

& git status --short

$gitStatusExitCode =
    $LASTEXITCODE

if ($gitStatusExitCode -ne 0)
{
    throw `
        "Unable to read Git status."
}

Write-Host ""
Write-Host `
    "==================================================" `
    -ForegroundColor DarkGray

Write-Host `
    "QUALITY GATE PASSED" `
    -ForegroundColor Green

Write-Host `
    "Build, tests, dependency scan, EF model and Git checks passed." `
    -ForegroundColor Green
