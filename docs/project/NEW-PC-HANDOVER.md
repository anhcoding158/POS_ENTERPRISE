# POS Enterprise - New-PC Handover

## Current checkpoint status — Inventory History navigation/filter-card hotfix — 2026-08-30

- The duplicate Shell Inventory History entry is removed; use `Sản phẩm & tồn kho → Kho & lưu trữ → Lịch sử kho`. The screen has one light filter card with database-side product search, safe `Tìm`/Enter/debounce, in-field clear, `Xóa bộ lọc` and `Làm mới`.
- Focused coverage is `22/22 PASS` in Debug and Release, relevant Release is `284/284 PASS`, normal-host full Release is `1410/1410 PASS`, build is `0/0`, and official Quality Gate is PASS with vulnerability and EF checks. No canonical database or runtime data was used.
- Physical WPF click/UIA/DPI acceptance remains manual pending because this desktop exposes no inspectable top-level HWND. R5.1 remains in manual acceptance, R5.2 is NOT STARTED, R4.2–R4.4 remain CLOSED/PRESERVED and Development Freeze is ACTIVE outside this hotfix.

## Current checkpoint status — Inventory History / Adjustment / Category UX hotfix — 2026-08-30

- Inventory History uses the familiar compact sidebar and card workspace while retaining direct search by product name/code/barcode. Manual stock adjustment quantity is blank until entered; Category form/list no longer exposes DisplayOrder and lists sort by Vietnamese name.
- Automated evidence: focused `44/44` Debug/Release, relevant `284/284` Release, normal-host full `1407/1407`, build `0/0`. Physical WPF click/UIA/DPI verification remains pending.
- No canonical database was opened or copied. R5.1 remains manual acceptance; R5.2 is NOT STARTED.

## Current checkpoint status — Inventory History UX/query hotfix — 2026-08-30

- Inventory History now searches directly by product name, code or barcode; search updates automatically, Enter applies immediately, `Xóa bộ lọc` clears all conditions including navigation scope, and `Làm mới` preserves them.
- Focused coverage is `16/16 PASS` in Debug/Release, relevant Release is `260/260 PASS`, normal-host full Release is `1399/1399 PASS`, failed/skipped `0/0`, Release build `0/0`. The six sandbox DPAPI/local-secure-storage failures pass on normal Windows; no security boundary changed.
- Physical WPF click/UIA/DPI acceptance remains manual pending. No canonical database or runtime data was used; R5.1 remains IN PROGRESS and R5.2 is NOT STARTED.

## Current checkpoint status — R5.1C/D UX remediation — 2026-08-30

- The Product import wizard was simplified for store users: choose a file, inspect only the necessary details, select duplicate handling, and perform one explicit import action. Compact canonical headers in the prepared CSV/XLSX fixtures auto-map all 11 fields; missing categories are a separate reference-data issue.
- Focused coverage is `27/27 PASS` in Debug/Release, relevant Release is `250/250 PASS`, normal-host full Release is `1389/1389 PASS`, failed/skipped `0/0`, build `0/0`, and official Quality Gate without `-SkipEfCheck` is PASS, including vulnerability, EF pending-model and Git checks. No canonical DB, credentials, package or migration was used.
- Manual fixtures remain at `D:\Projects_1\POS_Enterprise_DotNet\artifacts\R5.1C-manual-fixtures`; create/activate the exact fixture categories in the isolated setup before import. The isolated launcher accepts only an isolated source DB; select the fixture in the wizard after startup.
- Physical WPF click/UIA/DPI acceptance is still manual pending. R5.1A/B and R4.2–R4.4 remain CLOSED/PRESERVED, R5.1 is IN PROGRESS, R5.2 is NOT STARTED and Development Freeze is ACTIVE outside R5.1.

## Current checkpoint status — R5.1C/D — 2026-08-30

