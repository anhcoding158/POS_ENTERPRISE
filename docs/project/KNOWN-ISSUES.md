# KNOWN ISSUES — POS ENTERPRISE RETAIL V1

## R4.1 isolated-startup hotfix closeout — 2026-08-26

- The post-closeout manual smoke blocker is repaired. The failure was not a corrupt Store Setup file, unsafe path, printer enumeration or readiness error; it was an incomplete pre-startup DI composition without logging services.
- The hotfix keeps the generic user-facing startup error and adds only sanitized IsolatedTest diagnostics under the owned scenario database directory. No raw stack trace, connection string, credential, bank value or user data is written.
- The launcher now requires an already-built Release/Debug `POS.Enterprise.exe`, starts that exact executable, and reports effective Store Setup and managed-logo locations. It no longer routes acceptance through `dotnet run`/MSBuild.
- The current execution desktop did not expose a Win32 top-level window handle to the observer, so visual UIA is not claimed. Production milestones reached `LoginWindowConstructed` and `LoginWindowReady`; deterministic composition, persistence and UI contract tests remain the authority for the UI surface.

## R4.1 closeout limitations — 2026-08-26

- Store Setup is closed with typed validation, persistence and production consumers. A valid printer is discovered/tested through the production abstraction, but this machine had no approved physical printer target; no print-success claim is made.
- Scanner and cash-drawer settings are typed and readiness-validated. Hardware actuation is not implemented in this checkpoint, so unsupported/unconfigured capability is reported honestly.
- Database-directory changes are persisted as a safe pending/restart-required configuration. Active DbContext relocation in place is deliberately not claimed.
- The native logo picker and desktop UI Automation were not used as the acceptance authority; the owner-correct picker seam, ViewModel contract, managed-logo service and isolated runtime start were verified deterministically.

## R3.4 closeout note — 2026-08-26

- No confirmed R3.4 production defect remains. The real isolated backup → new database → external-worker restore → restart → sign-in → Orders/stock/receipt verification → integrity drill passed, and the canonical database/root state remained exact.
- A restore-test fixture used the three-argument `File.Replace` overload, which raised a Windows metadata portability error on the drill host. The fixture now uses `ignoreMetadataErrors: true`, matching production behavior; assertions and restore safety contracts are unchanged. The focused suite is `142/142` PASS and the official Quality Gate is `1317/1317` PASS with zero failures/skips.
- Two single-instance Named Pipe tests were denied only inside the restricted Codex sandbox and passed `2/2` under the normal Windows token; the final official Quality Gate was therefore run outside that IPC restriction and passed. Production's current-user-only ACL was not weakened.
- The R3.4 prerequisite blocker for pilot is removed. R3 and R4.1 are CLOSED; R4.2 Employee and Account UI is READY TO START. The RST14 combined-evidence limitation remains recorded below and was not rewritten by R3.4.

## R3.3 closeout note — 2026-08-25

