# POS Enterprise - New-PC Handover

## Handover status

- Branch: `main`.
- Development Freeze: ACTIVE after R4.4; do not begin R5.1 during handover.
- Source freeze immediately before this documentation commit: `34d886681e494e30c5dd6b704b9afad8a5958eb1` (`test(portability): remove canonical database dependency`).
- R4.2, R4.3 and R4.4: CLOSED.
- R5.1 Product CSV/Excel Import: NOT STARTED.

## Project and required tools

POS Enterprise is a WPF point-of-sale application with Domain, Application, Infrastructure and WPF composition layers. Source, tests, migrations, resources and build tooling are supplied by Git; runtime and business data are transferred separately.

Required development environment:

- .NET SDK `10.0.302`.
- Visual Studio with the WPF and .NET desktop development workload.
- Git and Windows PowerShell 5.1 or PowerShell 7.
- Network access to the configured Git remote and NuGet feeds.

Restore the local Entity Framework tool manifest from the repository; do not install a machine-global tool as a substitute:

```powershell
dotnet tool restore
```

## Clone and verification commands

Do not place credentials in commands, scripts or this document. Clone from the configured remote:

```powershell
git clone --branch main --single-branch <configured-origin-url> POS_Enterprise_DotNet
Set-Location .\POS_Enterprise_DotNet
git status --short --branch
git rev-list --left-right --count HEAD...origin/main
dotnet --version
dotnet tool restore
dotnet restore POS.Enterprise.slnx
dotnet build POS.Enterprise.slnx -t:Rebuild --configuration Release --no-restore -m:1 -nr:false -p:BuildInParallel=false --verbosity minimal
dotnet test POS.Enterprise.slnx --configuration Release --no-build --no-restore -m:1 -nr:false -p:BuildInParallel=false --verbosity minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-QualityGate.ps1
```

Expected automated baseline at handover is `1360/1360 PASS`, failed `0`, skipped `0`, build warnings/errors `0/0`, vulnerability scan PASS, EF pending-model PASS and Git checks PASS.

For an isolated startup smoke, build Release first and provide a source database path only when the source is an approved isolated fixture. The launcher copies that source into its own TEMP boundary and must not be pointed at production mode:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-POS-IsolatedTest.ps1 `
  -SourceDatabasePath "$env:TEMP\approved-isolated-source.db" `
  -Configuration Release
```

The isolated launcher uses `POS_RUNTIME_MODE=IsolatedTest` internally and owns its database/settings/logo/backup paths beneath `%TEMP%`. Do not use the canonical production database as an isolated fixture.

## Database and migration handover

Latest EF migration: `20260828173138_AddSecureAuditLogMetadata`.

Canonical development database identity at this handover:

- Path: `data\pos-enterprise.db`.
- Length: `937984` bytes.
- LastWriteTimeUtc: `2026-08-09T14:26:03.9619805Z`.
- SHA-256: `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`.
- `-wal`, `-shm` and `-journal`: absent.

The portability tests create a unique migrated SQLite database directly beneath an owned `%TEMP%\POS-Enterprise-*` directory. They seed only deterministic test data required by the relevant UI contract and never copy or migrate the canonical database.

## What comes from Git and what is runtime data

Source from Git includes the solution and project files, production source, tests, EF migrations and model snapshot, XAML/themes/resources, build configuration, tool manifest, Quality Gate scripts, isolated launcher, Project Memory, AGENTS/architecture documents and required non-sensitive static assets.

Runtime/business data is transferred separately and only through an approved operational process. Categories to assess and transfer separately are:

- Canonical database.
- Store Settings.
- Terminal identity.
- Store logo and other managed store assets.
- Backup configuration and backup files.
- Printer and receipt settings.
- VietQR configuration.
- Other machine-local application state required by the deployment.

Never transfer or commit `bin`, `obj`, `.vs`, `TestResults`, logs, evidence, TEMP directories, credentials, IDE caches, Git credentials or unrelated caches. Do not copy secrets into configuration or this guide.

Keep the old computer unchanged until new-PC acceptance is complete.

## New-PC acceptance checklist

1. Clone `main`; confirm clean status and divergence `0/0`.
2. Confirm SDK `10.0.302`, Visual Studio WPF/.NET desktop workload, Git and PowerShell.
3. Restore the local EF tool manifest and NuGet packages.
4. Rebuild Release with zero warnings and errors.
5. Run the full suite and confirm `1360/1360 PASS`, failed/skipped `0/0`.
6. Run the official Quality Gate without `-SkipEfCheck`; confirm vulnerability and EF pending-model PASS.
7. Perform the approved isolated startup smoke.
8. Complete manual application acceptance: Administrator login, Store Setup, Sales, Employee, Role/Permission and Audit Log.
9. Only after acceptance passes, transfer the separately approved runtime/business data and validate its operational backup/recovery process.

R5.1 Product CSV/Excel Import remains NOT STARTED. No feature work is included in this handover.
