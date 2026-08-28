# TEST BASELINE — POS ENTERPRISE RETAIL V1

## Post-R4.4 handover-freeze hotfix baseline — 2026-08-29

- The real Release composition regression probe reproduces and prevents the missing-`IAuditLogService` Audit load failure; Employee WPF bounds coverage verifies the list summary stays inside the master card and identity text does not overlap status at `1180×720`, `1366×768`, `1280×720` and `1000×620`. The focused hotfix suite is `10/10 PASS`.
- Release solution build after the source changes is `0` warnings / `0` errors. The final official Quality Gate passed with `1360/1360 PASS`, failed `0`, skipped `0`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- The latest isolated Release startup reached `DatabaseInitialized` and `ShellWindowReady`; external UIA still has no top-level HWND in this environment, so physical navigation and DPI smoke remain manual limitations.
- Canonical database safety remains length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with no WAL/SHM/journal. Development freeze is reactivated after closeout; R5.1 is not started.

## R4.4 Secure Audit Log UI baseline — 2026-08-29

- R4.4 focused coverage is `7/7 PASS` in Release: allowlisted safe changes, credential exclusion, enriched audit metadata, Application permission denial/allow, and real `AuditLogWindow` construction/layout at `1180×720`, `1366×768`, `1280×720` and `900×560` with virtualization and initialized action filter.
- R4.3 focused coverage is `3/3 PASS`; its final official Quality Gate was `1355/1355 PASS`. Final R4.4 official Quality Gate is rerun after this documentation/source update and must remain at least `1359/1359 PASS`, with failed/skipped `0`, build `0/0`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- Release executable is built from current `main` under `src/POS.Wpf/bin/Release/net10.0-windows/POS.Enterprise.exe`; isolated startup used an owned temporary copy and `POS_RUNTIME_MODE=IsolatedTest`. External UIA has no top-level HWND here, so no physical click claim is recorded.
- Canonical database target remains length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with no WAL/SHM/journal. Development freeze is active; R5.1 Product CSV/Excel Import is not started.

## R4.3 Role and Permission Management baseline — 2026-08-29

- Focused R4.3 role policy/service coverage: `3/3 PASS`.
- Release solution build after R4.3 source changes: `0` warnings / `0` errors. The accepted full-suite baseline entering R4.3 was `1352/1352 PASS`; final R4.3 Quality Gate is recorded after the final gate run.
- No canonical database, sidecar, credential or production settings changes are permitted. R4.4 metadata migration is intentionally not part of the R4.3 staged commit and remains the next checkpoint work.

## R4.2 final Employee detail visual polish baseline — CLOSED — 2026-08-28

- Entry baseline was live `main` at `b8c17e0383cee79c1ed73b8c5d9dcc5f3e7b86a4`, fetched/aligned `0/0` with `origin/main`, SDK `10.0.302`, clean/staged `0`, and exact canonical database identity.
- Final focused Employee UI coverage is `6/6 PASS` in Release, including real selected identity data, contextual statuses, grouped profile/action surfaces, optional-value fallback, long-code tooltip/trimming and WPF layout at four supported sizes. Related Employee/RBAC/Shell/Store Setup/Sales regressions are `179/179 PASS`.
- Release build is `0` warnings/`0` errors. The final official Quality Gate is required after this documentation update; the accepted prior baseline is `1352/1352 PASS` with failed `0`, skipped `0`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- Canonical safety target remains length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with no WAL/SHM/journal.
- R4.3 and R4.4 remain NOT STARTED.

## R4.2 final manual UI correction baseline — CLOSED — 2026-08-28

- Entry baseline is live `main` at `93da2045ced774e8bd02e34777bfc145aac682db`, fetched/aligned `0/0` with `origin/main`, SDK `10.0.302`, clean/staged `0`, no active POS/worker/testhost/vstest process, and exact canonical database identity.
- New real WPF regression coverage passes `8/8` in Release: Role selected-content after Loaded/layout, global-versus-filtered empty semantics, create mode/close affordance/responsive sizes/account continuation, source AutomationId/action contracts and existing Store Readiness dialog/readiness behavior.
- Release build and official Quality Gate pass with `0` warnings/errors and `1352/1352` tests, failed `0`, skipped `0`; vulnerability scan, EF pending-model and Git checks pass. Final Release executable: `src/POS.Wpf/bin/Release/net10.0-windows/POS.Enterprise.exe`, SHA-256 `EDA67E1BEBBC3CB63A27784B892BA5DA51E774CFE724223EF756ED0C6941F69D`.
- Isolated Release launcher used a fresh scenario-owned copy, reached `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered`, `ShellWindowOpening`, `ShellWindowResolving`, `ShellWindowConstructed` and `ShellWindowReady`. External UIA exposed no top-level HWND; no unobserved click is claimed.
- Canonical safety target remains length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with no WAL/SHM/journal.

## Post-modernization manual UX hotfix baseline — 2026-08-28

- Entry baseline was live `main` at `bf56138a46c0f56b1f84f7189c4f45311f739a87`, descendant of `cea5c4230487d95c379c9e18d3a7437e91dc7133`, fetched and aligned `0/0` with `origin/main`, SDK `10.0.302`, clean/staged `0`, and exact canonical database identity.
- New readiness/filter/empty/create/dialog tests: `8/8` PASS in Debug. They cover optional backup warning classification, real readiness-dialog resource/control construction and action, stable role sentinel, filtered no-result semantics, same authoritative Add/create transition, long-code and supported clear icon contracts, plus real production employee-window construction.
- Release solution build after final source change: PASS, `0` warnings, `0` errors. Release full suite: `1350` PASS, `2` restricted named-pipe failures, `0` skipped, `1352` total. Official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck`: PASS with `1352/1352`, failed `0`, skipped `0`; vulnerability scan PASS for all `5/5` projects, dotnet-ef restore PASS, EF pending-model PASS and Git checks PASS.
- Exact isolated launcher used fresh scenario-owned database/settings/logo/backup paths and `POS_RUNTIME_MODE=IsolatedTest`; the scenario was validated and cleaned after capture. No UIA visual interaction was claimed.
- Canonical safety target remains length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with no WAL/SHM/journal.

## R4.2 Employee and Account Management UI modernization baseline — 2026-08-28

- Entry baseline was `main` at `cea5c4230487d95c379c9e18d3a7437e91dc7133`, branch `main`, fetched and aligned `0/0` with `origin/main`, SDK `10.0.302`, clean/staged `0`, no active Git operation or POS/worker/testhost/vstest process, and exact canonical database identity.
- New Employee UI contract/behavior tests: `6/6` PASS in Release. They exercise real `EmployeeManagementWindow.InitializeComponent`, shared resources/AuthLabelStyle, DataContext/wiring, first-row selection, empty/no-result/no-selection state, typed friendly security row mapping and existing production isolated service behavior.
- Release solution build after final source change: PASS, `0` warnings, `0` errors. Official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck`: PASS; Debug build `0/0`, full tests `1350/1350`, vulnerability scan PASS for all `5/5` projects, local dotnet-ef restore PASS, EF pending-model PASS and Git whitespace/status PASS.
- Release full suite after final source change: `1348` PASS, `2` FAIL, `0` skipped, `1350` total; both failures are the known restricted named-pipe tests only. The focused Employee Release suite is `6/6` PASS.
- Approved reference `artifacts/design-reference/employee-management-modern-target.png` was inspected and remains Git-ignored. Real WPF construction and bindings were verified; external UIA/render observation was unavailable in this desktop, so manual visual clicks, 1366/1280 layout and 100/125/150% scaling are not claimed.
- Exact isolated launcher used fresh `%TEMP%` copies with `POS_RUNTIME_MODE=IsolatedTest` and scenario-owned settings/logo/backup paths. The bounded launcher evidence was sanitized outside the repository; temporary scenario roots were validated before removal. Store Setup/sidebar/Inventory behavior was not changed, R4.3 remains NOT STARTED.
- Canonical safety after all tests: length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, no canonical WAL/SHM/journal.

## Shell sidebar and inventory navigation hotfix baseline — 2026-08-27