- No confirmed R3.3 production or automated-regression defect remains after restore-targeted `89/89`, backup/restore UI `89/89`, Release build `0/0`, full Release `1317/1317`, vulnerability, EF and official Quality Gate PASS.
- RST14 is accepted by combined runtime, durable-state and automated contract evidence. Independent external UIA/PID-timeline evidence is incomplete because the Codex execution desktop did not expose a same-PID WPF root even though the parent process remained healthy for the full bounded readiness period. This limitation is explicit; it is not classified as a production Restore failure or as an external timeline PASS.
- All temporary observer/UIA harness development is retired. R3.3 is Closed / Committed / Pushed at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`. This dated R3.3 closeout position is superseded by the R3.4 closeout note above.
- The WAL checkpoint/hash production defect and the OpenFileDialog automation defect were corrected with regression coverage. RST7 real regular-file reparse handling is verified. RST15 confirms the canonical database and managed roots remained unchanged throughout isolated acceptance.

## R3.2 closeout note — 2026-08-23

- No confirmed R3.2 product defect remains after targeted `76/76`, official Quality Gate `1240/1240`, and ABM1–ABM6 acceptance. Runtime machine evidence used isolated `%TEMP%` resources; canonical database/root were unchanged.
- Store Setup configurability remains R4.1 and end-of-day backup remains R9; neither is an R3.2 defect. **Historical R3.2 closeout status on 2026-08-23:** R3.3 Restore Wizard was NOT STARTED; the current status is recorded in the newer R3.3 section above.

## R3.1 closeout note — 2026-08-09

- The full R3.1 implementation audit, official Quality Gate (`1183/1183`, `0` failed, `0` skipped), and isolated MB1 manual acceptance are PASS. The exact final artifact independently matched the UI path, `372736`-byte length and SHA-256, the fixture was unchanged, and no partial/temp or second destination entry remained.
- Product source and executed regression evidence prove that integrity and schema compatibility are checked before `ManualBackupResult.Success`, with positive length and SHA-256 required. The separate arbitrary-artifact SQLite probe remains **NOT RUN**; absence of a command-line probe is not classified as a product defect.
- No new confirmed R3.1 defect or known issue was established by the closeout audit. No official MB2 contract exists. R3.2 Automatic Backup and Retention remains **NOT STARTED**; no existing issue ID is closed or renumbered by this note.

## R2.4D closeout note — 2026-08-09

- Automated database runtime hardening is PASS at the current working tree, including source/test/publish path matrix, stale override blocking, isolated-mode validation, startup ordering and metadata-only diagnostics.
- Manual acceptance M1–M5 and the M6 no-open/no-modify filesystem/hash audit passed on 2026-08-09. The earlier duplicate M1 launch was discarded and the clean single-instance rerun was accepted.
- The Windows PowerShell `Collection was of a fixed size` script failure was reproduced in the earlier helper path, traced to using the wrong/non-mutable `ProcessStartInfo` environment collection, corrected in both start scripts, and not reproduced by the clean M1/M3 launches.
- R2.4D is Completed Locally; it is not yet Closed/Committed/Pushed because the current turn explicitly forbids those Git actions.

## R2.4C closeout note — 2026-08-08

- R2.4C authenticated storage status UI passed independent review, automated re-verification (`18/18` targeted; `166/166` combined), Release build, outside-sandbox Quality Gate `1142/1142`, vulnerability scan, EF pending-model check and manual acceptance.
- No confirmed R2.4C runtime defect was established. The UI reuses the production storage monitor, keeps startup-preflight authority in R2.4B, and adds no backup/restore/cleanup/delete behavior.
- A separate authentication incident remains an investigation item; its root cause is not confirmed and it does not invalidate R2.4C acceptance.
- R2.4 remains IN PROGRESS; R2.4D is the next checkpoint. No existing issue ID is closed or renumbered by this note.

## R2.4B verification note — 2026-08-08

- Independent review found and corrected two in-scope fail-safe defects: unknown preflight enum values could proceed, and an existing database with unavailable footprint metadata could be evaluated as requiring zero additional bytes. Regression tests now prove both paths stop before backup/schema mutation.
- No confirmed R2.4B runtime defect remains after targeted `15/15`, combined `99/99`, Release build and outside-sandbox Quality Gate `1124/1124` all passed; vulnerability and EF pending-model checks passed.
- The sandbox-only two R2.1 named-pipe failures recurred without production IPC changes. R2.4 remains IN PROGRESS; R2.4C/D, storage UI, backup/restore and retention remain outstanding. No existing issue ID is closed or renumbered by this note.

## R2.4A verification note — 2026-08-08

- No confirmed R2.4A runtime defect remains after independent review. Ambiguous existence probes, parent-reparse revalidation, cancellation-between-reads coverage and derived preflight `CanProceed` semantics were corrected within the checkpoint.
- Typed metadata-only snapshots and preflight policy passed targeted `89/89`, Release build and full Quality Gate `1114/1114`; vulnerability and EF pending-model checks passed.
- R2.4 remains IN PROGRESS. R2.4B startup/presentation integration is NOT STARTED; backup/restore and cleanup remain outside R2.4A. No existing issue ID is closed or renumbered by this note.

## R2.3C verification note — 2026-08-08

- No new confirmed runtime defect was established. R2.3A/B/C have automated verification PASS; R2.3C manual acceptance and R2.3D remain outstanding, so R2.3 stays IN PROGRESS.
- The authenticated Support Bundle modal has explicit consent, fixed database exclusion, safe typed presentation, single-flight cancellation and deferred-close automated coverage.
- `POS-VER-003` remains open only for manual R2.3D verification and future report/export surfaces beyond the automated R2.3A/B/C boundaries.

## R2.3B verification note — 2026-08-08

- R2.3 remains IN PROGRESS. R2.3A/B/C have automated verification PASS; R2.3C manual acceptance and R2.3D remain outstanding.
- Safe Support Bundle composition/export now has typed fail-closed outcomes, fixed allow-listed diagnostics, database exclusion, bounded shared-policy log export, atomic no-overwrite commit and cancellation/temp ownership coverage.
- `POS-VER-003` remains open only for R2.3D manual verification and future report/export surfaces outside the automated R2.3A/B/C boundaries; no confirmed disclosure was found.
- The sandbox-only R2.1 named-pipe failures recurred as expected; the complete outside-sandbox Quality Gate passed 1049/1049 without changing IPC.

## R2.3A verification note — 2026-08-08

- R2.3 is IN PROGRESS. R2.3A Safe Logging Foundation has automated verification PASS; no new confirmed runtime defect was found.
- File rotation, retention/quota, cleanup boundary, sensitive-data redaction, SQLite safe diagnostics, concurrency, flush, I/O failure containment, invalid options and idempotent DI registration are automated-covered.
- `POS-VER-003` remains open only for the not-yet-built Support Bundle/report/export boundary. It no longer describes the rotating application file-log foundation as lacking a direct sanitizer test.
- R2.3B/C/D, manual R2.3 acceptance and R2.4 remain outstanding.

## Corrective UX closeout note — 2026-08-08

- User manual acceptance PASS for the corrective Order History and Product List UX batches integrated on local `main`.
- Automated verification and the complete Quality Gate PASS at the integrated source commit `352d2814ba8b194c8abf875852462f16f05153d4`.
- No new confirmed runtime defect or verification gap was established by these two corrective batches. Existing stable issue records and the R2.3/R2.4 deferred boundaries remain unchanged.
- A runtime database was detected by file metadata only in the retained Product worktree; its rows were not opened or inspected, and the worktree must not be removed automatically.

## R2.2 closeout note — 2026-08-06

- R2.2 — SQLite Busy/Locked UX is COMPLETE/CLOSED.
- Acceptance Test A: Manual PASS.
- Acceptance Tests B/C/D: NOT MANUALLY RUN; covered by equivalent deterministic automated acceptance PASS. They are not recorded as Manual PASS.
- No confirmed R2.2 runtime defect remains open from this acceptance. Residual scope is explicit: R2.3 Logging and Support Bundle is NOT STARTED; R2.4 disk-space monitoring/database growth is NOT STARTED; automatic corruption repair/restore remains outside R2.2.

## 1. Metadata

- Document: `KNOWN-ISSUES.md`.
- Purpose: sổ đăng ký tập trung cho vấn đề vận hành hiện tại, giới hạn đã biết, verification gap và capability được hoãn; ngăn việc quên issue, nhầm roadmap gap thành runtime bug hoặc tuyên bố resolved khi chưa có acceptance evidence.
- RepositoryRoot: `D:\Projects_1\POS_Enterprise_DotNet`.
- Solution: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`.
- Branch: `main`.
- CapturedAtLocal: `2026-07-31T14:32:44.841+07:00`.
- Context Pack baseline HEAD: `70523861949aeb5eefe981633db33f50bc890145`.
- Live HEAD after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- Live origin/main after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- Ahead/behind: `0/0`.
- EvidenceMode: Read-only source/Project Memory audit plus user-supplied live Jenkins and artifact-smoke evidence.
- RuntimeExecutedInR0.5D: No.
- DatabaseReadInR0.5D: No.
- ExporterExecutedInR0.5E: Yes; latest pack exit code `0`, secret scan `0 findings`, coverage `501/501`, manifest/integrity/exclusion checks passed.
- DatabaseReadInR0.5E: No.
- CurrentCheckpoint: R2.4 Disk Space and Database Growth IN PROGRESS; R2.4A COMPLETED; R2.4B NOT STARTED. R2.3 is COMPLETED.
- Scope: inventory evidence-supported operating conditions only; R0.5D did not reproduce runtime failures, run gates, open a real database, inspect database rows or perform hardware/store acceptance.

