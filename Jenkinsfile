pipeline {
    agent {
        label 'windows'
    }

    options {
        disableConcurrentBuilds()
        skipDefaultCheckout(true)
        timeout(time: 90, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = 'true'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        NUGET_XMLDOC_MODE = 'skip'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Validate repository and .NET SDK') {
            steps {
                bat label: 'Validate required tools and inputs', script: '''
@echo off
setlocal

git rev-parse --is-inside-work-tree >NUL 2>&1
if errorlevel 1 exit /b 1

where dotnet
if errorlevel 1 exit /b 1

dotnet --version
if errorlevel 1 exit /b 1

for /f "tokens=1 delims=." %%M in ('dotnet --version') do set "DOTNET_MAJOR=%%M"
if not defined DOTNET_MAJOR exit /b 1
if not "%DOTNET_MAJOR%"=="10" (
    echo .NET 10 SDK is required. Detected major version: %DOTNET_MAJOR%
    exit /b 1
)

if not exist "POS.Enterprise.slnx" exit /b 1
if not exist "dotnet-tools.json" exit /b 1
if not exist "scripts\Test-QualityGate.ps1" exit /b 1
if not exist "src\POS.Infrastructure\POS.Infrastructure.csproj" exit /b 1
if not exist "src\POS.Wpf\POS.Wpf.csproj" exit /b 1
if not exist "tests\POS.Architecture.Tests\POS.Architecture.Tests.csproj" exit /b 1

endlocal
'''
            }
        }

        stage('Restore') {
            steps {
                bat label: 'Restore solution', script: 'dotnet restore "POS.Enterprise.slnx" --verbosity minimal -p:RestoreBuildInParallel=false'
            }
        }

        stage('Build Release') {
            steps {
                bat label: 'Build solution Release', script: 'dotnet build "POS.Enterprise.slnx" -c Release --no-restore -m:1 -nr:false -p:BuildInParallel=false'
            }
        }

        stage('Test Release') {
            steps {
                bat label: 'Run full Release test suite', script: 'dotnet test "POS.Enterprise.slnx" -c Release --no-build --no-restore -m:1 -nr:false -p:BuildInParallel=false'
            }
        }

        stage('Quality Gate') {
            steps {
                bat label: 'Run Quality Gate with EF check', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-QualityGate.ps1"'
            }
        }
    }
}