- Entry baseline was `main` at `0088b6f1a9185c125756bea352c2353108107573`, aligned `0/0` with `origin/main`, clean/staged `0`, SDK `10.0.302`, no active Git operation or POS/test process, and exact canonical database identity.
- New hotfix tests: `4/4` PASS. Product/Category/Shell/Store Setup/Employee/Orders focused set: `219/219` PASS. Broader Shell/Store/Employee/Sales/Orders/VietQR/auth/backup/restore/isolated set: `532/532` PASS. Final Release suite: `1348/1348` PASS, failed `0`, skipped `0`.
- Release solution build: PASS, `0` warnings, `0` errors. Official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck`: PASS; Debug build `0/0`, tests `1348/1348`, vulnerability scan PASS for all `5/5` projects, local dotnet-ef restore PASS, EF pending-model PASS and Git whitespace PASS.
- Real production-view coverage copies canonical data to an owned isolated database, runs the production migration/DI composition with `ValidateOnBuild=true` and `ValidateScopes=true`, constructs the real `ShellWindow`, validates the Products/Category bindings and responsive style contract, and executes the first product query. Route coverage proves three repeated Category ↔ Products transitions and no query when Products is already active.
- Release launcher evidence reached `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered`, `ShellWindowOpening`, `ShellWindowResolving`, `ShellWindowConstructed` and `ShellWindowReady`, with a completed production product query. UIA reported no top-level HWND/element, so no visual click claim is made.
- Canonical safety remained exact: length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, no canonical WAL/SHM/journal. Evidence: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-Sidebar-Inventory-Hotfix-20260827T153747399Z-977b9f0eca6540af95cf405f1bdbfbf3`. Store Setup UX is preserved and R4.3 is NOT STARTED.

## Store Setup UX polish and Shell navigation UX baseline — 2026-08-27

- Entry baseline was `main` at `29f7354ab683129edffcc163fd51358a6b9f5969`, SDK `10.0.302`, clean and aligned with `origin/main`; canonical database identity matched exactly before and after isolated verification.
- New Store Setup/Shell UX tests: `8/8` PASS. Related focused Store Setup/Shell/Employee/Sales/VietQR/backup/restore/isolated-startup regressions: `384/384` PASS. Final full suite: `1344/1344` PASS, `0` failed, `0` skipped.
- Release solution build: PASS, `0` warnings, `0` errors. Official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck`: PASS; Debug build `0/0`, vulnerability scan PASS for all `5/5` projects, local dotnet-ef restore PASS, EF pending-model PASS and Git whitespace PASS.
- Isolated Release launcher used a fresh `%TEMP%` copy and reached `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered`, `ShellWindowOpening`, `ShellWindowResolving`, `ShellWindowConstructed` and `ShellWindowReady`. Production Store Setup view/resource construction and initialization/query contracts passed; UIA had no top-level HWND, so no visual interaction is claimed.
- Canonical safety remained exact: length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, no canonical WAL/SHM/journal. R4.1 visual polish is completed by this checkpoint; R4.3 is NOT STARTED.

## R4.2 Employee and Account UI hotfix baseline — 2026-08-27

- Entry baseline was `main` at `bb93417034ea049aa9ab5e3ce8d4f2ca6fcd7536`, SDK `10.0.302`, fetched and aligned with `origin/main`, clean before the hotfix, and canonical database identity unchanged.
- Deterministic pre-fix production reproduction captured `System.Windows.Markup.XamlParseException` at `EmployeeManagementWindow.InitializeComponent` and inner `System.Exception: Cannot find resource named 'AuthLabelStyle'. Resource names are case sensitive.` This was the exact source of the observed module-open failure; the employee query itself was independently successful after the XAML fix.
- New hotfix tests: `2/2` PASS. Broader focused staff/auth/navigation/Sales set: `162/162` PASS. Official Quality Gate without `-SkipEfCheck`: `1338/1338` PASS, failed `0`, skipped `0`; Debug solution build `0` warnings/`0` errors; vulnerability scan PASS; dotnet-ef restore PASS; EF pending-model check PASS; Git checks PASS. Release solution build: `0` warnings/`0` errors.
- Real isolated verification used a fresh temporary copy and the production migration path. Launcher milestones reached `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered`, `ShellWindowOpening`, `ShellWindowResolving`, `ShellWindowConstructed` and `ShellWindowReady`; production auth restored Administrator, direct employee search returned `count=1`, and the real window/ViewModel initialized with `count=1`, `total=1`. UIA top-level HWND was unavailable, so visual interaction is not claimed.
- Canonical safety remained exact: length `937984`, LastWriteTimeUtc `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, and no canonical WAL/SHM/journal. R4.1 visual polish is deferred; R4.3 is NOT STARTED.

## R4.2 Employee and Account Management closeout baseline — 2026-08-26

- Entry baseline: `main` at `4d833dd48f467424b3156de067328288024cf492`, SDK `10.0.302`, clean and aligned with `origin/main`, canonical database unchanged.
- R4.2 focused staff/account, migration-upgrade and WPF contract coverage: `9/9` PASS, `0` failed, `0` skipped. The full Debug/Quality-Gate suite is `1336/1336` PASS, `0` failed, `0` skipped.
- Release solution build: PASS, `0` warnings, `0` errors. Official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck`: PASS; vulnerability scan PASS for all `5/5` projects, local dotnet-ef restore PASS, EF pending-model check PASS and Git checks PASS.
- Migration acceptance upgraded an isolated pre-R4.2 schema copy, preserved the existing User ID/history and backfilled the Employee record. No destructive `Up` operation was introduced.
- Real launcher smoke used exact `POS_RUNTIME_MODE=IsolatedTest`, copied only to `%TEMP%`, reported scenario database/settings/logo/backup paths, stayed alive through `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered`, `ShellWindowOpening`, `ShellWindowConstructed` and `ShellWindowReady`, and created no canonical artifacts. External UIA could not see a top-level window in the execution desktop; no visual claim is made. The exact owned PID was stopped after `CloseMainWindow` had no HWND, then the temporary scenario root was safely removed.
- Final full baseline: `1336/1336`; R4.1 visual polish is deferred. Exact next checkpoint is **R4.3 Role and Permission Management — READY TO START**.
- Evidence: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-R4.2-Evidence-20260826T160425505Z-2b7898f6839c4390ace8b93e34f0eba2`.

## R4.1 isolated-startup hotfix baseline — 2026-08-26

- Reproduction evidence: exact launcher returned exit code `1` and sanitized isolated diagnostics recorded the `AggregateException`/missing logging-service chain. The failing pre-fix path was the pre-startup `AddInfrastructure` provider with `ValidateOnBuild=true` and no logging registration.
- Hotfix focused suite: `11/11` PASS, including isolated production composition, Store Setup tests and launcher direct-executable/path contract. Release build: `0` warnings, `0` errors.
- Final full suite after the hotfix: `1327/1327` PASS, `0` failed, `0` skipped. Official Quality Gate without `-SkipEfCheck`: PASS, including vulnerability scan and EF pending-model check.
- Real isolated Release smoke: exact `POS.Enterprise.exe` child remained running, host/database initialization completed, effective settings/logo paths were inside the scenario, and diagnostics reached `LoginWindowReady` without startup-failure dialog. Win32 window enumeration was unavailable in the execution desktop and is recorded as a limitation, not a fabricated UI PASS.
- Evidence: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-R4.1-Hotfix-Evidence-20260826T151524982Z-ffafa24fe2524d57b54d0905d6d5b480`.

## R4.1 Store Setup closeout baseline — 2026-08-26

- Entry baseline: `main` at `d74bced32a74555bf36d57dea18f0e8d70708f71`, SDK `10.0.302`, clean and aligned with `origin/main`, canonical database unchanged.
- Focused Store Setup/UI contract: `9/9` PASS. Receipt/checkout regressions: `11/11` PASS. Backup/VietQR regressions: `37/37` PASS. Named-pipe regressions: `11/11` PASS.
- Final Release build: 0 warnings, 0 errors. Full suite: `1326/1326` PASS, `0` failed, `0` skipped. Official Quality Gate without `-SkipEfCheck`: PASS, including vulnerability scan and EF pending-model check.
- Isolated acceptance evidence: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-R4.1-Evidence-20260826T144301184Z-6708a06964e64ee096ddc03282543dea`. The copied scenario database was temporary and removed; no canonical database or user data was staged.
- R4.1 is the current closed baseline; exact next checkpoint is R4.2 Employee and Account UI.

