# ARCHITECTURE DECISIONS — POS ENTERPRISE RETAIL V1

## DEC-049 — Keep legacy Inventory History composition while retaining the new query pipeline

- **Status:** Accepted for the 2026-08-30 UX hotfix; physical WPF acceptance remains pending.
- **Decision:** Restore the pre-`dc27148` sidebar/workspace composition and local presentation styles, while retaining direct database-side name/code/barcode search, debounce, reset/refresh semantics, scope visibility and race protection.
- **Related behavior:** Manual stock adjustment starts with no quantity and category screens order by Vietnamese name before paging. `DisplayOrder` is not removed from persistence or contracts; it is no longer a user-facing ordering control.
- **Scope:** No product archiving/storage, Import Wizard, schema, migration, authorization, inventory write semantics or shared-style changes.

## DEC-048 — Inventory History uses direct database-side product search with one filter pipeline

- **Status:** Accepted for the Inventory History UX/query hotfix on `2026-08-30`; physical WPF acceptance remains pending.
- **Query:** Add optional normalized product search text to the existing typed inventory search request. Infrastructure applies escaped, parameterized matching against Product code/name/barcode before total count and paging. No client-side first-page lookup or hidden ProductId substitution is allowed.
- **Interaction:** Search debounces at 300 ms and Enter applies immediately. `Xóa bộ lọc` clears all user/navigation conditions; `Làm mới` preserves them. Older asynchronous responses cannot replace a newer query result.
- **Scope:** Read-only Inventory History presentation/query behavior only. No movement, stock, audit, authorization, schema, migration, package, Import Wizard or shared-style change.

## DEC-047 — R5.1C/D uses progressive disclosure and deterministic compact-header aliases

- **Status:** Accepted for the R5.1C/D UX remediation on `2026-08-30`; physical WPF acceptance remains pending.
- **User flow:** Present only the action useful for the current stage: choose a file, inspect and correct only when needed, explicitly choose the duplicate behavior, then perform one import action and show a business result. Full 11-field details and row diagnostics remain available behind user-invoked views.
- **Mapping:** Extend the existing `ProductImportSchemaCatalog` with verified compact canonical aliases (`unitname`, `saleprice`, `costprice`, `initialstock`, `minimumstock`, `isactive`). Do not use fuzzy matching or change the production schema. A missing/active-category failure remains reference data, not a header-mapping failure.
- **Scope:** Keep the existing Application/Infrastructure parser, validated snapshot, authorization, transaction and stock/audit semantics. No package, migration, shared-style change, export/template capability or new checkpoint is introduced.

## DEC-046 — R5.1C/D reuses the secure import services behind a transient WPF wizard

- **Status:** Accepted for R5.1C/D implementation on `2026-08-30`; physical WPF click/UIA/DPI acceptance remains pending.
- **Boundary:** Add one Product & Inventory entry point and a transient `ProductImportWizardWindow`/ViewModel. The wizard obtains metadata from `ProductImportSchemaCatalog`, delegates preview to `IProductImportPreviewService` and delegates mutation only after explicit confirmation to `IProductImportService`. No WPF type crosses into Application/Domain and no UI-side database access is introduced.
- **Staleness and safety:** File, worksheet, mapping and duplicate-policy changes invalidate the confirmation state. The typed preview carries the selected worksheet and mapping into the R5.1B reparse/fingerprint boundary. The existing 25 MiB, worksheet/row/column/cell and 100-row preview limits remain authoritative; the wizard never evaluates formulas or imports a bounded preview subset.
- **Dependency/scope:** No package, SDK, migration, parallel Product model, shared-style change or new authorization policy is needed. R5.1C/D remains within the existing Product/Inventory route; R5.2 and other modules are not started.

## DEC-045 — R5.1B uses an atomic validated-snapshot import and the existing audit action boundary

- **Status:** Accepted for R5.1B closeout on `2026-08-29`.
- **Import boundary:** Keep the exact R5.1A 11-field catalog as the only import schema. The Application use case consumes a typed preview, re-parses the same regular file and compares SHA-256 plus reference data before mutation. Infrastructure uses the existing EF `IUnitOfWork` transaction, Product repositories, Category active-ID resolution and append-only InventoryMovement opening-balance workflow.
- **Conflict/stock semantics:** The caller must choose `Skip`, `Update` or `Error`. Update preserves Product ID/history and rejects non-zero opening stock because the production Product update contract does not overwrite stock. New products use the existing product-creation default of tracked inventory; no new import-only tracking/unit/category model is introduced.
- **Audit/schema constraint:** Do not add a new `SecurityAuditAction`: the live EF model/database check constraint accepts actions 1–10 and this checkpoint does not authorize a migration. A successful import therefore records a sanitized batch summary through the existing audit repository with the existing valid `EmployeeUpdated` action plus `BusinessArea="Sản phẩm và nhập dữ liệu"` and `TargetType="Product import batch"`. Product rows, file content and secrets are never written to audit.
- **Scope:** R5.1B is CLOSED; R5.1 remains IN PROGRESS and R5.1C (WPF Import Wizard) is NEXT. R4.2/R4.3/R4.4 remain preserved and Development Freeze remains active outside this checkpoint.

## DEC-044 — R5.1A uses one exact Product schema and a package-free secure preview boundary

- **Status:** Accepted for R5.1A closeout on `2026-08-29`.
- **Source of truth:** Define one Application catalog with exactly 11 fields in production order: ProductCode, Barcode, Tên, Danh mục, Đơn vị tính, Giá bán, Giá vốn, Tồn đầu, Tồn tối thiểu, Trạng thái and Ghi chú. Map them to the live `Product`/`Category` model (`UnitName` remains text and `Description` is the notes field); do not create a parallel import model or silently omit unsupported fields.
- **Boundary:** Keep typed contracts/validation in Application and file I/O in Infrastructure. The R5.1A service only reads, maps, validates and returns bounded preview/summary data; it has no DbContext/repository write path and does not create categories/units or opening-stock movements.
- **Security/dependency:** Use maintained BCL ZIP/XML APIs for XLSX rather than adding a package. Enforce regular-file/signature checks, size/count/cell limits, cancellation, hardened XML, no formula evaluation, no external links/macros and safe error messages. Revisit the package decision only if R5.1B requires a capability that cannot be met safely.
- **Scope:** R5.1A is CLOSED; R5.1 is IN PROGRESS and R5.1B is NEXT. R4.2/R4.3/R4.4 remain preserved and Development Freeze is active outside this checkpoint.

## DEC-043 — Post-R4.4 hotfix isolates Audit loading failures and bounds Employee detail layout

- **Status:** Accepted for the post-R4.4 handover-freeze hotfix on `2026-08-29`.
- **Decision:** Register the existing `IAuditLogService` in the production WPF composition root; retain the Application authorization/query boundary and add only a sanitized module-level error/retry boundary for asynchronous list/detail loading. The observed failure was a missing DI registration, proven with a real isolated composition probe.
- **Layout:** Keep the completed Employee master-detail design and use explicit bounded Grid columns for list summary and identity/status presentation. Preserve long-value tooltips/ellipsis and all existing commands, security, and navigation behavior.
- **Freeze:** R4.3 and R4.4 remain closed, R5.1 remains not started, and development freeze is reactivated after the hotfix commit.

## DEC-042 — R4.4 uses one append-only secure audit viewer

- **Status:** Accepted for R4.4 closeout on `2026-08-29`.
- **Decision:** Reuse the current audit write store and add only additive safe metadata: actor/target snapshots, typed business/target labels, privacy-safe terminal identity and bounded allowlisted before/after changes. No arbitrary entity serialization, audit editing/deletion, export or second audit store is introduced.
- **Query/UI:** `ViewAuditLog` is enforced by the Application service; the WPF screen supplies friendly filters and detail presentation while database-side filtering, UTC ordering and bounded paging remain authoritative. Unknown historical metadata is shown explicitly as unknown/fallback, never invented.
- **Freeze:** R4.3 and R4.4 are closed and development is frozen for handover. The next roadmap checkpoint is R5.1 Product CSV/Excel Import and has not started.

