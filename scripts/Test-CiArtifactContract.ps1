[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Prepare', 'Validate')]
    [string]$Mode,

    [string]$RepositoryRoot
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

$artifactRoot = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($RepositoryRoot, '_ci_artifacts'))
$expectedArtifactRoot = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($RepositoryRoot, '_ci_artifacts'))

if ($artifactRoot -ne $expectedArtifactRoot)
{
    throw "Artifact root must be exactly '$expectedArtifactRoot'."
}

function Assert-NonEmptyFile
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath)

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf))
    {
        throw "Required artifact is missing: $LiteralPath"
    }

    $item = Get-Item -LiteralPath $LiteralPath
    if ($item.Length -le 0)
    {
        throw "Required artifact is empty: $LiteralPath"
    }
}

function Get-OptionalTrxCounter
{
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$Counters,

        [Parameter(Mandatory = $true)]
        [string]$Name)

    $attribute = $Counters.Attributes.GetNamedItem($Name)
    if ($null -eq $attribute)
    {
        return $null
    }

    $value = 0
    if (-not [int]::TryParse($attribute.Value, [ref]$value))
    {
        throw "TRX counter '$Name' is not an integer: $($attribute.Value)"
    }

    return $value
}

function Get-OptionalJsonPropertyValue
{
    param(
        [Parameter(Mandatory = $false)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name)

    if ($null -eq $Object)
    {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property)
    {
        return $null
    }

    return $property.Value
}

function Get-JsonItems
{
    param(
        [Parameter(Mandatory = $false)]
        [object]$Value)

    if ($null -eq $Value)
    {
        return @()
    }

    return @($Value)
}

function Assert-NoVulnerabilitiesInPackageCollection
{
    param(
        [Parameter(Mandatory = $false)]
        [object[]]$Packages,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$FrameworkName,

        [Parameter(Mandatory = $true)]
        [string]$CollectionName)

    foreach ($package in $Packages)
    {
        if ($null -eq $package)
        {
            continue
        }

        $vulnerabilities = Get-OptionalJsonPropertyValue -Object $package -Name 'vulnerabilities'
        $vulnerabilityItems = @(Get-JsonItems -Value $vulnerabilities)
        if ($vulnerabilityItems.Count -gt 0)
        {
            $packageId = Get-OptionalJsonPropertyValue -Object $package -Name 'id'
            if ([string]::IsNullOrWhiteSpace([string]$packageId))
            {
                $packageId = Get-OptionalJsonPropertyValue -Object $package -Name 'name'
            }

            throw (
                "Vulnerable package found: Project=$ProjectPath; Framework=$FrameworkName; " +
                "Collection=$CollectionName; Package=$packageId")
        }
    }
}

if ($Mode -eq 'Prepare')
{
    if (Test-Path -LiteralPath $artifactRoot)
    {
        if (-not (Get-Item -LiteralPath $artifactRoot).PSIsContainer)
        {
            throw "Artifact root exists but is not a directory: $artifactRoot"
        }

        Remove-Item -LiteralPath $artifactRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    foreach ($relativePath in @(
        'test-results',
        'logs',
        'reports/vulnerability',
        'publish/POS.Wpf/win-x64'))
    {
        New-Item -ItemType Directory `
            -Path ([System.IO.Path]::Combine($artifactRoot, $relativePath)) `
            -Force | Out-Null
    }

    Write-Host 'CI_ARTIFACT_PREPARATION=PASS'
    Write-Host "ArtifactRoot=$artifactRoot"
    exit 0
}

$requiredFiles = @(
    [System.IO.Path]::Combine($artifactRoot, 'test-results', 'POS.Architecture.Tests.trx'),
    [System.IO.Path]::Combine($artifactRoot, 'logs', 'restore.log'),
    [System.IO.Path]::Combine($artifactRoot, 'logs', 'build-release.log'),
    [System.IO.Path]::Combine($artifactRoot, 'logs', 'quality-gate.log'),
    [System.IO.Path]::Combine($artifactRoot, 'logs', 'publish-win-x64.log'),
    [System.IO.Path]::Combine($artifactRoot, 'reports', 'vulnerability', 'vulnerabilities.json'))

foreach ($requiredFile in $requiredFiles)
{
    Assert-NonEmptyFile -LiteralPath $requiredFile
}

$trxPath = $requiredFiles[0]
$trx = [xml](Get-Content -LiteralPath $trxPath -Raw)
$counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
if ($null -eq $counters)
{
    throw "TRX counters are missing: $trxPath"
}

$testTotal = Get-OptionalTrxCounter -Counters $counters -Name 'total'
if ($null -eq $testTotal)
{
    throw "TRX total counter is missing: $trxPath"
}

if ($testTotal -lt 975)
{
    throw "TRX test count $testTotal is below the required baseline of 975."
}

$testPassed = Get-OptionalTrxCounter -Counters $counters -Name 'passed'
if ($null -eq $testPassed)
{
    throw "TRX passed counter is missing: $trxPath"
}

if ($testPassed -ne $testTotal)
{
    throw "TRX semantic validation failed: passed=$testPassed, total=$testTotal."
}

$testExecuted = Get-OptionalTrxCounter -Counters $counters -Name 'executed'
if ($null -ne $testExecuted -and $testExecuted -ne $testTotal)
{
    throw "TRX semantic validation failed: executed=$testExecuted, total=$testTotal."
}

foreach ($zeroCounterName in @(
    'failed',
    'error',
    'timeout',
    'aborted',
    'inconclusive',
    'notExecuted',
    'skipped',
    'ignored'))
{
    $zeroCounter = Get-OptionalTrxCounter -Counters $counters -Name $zeroCounterName
    if ($null -ne $zeroCounter -and $zeroCounter -ne 0)
    {
        throw "TRX semantic validation failed: $zeroCounterName=$zeroCounter."
    }
}

$vulnerabilityPath = $requiredFiles[5]
$vulnerabilityText = Get-Content -LiteralPath $vulnerabilityPath -Raw
$vulnerabilityDocument = $vulnerabilityText | ConvertFrom-Json
$projects = @(Get-JsonItems -Value (Get-OptionalJsonPropertyValue -Object $vulnerabilityDocument -Name 'projects'))
if ($projects.Count -eq 0)
{
    throw "Vulnerability report has no projects: $vulnerabilityPath"
}

foreach ($project in $projects)
{
    $projectPath = [string](Get-OptionalJsonPropertyValue -Object $project -Name 'path')
    if ([string]::IsNullOrWhiteSpace($projectPath))
    {
        $projectPath = '<unknown-project>'
    }

    foreach ($collectionName in @('topLevelPackages', 'transitivePackages'))
    {
        $projectPackages = Get-OptionalJsonPropertyValue -Object $project -Name $collectionName
        Assert-NoVulnerabilitiesInPackageCollection `
            -Packages @(Get-JsonItems -Value $projectPackages) `
            -ProjectPath $projectPath `
            -FrameworkName '<project>' `
            -CollectionName $collectionName
    }

    $frameworks = @(Get-JsonItems -Value (Get-OptionalJsonPropertyValue -Object $project -Name 'frameworks'))
    foreach ($framework in $frameworks)
    {
        $frameworkName = [string](Get-OptionalJsonPropertyValue -Object $framework -Name 'framework')
        if ([string]::IsNullOrWhiteSpace($frameworkName))
        {
            $frameworkName = [string](Get-OptionalJsonPropertyValue -Object $framework -Name 'name')
        }
        if ([string]::IsNullOrWhiteSpace($frameworkName))
        {
            $frameworkName = '<unknown-framework>'
        }

        foreach ($collectionName in @('topLevelPackages', 'transitivePackages'))
        {
            $frameworkPackages = Get-OptionalJsonPropertyValue -Object $framework -Name $collectionName
            Assert-NoVulnerabilitiesInPackageCollection `
                -Packages @(Get-JsonItems -Value $frameworkPackages) `
                -ProjectPath $projectPath `
                -FrameworkName $frameworkName `
                -CollectionName $collectionName
        }
    }
}

if ($vulnerabilityText -match '(?i)\b(GHSA-[A-Za-z0-9-]+|CVE-\d{4}-\d+)\b')
{
    throw "Vulnerability identifiers were found in $vulnerabilityPath."
}

$publishRoot = [System.IO.Path]::Combine(
    $artifactRoot, 'publish', 'POS.Wpf', 'win-x64')
$publishFiles = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse)
if ($publishFiles.Count -eq 0)
{
    throw "Experimental publish output is empty: $publishRoot"
}

$requiredPublishPaths = @(
    [System.IO.Path]::Combine($publishRoot, 'POS.Enterprise.exe'),
    [System.IO.Path]::Combine($publishRoot, 'POS.Enterprise.dll'),
    [System.IO.Path]::Combine($publishRoot, 'POS.Enterprise.deps.json'),
    [System.IO.Path]::Combine($publishRoot, 'POS.Enterprise.runtimeconfig.json'),
    [System.IO.Path]::Combine($publishRoot, 'appsettings.json'))

foreach ($requiredPublishPath in $requiredPublishPaths)
{
    Assert-NonEmptyFile -LiteralPath $requiredPublishPath
}

$requiredPublishNames = @($requiredPublishPaths | ForEach-Object { [System.IO.Path]::GetFileName($_) })

$denyPattern =
    '(?i)(\.pdb$|\.db$|\.sqlite\d*$|\.bak$|\.zip$|\.msix$|\.msi$|\.nupkg$|(^|[\\/])\.env$|backup|audit|context-pack)'
$unexpectedFiles = @()

foreach ($publishFile in $publishFiles)
{
    $relativeName = $publishFile.FullName.Substring($publishRoot.Length + 1) -replace '\\', '/'

    if ($relativeName -match $denyPattern)
    {
        $unexpectedFiles += $relativeName
        continue
    }

    $allowed =
        ($relativeName -match '(?i)\.dll$') -or
        ($relativeName -in $requiredPublishNames)

    if (-not $allowed)
    {
        $unexpectedFiles += $relativeName
    }

    if ($publishFile.Length -le 0)
    {
        throw "Publish output is empty: $relativeName"
    }
}

if ($unexpectedFiles.Count -gt 0)
{
    throw ('Unexpected or denied publish output: ' + ($unexpectedFiles -join ', '))
}

$appSettingsPath = [System.IO.Path]::Combine($publishRoot, 'appsettings.json')
$appSettingsText = Get-Content -LiteralPath $appSettingsPath -Raw
$null = $appSettingsText | ConvertFrom-Json
if ($appSettingsText -match '(?i)"(?:DefaultAdminPassword|WifiPassword|BankBin|AccountNumber|AccountName)"\s*:\s*"[^"]+"')
{
    throw 'Non-empty sensitive configuration value found in appsettings.json.'
}

Write-Host 'CI_ARTIFACT_VALIDATION=PASS'
Write-Host "TestTotal=$testTotal"
Write-Host "TestPassed=$testPassed"
Write-Host "PublishFileCount=$($publishFiles.Count)"
foreach ($artifact in $requiredFiles + @($publishFiles | ForEach-Object { $_.FullName }))
{
    $item = Get-Item -LiteralPath $artifact
    Write-Host ("Artifact={0};Bytes={1}" -f $artifact, $item.Length)
}