## R3.4 Disaster Recovery Drill closeout baseline — 2026-08-26

- DR1–DR9 PASS on one validated `%TEMP%` scenario. Production manual backup created one verified/restorable artifact; a genuinely new normally initialized database lacked the source business markers; production restore preparation plus the real external Release worker reached durable `Verified`, shut down the exact parent, exited cleanly and started exactly one normal POS with no orphan.
- The actual restarted WPF application presented the restore-success acknowledgement once, accepted the isolated test login, and reached an authenticated Shell. A second controlled normal restart did not repeat the acknowledgement. Orders, stock and receipt evidence was then compared through production repository reads and independent read-only SQLite projections; normalized projections matched exactly.
- Final restored database checks: `PRAGMA integrity_check` = one `ok`; `PRAGMA foreign_key_check` = zero violations; migration history matched; clean shutdown left no WAL/SHM/journal; backup and pre-restore safety artifacts remained hash-stable until verification completed.
- Focused recovery suite after the test-fixture portability repair: `142/142` PASS, `0` failed, `0` skipped. The final official `scripts/Test-QualityGate.ps1` run outside the restricted IPC sandbox PASSed without `-SkipEfCheck`: Debug build `0` warnings/`0` errors, full suite `1317/1317`, vulnerability scan PASS for `5/5` projects, tool restore PASS, EF pending-model PASS and Git whitespace checks PASS.
- Canonical safety after cleanup: database length `937984`, timestamp `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`; canonical managed roots and sidecars absent; scenario root absent; owned POS/worker/test processes zero.
- Evidence root: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-R3.4-Evidence-20260825T155209292Z-bc53793763084524a966cf92206862bd`. Evidence is compact, sanitized and outside the repository. RST14 remains the R3.3 combined-evidence acceptance described below; R3.4 does not relabel it.

## R3.3 Restore Wizard and Rollback closeout baseline — 2026-08-25

- Acceptance ledger: RST1–RST13 PASS from retained immutable isolated evidence; RST14 **ACCEPTED by combined evidence** with the independent external UIA/PID-timeline limitation explicitly retained; RST15 cumulative canonical-safety audit PASS. No RST scenario used the canonical database.
- RST14 combined basis includes the RST1 real isolated end-to-end restore (Verified state, installed candidate hash, normal restart, terminal result, acknowledge once and no orphan), automated production contracts for parent PID/start-time, timeout, replacement/rollback/recovery and exactly-one restart, and a real isolated parent launch observed by the native Toolhelp timeline. It does not claim a complete external worker UIA/PID timeline.
- Fresh restore-targeted regressions: `89/89` PASS, `0` failed, `0` skipped. Fresh backup/restore UI regressions: `89/89` PASS, `0` failed, `0` skipped.
- Release rebuild: PASS, `0` warnings, `0` errors. Full Release suite: `1317/1317` PASS, `0` failed, `0` skipped. The independently discovered WAL checkpoint/hash regression is included in this baseline.
- Independent vulnerability scan PASS for all `5/5` projects; local tool restore PASS (`dotnet-ef` `10.0.10`); EF pending-model check PASS; Git checks PASS. Official `scripts/Test-QualityGate.ps1` PASS without `-SkipEfCheck`, including Debug build `0/0` and repeated `1317/1317` tests.
- Canonical safety after acceptance: database length `937984`, timestamp `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`; automatic/restore/pre-restore roots and canonical WAL/SHM/journal artifacts absent; POS/worker/test processes zero.

## R3.2 Automatic Backup and Retention closeout baseline — 2026-08-23

- Targeted R3.2 suite: `76/76` PASS, `0` failed, `0` skipped. Official outside-sandbox Quality Gate: `1240/1240` PASS, `0` failed, `0` skipped; restore/build, vulnerability scan, local tool restore, EF pending-model and Git checks PASS without `-SkipEfCheck`.
- ABM1/ABM2 PASS with human + machine evidence; ABM3–ABM6 PASS with user-approved machine-assisted production-runtime evidence. ABM3-B proved concurrent typed `Busy` on the same production coordinator; ABM4-B protected sparse GFS snapshots above 2 GiB without following a junction; ABM5 proved fail-closed recovery and retention warning; ABM6 proved the 5-second drain and safe cancellation.
- Canonical database remained `937984` bytes with SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`; canonical automatic root remained absent.

## R3.1 Manual Backup closeout baseline — 2026-08-09

- Product checkpoint commit `1cbabbbb8928a29c520897c849c405f6ad6e16de` contains the exact product/script source covered by the official Quality Gate via `powershell.exe -NoProfile -ExecutionPolicy Bypass -File D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1`: PASS, exit code `0`, without `-SkipEfCheck`; restore PASS; Debug build `0` warnings/`0` errors; `1183/1183` tests PASS, `0` failed, `0` skipped; vulnerability scan PASS for `5/5` projects; local tools restore PASS; EF pending-model and Git checks PASS.
- Product verification: `ManualBackupServiceTests.Manual_backup_success_must_create_verified_copy_with_hash_and_size` executed in the full gate and proves returned size/hash, schema compatibility, payload readability, SQLite integrity and unchanged source. Related executed regressions cover committed WAL content, unique no-overwrite output, invalid destination, missing source and corrupt-source/no-partial-output behavior. UI regressions cover consent, picker cancellation, single-flight execution, success metadata, typed failure presentation, architecture boundary and DI registration.
- MB1 backup action = PASS — Independently Verified. Fixture: length `372736`, SHA-256 `0B7CF7803264B3F909FCB8ECA97607124D40066EA9E23DCE27FF4DEA354AA34E`, unchanged PASS. Destination: exactly one final file, zero partial/temp entries. Artifact `pos-enterprise-pre-migration-20260809-122140483.db`: length `372736`, SHA-256 `96D79B539267E762096A5B594363D78C9CBB71A36112E04D1BCA4A0FEBF23EB8`; UI length/hash comparison PASS.
- Independent arbitrary-artifact SQLite integrity/schema probe: **NOT RUN**. This baseline does not relabel it PASS. The product verification claim instead rests on exact live source ordering plus the executed service regression above.
- Project-Memory-only post-push synchronization does not change the already-gated product/script source, so the `1183/1183` official gate is reused and not rerun. R3.1 is Closed, Committed and Pushed at product checkpoint `1cbabbbb8928a29c520897c849c405f6ad6e16de`; R3.2 is **NOT STARTED**.

## R2.4D database runtime hardening baseline — 2026-08-09

- Entry working tree contained the five user-reported scope items: four tracked modifications and one untracked test file. No index entries were present.
- Final targeted runtime/database/storage regression: `105/105` PASS, `0` failed, `0` skipped.
- Release build: PASS, `0` warnings, `0` errors. Release full regression: `1164/1164` PASS, `0` failed, `0` skipped, outside the IPC-restricted sandbox.
- Official Quality Gate: PASS without `-SkipEfCheck`; Debug build `0/0`, repeated full tests `1164/1164`, vulnerability scan PASS for all `5/5` projects, local tool restore PASS, EF pending-model check PASS and Git checks PASS.
- Sandbox full test observation was `1161/1163` with exactly two accepted R2.1 named-pipe access failures; the authoritative outside-sandbox rerun was `1163/1163` before the final diagnostics test, and the final Release/Quality Gate runs were `1164/1164`.
- Publish helper: PASS. Publish root contained the required POS.Enterprise executable/assembly and appsettings, with no development appsettings, database artifacts or PDBs. The complete CI artifact contract was not asserted by the publish-only probe because TRX/log/report groups were not generated.
- PowerShell script smoke: PASS for exit-code propagation and parent override/mode preservation. The probe intentionally passed an invalid `dotnet run` option, so it did not claim application/manual launch acceptance.
- Manual acceptance: M1 clean Normal source run PASS; M2 stale override safety block PASS; M3 Isolated Test PASS; M4-A standard publish/different cwd PASS; M4-B relocated publish PASS; M4-C renamed executable PASS; M5 published override plus `DOTNET_ENVIRONMENT=Development` safety block PASS; M6 filesystem/hash audit PASS. All WPF observations were made by the user.
- The earlier invalid-option smoke was not a valid non-launch probe because the argument was forwarded to the application and left a WPF process running; those owned processes were closed and the result was not counted as PASS. The fixed-size error was instead verified resolved by successful clean M1/M3 script launches.
- EF tooling `dotnet ef --version`: PASS (`10.0.10`); no database update was run. Process/User/Machine `Infrastructure__DatabasePath`: UNSET/UNSET/UNSET after verification.