## DEC-041 — R4.3 keeps built-in role policy authoritative and defers custom roles

- **Status:** Accepted for R4.3 on `2026-08-29`.
- **Decision:** Add a permission-gated role/permission viewer backed by the existing four typed roles and centralized policy. Keep role assignment in the existing EmployeeAccountService boundary. Defer custom roles because the repository has no persisted Role aggregate or permission assignment relation; a safe implementation would be a separate additive contract with migration, concurrency, lifecycle, audit and final-Administrator protections.
- **Security:** `ViewAuditLog` is a typed capability reserved for R4.4. UI filtering is supplemental; Application authorization remains authoritative and unknown role/permission values deny.

## DEC-039 — R4.2 final UI correction separates filtered-empty recovery from creation

- **Status:** Accepted for final R4.2 manual UI correction on `2026-08-28`.
- **Decision:** Treat global employee count and filtered result count as separate authoritative values. Show exactly one Add action for a true empty database; show Clear Filters plus a secondary Add action for filtered/search no-result. Both Add entry points call the existing `NewEmployeeCommand` and never dismiss the module.
- **Create flow:** Save the employee profile first with no account payload. If the user selected continuation, select the new employee, move to the existing account tab and require the current secure username/role/temporary-password flow. Do not create an incomplete account.
- **Role filter:** Keep one stable typed all-role sentinel and render its friendly value through the real WPF ComboBox selected-content path. Regression evidence must include `InitializeComponent`, Loaded/layout and selected-content observation.

## DEC-040 — R4.2 final UI correction preserves the shared Sales readiness route

- **Status:** Accepted for final R4.2 manual UI correction on `2026-08-28`.
- **Decision:** Verify the exact Release executable and shared readiness service before changing Sales. Optional settings remain warnings under the existing policy; genuine core blockers use the typed owner-correct Store Readiness dialog and its authorized Store Setup action. A stale screenshot or unavailable external UIA is not evidence to reintroduce an obsolete MessageBox.

## DEC-037 — Manual UX hotfix keeps Store Readiness authoritative and separates optional warnings

- **Status:** Accepted for post-modernization UX hotfix on `2026-08-28`.
- **Decision:** Sales reloads the shared external `IStoreSettingsStore` snapshot before every readiness evaluation. Only core operational errors block Sales; missing/unavailable optional backup storage and optional tax metadata are warnings. No separate Shell boolean or cache is introduced.
- **Dialog:** A typed WPF owner dialog lists friendly blocking issues and invokes the existing authorized Store Setup dialog through `IStoreSettingsDialogService`; raw exceptions and internal keys remain hidden.
- **Isolated behavior:** `Start-POS-IsolatedTest.ps1` intentionally copies only the database and gives each invocation an owned external settings root. Separate temporary scenarios do not inherit prior Store Setup state; production settings/secrets must not be copied to make acceptance appear ready.

## DEC-038 — Employee filtered states and create entry points remain one ViewModel state machine

- **Status:** Accepted for post-modernization UX hotfix on `2026-08-28`.
- **Decision:** Bind Role filtering to one stable typed “Tất cả vai trò” item, notify filter-derived state, show true-empty versus filtered/search no-result distinctly, and route every visible filtered-empty Add action to the existing `NewEmployeeCommand` create state. The list/detail design, security boundary, historical retention and R4.3 scope are unchanged.
- **Responsive/presentation:** Keep the empty state content-driven and reachable at reduced height, use supported vector icon geometry for clearing filters, and bound long employee-code presentation with an accessibility tooltip rather than mutating persisted codes.

## DEC-036 — R4.2 modernizes Employee content without changing the production Shell

- **Status:** Accepted for R4.2 Employee UI modernization closeout on `2026-08-28`.
- **Decision:** Keep the existing dark grouped Shell/sidebar, routes, permission filtering and Store Setup surface. Modernize only Employee and Account content with native WPF master-detail layout, shared design resources, virtualized list, database-side aggregate counts, stable Vietnamese state mappings and explicit no-selection/read-only-first detail states.
- **Interaction:** Select the first visible result after successful loads, preserve selection by employee ID where visible, clear stale detail on filtering, and require explicit entry for profile editing, account creation and sensitive security actions. Existing Application service authorization remains authoritative.
- **Scope:** Use three stable detail tabs for profile, account/security and effective permissions. Predefined roles and typed permission catalog remain read-only at this checkpoint; role administration is still the separate R4.3 scope. No schema, migration, password, lockout, audit or final-Administrator policy change is permitted by this decision.
- **Evidence:** Real WPF resource/window construction, focused Employee UI behavior `6/6`, Release build `0/0`, official Quality Gate `1350/1350`, EF pending-model PASS and isolated boundary checks. External UIA/render observation remains an explicit manual limitation.

## DEC-035 — R4.2 separates retained employees from optional login accounts

- **Status:** Accepted for R4.2 closeout on `2026-08-26`.
- **Decision:** Keep the existing User/AuthService/BCrypt stack and add one optional Employee business record linked one-to-one to User. Add typed account state, centralized role capabilities, force-password-change state and append-only sanitized security audit events; do not add ASP.NET Identity or a parallel authentication stack.
- **Persistence:** Use one additive EF migration. Existing Users keep their IDs/password hashes/history and receive deterministic Employee backfill records. Restrict employee-account deletion behavior and never cascade away transaction history.
- **Security:** Application services enforce permissions and final-Administrator protection transactionally. Password reset creates a temporary credential through the existing hasher and forces a mandatory change before Shell access. Deactivation disables the account without implicit reactivation/unlock.
- **UI:** Provide one permission-controlled Employee Management window with role-based effective permissions displayed read-only. Dedicated role/permission administration is intentionally the exact next R4.3 checkpoint; R4.1 visual polish remains deferred.
- **Evidence:** Isolated migration/service/security tests, WPF AutomationId/source contracts, full `1336/1336` suite and official Quality Gate PASS. The execution desktop's missing external WPF window handle is recorded as a combined-evidence limitation, not relabelled as visual UIA evidence.

## DEC-034 — R4.1 pre-startup providers must share logging-safe Infrastructure composition

- **Status:** Accepted for R4.1 hotfix closeout on `2026-08-26`.
- **Decision:** Centralize the existing restore-worker/startup-recovery `ServiceCollection` setup in `App.CreatePreStartupInfrastructureServices`; add logging before `AddInfrastructure`, then retain `ValidateOnBuild=true` and `ValidateScopes=true`. Start isolated acceptance through the compiled `POS.Enterprise.exe`, not `dotnet run`.
- **Evidence:** Exact isolated smoke reproduced an `AggregateException` for unresolved `ILoggerFactory`/`ILogger<T>` services. The focused isolated composition regression and final full/Quality Gate suites pass after the repair.
- **Consequence:** R4.1 Store Setup remains typed and fail-closed. Missing settings/printer remain readiness concerns, while pre-startup DI construction is deterministic. R4.2 remains the next checkpoint.

## DEC-033 — R4.1 uses a typed external Store Setup aggregate with restart-safe database location

