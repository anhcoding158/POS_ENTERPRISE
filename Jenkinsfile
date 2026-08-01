pipeline {
    agent {
        label 'windows'
    }

    options {
        disableConcurrentBuilds()
        skipDefaultCheckout(true)
        timeout(time: 90, unit: 'MINUTES')
        buildDiscarder(
            logRotator(
                numToKeepStr: '30',
                artifactNumToKeepStr: '10'))
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

        stage('Artifact Preparation') {
            steps {
                bat label: 'Prepare CI artifact root', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Test-CiArtifactContract.ps1" -Mode Prepare'
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
if not exist "scripts/Test-QualityGate.ps1" exit /b 1
if not exist "scripts/Invoke-CiArtifactCommand.ps1" exit /b 1
if not exist "scripts/Test-CiArtifactContract.ps1" exit /b 1
if not exist "src/POS.Infrastructure/POS.Infrastructure.csproj" exit /b 1
if not exist "src/POS.Wpf/POS.Wpf.csproj" exit /b 1
if not exist "tests/POS.Architecture.Tests/POS.Architecture.Tests.csproj" exit /b 1

endlocal
'''
            }
        }

        stage('Restore') {
            steps {
                bat label: 'Restore solution and capture log', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step Restore'
            }
        }

        stage('Build Release') {
            steps {
                bat label: 'Build solution Release and capture log', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step BuildRelease'
            }
        }

        stage('Test Release') {
            steps {
                bat label: 'Run full Release test suite and capture TRX', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step TestRelease'
            }
        }

        stage('Vulnerability Report') {
            steps {
                bat label: 'Create vulnerability JSON report', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step VulnerabilityReport'
            }
        }

        stage('Quality Gate') {
            steps {
                bat label: 'Run Quality Gate with EF check and capture log', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step QualityGate'
            }
        }

        stage('Experimental Publish') {
            steps {
                bat label: 'Publish native framework-dependent WPF output', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Invoke-CiArtifactCommand.ps1" -Step PublishWinX64'
            }
        }

        stage('Validate CI Artifacts') {
            steps {
                bat label: 'Validate successful-pipeline artifact contract', script: 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts/Test-CiArtifactContract.ps1" -Mode Validate'
            }
        }
    }

    post {
        always {
            script {
                def safeArtifacts = [
                    '_ci_artifacts/test-results/POS.Architecture.Tests.trx',
                    '_ci_artifacts/logs/restore.log',
                    '_ci_artifacts/logs/build-release.log',
                    '_ci_artifacts/logs/quality-gate.log',
                    '_ci_artifacts/logs/publish-win-x64.log',
                    '_ci_artifacts/reports/vulnerability/vulnerabilities.json'
                ]

                safeArtifacts.each { artifactPath ->
                    if (fileExists(artifactPath)) {
                        archiveArtifacts(
                            artifacts: artifactPath,
                            onlyIfSuccessful: false,
                            allowEmptyArchive: false,
                            fingerprint: false)
                    }
                }

                if (fileExists('_ci_artifacts/publish/POS.Wpf/win-x64')) {
                    archiveArtifacts(
                        artifacts: '_ci_artifacts/publish/POS.Wpf/win-x64/**/*.dll,_ci_artifacts/publish/POS.Wpf/win-x64/*.dll,_ci_artifacts/publish/POS.Wpf/win-x64/**/*.exe,_ci_artifacts/publish/POS.Wpf/win-x64/*.exe',
                        onlyIfSuccessful: false,
                        allowEmptyArchive: true,
                        fingerprint: true)

                    archiveArtifacts(
                        artifacts: '_ci_artifacts/publish/POS.Wpf/win-x64/**/*.json,_ci_artifacts/publish/POS.Wpf/win-x64/*.json',
                        onlyIfSuccessful: false,
                        allowEmptyArchive: true,
                        fingerprint: false)
                }
            }
        }
    }
}