## R2.4C closeout baseline — 2026-08-08

- R2.4C authenticated storage status UI implementation, independent review and automated re-verification passed on the accepted unstaged R2.4C working tree at HEAD `166be3ec8cce555f772c6d068147d1eb8cd13f09`, aligned with local `origin/main`, with an empty index before closeout.
- Targeted R2.4C tests: `18/18` PASS, 0 failed, 0 skipped. Combined regression: `166/166` PASS, 0 failed, 0 skipped.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors. Full Quality Gate outside sandbox: `1142/1142` PASS without `-SkipEfCheck`; vulnerability scan PASS; EF pending-model check PASS. Any sandbox-only named-pipe provenance remains the accepted R2.1 environment boundary; production IPC was unchanged.
- Manual acceptance: PASS for the authenticated Shell entry, modal owner/single-instance behavior, repeated open/close, layout and wording, metrics, refresh, privacy/forbidden controls, Shell navigation and application shutdown.
- The UI uses production storage-monitor contracts, has no backup/restore/cleanup/delete controls, exposes no database path or connection string, and does not alter startup migration authority. No package, schema, migration or ModelSnapshot changed; no database operation was performed for this checkpoint.
- A separate authentication incident is not treated as a confirmed R2.4C defect or root cause.
- R2.4A, R2.4B and R2.4C are COMPLETED; R2.4D is the next checkpoint and NOT STARTED. R3 and R4 remain NOT STARTED.

## R2.4B independent-review and closeout baseline — 2026-08-08

- Entry branch/HEAD: `main` at `42664fd89c7214682ad5d0c7de327ec8a9a7271d`, aligned with local `origin/main`, with exactly three unstaged R2.4B implementation files and an empty index.
- Independent review corrected fail-open unknown-status handling and the zero-byte request used when an existing database footprint was unavailable. The final flow performs preflight only for pending migrations and before integrity, verified backup, or schema mutation; cancellation remains unchanged.
- Exact `DatabaseInitializerSafetyTests`: `15/15` PASS, 0 failed, 0 skipped. Combined storage/path/options/DI/architecture/configuration/SQLite regressions: `99/99` PASS, 0 failed, 0 skipped.
- Release solution build: PASS, 0 warnings, 0 errors.
- Full Quality Gate outside sandbox: PASS without `-SkipEfCheck`; `1124/1124` PASS, 0 failed, 0 skipped; gate build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model and Git checks PASS. The sandbox attempt had 1122 PASS/2 FAIL only at the accepted R2.1 named-pipe environment boundary; production IPC was unchanged.
- Manual acceptance: **NOT APPLICABLE — no manual surface in R2.4B**. The WPF application was not run.
- No package, schema, migration or ModelSnapshot changed. Tests used isolated TEMP databases; the main database was not opened, read, copied, hashed, renamed, modified, deleted, backed up, or migrated.
- R2.4A and R2.4B are COMPLETED; R2.4 remains IN PROGRESS; R2.4C/D, R3 and R4 are NOT STARTED.

## R2.4A independent-review and closeout baseline — 2026-08-08

- Entry branch/HEAD: `main` at `c687b2f0c2745f8be5c6e5e1b9450339a3309d6f`, aligned with local `origin/main`, with exactly the accepted 11-file R2.4A implementation and an empty index.
- Independent review corrected ambiguous inaccessible-vs-missing metadata classification, added bounded parent-reparse revalidation, derived `CanProceed` from typed status and proved cancellation between metadata reads.
- Exact final `DatabaseStorageMonitorTests`: `44/44` PASS, 0 failed, 0 skipped. Targeted storage/path/options/DI/architecture/configuration/DatabaseInitializer/SQLite UX regressions: `89/89` PASS, 0 failed, 0 skipped.
- Release solution build: PASS, 0 warnings, 0 errors.
- Full Quality Gate outside sandbox: PASS without `-SkipEfCheck`; `1114/1114` PASS, 0 failed, 0 skipped; gate build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model and Git checks PASS.
- Manual acceptance: **NOT APPLICABLE — no manual surface in R2.4A**. The WPF application was not run.
- No package, schema, migration or ModelSnapshot changed. The main database was not opened, read, copied, hashed, renamed, modified, deleted or migrated; no database/WAL/SHM/journal was created.
- R2.4A is COMPLETED; R2.4 remains IN PROGRESS; R2.4B and R3 are NOT STARTED. Startup integration, UI, backup/restore and cleanup were not implemented.

## R2.3D manual acceptance and final closeout baseline — 2026-08-08

- Manual acceptance M01–M09: PASS with no observed issue. Authenticated Shell placement, modal ownership, disclosure/consent, initial state, picker cancellation, two successful unique exports, reset, cancellation/close behavior and post-close Shell smoke behavior were accepted.
- The manual archive remained outside the repository and was inspected locally read-only with permission. ZIP open/path/allow-list rules, all six fixed JSON entries, managed-log entry boundary, database/WAL/SHM/journal/backup exclusion, manifest database exclusion and no temporary archive all PASS. No raw bundle content or machine-specific path is recorded here.
- Final targeted verification: R2.3C 21/21; R2.3B 12/12; R2.3A 11/11; exact corrective tests 2/2; R2.2 live 26/26; architecture/privacy/security 22/22. Every group has 0 failed/0 skipped.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Final Quality Gate outside sandbox: PASS without `-SkipEfCheck`; full suite 1070/1070 PASS, 0 failed/0 skipped; build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model consistency and Git checks PASS.
- The earlier sandbox provenance remains 1068 PASS/2 FAIL only at the accepted R2.1 named-pipe environment boundary. The outside-sandbox result is authoritative and production IPC was unchanged.
- No package or migration change was introduced. The main database was not read, copied, hashed, modified or deleted. R2.3 is COMPLETE; R2.4 remains NOT STARTED.

## R2.3C Support Bundle UI automated baseline — 2026-08-08

- Entry branch/HEAD: `main` at `e8d8d20395227736072175ad147a11752f54b0b4`, aligned with `origin/main`, retaining the accepted uncommitted R2.3A/B working tree and empty index.
- Targeted R2.3C: 21/21 PASS, 0 failed, 0 skipped. Tests use fake `ISupportBundleService`, fake folder picker and controllable tasks; no ZIP, database, runtime log directory, user profile or real destination is used.
- Coverage includes initial/consent/readiness, picker cancellation/validation, fixed database exclusion, async single-flight, indeterminate progress, control locking, cancellation/Cancelling, late-success boundary, all typed results, exception-canary containment, close-while-running, CTS renewal/reset, XAML disclosure/no-network/no-database controls, authenticated Shell placement, abstraction-only ViewModel dependencies and exact DI registration.
- Regressions: R2.3B 12/12; R2.3A 11/11; exact corrective tests 2/2; R2.2 live 26/26; architecture/privacy/security 22/22. All have 0 failed/0 skipped.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Full Quality Gate outside sandbox: PASS without `-SkipEfCheck`; 1070/1070 PASS, 0 failed/0 skipped; build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model and Git checks PASS.
- Sandbox provenance: build passed and 1068 tests passed; only the two accepted R2.1 named-pipe tests failed because sandbox pipe access was denied. The outside-sandbox result is authoritative and production IPC was unchanged.
- Manual R2.3C acceptance was not run. R2.3D and R2.4 were not started; no WPF launch, package/schema/migration/database/main-data operation, stage/commit/push occurred.