- **Status:** Accepted for R4.1 closeout on `2026-08-26`.
- **Decision:** Keep the Application/Domain-facing store contract free of EF/WPF/JSON/Win32 types. Persist one schema-versioned immutable snapshot atomically in managed application data, with optimistic version checks and typed recovery. Adapt receipt, VietQR, printing and backup/retention consumers to this snapshot.
- **Database location:** A changed database directory is a verified pending/restart-required setting. Runtime never swaps an active DbContext or silently opens a new empty database; IsolatedTest always overrides effective paths inside its temporary boundary.
- **Consequences:** Store Setup is Administrator-only and register readiness is shared. Physical printer/scanner/cash-drawer behavior remains capability-honest when hardware is unavailable or not implemented. No EF migration or package drift is introduced.

## DEC-032 — Disaster recovery requires a real database switch and layered business verification

- **Status:** Accepted for R3.4 closeout on `2026-08-26`.
- **Decision:** The recovery drill must use the production manual-backup service, a genuinely new database initialized through the normal path, production restore preparation, and the real external restore worker. Direct harness copying cannot establish restore success. Durable state must reach `Verified`, the parent/worker/restart lifecycle must be exact, and the restored WPF application must sign in successfully.
- **Evidence decision:** Business recovery is established by exact Orders, stock and receipt comparisons through production repository reads plus independent read-only SQLite projections. UI evidence proves acknowledgement and authenticated Shell readiness; it is not substituted for database comparison. Final SQLite integrity, foreign-key, migration, sidecar and artifact-hash checks remain mandatory.
- **Safety decision:** All destructive activity is confined to a validated `%TEMP%` boundary. Canonical metadata/hash and managed-root state are read-only invariants, cleanup targets are exact and reparse-validated, and only checkpoint-owned PIDs may be closed.
- **Consequence:** R3.4 and R3 are closed. The R3.4 prerequisite blocker for pilot is removed, while the remaining roadmap still applies. R4.1 Store Setup is the exact next checkpoint. RST14 retains its explicit R3.3 combined-evidence classification.

## DEC-031 — Restore is a durable fail-closed worker workflow; RST14 closeout uses explicit combined-evidence governance

- **Status:** Accepted for R3.3 closeout on `2026-08-25`.
- **Production decision:** Artifact inspection and preparation remain outside WPF, validate regular-file/reparse safety and SQLite compatibility, checkpoint committed WAL content, and persist a sanitized operation plan/state before shutdown. The early startup worker validates the exact parent PID and start time, waits for parent exit, performs replacement with rollback protection, restarts normal POS exactly once, and leaves terminal recovery/acknowledgement state durable.
- **Acceptance decision:** RST1–RST13 remain PASS from retained isolated evidence. RST14 is accepted from combined real runtime, durable-state and automated contract evidence; the incomplete independent external UIA/PID timeline is recorded and is not relabelled as a machine-assisted PASS. Temporary observer/UIA harness development is retired.
- **Safety decision:** RST15 is a cumulative read-only audit, not another restore. Acceptance may target only validated `%TEMP%` databases; canonical metadata/hash and managed-root absence must remain exact.
- **Consequence:** R3.3 may close after all automated gates pass. R3.4 Disaster Recovery Drill is the exact next checkpoint and a mandatory blocker before pilot, including worker shutdown/restart/no-orphan lifecycle verification.

## DEC-030 — Automatic backup uses daily verified execution and protected GFS retention

- **Status:** Accepted for R3.2 closeout on `2026-08-23`.
- **Decision:** The 24-hour due interval is the R3.2 daily policy. Retain 7 latest, 4 distinct UTC ISO-week and 3 distinct UTC monthly verified owned artifacts. The 2 GiB quota remains secondary; required GFS snapshots and the newest verified artifact are never deleted solely to meet quota, and a typed warning is returned instead.
- **Concurrency/scope:** Manual and automatic pipelines share one singleton coordinator. Store Setup configuration is deferred to R4.1, end-of-day backup to R9, and shutdown-triggered backup is not live R3.2 scope. Existing verified pre-migration ordering remains the migration safety boundary.
- **Evidence:** targeted `76/76`, official Quality Gate `1240/1240`, ABM1/2 human + machine and ABM3–6 user-approved machine-assisted production-runtime acceptance.

## DEC-029 — External database overrides require an isolated child-process contract

- **Status:** Accepted locally for R2.4D closeout on `2026-08-09`; user-observed M1–M5 manual acceptance and M6 filesystem/hash audit passed. Git closeout remains unstaged and uncommitted by instruction.
- **Context/problem:** A long-lived PowerShell `Infrastructure__DatabasePath` could make an ordinary `dotnet run` open a test database, and a published copy inside the repository could inherit development path semantics.
- **Decision:** Classify source/test output through the existing development configuration marker and route all other application bases to `%LocalAppData%\\POS Enterprise`. Block every external path provider before host/database startup unless the source-output child declares exact `POS_RUNTIME_MODE=IsolatedTest` and supplies an absolute path different from the canonical database. Do not use executable name or `DOTNET_ENVIRONMENT` as an override bypass.
- **Evidence:** `DatabaseRuntimeGuard`, `DatabasePathResolver`, `App.xaml.cs`, the two child-process scripts, `DatabaseRuntimeGuardTests`, `DatabasePathResolverTests`, Release `1164/1164`, Quality Gate `1164/1164`, EF pending-model PASS and vulnerability scan `5/5` PASS.
- **Consequences:** Normal stale overrides fail closed; isolated snapshots are process-scoped; published output cannot be redirected by environment override. M1–M5 WPF/runtime acceptance and M6 filesystem/hash audit are recorded as PASS.
- **Revisit trigger/checkpoint:** R2.4D manual closeout or a future publish/build identity change.

## DEC-028 — Database storage monitoring and startup migration preflight are typed, metadata-only and overflow-safe

- **Status:** Accepted for R2.4A and R2.4B on `2026-08-08`; R2.4 remains IN PROGRESS.
- **Decision:** Keep snapshot/preflight contracts in Application and filesystem/volume metadata in Infrastructure. Resolve the canonical database path without side effects; inspect only main database metadata and exact WAL/SHM/journal siblings; never open database content, enumerate directories, follow reparse points or create/delete storage artifacts.
- **Policy:** Warn when free space is at or below 5 GiB or 10%; classify below 512 MiB reserved headroom as insufficient. A future backup estimate is footprint plus `max(256 MiB, ceil(10% of footprint))`. Required-operation arithmetic is overflow-safe, equality at required-plus-reserve is allowed, and unavailable metrics have `CanProceed=true` for the future startup-migration integration.
- **Reliability/privacy:** Metadata failure and bounded races return typed safe reasons without raw exception/path logging. Missing main database is distinct from zero bytes; missing exact sidecars contribute zero. Application exposes no filesystem implementation types.
- **Startup integration:** Run preflight only after pending migrations are confirmed and before integrity, the existing verified backup, or schema mutation. Existing databases use the production backup estimate; unknown footprint follows a saturating fail-safe estimate. Fresh/missing databases request zero additional backup bytes and create no pre-migration backup. Allowed, warning, and unavailable metrics continue; insufficient and unknown statuses fail closed. Cancellation is not translated.
- **Evidence:** R2.4B independent review plus `DatabaseInitializerSafetyTests` `15/15`, combined regressions `99/99`, Release build 0 warnings/0 errors, and outside-sandbox Quality Gate `1124/1124` PASS with vulnerability 5/5, EF pending-model and Git checks PASS.
- **Consequences:** R2.4A and R2.4B are COMPLETED. R2.4C/D are NOT STARTED. No storage UI, new backup/restore/retention workflow, package, schema or migration is included.

## DEC-027 — Support Bundle UI is an authenticated owner-modal with explicit per-operation consent