Source-of-truth order:

1. Live repository source.
2. Live Git state and history.
3. Existing Project Memory.
4. Live production source, tests, migrations, configuration and scripts.
5. Historical evidence retained in the repository.
6. Older reports as hints only.

No confirmed open runtime defect was established by the R0.5D read-only audit.

## 2. Taxonomy

- **Confirmed Defect:** current behavior is proven by evidence to violate expected behavior. A source gap or future roadmap item alone does not qualify.
- **Known Operational Limitation:** known, currently accepted behavior has a real operating boundary. It is not automatically a defect.
- **Verification Gap:** implementation or policy may exist, but automated, manual, hardware or production-like evidence is insufficient.
- **Deferred Roadmap Capability:** capability belongs to a future R1–R13 checkpoint and is not part of the accepted current baseline. It is not called a bug without observed incorrect behavior.
- **Resolved Historical Issue:** retained only when closure history is operationally necessary and supported by closure evidence. Ordinary completed changes are not copied here.

## 3. Status vocabulary

- **Open:** evidence-supported gap requires action or an explicit disposition.
- **Monitoring:** accepted boundary remains active and must be considered during operation or future changes.
- **Deferred:** work belongs to a named future roadmap checkpoint.
- **Resolved:** closure criteria and acceptance evidence exist.
- **Not Revalidated:** condition is known from source/Project Memory, but the relevant runtime, manual, hardware or production-like check has not been repeated for the current review.

`Resolved` is prohibited without evidence. `Not Revalidated` is not equivalent to failure.

## 4. Severity vocabulary

- **Critical:** evidenced condition can cause catastrophic loss, compromise or inability to operate and requires immediate stop.
- **High:** evidenced impact is severe and materially blocks safe operation.
- **Medium:** evidenced or explicitly bounded impact affects important operational correctness or readiness.
- **Low:** limited operational impact with a narrow, understood boundary.
- **Informational:** governance/readiness record with no evidenced current runtime failure.

Severity describes supported impact, not roadmap priority. Insufficient evidence must not be used to elevate severity; it remains a revalidation need.

## 5. Active register

### POS-OPS-001 — VietQR uses cashier confirmation, not bank auto-reconciliation