## R2.3B Safe Support Bundle automated baseline — 2026-08-08

- Entry branch/HEAD: `main` at `e8d8d20395227736072175ad147a11752f54b0b4`, aligned with `origin/main`, retaining the accepted uncommitted R2.3A working tree and empty index.
- Targeted R2.3B: 12/12 PASS, 0 failed, 0 skipped. Coverage includes typed outcomes, fixed ZIP schema, privacy scan of all text entries, destination/temp/final collision ownership, database exclusion, cancellation, partial diagnostics, bounded managed-log export, active writer sharing, non-recursive selection, second-pass shared sanitization and DI/layering.
- R2.3A regression: 11/11 PASS; exact two corrective ownership tests 2/2 PASS. R2.2 live class: 26/26 PASS. Related architecture/privacy/security: 22/22 PASS.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Full Quality Gate outside sandbox: PASS without `-SkipEfCheck`; full suite 1049/1049 PASS, build 0 warnings/0 errors, vulnerability scan PASS for 5/5 projects, EF pending-model consistency PASS and Git checks PASS.
- Sandbox gate provenance: restore/build passed; full suite reached 1047 PASS/2 FAIL only in the accepted R2.1 named-pipe tests because sandbox pipe access was denied. The complete outside-sandbox rerun is authoritative; production IPC was unchanged.
- Tests used only self-created TEMP directories and isolated SQLite files. No WPF app, migration/update, main database operation, UI/manual acceptance, package change, stage/commit/push occurred.

## R2.3A corrective managed-log ownership baseline — 2026-08-08

- Corrective scope: reject managed-looking directories/reparse points, require canonical direct-parent ownership, defer length/time reads until after the guard, revalidate before delete and create active segments with `FileMode.CreateNew`.
- Targeted R2.3A: 11/11 PASS, 0 failed, 0 skipped; increased from 9 by two ownership/path regression tests.
- Current-source R2.2 regression: 26/26 PASS, 0 failed, 0 skipped. Historical accepted R2.2 closeout remains 27/27.
- Related receipt-privacy/infrastructure-registration tests: 14/14 PASS, 0 failed, 0 skipped.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Complete Quality Gate outside sandbox: PASS without `-SkipEfCheck`; full suite 1037/1037 PASS, 0 failed, 0 skipped; gate build 0 warnings/0 errors; dependency vulnerability scan PASS for 5/5 projects; EF pending-model consistency PASS.
- No WPF application, migration, database update, database read/hash/copy or ZIP/Support Bundle operation was performed. R2.3B remains NOT STARTED.

## R2.3A Safe Logging Foundation baseline — 2026-08-08

- Entry branch/HEAD: `main` at `e8d8d20395227736072175ad147a11752f54b0b4`, aligned with `origin/main`, clean before implementation.
- Targeted R2.3A behavior/filesystem/privacy/DI tests: 9/9 PASS, 0 failed, 0 skipped.
- Current-source R2.2 regression class: 26/26 PASS, 0 failed, 0 skipped. The historical accepted R2.2 closeout remains 27/27; the live class in this run discovered 26 cases.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Complete Quality Gate outside sandbox: PASS without `-SkipEfCheck`; repeated full suite 1035/1035 PASS, 0 failed, 0 skipped; gate build 0 warnings/0 errors.
- Dependency vulnerability scan: PASS; 5/5 solution projects have no vulnerable package from configured sources. No package was added or upgraded.
- EF pending-model consistency: PASS; no changes since the latest migration. No migration or database update was run.
- `git diff --check` and the Quality Gate Git check: PASS. Final cached check is recorded at handoff after memory edits.
- Environment note: the first sandboxed gate attempt timed out after a sandbox-only 2-test named-pipe failure. The exact `SingleInstanceInfrastructureTests` class passed 11/11 outside sandbox; the complete outside-sandbox gate is the accepted result.
- R2.3 remains IN PROGRESS. R2.3A has automated verification PASS; R2.3B/C/D and manual R2.3 acceptance are not performed.

## Corrective UX integration baseline — 2026-08-08

- Verified integrated `main` source at `352d2814ba8b194c8abf875852462f16f05153d4` before this documentation-only closeout commit.
- Targeted Order History UI contracts: 43/43 PASS, 0 failed, 0 skipped.
- Targeted Product archiving/UI contracts: 9/9 PASS, 0 failed, 0 skipped.
- POS.Wpf Release build: PASS, 0 warnings, 0 errors.
- Full Release suite: 1026/1026 PASS, 0 failed, 0 skipped; this is seven tests above the accepted R2.2 baseline of 1019.
- Full Quality Gate: PASS without `-SkipEfCheck`; repeated tests 1026/1026 PASS and build 0 warnings/0 errors.
- Dependency vulnerability scan: PASS; 5/5 solution projects have no vulnerable package from the configured sources.
- EF pending-model consistency: PASS; no changes since the latest migration. This did not apply migrations or open the development database.
- `git diff --check`: PASS. User manual acceptance for both corrective UX batches: PASS.

## R2.2 closeout baseline — 2026-08-06

- Targeted R2.2 deterministic automated acceptance: 27/27 PASS.
- Full Release suite: 1019/1019 PASS, 0 failed, 0 skipped. The first sandboxed run passed 1017/1019 and failed only the two known R2.1 named-pipe tests because local IPC access was denied; the complete outside-sandbox rerun is the accepted result.
- Full Quality Gate: PASS without `-SkipEfCheck`; repeated tests 1019/1019 PASS and build 0 warnings/0 errors.
- Dependency vulnerability scan: PASS; 5/5 solution projects have no vulnerable package from the configured sources.
- EF pending-model consistency: PASS; no changes since the latest migration.
- Changed-file security/secret scan: PASS; 12 R2.2 code/test files scanned, 0 high-confidence secret findings.
- `git diff --check`: PASS; CRLF/LF conversion notices are not whitespace errors.
- Acceptance Test A: **Manual PASS** with real SQLite `BEGIN IMMEDIATE`, preserved cart/input/method, no false success/receipt, no partial persistence, bounded recovery and one successful retry. Evidence directory `POS-R22-Acceptance-20260806-Manual1` remains outside Git.
- Acceptance Tests B/C/D: **NOT MANUALLY RUN**; **Covered by equivalent deterministic automated acceptance: PASS**. This is not Manual PASS.
- Harness boundary: isolated TEMP databases only; self-created acceptance data and lock; before/after atomic and duplicate checks; safe error-code simulation for `SQLITE_FULL`; separate not-a-database file with invariant SHA-256/length; deterministic disposal/pool clearing/cleanup. No development database was used.
- R2.2 is COMPLETE/CLOSED. R2.3 — Logging and Support Bundle is NOT STARTED.

## R2.1 closeout baseline — 2026-08-02

- `git diff --check`: PASS.
- Release rebuild: PASS, 0 warnings, 0 errors.
- Targeted named-pipe tests: 2/2 PASS individually outside the IPC-restricted execution sandbox.
- `SingleInstanceInfrastructureTests`: 11/11 PASS.
- Finite IPC stability: 10/10 rounds PASS, 2/2 tests per round.
- Full Release tests: 992/992 PASS, 0 failed, 0 skipped.
- Full Quality Gate via `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Test-QualityGate.ps1`: PASS without `-SkipEfCheck`; its repeated full test run passed 992/992.
- Dependency vulnerability scan: PASS; no vulnerable packages in the five solution projects.
- EF pending-model check: PASS; no changes since the last migration.
- Final post-memory Project Context security scan: PASS; coverage 100%, manifest verification PASS, `SecurityFindingCount=0`; generated packs are ignored/not staged.
- Manual R2.1 Tests A/B/C: PASS. Test D attempt 1 remains INCOMPLETE / NOT PROVEN. Test D attempt 2 PASS with old owner exit classification `HANDLE_SIGNALED_AND_OS_PID_ABSENT`, new Store A PID and Login confirmation, unchanged Store B identity/GUI, ETL SHA-256 `DF08062C04E37BFAD920A2405713DA59DC6E17AD63A673F65C9F22340FB7560C`, `lostEvents=0` and `skippedEvents=0` in the bounded report.
- The first sandboxed full run reported 990/992 because the sandbox denied local named-pipe client access; a minimal pipe reproduced the same denial. Outside the sandbox, both affected tests passed before deterministic harness cleanup. This is environment evidence, not an application runtime failure.