- **Status:** Accepted for R2.3C automated UI on `2026-08-08`; R2.3 remains IN PROGRESS.
- **Placement/access:** Add the smallest `Gói hỗ trợ` entry to the authenticated Shell. No suitable Help/Settings/Admin module and no support capability exists, so all signed-in users receive access; no new permission, role, database table or migration is invented.
- **Interaction:** Use the existing modal-owner, DI, theme and `AsyncRelayCommand` conventions plus `Microsoft.Win32.OpenFolderDialog`. Consent is never prechecked and resets after each terminal operation. Destination selection alone never starts export; the UI always hard-codes `IncludeDatabase=false`.
- **Lifecycle/privacy:** A finite single-flight ViewModel owns one CTS per operation, shows indeterminate progress, maps typed results to fixed Vietnamese messages and never exposes raw exception detail. Close while busy requests cancellation and waits for terminal completion; late Success remains Success. No Explorer launch, upload, send, retry background or network action is added.
- **Evidence:** R2.3C 21/21, R2.3B 12/12, R2.3A 11/11, corrective 2/2, R2.2 live 26/26, architecture/privacy/security 22/22, Release build 0 warnings/0 errors and outside-sandbox Quality Gate 1070/1070 with vulnerability 5/5 and EF pending-model PASS.
- **Consequences:** R2.3C automated verification PASS; manual acceptance is not performed and R2.3D remains NOT STARTED. R2.3 is not complete.

## DEC-026 — Support Bundle export is fixed-schema, database-excluding and atomically committed

- **Status:** Accepted for R2.3B automated service on `2026-08-08`; R2.3 overall remains IN PROGRESS.
- **Decision:** Keep typed contracts in Application and composition in Infrastructure. Export only the fixed manifest/diagnostic allow-list and R2.3A-managed logs. Database inclusion is unsupported and fails before artifact or database access; no database/WAL/SHM/journal/backup content is copied or hashed.
- **Privacy and bounds:** Reuse the exact R2.3A managed-file and sanitizer policies. Snapshot top-level regular logs newest-first, cap exported output at 20 MiB by default, stream bounded UTF-8 records, discard overlong records safely and sanitize a second time. Diagnostics contain only versioned typed values/codes; raw configuration, environment, paths, identities, command lines, exception/provider details, SQL and business rows are forbidden.
- **Atomicity:** Create one unique operation-owned temporary file in the caller's existing destination with `CreateNew`; close and flush ZIP before a no-overwrite final move. Failure/cancellation may delete only that owned temp. A foreign temp/final collision is preserved and returned as a typed failure; no partial archive receives the final name.
- **Evidence:** targeted R2.3B 12/12, R2.3A 11/11, corrective 2/2, R2.2 live 26/26, architecture/privacy/security 22/22, POS.Wpf Release 0 warnings/0 errors, and complete outside-sandbox Quality Gate 1049/1049 with vulnerability 5/5 and EF pending-model PASS.
- **Consequences:** R2.3B automated verification is complete. R2.3C/D, UI/manual acceptance and R2.4 are not started/completed; R2.3 remains IN PROGRESS.

## DEC-025 — Safe application logs use the built-in logging stack and bounded managed files

- **Status:** Accepted for R2.3A automated foundation on `2026-08-08`; R2.3 overall remains IN PROGRESS.
- **Decision:** Reuse Microsoft Extensions Logging without a new package. Register one Infrastructure file provider in the WPF composition root. Store system-named files under `%LocalAppData%\POS Enterprise\logs`; rotate on UTC day or 5 MiB; retain at most 10 segments, 50 MiB and 14 days, with the strictest condition winning and oldest managed files removed first.
- **Privacy:** `PosLog` sanitizes structured properties before all providers and never forwards raw exceptions. SQLite diagnostics retain only classification context supplied by call sites, exception type and numeric primary/extended codes. Exact foreign files are never cleanup candidates; raw SQL, messages, paths, credentials and customer/payment payloads are forbidden.
- **Reliability:** Provider I/O is locked, bounded and exception-contained; directory/write/cleanup failure disables file output without affecting business results. Host disposal flushes/closes the writer. Existing type-only Trace/Debug fallback remains safe when file output is unavailable.
- **Evidence:** corrective targeted 11/11 (including reparse/direct-parent ownership), current-source R2.2 regression 26/26, related privacy/architecture 14/14, POS.Wpf Release 0 warnings/0 errors, full Quality Gate 1037/1037, vulnerability 5/5 and EF pending-model PASS.
- **Consequences:** R2.3A is automated-complete. Support Bundle UI/export, R2.3B/C/D, manual R2.3 acceptance and R2.4 are not implemented.

## DEC-024 — SQLite operational failures use provider classification and safe presentation

- **Status:** Accepted for R2.2 on `2026-08-06`.
- **Decision:** Keep the provider-neutral failure kind/exception contract in Application, classify SQLite numeric base codes in Infrastructure, translate at EF save/begin/commit boundaries, and present only sanitized actionable text in WPF. Preserve technical exceptions only as inner causes for controlled logging. Never retry checkout/transaction commit blindly; bounded retry is available only to callers that establish a read-only or idempotent safe operation.
- **Startup:** Database initialization must complete before the session loop. Corruption/not-a-database blocks startup and must not trigger delete, recreate or overwrite behavior.
- **Acceptance:** Test A is Manual PASS. Tests B/C/D are NOT MANUALLY RUN and are covered by equivalent deterministic automated acceptance PASS. Targeted 27/27, full Release 1019/1019 and Quality Gate without `-SkipEfCheck` PASS.
- **Consequences:** R2.2 is COMPLETE/CLOSED. R2.3 — Logging and Support Bundle is NOT STARTED. R2.2 does not claim global log sanitization, Support Bundle, disk monitoring or database-growth handling.

## DEC-023 — Single-instance ownership is scoped to canonical database identity

- **Status:** Accepted for R2.1 on `2026-08-02`.
- **Decision:** Resolve the runtime database path without opening SQLite, canonicalize it case-insensitively for Windows, hash it with SHA-256, and use only that hash in a per-session mutex and activation-pipe name. Acquire ownership before Host start/database initialization. A same-identity contender requests activation and exits; a different database identity may own an independent instance.
- **Security:** The activation pipe ACL is protected and grants access only to the current Windows user SID. Do not broaden it to Everyone, WorldSid or Authenticated Users. Raw database paths are not exposed in named object names.
- **Lifecycle:** The owner awaits listener cancellation/disposal and releases the mutex deterministically; abandoned mutex ownership is recoverable after crash. WPF keeps one current activation target across setup/login/shell transitions and restores or requests attention best-effort.
- **Evidence:** Release rebuild 0 warnings/0 errors; targeted IPC tests PASS; class 11/11 PASS; 10/10 finite IPC stability rounds; full Release and Quality Gate tests 992/992 PASS; vulnerability/EF/security checks PASS; manual Tests A/B/C/D PASS. Test D attempt 1 remains incomplete; attempt 2 proves old owner exit, new PID relaunch/Login and unchanged Store B identity.
- **Environment note:** The tool execution sandbox denies local named-pipe client access and caused a reproducible 990/992 false failure. Verification requiring named pipes runs outside that sandbox; production ACL semantics remain unchanged.
- **Consequences:** R2.1 is COMPLETE. R2.2 later completed/closed under `DEC-024`; R2.3 is next and NOT STARTED.

## 1. Metadata và cách dùng

- CapturedAtLocal/ReconstructedAtLocal: `2026-07-31T11:49:51.828+07:00`.
- EvidenceNormalizationReviewedAtLocal: `2026-07-31T12:24:15.171+07:00`.
- Base HEAD: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Current live HEAD reviewed during R0.5E: `70523861949aeb5eefe981633db33f50bc890145`.
- R0.5 closeout reconciliation live HEAD: `afdda252ce124413b9190607a96a0046cf5097e7` on `2026-08-01`; this does not replace the R0.5E Context Pack baseline above.
- Các quyết định lịch sử dưới đây được reconstructed từ source live; không giả định ADR đã tồn tại khi code ban đầu được viết.
- Original decision date là **Unknown** khi Git/source không cung cấp evidence chính xác.
- Không bịa người quyết định, ngày quyết định hoặc alternatives từng được cân nhắc.