- R5.1C/D Product CSV/Excel Import Wizard is IMPLEMENTED and AUTOMATED VERIFIED; physical WPF click/UIA/DPI acceptance is MANUAL PENDING. R5.1A/B remain CLOSED/PRESERVED, R5.1 remains IN PROGRESS and R5.2 is NOT STARTED.
- Open the Product & Inventory route and use `Nhập CSV/Excel`. The wizard uses the existing secure parser and transactional service, supports the exact 11-field schema, explicit worksheet/mapping/policy, full validation, bounded preview and typed commit/rollback result. No mutation occurs before `Xác nhận nhập`.
- Evidence: import-focused `25/25 PASS` in Debug/Release, relevant Release `248/248 PASS`, normal-host full Release `1387/1387 PASS`, build `0/0`, official Quality Gate PASS with vulnerability and EF checks. Isolated startup reached `InitialSetupWindowReady` with an owned TEMP source DB; no canonical/runtime data was used.
- Synthetic manual fixtures are at `D:\Projects_1\POS_Enterprise_DotNet\artifacts\R5.1C-manual-fixtures`: `valid-products.csv`, `valid-products.xlsx`, `duplicate-products.csv`, `row-errors-products.csv` and `over-100-products.csv` (the last has an invalid value at source row 126). The launcher requires a separate isolated source DB and the fixture is selected in the wizard after startup.
- Manual limitation: this environment exposes no inspectable top-level HWND/UIA, so click-through, accessibility and physical DPI checks are not claimed. R4.2/R4.3/R4.4 remain CLOSED/PRESERVED and Development Freeze is ACTIVE outside R5.1.

## Current checkpoint status — R5.1B — 2026-08-29

- R5.1B Transactional Product Import is CLOSED and R5.1 remains IN PROGRESS; R5.1C WPF Import Wizard is NEXT. R5.1A is CLOSED/PRESERVED.
- The import use case is Application/Infrastructure only: it revalidates the exact 11-field typed preview snapshot, applies explicit duplicate policy in an EF database transaction, uses active existing Categories and the existing opening-balance ledger, and writes no canonical/runtime data. No migration or package was added.
- Focused R5.1B is `9/9 PASS` in Debug and Release; relevant Product/Inventory/RBAC is `32/32 PASS`; normal-host full Release is `1383/1383 PASS`; Release build and Quality Gate are `0/0` warnings/errors with vulnerability and EF checks PASS. Six sandbox-only DPAPI tests remain a known environment limitation and pass on normal host.
- Development Freeze is ACTIVE outside this authorized R5.1B checkpoint. R4.2/R4.3/R4.4 remain CLOSED/PRESERVED.

## Previous handover checkpoint — R5.1A — 2026-08-29

- R5.1A Product CSV/Excel secure preview foundation was CLOSED and R5.1 overall was IN PROGRESS; R5.1B was NEXT. The foundation is read-only and does not create an Import Wizard or mutate database data.
- The exact 11-field schema is sourced from the live Product model: ProductCode, Barcode, Tên, Danh mục, Đơn vị tính, Giá bán, Giá vốn, Tồn đầu, Tồn tối thiểu, Trạng thái and Ghi chú. Focused import coverage is `12/12 PASS` in Debug and Release; normal-host full Release is `1374/1374 PASS`; Quality Gate, vulnerability and EF checks PASS.
- That handover state used no canonical database, production settings, credentials or package/SDK update.

## Handover status

- Branch: `main`.
- Development Freeze: ACTIVE outside the authorized R5.1B checkpoint.
- Source freeze immediately before this documentation commit: `34d886681e494e30c5dd6b704b9afad8a5958eb1` (`test(portability): remove canonical database dependency`).
- R4.2, R4.3 and R4.4: CLOSED.
- R5.1 Product CSV/Excel Import: IN PROGRESS; R5.1A CLOSED/PRESERVED, R5.1B CLOSED, R5.1C NEXT.
- The current R5.1B verification is recorded above: focused `9/9 PASS` in Debug/Release, relevant `32/32 PASS`, normal-host full Release `1383/1383 PASS`, Release build `0/0` warnings/errors, vulnerability PASS and EF pending-model PASS. Physical WPF import-wizard checks are deferred to R5.1C.

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

Expected automated baseline at handover after the scoped hotfix is `1362/1362 PASS`, failed `0`, skipped `0`, build warnings/errors `0/0`, vulnerability scan PASS, EF pending-model PASS and Git checks PASS.

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
5. Run the full suite and confirm the current baseline `1383/1383 PASS`, failed/skipped `0/0`.
6. Run the official Quality Gate without `-SkipEfCheck`; confirm vulnerability and EF pending-model PASS.
7. Perform the approved isolated startup smoke.
8. Complete manual application acceptance: Administrator login, Store Setup, Sales, Employee, Role/Permission and Audit Log.
9. Only after acceptance passes, transfer the separately approved runtime/business data and validate its operational backup/recovery process.

R5.1B transactional Product import is CLOSED. R5.1C WPF Import Wizard remains the next authorized checkpoint; no R5.1C feature work is included in this handover.