## 1. Metadata

- Document: `TEST-BASELINE.md`.
- Purpose: preserve the accepted verification baseline, its evidence boundary and mandatory revalidation conditions without presenting historical evidence as a fresh run.
- RepositoryRoot: `D:\Projects_1\POS_Enterprise_DotNet`.
- Solution: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`.
- Branch: `main`.
- CapturedAtLocal: `2026-07-31T14:32:44.841+07:00`.
- Context Pack baseline HEAD: `70523861949aeb5eefe981633db33f50bc890145`.
- Live HEAD after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- Live origin/main after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- R0.5 formal closeout commit: `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- R1.1 formal repository closeout commit: `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`.
- Ahead/behind: `0/0`.
- Baseline commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Baseline relationship: R0 historical baseline is at `e330b616`; R0.5 Context Pack and its historical verification evidence are at `7052386`; live pre-closeout Git is at `afdda252`. These commits are not interchangeable.
- Baseline type: Accepted Historical R0 Baseline.
- Current checkpoint: R1.3/R1 Project Memory formal closeout preparation; R1.1 and R1.2 are Closed / Committed / Pushed / Git-clean.
- R0.5D execution policy: read-only evidence audit; restore, build, tests, Quality Gate, EF commands, migrations, database access and application execution were prohibited.
- RuntimeExecutedInR0.5D: No.
- DatabaseReadInR0.5D: No.

## 2. Evidence concepts

### A. Accepted Historical Baseline

A result that was executed and accepted before R0.5D and is retained by authoritative Project Memory at a specific commit. It can be cited as historical evidence, but not as a fresh run.

### B. Current R0.5D Observation

A read-only review of live Project Memory, Git metadata, source, test project and Quality Gate script. R0.5D did not execute any gate and therefore cannot create a new runtime baseline.

### C. Required Revalidation

The execution required by a later authorized checkpoint before its exit, commit, release or after a listed trigger. Historical PASS does not waive a checkpoint contract.

These concepts must remain distinct.

## 3. Accepted R0 baseline

The live Project Memory consistently records:

- Build: PASS.
- Warnings: 0.
- Errors: 0.
- Full automated tests: 969/969 PASS.
- Failed: 0.
- Skipped: 0.
- Quality Gate: PASS.
- EF pending-model check: PASS; Quality Gate did not use `-SkipEfCheck`.
- Manual acceptance: PASS for authoritative R0 closeout.

Evidence:

- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R0 completion evidence.
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` — accepted R0 baseline.
- `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` — live stage implementation at the same baseline/current commit.
- Git commit `e330b616b277bde3bed2a46e71fe511cb4531ce8` — `feat(checkout): add controlled discounts and durable VietQR recovery`.

The exact gate execution timestamp is **Unknown / Not recorded**. The Git commit time is not treated as the gate execution time. No `.trx`, test-result or gate-log artifact was found in the live repository audit. No contradictory test-result evidence was found. The 969 count was not reconstructed from static source and was not rerun in R0.5D.

## 3B. Current R0.5 verification evidence

The following build/test/gate/manual results were supplied as completed live-run evidence and reconciled with the current Git history. They were not rerun in this documentation/exporter turn; the exporter was rerun below.

- Current verification commit: `70523861949aeb5eefe981633db33f50bc890145`.
- Sequential build: PASS; 0 warnings; 0 errors.
- Full automated tests: 975/975 PASS; 0 failed.
- Quality Gate: PASS; Restore, Build, Tests, vulnerability scan, local tools restore, EF pending-model check and Git whitespace check passed; exit code 0; no `-SkipEfCheck`.
- VietQR manual acceptance: PASS; Presented PaymentIntent persistence was verified before the QR dialog.
- R0.5E exporter: syntax PASS; latest pack exit code `0`; secret scan `0 findings`; source coverage `501/501` (100%); manifest/hash/integrity/exclusion checks PASS. The former credential value is not copied into this baseline.

## 3C. R0.5E and R0.5F acceptance evidence

- Context Pack: `project-context-20260801T0647171300576Z`.
- Context Pack baseline: `70523861949aeb5eefe981633db33f50bc890145`.
- Exporter exit code: `0`; coverage `501/501` (100%); security findings `0`; excluded candidates `0`; manifest integrity `16/16` PASS across the 16 non-manifest artifacts.
- ChatGPT fresh-session verification: PASS on `2026-08-01`. Provenance: user-supplied fresh-session transcript and manifest follow-up. The session identified repository, solution, architecture, pack state, working rules, R1/R1.1 sequence and deferred boundaries without claiming R0.5 closeout.
- Codex fresh-session verification: PASS on `2026-08-01`. Provenance: independent fresh-session report against Project Memory and live Git at `afdda252ce124413b9190607a96a0046cf5097e7`; it reported the `7052386`/`afdda252` distinction and the KNOWN-ISSUES recount discrepancy without modifying or staging files.
- The Context Pack predates both fresh-session checks; absence of their later results from the historical pack is expected and is not an integrity failure.

## 3D. Supplied R1.1 runtime evidence boundary

- Jenkins-verified commit: `afdda252ce124413b9190607a96a0046cf5097e7`.
- R1.1 runtime E2E: PASS from user-supplied evidence: correct SCM checkout, Windows agent, .NET SDK `10.0.302`, Release build with 0 warnings/0 errors, 975 passed/0 failed/0 skipped, Quality Gate PASS, vulnerability scan PASS, EF pending-model PASS, intentional exit `23` propagated pipeline FAILURE with later stages skipped, and final normal rerun SUCCESS.
- The intentional replay probe is not repository Jenkinsfile content. R1.1 repository closeout was the next repository checkpoint for this evidence and is now Closed / Committed / Pushed / Git-clean; R1.2 is Closed / Committed / Pushed / Git-clean and R1.3 is the current implementation/staged-review checkpoint.
- This R1.1 evidence does not replace R0.5F evidence or change Context Pack baseline `7052386`.

## 3E. Final R0.5 closeout verification — 2026-08-01

- Live pre-closeout HEAD/origin/main: `afdda252ce124413b9190607a96a0046cf5097e7` / `afdda252ce124413b9190607a96a0046cf5097e7`.
- `git diff --check`: PASS; only the informational LF-to-CRLF warning for the tracked bootstrap script was emitted.
- Explicit restore: PASS; all projects up-to-date.
- Explicit Release build: PASS; 0 warnings, 0 errors.
- Explicit Release full tests: 975 passed, 0 failed, 0 skipped.
- First sandboxed Quality Gate invocation: restore/build/tests PASS, then the network-dependent vulnerability command exited `1` without a package result; EF and later stages did not run in that attempt. The identical direct vulnerability scan passed outside the sandbox with NuGet access.
- Accepted complete Quality Gate rerun outside the sandbox: PASS, exit code `0`, no `-SkipEfCheck`; Debug build 0 warnings/0 errors; 975 passed/0 failed/0 skipped; no vulnerable packages; local tool restore PASS; EF reports no changes since the last migration; Git whitespace/status checks PASS.
- Jenkins safeguards: intentional R1.1 failure-propagation probe absent; Jenkinsfile local diff empty.
- No database update, real database read, bootstrap execution or Context Pack export was performed.

## 3F. R1.1 repository closeout reconciliation — 2026-08-01

- Entry criterion from R0.5 is satisfied by R0.5 closeout commit/push `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; this is distinct from the Jenkins runtime evidence commit.
- R1.1 is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`.
- Runtime evidence remains attributed to `afdda252ce124413b9190607a96a0046cf5097e7`; the R0.5 Context Pack baseline remains `70523861949aeb5eefe981633db33f50bc890145`.
- The accepted Jenkins evidence covers SCM checkout, Windows agent, .NET SDK `10.0.302`, Release build with 0 warnings/0 errors, 975/975 tests, Quality Gate, vulnerability scan, EF pending-model check, failure propagation and final normal rerun.
- Manual UI acceptance for this docs-only repository-closeout payload: N/A. This does not replace the accepted Jenkins runtime evidence.
- R1 remains In Progress. R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. R1.3 CI Artifacts is implemented and locally verified; the live Jenkins verification below is now PASS on the exact pushed revision, while formal R1 closeout still requires the Project Memory closeout commit/push and Git-clean verification.
- Final local verification in this R1.1 turn: explicit restore PASS; explicit Release build PASS with 0 warnings/0 errors; explicit Release full tests PASS with 975 passed, 0 failed and 0 skipped.
- Quality Gate first sandbox attempt reached restore/build/tests but the network-dependent vulnerability scan exited `1`; the complete rerun with NuGet access exited `0`, without `-SkipEfCheck`, and passed dependency scan, local tool restore, EF pending-model check, Git whitespace and Git status checks. Its internal build had 0 warnings/0 errors and tests had 975 passed, 0 failed and 0 skipped.
- Replay-probe check returned `rg` exit `1` with no match, which is the required absent result. `git diff -- Jenkinsfile` was empty.

## 3G. R1.2 repository standards verification — 2026-08-01

- SDK: `dotnet --version` returned `10.0.302`, matching `global.json`; no SDK update was performed.
- Repository standards: `.gitattributes` sets automatic text detection with LF checkout policy and explicit binary database/image patterns; `.editorconfig` sets UTF-8, LF, final newline and stable indentation; `.gitignore` adds `/_audit_temp/`; `CHANGELOG.md` establishes the Unreleased/dated release convention.
- Existing build metadata: `Directory.Build.props` already provides deterministic builds and conditional `ContinuousIntegrationBuild`; it was retained without inventing a product version.
- Restore: PASS; all projects up-to-date.
- Release build: PASS, 0 warnings and 0 errors.
- Release full tests: PASS, 975 passed, 0 failed, 0 skipped.
- Quality Gate: PASS, exit code `0`, without `-SkipEfCheck`; Debug build 0 warnings/0 errors, 975/975 tests, vulnerability scan PASS, local tool restore PASS, EF pending-model check PASS and Git checks PASS.
- Jenkinsfile diff: empty. Replay-probe search: absent (`rg` exit `1`).
- Manual UI acceptance: N/A; this payload changes repository metadata and Project Memory only.
- R1.2 formal repository baseline is closed at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. The statements in this historical section predate the live R1.3 run recorded below.

## 3H. R1.3 CI artifact local verification — 2026-08-01

- SDK: `10.0.302`.
- Restore: PASS. Release build: PASS, 0 warnings and 0 errors.
- Full Release tests: PASS, 975 passed, 0 failed, 0 skipped. TRX: `_ci_artifacts/test-results/POS.Architecture.Tests.trx`, 1,417,961 bytes. Semantic counters: total `975`, passed `975`, executed `975`, failed `0`, error `0`, timeout `0`, aborted `0`, inconclusive `0`, notExecuted `0`; the `skipped` attribute was not present.
- Vulnerability JSON: 775 bytes, parse and semantic validation PASS, five solution projects, no vulnerable package.
- Logs before cleanup: `restore.log` 724 bytes; `build-release.log` 861 bytes; `quality-gate.log` 3,896 bytes; `publish-win-x64.log` 1,346 bytes. All non-empty and console-visible through the helper.
- Native publish: 50 files, 11,807,483 bytes; required root files are `POS.Enterprise.exe` (162,304), `POS.Enterprise.dll` (703,488), `POS.Enterprise.deps.json` (39,586) and `POS.Enterprise.runtimeconfig.json` (588). `appsettings.json` is 829 bytes; PDB count `0`; database/backup-like denylist count `0`; allowlist/denylist validation PASS.
- Contract artifact total: 13,233,046 bytes. `failure-probe.log` was 47 bytes only during the controlled probe, is excluded from the contract total and was deleted afterward.
- Failure propagation: controlled `cmd.exe /c exit 23` probe returned exact exit code `23`. Generated `_ci_artifacts` was deleted safely at the exact root; it is not staged.
- Local manual UI acceptance: N/A; this local checkpoint changed CI/repository metadata only.

## 3I. R1.3 live Jenkins verification and artifact smoke test — 2026-08-02

Evidence source: user-supplied live Jenkins evidence; this preparation turn did not rerun the pipeline.

- Job: `POS_ENTERPRISE_R1_1_CI`.
- Build: `#5`; URL: `http://localhost:8080/job/POS_ENTERPRISE_R1_1_CI/5/`.
- Result: `SUCCESS`.
- SCM revision: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- Release build: 0 warnings, 0 errors.
- Tests: total `975`, executed `975`, passed `975`, failed `0`, skipped/notExecuted `0`.
- Quality Gate: `PASS`.
- Vulnerability scan: `PASS`.
- EF pending model changes: none.
- Publish win-x64: `PASS`.
- Artifact contract validator: `PASS`.
- Archive/fingerprint: `PASS`.
- Publish: `50` files, `11,807,483` bytes.
- Complete archived contract: `56` files, `13,291,155` bytes.
- Artifact ZIP SHA-256: `38adf83096b23b19b3e17ee5fc143025cbf15bcbbb12cdb688efe160414c848d`.