Status:

- **Observed and Accepted:** source hiện tại và accepted baseline đang vận hành theo quyết định.
- **Policy:** quy tắc quản trị được `AGENTS.md`/Master Roadmap chấp nhận.
- **Deferred:** có chủ ý để checkpoint tương lai xử lý.
- **Superseded:** chỉ dùng khi history/source chứng minh bị thay thế.

ID là ổn định và không renumber.

## DEC-001 — Bốn production projects với dependency hướng vào Domain

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Business model cần độc lập UI/persistence.
- **Decision:** Dùng `POS.Domain`, `POS.Application`, `POS.Infrastructure`, `POS.Wpf`; Domain không tham chiếu outer layer, Application chỉ tham chiếu Domain.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\POS.Domain.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\POS.Application.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\POS.Infrastructure.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\POS.Wpf.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\POS.Architecture.Tests.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ArchitectureDependencyTests.cs` — `ArchitectureDependencyTests.Domain_must_not_reference_outer_layers`, `Application_must_not_reference_infrastructure_or_wpf`.
- **Consequences:** Contract ở Application, adapter ở Infrastructure, composition ở WPF.
- **Trade-offs:** Nhiều interface/DTO và registration hơn.
- **Related constraints/invariants:** Dependency rules trong `AGENTS.md`.
- **Revisit trigger/checkpoint:** Chỉ khi có architecture change được checkpoint cho phép.
- **Supersedes/superseded by:** Không có evidence.

## DEC-002 — WPF App là composition root và quản lý scope

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Windows UI composition root tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` cần host, configuration, DI lifetime và modal navigation.
- **Decision:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` tạo Generic Host, gọi `AddInfrastructure`, đăng ký authorized decorators/ViewModels/Windows và tạo scope theo startup/login/shell/dialog.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.OnStartup`, `ConfigureApplicationServices`, `RunSessionLoopAsync`, `ShowLoginWindow`, `ShowShellWindow`.
- **Consequences:** Scoped DbContext không bị resolve từ root; UI owns window lifecycle.
- **Trade-offs:** Composition root lớn và registration manual.
- **Related constraints/invariants:** View/ViewModel không truy cập database trực tiếp.
- **Revisit trigger/checkpoint:** R2 platform hardening hoặc khi navigation framework được đề xuất.
- **Supersedes/superseded by:** Không có evidence.

## DEC-003 — SQLite/EF Core cho offline single-store runtime

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Retail V1 chạy offline Windows cho một cửa hàng/chi nhánh.
- **Decision:** Dùng EF Core SQLite, connection path qua `DatabasePathResolver`, một scoped `PosDbContext`.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, `UseSqlite`, `AddDbContext<PosDbContext>`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabasePathResolver.cs` — `DatabasePathResolver.CreateConnectionString`; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — Mục tiêu sản phẩm.
- **Consequences:** Deployment cục bộ đơn giản; phải xử lý lock, backup và growth.
- **Trade-offs:** Concurrency/scale khác server DB; multi-branch sync ngoài V1.
- **Related constraints/invariants:** `INV-MIGRATION-001`, `INV-BACKUP-001`.
- **Revisit trigger/checkpoint:** R2 busy/locked/disk growth; không đổi provider tùy ý.
- **Supersedes/superseded by:** Không có evidence.

## DEC-004 — Scoped persistence và explicit Application transactions

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Nhiều writes của một use case phải atomic và không leak EF ra Application.
- **Decision:** DbContext/repositories/UoW scoped; Application service mở `IApplicationTransaction`, gọi UoW save và commit; dispose chưa commit rollback.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfUnitOfWork.cs` — `EfUnitOfWork.SaveChangesAsync`, `BeginTransactionAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfApplicationTransaction.cs` — `CommitAsync`, `RollbackAsync`, `DisposeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — `CheckoutService.CheckoutCoreAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — return transaction path; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — intent transaction paths.
- **Consequences:** Application owns business transaction boundary, Infrastructure owns EF adapter/error translation.
- **Trade-offs:** Service phải xử lý rollback/conflict rõ ràng.
- **Related constraints/invariants:** `INV-STOCK-002`, `INV-CHECKOUT-002`.
- **Revisit trigger/checkpoint:** Bất kỳ đề xuất đổi transaction boundary.
- **Supersedes/superseded by:** Không có evidence.

## DEC-005 — Authorized decorator ở Application boundary

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** UI gating không đủ để ngăn alternate call path vượt quyền.
- **Decision:** Resolve service interfaces qua `Authorized*Service` factory decorators; `PermissionService` dùng current session và role policy.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.ConfigureApplicationServiceDecorators`, scoped factory registrations; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, authorized order-history/return scoped factories; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PermissionServiceTests.cs` — `PermissionServiceTests.Denied_permission_must_return_forbidden`.
- **Consequences:** Application use case có authorization guard nhất quán.
- **Trade-offs:** Concrete service và decorated interface đều phải đăng ký đúng lifetime.
- **Related constraints/invariants:** `INV-AUTH-003`.
- **Revisit trigger/checkpoint:** R4 role/permission management.
- **Supersedes/superseded by:** Không có evidence.

## DEC-006 — Durable checkout journal và canonical idempotency

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Retry/restart/concurrency không được tạo duplicate Order.
- **Decision:** Persist prepare quote/request với `ClientRequestId`, canonical fingerprints và lifecycle Prepared/Completed/Acknowledged/Abandoned; unique/schema/concurrency guards.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\CheckoutRequestJournal.cs` — lifecycle/fingerprint members; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — prepare/process/recovery/acknowledgement paths; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs` — unique indexes/state-shape constraints and `ConfigureAuditableEntity` invocation; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs` — shadow GUID concurrency token; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Concurrent_same_request_commits_exactly_one_business_result`, `Restart_recovery_distinguishes_prepared_completed_acknowledged_and_abandoned`.
- **Consequences:** Completed replay độc lập catalog live; restart recovery có durable state.
- **Trade-offs:** Hai-phase API và acknowledgement cleanup phức tạp hơn.
- **Related constraints/invariants:** `INV-CHECKOUT-001` đến `003`.
- **Revisit trigger/checkpoint:** R2 reliability hoặc protocol version change.
- **Supersedes/superseded by:** Không có evidence.

## DEC-007 — Immutable versioned receipt snapshot là nguồn reprint

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Product/store data có thể đổi sau checkout nhưng chứng từ lịch sử phải ổn định.
- **Decision:** Build allow-listed `ReceiptRequest`, serialize deterministic versioned JSON và persist một snapshot/Order trong checkout transaction; reprint deserialize snapshot.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs` — `ReceiptSnapshotFactory.Create`, `CreateReprint`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptSnapshotJsonSerializer.cs` — `Serialize`, `Deserialize`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReceiptSnapshotConfiguration.cs` — required one-to-one `OrderId`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_snapshot_must_not_change_when_product_changes_after_checkout`.
- **Consequences:** Reprint không phụ thuộc live Product; payload phải version/validate.
- **Trade-offs:** Storage tăng và schema JSON cần compatibility.
- **Related constraints/invariants:** `INV-RECEIPT-001` đến `003`.
- **Revisit trigger/checkpoint:** Khi thêm receipt schema version hoặc R9 return receipt.
- **Supersedes/superseded by:** Không có evidence.