- Stable ID: `POS-OPS-001`.
- Title: VietQR uses cashier confirmation, not bank auto-reconciliation.
- Classification: Known Operational Limitation.
- Status: Monitoring.
- Severity: Medium.
- Affected area: VietQR payment confirmation and reconciliation.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — `PaymentIntentService.ConfirmReceivedAsync`, `ResolveManuallyAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Payments\VietQrPaymentGateway.cs` — `VietQrPaymentGateway.Build`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Checkout_journal_is_created_only_after_manual_confirmation`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — API ngân hàng tự động đối soát is outside Retail V1.
- Observed or known condition: source implements QR payload presentation plus explicit user confirmation and manual resolution; no source evidence establishes automatic bank settlement confirmation.
- Expected condition or intended boundary: Retail V1 must describe this as manual confirmation and must not claim bank API confirmation.
- User/business impact: cashier verification remains part of the payment workflow; automatic settlement certainty is not provided by the current boundary.
- Trigger or reproduction precondition: source-defined condition when completing a VietQR payment; no runtime reproduction was performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-PAYMENT-001` to `INV-PAYMENT-003`; `DEC-009`; automatic bank reconciliation is outside Retail V1.
- Owner checkpoint: no committed owner checkpoint; requires an approved roadmap change if automatic reconciliation is added.
- Closure criteria: an approved checkpoint supplies provider reconciliation, idempotency, security, failure recovery and acceptance evidence, or the manual boundary remains explicitly accepted.
- Revalidation trigger: any payment-provider integration, confirmation-flow or reconciliation-policy change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` read-only source review; runtime not executed.
- Notes: this is an accepted operating boundary, not evidence of a payment defect.

### POS-OPS-002 — Held sale is a durable snapshot, not a stock reservation

- Stable ID: `POS-OPS-002`.
- Title: Held sale is a durable snapshot, not a stock reservation.
- Classification: Known Operational Limitation.
- Status: Monitoring.
- Severity: Low.
- Affected area: held-sale resume, product availability and price review.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — `HeldSaleService.CreateHeldSaleAsync`, `GetHeldSaleForResumeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs` — `HeldSaleApplicationIntegrationTests.Create_held_sale_persists_snapshot_without_business_mutation`, `Resume_reports_price_stock_and_unavailable_without_mutation`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-010`.
- Observed or known condition: holding a cart does not reserve or reduce stock; resume revalidates active state, stock and price.
- Expected condition or intended boundary: the operator must review unavailable, insufficient-stock or changed-price lines before checkout.
- User/business impact: a held cart may not remain immediately sellable at its original price or quantity.
- Trigger or reproduction precondition: another operation changes product availability, stock or price between hold and resume; no runtime reproduction was performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-HELD-001`, `INV-HELD-002`; `DEC-010`.
- Owner checkpoint: no committed reservation checkpoint; revisit only if an approved reservation policy is added.
- Closure criteria: either the snapshot/no-reservation boundary remains accepted and correctly presented, or a new approved reservation design and acceptance evidence supersede it.
- Revalidation trigger: held-sale lifecycle, stock policy, resume UI or reservation-policy change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` read-only source review; runtime not executed.
- Notes: stock change after hold is expected under the current design and is not itself a defect.

### POS-OPS-003 — Physical receipt printing is a post-commit side effect

- Stable ID: `POS-OPS-003`.
- Title: Physical receipt printing is a post-commit side effect.
- Classification: Known Operational Limitation.
- Status: Monitoring.
- Severity: Medium.
- Affected area: checkout receipt printing and operator recovery.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\WpfReceiptService.cs` — `WpfReceiptService.PrintAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\ReceiptPreviewService.cs` — `ReceiptPreviewService`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Checkout_service_must_not_depend_on_print_service`, `Persisted_snapshot_must_not_change_when_product_changes_after_checkout`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-007`, `DEC-008`.
- Observed or known condition: sale and immutable receipt snapshot commit independently of the physical print operation; printer failure must not roll back or duplicate the sale.
- Expected condition or intended boundary: UI must report print failure separately and preserve the committed transaction for later receipt access.
- User/business impact: a sale can be successful while its first physical print is unavailable.
- Trigger or reproduction precondition: printer unavailable, offline, paused, out of paper or in error after checkout commit; hardware reproduction was not performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-CHECKOUT-002`, `INV-RECEIPT-001`, `INV-RECEIPT-002`; `DEC-007`, `DEC-008`; R11.
- Owner checkpoint: R11 — Store and Hardware Acceptance.
- Closure criteria: R11 printer scenarios prove print/reprint, offline, paper-out and post-commit failure behavior on target hardware without duplicate checkout.
- Revalidation trigger: receipt schema, print orchestration, printer configuration or checkout transaction change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` read-only source review; hardware not executed.
- Notes: source existence proves a print pipeline, not physical printer acceptance.

### POS-VER-001 — Real hardware, display and production-scale acceptance are not established

- Stable ID: `POS-VER-001`.
- Title: Real hardware, display and production-scale acceptance are not established.
- Classification: Verification Gap.
- Status: Not Revalidated.
- Severity: Medium.
- Affected area: scanner, K80 printer, label printer, cash drawer, DPI/display and production-scale performance.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md` — hardware/load boundaries; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R11 scope and manual acceptance.
- Observed or known condition: Project Memory states that real hardware and load acceptance have not been established; R0.5D did not run WPF or hardware checks.
- Expected condition or intended boundary: R11 exit criteria and its explicit manual scenarios must PASS before hardware/store readiness is claimed.
- User/business impact: current automated/source evidence cannot guarantee behavior on target peripherals, display scaling or production-scale data.
- Trigger or reproduction precondition: target hardware, display or load environment is required; no reproduction was performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-RECEIPT-002`; `DEC-008`, `DEC-018`; R11.
- Owner checkpoint: R11 — Store and Hardware Acceptance.
- Closure criteria: all R11.1–R11.6 manual acceptance scenarios have recorded environment, actual result and PASS evidence.
- Revalidation trigger: printer/scanner/cash-drawer code, UI layout, supported display matrix, performance target or deployment hardware change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` Project Memory review; no hardware execution.
- Notes: no physical-device failure was asserted.