Manual artifact smoke test: downloaded the Jenkins ZIP, extracted it completely, and opened `POS.Enterprise.exe`; basic operation was stable. The test used an existing Windows user profile. Clean Windows profile/clean-machine first-run behavior remains `Not Revalidated`; customer clean-install acceptance is not claimed. The archived `appsettings.json` contains blank values for `Payment.BankBin`, `Payment.AccountNumber`, `Payment.AccountName` and `Infrastructure.DefaultAdminPassword`. The ZIP contains no database. VietQR recipient values that reappeared after extraction came from configuration previously persisted under the existing Windows user profile, not from the ZIP. No actual bank, account-holder, account, password or QR values are recorded here.

Forgot password and change password are not implemented. Store/VietQR first-run setup and account/password-management gaps belong to R4 under the current Master Roadmap. R1.3/R1 remains formally open until the Project Memory closeout commit/push and final Git-clean verification; R2 remains Not Started.

## 4. Baseline result table

| Gate | Accepted result | Passed | Failed | Skipped | Warnings | Errors | EF pending-model result | Baseline commit | Evidence time | Evidence source | R0.5D run status | Revalidation requirement | Notes |
|---|---|---:|---:|---:|---:|---:|---|---|---|---|---|---|---|
| Restore | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT RECORDED | 0 implied by successful gate stage | NOT APPLICABLE | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`; `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` | NOT RUN — prohibited by R0.5D scope | R0.5F or any checkpoint requiring restore/gate execution | Quality Gate stage 1 restores the solution; no separate historical restore log is retained. |
| Rebuild/build | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | 0 | 0 | NOT APPLICABLE | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` | NOT RUN — prohibited by R0.5D scope | Rebuild before authorized checkpoint exit | Accepted result does not retain the exact historical standalone command. |
| Full automated tests | HISTORICAL PASS — NOT RERUN IN R0.5D | 969 | 0 | 0 | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` | NOT RUN — prohibited by R0.5D scope | Full suite before authorized checkpoint exit | Static source scan is not a replacement for `dotnet test`. |
| Quality Gate | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | Build stage accepted as 0 | Build stage accepted as 0 | PASS | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`; `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` | NOT RUN — prohibited by R0.5D scope | Run without `-SkipEfCheck` at R0.5F and other required closeouts | Historical PASS includes the live script stages at the same commit; no persisted gate log was found. |
| EF pending-model check | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | 0 implied by successful command | PASS | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`; `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` | NOT RUN — prohibited by R0.5D scope | Mandatory after model/migration change and at required closeouts | A PASS does not prove any migration was applied to a real database. |
| Manual acceptance — R0 | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | 0 recorded | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` | NOT RUN — prohibited by R0.5D scope | New manual acceptance is required when a checkpoint contract says so | Detailed scenario record, tester, environment and time are not retained in current Project Memory. |
| WPF smoke test | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT RECORDED | Unknown / Not recorded | No separate repository evidence found | NOT RUN — prohibited by R0.5D scope | Required when UI/composition/runtime behavior changes or checkpoint requires it | R0 manual acceptance must not be relabeled as a separately evidenced WPF smoke test. |
| Hardware acceptance | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT RECORDED | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R11 is Not Started | NOT RUN — prohibited by R0.5D scope | R11 manual acceptance | No printer, scanner, label printer or cash-drawer PASS is claimed. |
| Database migration/application | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT RECORDED | NOT RECORDED | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` | NOT RUN — prohibited by R0.5D scope | Authorized migration/restore checkpoint only | Source/model checks are distinct from real database applied state. |
| Package vulnerability scan | HISTORICAL PASS — NOT RERUN IN R0.5D | NOT RECORDED | 0 vulnerable identifiers implied by stage PASS | NOT APPLICABLE | NOT APPLICABLE | 0 gate errors | NOT APPLICABLE | `e330b616b277bde3bed2a46e71fe511cb4531ce8` | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`; `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` | NOT RUN — prohibited by R0.5D scope | Re-run with Quality Gate after package/project changes and at closeout | This is package vulnerability evidence only. |
| Comprehensive security/privacy check | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT RECORDED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT RECORDED | Unknown / Not recorded | `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md` — `INV-SECURITY-001`, `INV-SECURITY-002` | NOT RUN — prohibited by R0.5D scope | R0.5F secret scan and relevant future security gates | Package scanning does not prove global log/output redaction or absence of all secrets. |