## DEC-008 — Physical printing là side effect sau commit

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Printer failure không được rollback/duplicate sale.
- **Decision:** Checkout không phụ thuộc `IReceiptService`; snapshot persist trong transaction, preview/print do WPF gọi sau success.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Checkout_service_must_not_depend_on_print_service`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\ReceiptPreviewService.cs` — preview/print orchestration; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\WpfReceiptService.cs` — `WpfReceiptService.PrintAsync`.
- **Consequences:** Có thể reprint sau printer failure; UI phải báo lỗi độc lập.
- **Trade-offs:** Sale thành công nhưng bản in đầu có thể thất bại.
- **Related constraints/invariants:** `INV-CHECKOUT-002`, `INV-RECEIPT-002`.
- **Revisit trigger/checkpoint:** R11 printer acceptance.
- **Supersedes/superseded by:** Không có evidence.

## DEC-009 — PaymentIntent bền vững cho VietQR manual confirmation

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Tiền có thể được cashier xác nhận trước khi checkout commit; restart không được mất recovery.
- **Decision:** Persist PaymentIntent lifecycle/payload/quote/checkout snapshot; Created/Presented không tạo sale; manual confirmation tạo Confirmed; confirmed retry dùng persisted snapshot/intent ID.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\PaymentIntent.cs` — lifecycle transitions/snapshots; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — create/present/confirm/recovery transaction paths; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Confirmed_intent_restart_offers_payment_retry`, `Checkout_journal_is_created_only_after_manual_confirmation`.
- **Consequences:** Durable recovery và concurrency guards; không tuyên bố bank auto-confirmation.
- **Trade-offs:** State machine/manual-resolution UX phức tạp.
- **Related constraints/invariants:** `INV-PAYMENT-001` đến `003`.
- **Revisit trigger/checkpoint:** Khi provider reconciliation được đưa vào roadmap mới.
- **Supersedes/superseded by:** Không có evidence.

Current closeout evidence: Presented PaymentIntent is persisted before the QR dialog; manual VietQR acceptance PASS at `7052386`.

## DEC-010 — HeldSale là snapshot bền vững, checkout mới mutate stock

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Giữ cart qua thời gian/restart nhưng không reserve/mutate business sale.
- **Decision:** Persist product/price/discount snapshot; resume revalidates live eligibility; checkout completes HeldSale atomically với Order.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — `HeldSaleService.CreateHeldSaleAsync`, `GetHeldSaleForResumeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs` — `HeldSaleApplicationIntegrationTests.Create_held_sale_persists_snapshot_without_business_mutation`, `Resume_reports_price_stock_and_unavailable_without_mutation`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleCheckoutIntegrationTests.cs` — `HeldSaleCheckoutIntegrationTests.Checkout_with_active_held_sale_completes_atomically`.
- **Consequences:** Hold không làm sai tồn; resume có review price/stock.
- **Trade-offs:** Không phải hard reservation nên stock có thể thay đổi.
- **Related constraints/invariants:** `INV-HELD-001`, `INV-HELD-002`.
- **Revisit trigger/checkpoint:** Nếu roadmap thêm reservation policy.
- **Supersedes/superseded by:** Không có evidence.

## DEC-011 — Active PaymentIntent sở hữu độc quyền HeldSale

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Một held cart không được đồng thời do cash path và VietQR path xử lý.
- **Decision:** Policy check ở list/resume/cancel/checkout và filtered unique index ngăn hai active intents cùng `HeldSaleId`.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSalePaymentOwnershipPolicy.cs` — `HeldSalePaymentOwnershipPolicy.Evaluate`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs` — filtered unique index `UX_PaymentIntents_Active_HeldSaleOwner`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs` — `HeldSalePaymentOwnershipTests.Two_payment_intents_cannot_own_same_held_sale`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs` — forward schema guard.
- **Consequences:** Một UI/payment owner; terminal intent release theo policy.
- **Trade-offs:** Stale UI action bị block và cần refresh/recovery.
- **Related constraints/invariants:** `INV-HELD-003`.
- **Revisit trigger/checkpoint:** Thay đổi payment lifecycle.
- **Supersedes/superseded by:** Không có evidence.

## DEC-012 — Controlled Discount dùng integer fixed/basis points và immutable snapshot

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Discount phải tránh floating-point, total âm và thiếu actor/reason.
- **Decision:** Hỗ trợ None/fixed integer/percentage basis points; Domain resolve/floor; persist one-to-one Order snapshot với actor/reason/time.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs` — `SalesDiscountCalculator.Resolve`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderDiscountSnapshot.cs` — immutable snapshot members; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderDiscountSnapshotConfiguration.cs` — one-to-one unique Order index/checks; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Fixed_amount_is_integer_and_bounded`, `Percentage_uses_basis_points_and_floor`, `Snapshot_has_unique_order_fk_and_integer_money`.
- **Consequences:** Deterministic totals và audit snapshot.
- **Trade-offs:** Chưa phải line discount/coupon/voucher/promotion engine.
- **Related constraints/invariants:** `INV-DISCOUNT-001`, `INV-DISCOUNT-002`.
- **Revisit trigger/checkpoint:** R8.
- **Supersedes/superseded by:** Không có evidence.

## DEC-013 — Return dùng immutable document, per-line balance và atomic stock reversal

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Retry/concurrency không được over-return hoặc double-refund/stock.
- **Decision:** Canonical client request, persisted return document/lines, one balance per original item và concurrency token; transaction cập nhật return/order/item/product/movement cùng nhau.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — return validation/transaction/idempotency path; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnConfiguration.cs` — unique client-request index/return constraints; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnItemConfiguration.cs` — quantity constraints; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnBalanceConfiguration.cs` — balance constraints/concurrency token; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Client_request_id_unique_index_must_reject_duplicate`, `Return_balance_constraints_must_reject_negative_values`.
- **Consequences:** Durable history/idempotency; external refund và printable immutable return receipt vẫn là boundary.
- **Trade-offs:** Thêm balance state và conflict handling.
- **Related constraints/invariants:** `INV-RETURN-001` đến `003`.
- **Revisit trigger/checkpoint:** R9.
- **Supersedes/superseded by:** Không có evidence.

## DEC-014 — Backup verified trước migration của database hiện hữu

- **Status:** Observed and Accepted.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Schema upgrade có thể thất bại và phải có recovery point.
- **Decision:** Nếu database hiện hữu có pending migrations, tạo/verify SQLite backup rồi mới `MigrateAsync`; failure chặn migration. Fresh/no-pending không backup không cần thiết.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs` — `DatabaseInitializer.InitializeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\SqliteDatabaseSafetyService.cs` — `SqliteDatabaseSafetyService.CreateVerifiedBackup`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs` — `DatabaseInitializerSafetyTests.Existing_database_with_pending_migrations_must_create_verified_backup`, `Backup_failure_must_block_migration`.
- **Consequences:** Startup có thể dừng an toàn; backup được giữ khi migration failure.
- **Trade-offs:** Startup lâu hơn và cần disk space.
- **Related constraints/invariants:** `INV-BACKUP-001`.
- **Revisit trigger/checkpoint:** R3 backup/restore/drill.
- **Supersedes/superseded by:** Không có evidence.

## DEC-015 — Migration là forward-only

- **Status:** Policy.
- **Original decision date:** Unknown.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** EF không rerun migration đã recorded khi file cũ bị sửa.
- **Decision:** Không sửa migration đã áp làm upgrade mechanism; dùng migration mới và không chỉnh ModelSnapshot độc lập.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md` — “Database và migration” forward-only policy; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentMigrationTests.cs` — `PaymentIntentMigrationTests.Applied_migration_is_not_assumed_to_rerun_after_its_file_changes`.
- **Consequences:** Upgrade path audit được; schema correction cần forward migration.
- **Trade-offs:** Có thêm migration thay vì rewrite history.
- **Related constraints/invariants:** `INV-MIGRATION-002`.
- **Revisit trigger/checkpoint:** Không revisit trừ documented replacement decision.
- **Supersedes/superseded by:** Không có evidence.