### POS-VER-002 — Real database applied state and store-data recovery are not verified

- Stable ID: `POS-VER-002`.
- Title: Real database applied state and store-data recovery are not verified.
- Classification: Verification Gap.
- Status: Not Revalidated.
- Severity: Medium.
- Affected area: EF applied migrations, real database integrity, restore and disaster recovery.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs` — `DatabaseInitializer.InitializeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\SqliteDatabaseSafetyService.cs` — `SqliteDatabaseSafetyService.CreateVerifiedBackup`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs` — `DatabaseInitializerSafetyTests.Existing_database_with_pending_migrations_must_create_verified_backup`, `Backup_failure_must_block_migration`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md` — applied-state boundary; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R3.
- Observed or known condition: source and accepted test evidence cover migration/backup logic, but R0.5D did not inspect a database, migration history, real rows or a restore drill.
- Expected condition or intended boundary: applied state must come from authorized database evidence; R3 must prove backup, restore, rollback and disaster recovery on test data before readiness claims.
- User/business impact: source-level safety does not establish recoverability of an actual store database.
- Trigger or reproduction precondition: database upgrade, restore or disaster-recovery exercise; none was performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-MIGRATION-001`, `INV-BACKUP-001`, `INV-MIGRATION-002`; `DEC-014`, `DEC-015`; R3.
- Owner checkpoint: R3 — Backup and Restore.
- Closure criteria: authorized migration/applied-state evidence plus R3 restore and disaster-recovery acceptance PASS without reading or exporting production customer data into Project Memory.
- Revalidation trigger: migration, ModelSnapshot, database initializer, backup/restore code, SQLite version or database-path policy change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` read-only source review; database not read.
- Notes: migration source existence does not prove migration application.

### POS-VER-003 — Support-output redaction remains to be enforced

- Stable ID: `POS-VER-003`.
- Title: Remaining future support/report output redaction must preserve the shared policy.
- Classification: Verification Gap.
- Status: Open.
- Severity: Medium.
- Affected area: R2.3D manual Support Bundle verification and future reports/exported diagnostic content outside R2.3C.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Common\PosLog.cs` — centralized logging helpers; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_payload_must_not_contain_cost_price_or_known_secrets`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md` — `INV-SECURITY-002` is Partially Enforced; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — universal logging redaction has insufficient source evidence.
- Observed or known condition: R2.3A/B/C enforce automated logging, safe Support Bundle composition/export and safe UI presentation. Manual R2.3D and future report/export surfaces remain unverified.
- Expected condition or intended boundary: all logs, support/report output and exports must sanitize secrets and sensitive customer/payment values.
- User/business impact: incomplete coverage creates an unverified disclosure boundary; no actual secret disclosure was observed.
- Trigger or reproduction precondition: R2.3D manual UI/export acceptance or future report/export output beyond R2.3C.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-SECURITY-001`, `INV-SECURITY-002`; R0.5F and R2.3.
- Owner checkpoint: R0.5F for Project Memory/export scan; R2.3 for logging and Support Bundle.
- Closure criteria: repository-wide output inventory, automated sensitive-field tests and required secret scans PASS, with failures blocking commit/release as applicable.
- Revalidation trigger: any new log template, support bundle, report, export, receipt schema or secret-bearing configuration.
- Last verified base: `e8d8d20395227736072175ad147a11752f54b0b4` with uncommitted R2.3A/B/C working tree.
- Last verified time: `2026-08-08` automated R2.3A/B/C verification and full Quality Gate PASS.
- Notes: this record is a verification gap, not a confirmed leak.

### POS-VER-004 — Direct concurrent over-return regression evidence is not established

- Stable ID: `POS-VER-004`.
- Title: Direct concurrent over-return regression evidence is not established.
- Classification: Verification Gap.
- Status: Open.
- Severity: Medium.
- Affected area: concurrent order returns, refund balance and stock reversal.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — `OrderReturnService.ProcessAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnBalanceConfiguration.cs` — balance constraints/concurrency token; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Client_request_id_unique_index_must_reject_duplicate`, `Return_balance_constraints_must_reject_negative_values`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md` — `INV-RETURN-001` gap/revisit.
- Observed or known condition: source has transaction, idempotency, balance and concurrency guards, but the audited evidence does not cite a direct concurrent over-return integration test.
- Expected condition or intended boundary: concurrent requests must not over-return quantity, double-refund or double-restock.
- User/business impact: the critical concurrency behavior is source-guarded but lacks direct regression proof identified by this audit; no over-return defect was observed.
- Trigger or reproduction precondition: two overlapping return requests against the same remaining order-item balance; no runtime reproduction was performed in R0.5D.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-RETURN-001`, `INV-RETURN-002`; `DEC-013`; R9.
- Owner checkpoint: R9 — Documents, Cashbook and Daily Close.
- Closure criteria: a direct concurrency regression test proves exactly one valid business outcome, correct replay/conflict semantics and atomic stock/refund state, then required gates PASS.
- Revalidation trigger: return transaction, balance concurrency token, client-request fingerprint, stock-restock or refund allocation change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` static test/source review; tests not run.
- Notes: missing direct evidence is not proof of incorrect runtime behavior.

### POS-VER-005 — Bootstrap script contained a non-placeholder default admin password literal

- Stable ID: `POS-VER-005`.
- Title: Bootstrap script contained a non-placeholder default admin password literal.
- Classification: Verification Gap.
- Status: Resolved.
- Severity: Medium.
- Affected area: Project Context export, repository secret hygiene and bootstrap tooling.
- Evidence: `D:\Projects_1\POS_Enterprise_DotNet\Create-POS-Enterprise-Structure.DO-NOT-RUN.ps1` — authorized local remediation disables default administrator seeding and removes the fixed credential; `D:\Projects_1\POS_Enterprise_DotNet\scripts\Export-ProjectContext.ps1` — latest pack reports security finding count `0`, without recording the former value.
- Observed or known condition: the former non-placeholder literal is no longer present in the live bootstrap script; no raw value is retained in Project Memory or the Context Pack.
- Expected condition or intended boundary: no non-placeholder password literal may remain in a tracked source or bootstrap script used by a context export.
- User/business impact: a copied or reused bootstrap credential could weaken initial account security; no evidence establishes that the value was used in production.
- Trigger or reproduction precondition: run the R0.5E exporter against the current repository; no database or runtime credential was accessed.
- Workaround or recovery behavior: bootstrap script remains `DO-NOT-RUN`; no default administrator is seeded.
- Related invariant/decision/roadmap checkpoint: `INV-SECURITY-001`, `INV-SECURITY-002`; R0.5E/R0.5F; `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- Owner checkpoint: R0.5E/R0.5F security review.
- Closure criteria: authorized remediation removes the non-placeholder literal or establishes an approved non-secret input boundary; exporter exit code `0`, security finding count `0`, manifest/coverage checks PASS, and required review evidence is recorded. These criteria and R0.5F review were met and the remediation was committed in `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- Revalidation trigger: change to bootstrap scripts, exporter redaction rules, authentication setup or Project Memory export policy.
- Last verified commit/base: `dfb0eb7a000054664aa7feccb51778fe80aa32a7` (remediation and R0.5 formal closeout).
- Last verified time: `2026-08-01` latest R0.5E exporter execution; former value intentionally not retained.
- Notes: this is a source/export security verification gap, not a claim of production exposure.

### POS-ROAD-001 — R1 CI runtime is partially verified; repository closeout and later subcheckpoints remain

- Stable ID: `POS-ROAD-001`.
- Title: R1 CI runtime is partially verified; repository closeout and later subcheckpoints remain.
- Classification: Verification Gap.
- Status: In Progress.
- Severity: Informational.
- Affected area: automated push pipeline, repository standards and CI artifacts.
- Evidence: Policy/roadmap evidence at `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R1; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-018`.
- Observed or known condition: supplied Jenkins evidence establishes R1.1 runtime E2E PASS at `afdda252ce124413b9190607a96a0046cf5097e7`, including normal success and intentional failure propagation. R1.1 repository closeout is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`. R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. R1.3 live Jenkins job `POS_ENTERPRISE_R1_1_CI` build `#5` is SUCCESS on exact revision `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`, with all five artifact groups validated and the downloaded ZIP smoke-tested using an existing Windows profile. The binary-name blocker is resolved; only formal Project Memory closeout commit/push and Git-clean verification remain.
- Expected condition or intended boundary: preserve the exact Jenkins/artifact evidence, complete the Project Memory closeout commit/push, verify Git-clean, and only then close R1. R2 remains Not Started until that sequence is complete.
- User/business impact: R1 readiness is evidenced by the live run, but R1 must not be reported Closed before the required repository closeout mechanics.
- Trigger or reproduction precondition: R1.2 standards change or R1.3 CI artifact implementation/closeout.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-MIGRATION-002`; `DEC-018`, `DEC-022`; R1.
- Owner checkpoint: R1.
- Closure criteria: R1.1–R1.3 exit criteria and manual pipeline acceptance PASS.
- Revalidation trigger: start or change of R1 scope, Jenkinsfile, repository standards or CI artifact policy.
- Last verified commit: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
- Last verified time: `2026-08-02` user-supplied Jenkins build #5 and artifact smoke evidence.
- Notes: reconciled verification gap, not a runtime bug and not a claim that all of R1 is complete.

### POS-ROAD-002 — Remaining R2.3–R4 operational hardening, recovery and store administration are deferred

- Stable ID: `POS-ROAD-002`.
- Title: Remaining R2.3–R4 operational hardening, recovery and store administration are deferred.
- Classification: Deferred Roadmap Capability.
- Status: Deferred.
- Severity: Informational.
- Affected area: SQLite busy/locked UX, support bundle, disk monitoring, backup/restore, first-run store/VietQR setup, employees, accounts, password management, roles and audit UI.
- Evidence: Policy/roadmap evidence at `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R2, R3 and R4; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — decisions not reconstructed and `DEC-018`.
- Observed or known condition: R2.1 and R2.2 are COMPLETE/CLOSED; R2.3 is COMPLETED. R2.4 is IN PROGRESS with R2.4A completed and R2.4B not started; R3 and R4 remain Not Started. Forgot password and change password are not implemented. Clean-profile first-run behavior was not revalidated: the artifact has blank VietQR recipient configuration and blank default-admin-password configuration, and no database; prior VietQR values that reappeared came from an existing Windows profile’s persisted configuration. This is a readiness/verification gap, not evidence that a clean install failed.
- Expected condition or intended boundary: each stage must meet its own exit criteria and manual acceptance. R4.1 covers typed/validated store and VietQR setup; R4.2 covers employee/account workflows including password reset/management; clean-profile first-run acceptance must be performed before customer clean-install claims.
- User/business impact: foundation source cannot be promoted to operational/store-management completion.
- Trigger or reproduction precondition: entry into R2.3–R2.4, R3 or R4 after dependency stages PASS.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-BACKUP-001`, `INV-AUTH-001` to `INV-AUTH-003`, `INV-SECURITY-002`; `DEC-023`; R2.2–R4.
- Owner checkpoint: R2, R3 and R4 respectively.
- Closure criteria: every named stage meets its exit criteria, required gates and manual acceptance.
- Revalidation trigger: start of R2–R4 or any relevant reliability, database-recovery, authentication or administration change.
- Last verified base/closeout: R2.1 base `b437be5de3a3f6deb03b142c88dd913610ebc834`; R2.1 source and memory close together in the commit containing this record.
- Last verified time: `2026-08-02` R2.1 automated gates and manual Tests A/B/C/D.
- Notes: deferred capability group, not a list of observed defects.

### POS-ROAD-003 — R5–R7 product operations, supply chain and customer capability are deferred

- Stable ID: `POS-ROAD-003`.
- Title: R5–R7 product operations, supply chain and customer capability are deferred.
- Classification: Deferred Roadmap Capability.
- Status: Deferred.
- Severity: Informational.
- Affected area: product import/export/bulk/labels, suppliers/purchases/lots/expiry, customers/loyalty.
- Evidence: Policy/roadmap evidence at `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R5, R6 and R7; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-018`.
- Observed or known condition: R5, R6 and R7 are Not Started; isolated models or screens do not establish commercial-stage completion.
- Expected condition or intended boundary: implementation and acceptance follow roadmap order and stage contracts.
- User/business impact: these capabilities are not part of the accepted current product-stage baseline.
- Trigger or reproduction precondition: entry into R5, R6 or R7 after dependencies PASS.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: stock/held-sale boundaries; `DEC-018`; R5–R7.
- Owner checkpoint: R5, R6 and R7 respectively.
- Closure criteria: every named stage meets its exit criteria, automated gates and manual acceptance.
- Revalidation trigger: start or scope change of R5–R7.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` roadmap review.
- Notes: deferred capability group, not a runtime bug.