## 5. Canonical verification commands

These commands are templates for an authorized checkpoint. They were not executed in R0.5D.

```powershell
Set-Location "D:\Projects_1\POS_Enterprise_DotNet"

git diff --check

dotnet build "D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx" -t:Rebuild --no-restore -m:1 -nr:false -p:BuildInParallel=false

dotnet test "D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx" --no-build --no-restore -m:1 -nr:false -p:BuildInParallel=false

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1"
```

The Quality Gate performs its own restore. A separate restore command is used only when the checkpoint or environment explicitly requires it. A closeout that requires the EF check must not pass `-SkipEfCheck`.

## 6. Live Quality Gate inventory

Evidence script: `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1`.

| Stage | Command or implementation | Failure condition | Evidence produced | Pipeline blocking | Notes |
|---|---|---|---|---|---|
| 1. Restore solution | `Invoke-DotNetStep` → `dotnet restore <solution> --verbosity minimal -p:RestoreBuildInParallel=false` | Non-zero exit code | Console output and exit code | Yes | Always runs. |
| 2. Build solution | `Invoke-DotNetStep` → `dotnet build <solution> --no-restore -m:1 -nr:false -p:BuildInParallel=false` | Non-zero exit code | Console output and exit code | Yes | Serialized MSBuild graph. |
| 3. Run automated tests | `Invoke-DotNetStep` → `dotnet test <solution> --no-build --no-restore -m:1 -nr:false -p:BuildInParallel=false` | Non-zero exit code | Console test summary and exit code | Yes | Serialized solution test invocation. |
| 4. Scan vulnerable packages | `dotnet list <solution> package --vulnerable --include-transitive` | Non-zero exit code or output matches a GHSA/CVE identifier | Console package scan output | Yes | Does not replace a comprehensive privacy/secret review. |
| 5. Restore local tools | `Invoke-DotNetStep` → `dotnet tool restore` | Non-zero exit code | Console output and exit code | Yes | Runs only when `-SkipEfCheck` is absent. |
| 6. Check pending EF model changes | `dotnet ef migrations has-pending-model-changes --project <Infrastructure project> --startup-project <WPF project>` | Non-zero exit code | Console EF result and exit code | Yes | Runs only when `-SkipEfCheck` is absent; does not read or prove real migration applied state. |
| 7. Check Git whitespace | `git diff --check` | Non-zero exit code | Console output and exit code | Yes | Checks unstaged tracked diff; untracked file content still requires direct review. |
| 8. Git status | `git status --short` | Non-zero exit code | Console status | Yes | Reports state; the script does not require a clean tree. |

The script writes console output only; it does not create a persisted test, vulnerability or gate report artifact. Its final success sentence is unconditional, so an invocation with `-SkipEfCheck` must never be cited as evidence that the EF model check passed.

## 7. Test inventory boundary

- Test project: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\POS.Architecture.Tests.csproj`.
- Framework: xUnit v3 (`xunit.v3`) with `Microsoft.NET.Test.Sdk` and Visual Studio runner versions managed in `D:\Projects_1\POS_Enterprise_DotNet\Directory.Packages.props`.
- Target/output: `net10.0-windows`, executable test project.
- Solution membership: one test project is listed in `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`.
- Source categories present: dependency architecture, domain rules, Application authorization/use cases, EF/SQLite persistence and migration, concurrency/recovery, WPF UI contracts, printing and VietQR infrastructure.

Direct critical-flow test source includes:

- Checkout idempotency/restart/concurrency: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs`.
- PaymentIntent confirmation/recovery/concurrency: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentConcurrencyTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentRecoveryActionTests.cs`.
- Held-sale persistence/resume/ownership: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs`.
- Discount money/audit behavior: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs`.
- Return persistence/idempotency: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnApplicationTests.cs`.
- Receipt snapshot/serialization/print boundaries: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptPrinterPipelineTests.cs`.
- Migration/backup safety: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentMigrationTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs`.
- Authentication/RBAC: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthServiceIntegrationTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PermissionServiceTests.cs`, `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthorizedCheckoutServiceTests.cs`.

All listed tests are source evidence only in R0.5D. They were not executed. The historical 969 count comes from the accepted baseline, not from counting attributes, methods or files.

Manual or hardware scenarios outside the automated baseline include real scanner input, physical K80/label printing, cash-drawer behavior, display/DPI matrix, production-scale performance, installer/upgrade/rollback and store pilot operations.

## 8. Baseline limitations

The accepted automated baseline does not by itself prove:

- a physical printer works;
- a real scanner works;
- a real cash drawer works;
- VietQR was automatically confirmed by a bank;
- an installer or production upgrade passed;
- restore on real store data passed;
- a store pilot passed;
- a new manual WPF acceptance passed;
- production-scale performance passed;
- a migration was applied to a real database;
- universal log/support-output redaction passed.

## 9. Mandatory revalidation triggers

Revalidate the applicable gates when:

- production source changes;
- test source changes;
- project or package configuration changes;
- `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1` changes;
- a migration or ModelSnapshot changes;
- a transaction boundary changes;
- pricing, discount, return or payment behavior changes;
- authentication or RBAC changes;
- receipt, persistence or recovery behavior changes;
- .NET SDK/runtime changes;
- a checkpoint approaches exit;
- a checkpoint contract requires verification before commit/push;
- a release candidate is prepared;
- a merge or conflict resolution occurs;
- baseline evidence and current HEAD are no longer the same commit;
- a known issue closure criterion requires new evidence.

Both R0.5F fresh-session checks PASS. R0.5 was Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`. R1.1 and R1.2 are closed at their corresponding commits. R1.3 live Jenkins verification is PASS at build #5 on `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`; Project Memory closeout commit/push and Git-clean verification remain pending before formal R1 closeout. No pipeline was rerun in this documentation turn.

## 10. Future failure handling

If a gate fails in an authorized future checkpoint:

1. Stop the checkpoint.
2. Do not commit or push.
3. Record the exact command, exit code and failure output after sanitization.
4. Determine whether the failure is new or part of an accepted baseline.
5. Do not delete or weaken tests to make the gate green.
6. Do not lower an assertion without business evidence.
7. Fix only within checkpoint scope.
8. Re-run from the appropriate preceding gate.
9. Update `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md` if the issue cannot be resolved.
10. Change this baseline only after new evidence is executed, reviewed and accepted.

## 11. R0.5D execution statement

R0.5D did not run restore, build, tests, Quality Gate, EF commands, migration, database operations, WPF or the application. Every PASS in this document is explicitly historical and tied to the accepted R0 baseline.