## DEC-016 — Project Memory và một Master Roadmap là governance source

- **Status:** Policy.
- **Original decision date:** 31/07/2026 cho Project Memory foundation; roadmap gốc 29/07/2026.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Session mới không được phụ thuộc chat/snapshot cũ hoặc đổi checkpoint tùy hứng.
- **Decision:** Đọc `AGENTS.md` và Project Memory; duy trì một `MASTER-ROADMAP.md`; source live có ưu tiên cao nhất.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md` — “Thứ tự nguồn sự thật” và “Project Memory”; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — “Governance”.
- **Consequences:** Architecture/roadmap change phải cập nhật register cùng checkpoint.
- **Trade-offs:** Tăng discipline tài liệu.
- **Related constraints/invariants:** `INV-SECURITY-001`; Project Memory workflow.
- **Revisit trigger/checkpoint:** Khi governance được thay bằng decision mới có supersession rõ.
- **Supersedes/superseded by:** Không có evidence.

## DEC-017 — R0.5 chỉ commit/push tại closeout R0.5F

- **Status:** Policy.
- **Original decision date:** 31/07/2026.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Project Memory A–E riêng lẻ không được commit ở trạng thái lệch/thiếu verification.
- **Decision:** R0.5B/C/D/E remain uncommitted until R0.5F review/gates PASS; each subcheckpoint must retain its actual local status, including Blocked when verification evidence fails.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md` — “Closeout note”; `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — “R0.5 — PROJECT MEMORY FOUNDATION” exit criteria.
- **Consequences:** Project Memory và exporter artifacts tiếp tục untracked; R0.5 chưa Completed và không được tuyên bố Closed.
- **Trade-offs:** Worktree dirty có chủ ý trong nhiều subcheckpoints.
- **Related constraints/invariants:** Git safety trong `AGENTS.md`.
- **Revisit trigger/checkpoint:** R0.5F closeout.
- **Supersedes/superseded by:** Không có evidence.

## DEC-018 — Các capability tương lai không được nâng stage theo partial source

- **Status:** Deferred historical policy; R1 state later reconciled by `DEC-019`.
- **Original decision date:** 29/07/2026 roadmap; reconstructed 31/07/2026.
- **ReconstructedAtLocal:** `2026-07-31T11:49:51.828+07:00`.
- **Context/problem:** Controlled Discount, Return, receipt printing và Jenkinsfile đã có partial source nhưng acceptance stage rộng hơn chưa chạy.
- **Decision:** At reconstruction time, R1–R13 giữ Not Started; scope còn lại và acceptance thực hiện tại checkpoint roadmap tương ứng. The later R1.1-before-R0.5-closeout sequence is recorded without rewriting this history in `DEC-019`.
- **Evidence:** `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md` — trạng thái tổng quan và ví dụ partial capability không hoàn thành future stage.
- **Consequences:** Không diễn giải partial feature thành stage PASS hoặc runtime bug.
- **Trade-offs:** Capability tồn tại nhưng product readiness chỉ được công nhận sau stage acceptance.
- **Related constraints/invariants:** Receipt hardware R11, return R9, discount R8.
- **Revisit trigger/checkpoint:** Từng stage R1–R13.
- **Supersedes/superseded by:** R1 current-state portion is superseded by `DEC-019`; R2–R13 policy remains active.

## DEC-019 — Formal-closeout R0.5 without rewriting the earlier R1.1 history

- **Status:** Policy.
- **Original decision date:** `2026-08-01` reconciliation.
- **Context/problem:** R0.5 Project Memory had not received its formal closeout commit when Jenkins R1.1 implementation and runtime verification occurred at `afdda252ce124413b9190607a96a0046cf5097e7`. Rewriting or rebasing that history would erase the actual sequence and create false governance evidence.
- **Decision:** Preserve Git history. Formal-closeout R0.5 in a separate reviewed commit after recording both fresh-session PASS results and final R0.5 gates. Treat R1 as In Progress by reconciliation: R1.1 runtime E2E is PASS from supplied Jenkins evidence, while R1.1 repository closeout remains PENDING and must be handled in a separate post-R0.5 checkpoint before R1.2 may start. R1.2 and R1.3 remain NOT STARTED.
- **Evidence:** live Git history places R1.1 commits `cc1820f` and `afdda25` after Context Pack baseline `7052386`; user-supplied Jenkins evidence verifies runtime behavior at `afdda252ce124413b9190607a96a0046cf5097e7`; R0.5 ChatGPT and Codex fresh-session checks both PASS on `2026-08-01` with distinct provenance.
- **Consequences:** Context Pack baseline `70523861949aeb5eefe981633db33f50bc890145` and live pre-closeout HEAD `afdda252ce124413b9190607a96a0046cf5097e7` remain separate facts. R0.5 closeout does not claim governance was followed before reconciliation and does not claim R1.1 repository closeout is complete.
- **Trade-offs:** R1.1 requires a dedicated repository evidence closeout after the R0.5 commit/push instead of being silently folded into R0.5.
- **Related constraints/invariants:** `DEC-016`, `DEC-017`, `DEC-018`; exact staging and evidence requirements in `CHECKPOINT-WORKFLOW.md`.
- **Revisit trigger/checkpoint:** R1.1 repository closeout/reconciliation.
- **Supersedes/superseded by:** Không supersede historical decisions; records the exception and recovery path.

## DEC-020 — R1.1 repository closeout follows the formal R0.5 closeout

- **Status:** Policy for the R1.1 repository-closeout checkpoint.
- **Original decision date:** `2026-08-01`.
- **Context/problem:** R1.1 runtime verification occurred at `afdda252ce124413b9190607a96a0046cf5097e7` before the separate R0.5 formal closeout commit `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- **Decision:** Preserve both proven commit identities and provenance. Record R1.1 runtime E2E as PASS from the supplied Jenkins evidence, prepare a separate repository-closeout payload after R0.5 is Closed / Committed / Pushed, and require its own commit/push and Git-clean verification before R1.2 starts. Do not rewrite or rebase history, and do not treat R1.2 or R1.3 as started.
- **Evidence:** Live Git state at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; supplied Jenkins runtime evidence at `afdda252ce124413b9190607a96a0046cf5097e7`; R1.1 acceptance details in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`.
- **Consequences:** Runtime evidence and repository-closeout commit remain distinct. R1 stays In Progress; R1.2 is the next permitted checkpoint only after R1.1 closeout; R1.3 remains NOT STARTED.
- **Trade-offs:** Repository governance is reconciled with the actual Git sequence without erasing the earlier runtime verification.
- **Related constraints/invariants:** `DEC-016`, `DEC-017`, `DEC-019`; exact staging and closeout requirements in `CHECKPOINT-WORKFLOW.md`.
- **Revisit trigger/checkpoint:** R1.1 closeout commit/push or a later change to R1 scope.
- **Supersedes/superseded by:** Does not supersede historical decisions.

## DEC-021 — R1.2 repository standards are explicit without normalization

- **Status:** Policy for the R1.2 Repository Standards checkpoint.
- **Original decision date:** `2026-08-01`.
- **Context/problem:** The live roadmap assigns line endings, versioning, build metadata and changelog convention to R1.2, while the repository had no `.gitattributes`, `.editorconfig`, `global.json` or `CHANGELOG.md` and had local `_audit_temp` protection only outside the repository `.gitignore`.
- **Decision:** Add minimal repository standards: automatic text detection with LF policy and explicit binary patterns, UTF-8/editor defaults, SDK pin `10.0.302` supported by live Jenkins evidence and installed locally, a lightweight Unreleased/dated changelog convention, and `/_audit_temp/` ignore protection. Retain existing deterministic and CI build metadata in `Directory.Build.props`; do not invent a product version, normalize existing files, or implement R1.3 artifacts.
- **Evidence:** Live R1 scope in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`; candidate inventory before change; `dotnet --version` `10.0.302`; restore PASS; Release build 0 warnings/0 errors; 975/975 tests; Quality Gate exit `0` with vulnerability and EF pending-model checks PASS; Jenkinsfile unchanged; replay probe absent.
- **Consequences:** Future text files have an explicit repository policy without a mass EOL rewrite. R1 remains In Progress; R1.2 is Completed/PASS in the reviewed implementation/closeout payload but needs its own commit/push and Git-clean verification; R1.3 remains NOT STARTED.
- **Trade-offs:** Product/version semantics remain intentionally undefined until a checkpoint provides evidence for a release version; CI artifact publication remains R1.3.
- **Related constraints/invariants:** `DEC-020`; R1.2/R1.3 scope and exact staging rules in `CHECKPOINT-WORKFLOW.md`; no repo-wide normalization or package changes.
- **Revisit trigger/checkpoint:** R1.2 closeout or a later release/versioning policy change.
- **Supersedes/superseded by:** Does not supersede historical decisions.