### POS-ROAD-004 — R8–R10 pricing, documents and reporting capability are deferred

- Stable ID: `POS-ROAD-004`.
- Title: R8–R10 pricing, documents and reporting capability are deferred.
- Classification: Deferred Roadmap Capability.
- Status: Deferred.
- Severity: Informational.
- Affected area: line discounts, coupon/voucher/promotion, return receipt, cashbook, daily close and reports.
- Evidence: Policy/roadmap evidence at `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R8, R9 and R10; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md` — existing controlled-discount/return foundation boundaries; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-012`, `DEC-013`, `DEC-018`.
- Observed or known condition: controlled discount and return foundation exists, but R8–R10 are Not Started and their broader scope/acceptance is incomplete.
- Expected condition or intended boundary: future stage completion must use immutable historical evidence, correct allocation and named acceptance; gross profit must not be called net profit.
- User/business impact: existing foundation must not be advertised as complete promotion, cashbook, close or reporting capability.
- Trigger or reproduction precondition: entry into R8, R9 or R10 after dependencies PASS.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: `INV-DISCOUNT-001`, `INV-DISCOUNT-002`, `INV-RETURN-001` to `INV-RETURN-003`; R8–R10.
- Owner checkpoint: R8, R9 and R10 respectively.
- Closure criteria: every named stage meets its exit criteria, regression gates and manual acceptance.
- Revalidation trigger: pricing, discount, return, receipt, close or reporting scope change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` source/roadmap review.
- Notes: partial source is neither a completed commercial checkpoint nor a confirmed defect.

### POS-ROAD-005 — R11–R13 hardware, release and pilot capability are deferred

- Stable ID: `POS-ROAD-005`.
- Title: R11–R13 hardware, release and pilot capability are deferred.
- Classification: Deferred Roadmap Capability.
- Status: Deferred.
- Severity: Informational.
- Affected area: hardware/load acceptance, installer/license/update and store pilot.
- Evidence: Policy/roadmap evidence at `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — R11, R12 and R13; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md` — `DEC-008`, `DEC-018`.
- Observed or known condition: R11, R12 and R13 are Not Started; no R0.5D hardware, installer, upgrade or store-pilot execution occurred.
- Expected condition or intended boundary: physical devices, target displays/load, release/update/rollback and pilot operations require their explicit acceptance evidence.
- User/business impact: the current baseline is not a release or pilot acceptance claim.
- Trigger or reproduction precondition: entry into R11, R12 or R13 after dependency stages PASS.
- Workaround or recovery behavior: No verified workaround recorded.
- Related invariant/decision/roadmap checkpoint: receipt hardware boundary; `DEC-008`, `DEC-018`; R11–R13.
- Owner checkpoint: R11, R12 and R13 respectively.
- Closure criteria: all stage exit criteria, manual acceptance and release/pilot evidence PASS in roadmap order.
- Revalidation trigger: hardware, supported environment, installer, license, update, release or pilot plan change.
- Last verified commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Last verified time: `2026-07-31T14:32:44.841+07:00` roadmap review.
- Notes: deferred capability group, not a runtime bug.