## DEC-022 — R1.3 CI artifact contract

- **Status:** Owner-approved policy for the R1.3 CI Artifacts checkpoint.
- **Original decision date:** `2026-08-01`.
- **Context/problem:** The live roadmap names test results, build logs, gate logs, vulnerability report and experimental published binaries, but the operational artifact contract was not previously recorded.
- **Decision:** Use the exact generated root `_ci_artifacts/`, clean only that root before preparation, produce one valid TRX per full-test project, three command logs, one Quality Gate log, one full-solution transitive vulnerability JSON report and a validated experimental framework-dependent publish payload from `src/POS.Wpf/POS.Wpf.csproj`. The native POS.Enterprise application identity has four root files: `POS.Enterprise.exe`, `POS.Enterprise.dll`, `POS.Enterprise.deps.json` and `POS.Enterprise.runtimeconfig.json`. The validator directly checks those four files plus `appsettings.json` at the publish root, for five direct-root files total. Keep output console-visible and preserve every native exit code.
- **Publication:** Archive safe artifacts from Declarative Pipeline `post { always { ... } }`. Only `*.exe` and `*.dll` use `fingerprint: true`; every `*.json` uses `fingerprint: false`, including the two POS.Enterprise metadata files and `appsettings.json`. Logs/reports/TRX also use `fingerprint: false`. Binary and JSON archive calls use `onlyIfSuccessful: false`; `allowEmptyArchive: true` only protects post/always publication when failure occurs before publish. Successful pipelines must independently validate non-empty binaries and all five artifact groups.
- **Retention:** Keep metadata/console for 30 builds and archived artifacts for 10 builds through `buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))`, unless a stricter live policy exists. No plugin dependency is added.
- **Safety boundary:** Allowlist the approved publish output only; deny databases, backups, secrets, credentials, customer data, source archives, workspace-wide files, PDBs, installers, coverage/JUnit/HTML reports and Context Pack content.
- **Failure contract:** Artifact preparation, command, validation or archive failures are non-zero failures. Existing build/test/Quality Gate failures remain failures; publication must not make a red build green or replace the root failure.
- **Evidence boundary:** Local artifact production/parse/allowlist verification establishes implementation evidence. Live Jenkins publication on the exact pushed commit was observed in job `POS_ENTERPRISE_R1_1_CI` build `#5` at `http://localhost:8080/job/POS_ENTERPRISE_R1_1_CI/5/`, SCM revision `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`, with all five artifact groups validated. Formal R1 closeout is Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`.
- **Local/live evidence:** SDK `10.0.302`; Release build 0 warnings/0 errors; 975/975 tests; valid five-project vulnerability JSON; Quality Gate exit `0` with EF check; native publish validation PASS with 50 files and required `POS.Enterprise.*` root files; controlled exit-23 probe preserved the non-zero result; generated `_ci_artifacts` was removed after local verification. Live build #5 reports 50 published files / 11,807,483 bytes, complete archived contract 56 files / 13,291,155 bytes, and ZIP SHA-256 `38adf83096b23b19b3e17ee5fc143025cbf15bcbbb12cdb688efe160414c848d`. Download/extraction/application smoke test PASS used an existing profile; clean-profile first-run remains Not Revalidated.
- **Consequences:** R1 and R1.3 are Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`; the binary-name blocker is resolved by the owner-approved correction to native `POS.Enterprise.*` output. R1.3 does not include an assembly identity change, `AssemblyName`/`TargetName` override, binary copy/rename, coverage, JUnit conversion, HTML reports, installer/package release, product behavior, database/migration or SDK/package changes. Later R2.1 completion is governed by `DEC-023` and R2.2 completion by `DEC-024`.
- **Related constraints/invariants:** `DEC-021`; R1 scope and exact artifact contract in `MASTER-ROADMAP.md`; Jenkins failure propagation and Project Memory evidence rules in `CHECKPOINT-WORKFLOW.md`.
- **Revisit trigger/checkpoint:** R1.3 formal closeout or a later owner-approved CI artifact policy change.
- **Supersedes/superseded by:** Does not supersede historical decisions.

## 2. Roadmap state decision

- R0: Completed theo authoritative closeout.
- R0.5A–R0.5F: PASS in the reviewed R0.5 closeout payload.
- R0.5: Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; HEAD and origin/main were aligned and the post-commit worktree was clean.
- R0.5E pack: `project-context-20260801T0647171300576Z`, baseline `70523861949aeb5eefe981633db33f50bc890145`, exporter exit code `0`, security findings `0`, coverage `501/501`, excluded candidates `0`, manifest integrity `16/16` PASS.
- R0.5F: ChatGPT and Codex fresh-session verifications both PASS on `2026-08-01`.
- R1: Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`. R1.1 is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`; runtime E2E remains attributed to `afdda252ce124413b9190607a96a0046cf5097e7`. R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. R1.3 implementation and live Jenkins build #5 are PASS on `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`; its formal closeout is the R1 closeout commit above.
- R2: In Progress overall. R2.1 is COMPLETE under `DEC-023`; R2.2 is COMPLETE/CLOSED under `DEC-024`; R2.3 is IN PROGRESS under `DEC-025`–`DEC-027` with R2.3A/B/C automated-verified; R2.3C manual acceptance, R2.3D, R2.4 and R3–R13 remain Not Started.
- Partial feature không làm future stage Completed.

## 3. Decisions not reconstructed

- DI alternatives, database-provider alternatives và navigation-framework alternatives từng được cân nhắc: không có evidence.
- Người phê duyệt và original dates của DEC-001 đến DEC-015: không có evidence.
- Automatic bank reconciliation, cloud sync, multi-store và full accounting: ngoài Retail V1 hoặc deferred roadmap, không phải accepted architecture.
- Universal logging redaction, SQLite busy retry policy và restore rollback orchestration: chưa đủ source evidence; xử lý tại R2/R3.

## 4. Future update rule

Mọi thay đổi kiến trúc hoặc roadmap sau khi register này tồn tại phải:

1. Giữ ID cũ.
2. Tạo decision ID mới nếu thay thế.
3. Ghi `Supersedes`/`superseded by` và evidence.
4. Cập nhật cùng checkpoint code.
5. Không rewrite lịch sử để giả như decision mới chưa từng thay đổi.

## 5. Register summary

| Status | Count |
|---|---:|
| Observed and Accepted | 14 |
| Policy | 7 |
| Deferred | 1 |
| Superseded | 0 |
| **Total** | **22** |