## 6. Roadmap and evidence boundary

- R1.3/R1 formal closeout preparation and R2–R13 being Not Started does not mean every item in those scopes is a bug; R1.1 and R1.2 are separately Closed / Committed / Pushed / Git-clean, R1.3 live Jenkins build #5 and artifact smoke evidence are PASS, and only the closeout commit/push plus Git-clean verification remain.
- Foundation source may exist while its commercial checkpoint remains Not Started.
- Completed architecture foundation does not mean hardware, manual or store acceptance has passed.
- Source existence does not equal runtime acceptance.
- Test source existence does not mean tests were freshly run.
- Roadmap completion depends on the checkpoint exit criteria, required gates and manual acceptance.
- Manual VietQR confirmation is not bank API confirmation.
- Receipt printing source is not physical-printer acceptance.
- Return business state is not proof of external refund execution or an immutable printed return receipt.
- Migration and ModelSnapshot source are not proof of migration application to a real database.

## 7. Closure discipline

An issue may change to `Resolved` only when its closure criteria are met and the evidence is recorded. A future session must:

1. Re-read the live issue record and related invariant/decision.
2. Reproduce or verify the condition in an authorized checkpoint.
3. Add regression or manual acceptance evidence appropriate to the boundary.
4. Run all required gates.
5. Record the accepted commit and evidence time.
6. Update this register and related Project Memory in the same checkpoint.

## 8. Recounted summary

| Dimension | Value |
|---|---:|
| Total records | 13 |
| Confirmed Defect | 0 |
| Known Operational Limitation | 3 |
| Verification Gap | 6 |
| Deferred Roadmap Capability | 4 |
| Resolved Historical Issue | 0 |
| Open | 2 |
| Monitoring | 3 |
| In Progress | 1 |
| Deferred | 4 |
| Resolved | 1 |
| Not Revalidated | 2 |
| Critical | 0 |
| High | 0 |
| Medium | 7 |
| Low | 1 |
| Informational | 5 |

The counts above were recounted directly from these 13 stable IDs: `POS-OPS-001`, `POS-OPS-002`, `POS-OPS-003`, `POS-VER-001`, `POS-VER-002`, `POS-VER-003`, `POS-VER-004`, `POS-VER-005`, `POS-ROAD-001`, `POS-ROAD-002`, `POS-ROAD-003`, `POS-ROAD-004`, `POS-ROAD-005`. There are no confirmed open runtime defects in this register.
