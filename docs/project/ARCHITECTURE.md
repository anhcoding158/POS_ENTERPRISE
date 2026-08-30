# ARCHITECTURE — POS ENTERPRISE RETAIL V1

## R5.1C/D Product import presentation boundary — 2026-08-30

- The Product & Inventory Shell route opens a transient WPF wizard through `IProductImportDialogService`; the wizard ViewModel owns asynchronous file selection, preview/mapping state, cancellation and confirmation, while Application contracts remain free of WPF types. The dialog service enforces the existing `ManageProducts` capability before construction.
- The wizard delegates all parsing, security limits, typed mapping/validation and transaction semantics to the existing R5.1A/B Application/Infrastructure services. It does not access a database directly, mutate stock, create categories/units or implement a second import engine. Worksheet and mapping choices are carried through the typed preview snapshot so the B-layer reparse/fingerprint boundary remains authoritative.
- The wizard is intentionally local to Product & Inventory; no shared DataGrid/theme or Audit Log surface was changed. R5.1C/D adds no package, migration, authorization policy or persistence boundary.

## Post-R4.4 handover-freeze hotfix boundary — 2026-08-29

- `App.ConfigureApplicationServices` now composes the existing `IAuditLogService` implementation used by `AuditLogViewModel`; no second service, container or authorization path was introduced. The ViewModel catches recoverable list/detail failures, logs only a sanitized exception chain and exposes a retryable module status while the Shell remains alive.
- Employee clipping is a WPF-only layout correction: the master summary is bounded within its card and the selected identity header uses `Auto/*/Auto` avatar/identity/status columns. Audit persistence/query contracts, Employee commands and all cross-module boundaries are unchanged.

## R4.4 Secure Audit Log UI boundary — CLOSED — 2026-08-29

- Audit writes continue through the existing `ISecurityAuditRepository` append boundary. `SecurityAuditEvent` now carries safe historical actor/target snapshots, typed business/target metadata, terminal identity and a bounded allowlisted change set; the viewer has no edit/delete/export path.
- `IAuditLogService` enforces `ViewAuditLog` before delegating to `ISecurityAuditQueryRepository`, which applies filters, ordering and bounded pagination in the database. `AuditLogWindow` is a WPF composition surface only and does not authorize access.
- `20260828173138_AddSecureAuditLogMetadata` is additive and preserves existing IDs/rows with explicit empty metadata defaults; indexes support newest-first and business/action queries. R5.1 Product CSV/Excel Import is the next recorded checkpoint and is not started.

## R4.3 Role and Permission Management boundary — 2026-08-29

- The role model remains `POS.Domain.Enums.Role` with four stable built-in values. `RolePermissionPolicy` remains the only evaluator mapping role to `SystemCapability`; `PermissionCatalog` adds presentation metadata without duplicating permission keys.
- The R4.3 Application service queries role usage through `IUserRepository.CountByRoleAsync` and returns typed snapshots. WPF resolves it through the existing scoped DI composition and only adds a permission-gated grouped Shell route.
- Custom roles are deferred because there is no persisted role/permission aggregate in the current dependency graph. No alternate authorization stack, schema change or role assignment path was introduced by R4.3.

## R4.2 final manual UI correction boundary — 2026-08-28

- Empty-state truth is supplied by the existing database-side summary plus the filtered query: `GlobalEmployeeCount == 0` is true-empty, while `GlobalEmployeeCount > 0 && FilteredResultCount == 0 && HasActiveSearchOrFilter` is filtered/search no-result. These properties are notified together with filter/page changes so only one presentation can be visible.
- All Add entry points remain in the existing `EmployeeManagementViewModel` state machine. Filtered-empty Clear Filters invokes `ClearFiltersCommand`; Add invokes `NewEmployeeCommand`; a successful create clears incompatible filters only as needed to select the new persisted employee and then optionally opens the existing account-creation tab.
- The Role filter remains a typed stable sentinel object. Its display is produced by the object’s friendly `ToString` value and the real WPF window test verifies selected content after Loaded/layout; no selected-index race or second DI/container boundary was introduced.
- Create profile fields, optional account continuation and sticky actions remain WPF presentation over the existing Application service. The service never receives incomplete account data during employee creation; account creation still goes through the existing authorized secure command.
- Sales readiness remains shared/authoritative: the current Sales route reloads external settings, evaluates typed blockers and uses the existing actionable readiness dialog. No authorization, readiness policy, Store Setup surface, Shell/sidebar route or persistence schema was changed by this correction.

## Post-modernization manual UX hotfix boundary — 2026-08-28

- Sales readiness remains an Application/Infrastructure-owned typed evaluation. WPF reloads the singleton external settings store before evaluating it, presents only typed friendly blockers, and opens Store Setup through the existing authorized dialog service. Backup/tax optional conditions are warnings; core store/database errors remain blocking.
- IsolatedTest settings are deliberately external to the copied SQLite database and rooted under the fresh scenario boundary. The launcher does not copy production settings, credentials, bank data or logos; persistence across separate isolated scenarios is not an expected contract.
- Employee UI fixes remain within the existing WPF ViewModel state machine: stable typed filter sentinel, notified derived empty-state properties, one create command, content-safe empty panel, supported vector icon and tooltip-bounded employee code. No dependency direction, DI lifetime, persistence schema or security boundary changed.

## R4.2 Employee UI modernization boundary — 2026-08-28

- The production dependency direction is unchanged: WPF resolves `IEmployeeAccountService` through the existing scoped composition; ViewModels do not access `PosDbContext`, and no second DI container or alternate authorization path was introduced.
- Employee presentation is a native WPF master-detail surface. The list remains virtualized and database-backed for search/filter/paging; the summary cards use repository aggregate counts rather than loading the employee table into memory. The detail surface is read-only first with explicit profile edit, account/security and role/permission tabs.
- Typed domain/application state supplies Vietnamese labels for employment/account status, role, last-login time-zone formatting, failed-login attention and effective permission groups. Passwords, hashes, temporary secrets and internal permission keys are not exposed by the UI DTO/presentation boundary.
- Responsive behavior uses live available space, a normal-width approximately 63/37 split, deliberate toolbar wrapping and controlled list horizontal scrolling. The existing native window behavior, owner, keyboard access and dirty-close guard are retained; no fragile borderless WindowChrome was introduced.
- This is content-only R4.2 visual modernization. Shell/sidebar, Inventory navigation hotfix, Store Setup and all existing production routes remain outside the changed UI boundary. R4.3 role/permission administration has not started.

## R4.2 Employee and Account Management boundary — 2026-08-26

- Domain owns `Employee`, the optional Employee-to-User relationship, typed employee/account/lock/audit enums and immutable-free-of-credential audit entities. Domain remains independent of EF, WPF and hashing providers.
- Application owns the typed employee/account requests and DTOs, centralized password policy, role capability catalog, permission checks and `IEmployeeAccountService`. Public results never contain password hashes or reset credentials.
- Infrastructure owns EF mappings/repositories, the additive `AddEmployeeAccountManagement` migration, deterministic existing-User backfill and append-only security audit persistence. Existing User/order/receipt IDs are preserved and employee/account deletion is restricted.
- WPF owns one permission-gated Employee Management window and a mandatory forced-password-change window. ViewModels resolve application services through scopes; they do not access `PosDbContext`. The employee window has stable AutomationIds, paging/filter UI, dirty-state/close guard and owner-correct actions.
- Authentication remains the existing BCrypt/AuthService/CurrentUser stack. Force-password-change is carried in the existing authenticated-session DTO and is intercepted before Shell construction.

## R4.1 isolated startup safety hotfix boundary — 2026-08-26

- Pre-startup restore/worker resolution uses one shared `App.CreatePreStartupInfrastructureServices` composition. It registers logging before Infrastructure and still builds with `ValidateOnBuild=true` and `ValidateScopes=true`; logger-dependent Store Setup/receipt/VietQR services therefore resolve before normal host startup.
- Isolated acceptance launches the already-built `POS.Enterprise.exe` directly. The launcher creates only the copied database boundary, validates all reported settings/logo/backup paths before use, and never writes production `LOCALAPPDATA` settings or logo artifacts.
- Isolated startup diagnostics are allow-listed milestones and sanitized exception-chain metadata written only to the existing scenario directory. Failure logging cannot change the fail-closed result.

## R4.1 typed Store Setup boundary — 2026-08-26

- Application owns the infrastructure-free immutable `StoreSettingsSnapshot`, typed enums/retention policy, validator, readiness issues and persistence/test-operation contracts. Defaults and schema version are centralized.
- Infrastructure owns atomic versioned JSON persistence under the managed application-data root, managed logo assets, local QR preview, printer discovery/test, safe path evaluation and adapters consumed by receipt, VietQR and backup/retention services. No second service provider or EF migration was required.
- WPF owns the Administrator-only owner-modal Store Setup window and MVVM draft state. It exposes stable AutomationIds, dirty-state/unsaved-change protection, typed validation summary, single-flight Save/QR/printer tests and restart-required indication.
- Database-directory changes are pending until restart; the active DbContext is never moved or replaced in place. In IsolatedTest, effective database/settings/logo/backup paths are forced inside the scenario boundary regardless of persisted production settings.
- Sales/register entry uses the same readiness evaluator as Store Setup. Receipt and VietQR consumers use the saved typed snapshot while historical receipt snapshots remain immutable.

## R3.3 verified restore and rollback boundary

- `POS.Application` owns typed restore inspection, preparation, plan and execution contracts. WPF owns only authenticated workflow/presentation and file-picker adaptation; it does not open SQLite or implement replacement logic.
- `POS.Infrastructure` validates candidate identity, regular-file/reparse safety, SQLite integrity/schema compatibility and committed WAL content before accepting a candidate. It persists sanitized operation state and uses operation-owned paths beneath the active isolated/runtime database boundary.
- Restore replacement runs in an early command mode before normal host/UI startup. The worker binds to the exact parent PID and start time, waits for parent exit, checkpoints and verifies the candidate, protects the installed database with pre-restore rollback state, replaces atomically, verifies the result, and starts normal POS exactly once. Failure transitions remain durable and rollback is fail-closed.
- Startup recovery and the Restore Wizard consume typed durable state. Terminal results are acknowledged once; raw restore tokens, connection strings, command lines and database contents are not persisted in evidence or UI diagnostics.
- The OpenFileDialog automation and WAL checkpoint/hash defects discovered during R3.3 have production regressions. RST7 proves regular-file reparse rejection with a real fixture. RST15 proves every destructive acceptance path stayed in `%TEMP%` and the canonical database/root remained unchanged.
- RST14 closeout is combined-evidence acceptance, not a claim of a complete independent external UIA/PID timeline. R3.4 must independently drill real backup, switch, restore, login, business data/integrity and the full worker shutdown/restart/no-orphan lifecycle before pilot.

## R2.4D database runtime hardening boundary

- The WPF composition root validates the effective database-path provider before safe logging registration, single-instance identity resolution, host start, `DbContext` construction, SQLite access or `DatabaseInitializer`. A non-JSON external provider is never silently accepted in normal or published runtime.
- `IsolatedTest` is an explicit child-process contract: `POS_RUNTIME_MODE=IsolatedTest`, an absolute override path, source/test development output identified by the existing development configuration marker, and a path different from the canonical development/published database. Published output always fails closed for an external override, independent of `DOTNET_ENVIRONMENT`.
- `DatabasePathResolver` uses the source/test output marker plus repository structure for development resolution, routes other application bases to `%LocalAppData%\\POS Enterprise`, and keeps the explicit `dotnet-ef` tooling fallback. Executable name and current working directory do not select published database storage.
- `Start-POS-Normal.ps1` and `Start-POS-IsolatedTest.ps1` construct child environment state without assigning the parent environment. The isolated script copies only the main source database to a timestamped TEMP snapshot and never points the child at the canonical file.
- Startup diagnostics use only allow-listed metadata and the safety block message contains no path or raw override. User-observed M1–M5 WPF/runtime acceptance and M6 filesystem/hash audit passed on 2026-08-09; Git closeout remains separate and is still pending.

## R2.4B startup migration storage-preflight boundary

- `DatabaseInitializer` remains scoped and receives the singleton `IDatabaseStorageMonitor` by constructor injection. It checks pending migrations first; only a non-empty pending set triggers one snapshot, one production backup estimate for an existing database, and one preflight evaluation.
- The order is storage snapshot/estimate/evaluation, pre-migration integrity, existing verified backup, `MigrateAsync`, then post-migration integrity. `Allowed`, `AllowedWithWarning`, and `MetricsUnavailable` proceed. `Insufficient` raises a typed Application exception before integrity/backup/schema mutation; unknown status values fail closed.
- An existing database with unavailable footprint metadata uses the monitor's saturating estimate path rather than assuming zero bytes. A fresh/missing database requests zero additional backup bytes and retains the existing no-backup initialization flow. Reserved headroom remains owned by the monitor and is not double-counted in the initializer.
- Cancellation flows unchanged and status-only structured logging does not expose paths, volume roots, connection strings, identities, or raw exception details. R2.4B adds no UI, package, schema/migration, backup/restore workflow, retention, mutable global state, or service locator.

## R2.4A typed storage-monitoring boundary

- `POS.Application` owns `IDatabaseStorageMonitor` and typed snapshot/preflight DTOs only. It exposes no EF, WPF, `FileInfo`, `DirectoryInfo` or `DriveInfo` implementation type and carries no raw exception detail.
- `POS.Infrastructure.Storage` owns the validated policy and framework metadata adapter. It resolves the same canonical database path through a side-effect-free `DatabasePathResolver` API, checks only the main file and exact `-wal`, `-shm` and `-journal` siblings, rejects reparse points and returns typed unavailable states for unsafe or unstable metadata.
- The monitor is metadata-only: it does not open SQLite or file streams, enumerate directories, read rows/content, create missing paths, or delete database artifacts. Existing runtime callers retain the original directory-creation behavior through `ResolveDatabasePath`.
- Warning policy is 5 GiB absolute or 10% available capacity, with 512 MiB reserved headroom. Future backup estimation is footprint plus `max(256 MiB, ceil(10% of footprint))`; checked/saturating arithmetic prevents wrap. `MetricsUnavailable` permits future startup behavior to continue, but R2.4A does not integrate with `DatabaseInitializer`.
- The stateless monitor, metadata provider and framework `TimeProvider` are singleton registrations; options bind under `Infrastructure:DatabaseStorage` with startup validation. R2.4A adds no UI, schema, migration, package, backup/restore or cleanup workflow.

## R2.3C Support Bundle presentation boundary

- Placement is a minimal navigation entry in the authenticated Shell because no Help/Settings/Admin module or support permission exists. Shell authentication remains the access boundary; R2.3C adds no role, capability, schema or migration.
- `SupportBundleViewModel` depends only on the Application `ISupportBundleService` contract and the WPF `ISupportBundleFolderPicker` adapter. The adapter owns `Microsoft.Win32.OpenFolderDialog`; the ViewModel has no Infrastructure/EF/SQLite/ZIP/options/service-locator dependency.
- The modal window uses the existing owner-centered dialog, transient ViewModel/window, scoped service and shared theme conventions. UI states are finite and single-flight. Progress is indeterminate because the Application contract has no percentage progress.
- Consent is explicit and false by default. The UI has no database option and always constructs `SupportBundleRequest(..., IncludeDatabase: false)`. Typed results map to fixed Vietnamese presentation; only typed Success supplies archive path/name, and raw exceptions are contained without message logging.
- Window close/X/Esc while busy is deferred: the ViewModel requests its current CTS cancellation, remains alive until a typed terminal result, then signals the Window to close. A late cancellation followed by Success remains Success. CTS instances are operation-scoped, disposed and never reused.
- R2.3C is automated-verified only. Manual DPI, focus/tab, visual wording, owner, close/cancel and real export acceptance belongs to R2.3D.

## R2.3B safe Support Bundle boundary

- Application owns only `ISupportBundleService` and typed request/result contracts. It has no Infrastructure, EF, SQLite, ZIP or WPF dependency; only `Success` carries an absolute archive path.
- Infrastructure owns composition and export. `IncludeDatabase=true` fails before every artifact/collector/database boundary. The service creates a destination-local unique temporary file with `CreateNew`, writes a fixed entry allow-list, closes/flushes, then commits with a no-overwrite move. Cancellation/failure removes only a temp created by that operation; late cancellation after commit cannot delete the completed archive.
- Diagnostic collectors export typed safe codes/values only: version allow-list; EF known/applied/pending migration IDs; normalized bounded `PRAGMA quick_check`; selected numeric/boolean configuration policy; OS/runtime description and architecture. Individual collector failure becomes `unavailable` while destination/archive failure remains typed at the operation level.
- R2.3A logger and R2.3B exporter share `SafeDiagnosticPolicy` and `ManagedLogPolicy`; there is no second regex/path ownership policy. Logs are top-level managed direct-child regular files only, revalidated around open/read, newest-first and bounded by snapshot length/output budget. Streaming record handling is bounded, UTF-8 aware, replaces overlong records and sanitizes again before ZIP output.
- `POS.Infrastructure.DependencyInjection.AddInfrastructure` registers the scoped service once and validates bounded options. No Support Bundle UI is introduced; R2.3C/D remain separate.

## R2.3A safe logging boundary

- `POS.Application.Common.PosLog` is the centralized allow/deny boundary for structured property rendering. Sensitive names and unsafe values become `[REDACTED]`; raw exceptions are never forwarded. Only exception type and, when present, SQLite primary/extended numeric codes are retained.
- `POS.Infrastructure.Logging.SafeFileLoggerProvider` owns non-recursive, top-directory-only managed files under `%LocalAppData%\POS Enterprise\logs`. It is thread-safe, synchronously bounded, fail-closed on I/O errors, rotates by UTC day/size, flushes on dispose and deletes only exact managed names by oldest-first retention/quota policy.
- Managed ownership additionally requires a canonical direct parent and regular non-reparse attributes before metadata reads. The provider repeats that guard immediately before delete and opens new system-named segments with create-new semantics. Unsafe/disappeared candidates are ignored; no symbolic-link target is resolved or followed intentionally.
- `POS.Wpf.App` registers the provider once through the existing Generic Host composition root. Domain and Application gain no Infrastructure/WPF dependency. Existing Debug fallback receives the already-sanitized `PosLog` state; Trace fallbacks emit exception type only.
- R2.3A provides the managed log source consumed by the R2.3B export service; Support Bundle UI remains unimplemented.

## R2.2 SQLite failure and UX boundary

- `POS.Application` owns `IDatabaseFailureClassifier`, `DatabaseFailureKind` and `DatabaseOperationException`; it remains independent of SQLite/EF/WPF.
- `POS.Infrastructure.Persistence.SqliteFailureClassifier` maps numeric SQLite base codes: busy `5`, locked `6`, disk full `13`, corruption/not-a-database `11/26`, other SQLite failures to unknown. `EfUnitOfWork` and `EfApplicationTransaction` translate classified begin/save/commit failures while retaining the provider exception as the inner cause.
- `SqliteSafeOperationRetry` is finite (maximum three attempts, fixed 100 ms delay) and only for operations the caller has proved read-only/idempotent. Checkout writes and commit are deliberately not routed through it.
- `DatabaseFailurePresenter` supplies sanitized WPF messages. `SalesViewModel` preserves cart/payment state and re-enables checkout after failure; receipt preview remains post-success only. `App` blocks startup before `RunSessionLoopAsync` for unsafe database failures.
- Deterministic acceptance uses only TEMP databases and synthetic data. It covers real persistent locking, atomic four-table counts, duplicate rejection, safe `SQLITE_FULL` simulation, corruption startup blocking, SHA-256 preservation and cleanup.
- Acceptance provenance: Test A Manual PASS; Tests B/C/D NOT MANUALLY RUN and covered by equivalent deterministic automated acceptance PASS.

This boundary completes/closes R2.2 only. R2.3 is now IN PROGRESS through the separate R2.3A boundary above.

## R2.1 Windows process ownership and activation boundary

`POS.Wpf.App` is the composition root for single-instance ownership. It loads configuration, resolves `Infrastructure.DatabasePath` to `DatabaseIdentity`, and asks `WindowsSingleInstanceCoordinator` to acquire ownership before `Host.StartAsync`, database initialization or SQLite access.

- `DatabasePathResolver` produces the same absolute path used by persistence.
- `DatabaseIdentity` canonicalizes the Windows path and derives SHA-256-scoped mutex/pipe names without exposing the raw path.
- `WindowsSingleInstanceCoordinator` owns the mutex and current-user-only named-pipe listener. Same identity is mutually exclusive; different identities remain independent; abandoned ownership is recoverable after crash.
- A contender sends only the fixed activation payload. It does not initialize Host or access the database.
- `WindowActivationCoordinator` retains at most one pending activation across setup/login/shell target transitions. `WindowActivationService` restores minimized windows, attempts foreground activation and falls back to taskbar attention without putting business logic in code-behind.
- Listener cancellation is awaited and named resources are disposed before ownership release. Tests use unique identities and signal readiness only after listener startup returns.

This boundary completes R2.1 only. SQLite busy/locked classification and UX belong to R2.2 and are not implemented here.

## 1. Mục đích và evidence boundary

Đây là bản đồ kiến trúc được tái dựng từ source live, không phải thiết kế suy đoán.

- CapturedAtLocal: `2026-07-31T11:49:51.828+07:00`.
- EvidenceNormalizationReviewedAtLocal: `2026-07-31T12:24:15.171+07:00`.
- Evidence base HEAD: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Current live HEAD reviewed during R0.5E: `70523861949aeb5eefe981633db33f50bc890145`.
- Tại thời điểm capture R0.5C, ba file R0.5B và ba file R0.5C chưa commit. Chúng sau đó đã được formal-closeout/commit/push trong R0.5 tại `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- Không đọc database rows, `__EFMigrationsHistory`, WAL, SHM, journal hoặc backup database.
- Không chạy build, test, Quality Gate, migration hoặc ứng dụng trong R0.5C.

Nhãn độ chắc chắn:

- **Confirmed by source:** type/member/registration/schema hoặc call chain hiện hữu.
- **Confirmed by tests:** source test trực tiếp tồn tại; test không được chạy lại trong checkpoint này.
- **Policy only:** quy tắc trong `AGENTS.md` hoặc roadmap, chưa được chứng minh là runtime guard đầy đủ.
- **Partially verified:** có evidence cho một phần đường đi.
- **Not verified:** không đủ evidence để kết luận.

## 2. Solution topology

Hướng dependency được project references live xác nhận:

`POS.Domain → POS.Application → POS.Infrastructure → POS.Wpf`

Mũi tên biểu diễn tầng phía sau được tầng kế tiếp sử dụng; `POS.Infrastructure` tham chiếu cả Application và Domain, `POS.Wpf` tham chiếu Application và Infrastructure.

| Project | Target framework | Project references | Responsibility | Forbidden dependency direction |
|---|---|---|---|---|
| `POS.Domain` | `net10.0` | Không có | Entity, enum, domain rule/calculator | EF Core, Infrastructure, WPF, tests |
| `POS.Application` | `net10.0` | `POS.Domain` | Use case, contract, DTO, authorization decorator, validation | Infrastructure, WPF, tests |
| `POS.Infrastructure` | `net10.0-windows`, WPF enabled | `POS.Domain`, `POS.Application` | EF/SQLite, repositories, printing, VietQR, password/config adapters | WPF application project, tests |
| `POS.Wpf` | `net10.0-windows`, WPF enabled | `POS.Application`, `POS.Infrastructure` | UI và composition root | Tests |
| `POS.Architecture.Tests` | `net10.0-windows` | Cả bốn production projects | Architecture, domain, application, persistence và UI contract tests | Không project production nào được tham chiếu ngược |

Evidence: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\POS.Domain.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\POS.Application.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\POS.Infrastructure.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\POS.Wpf.csproj`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\POS.Architecture.Tests.csproj`. Dependency rules: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ArchitectureDependencyTests.cs` — `ArchitectureDependencyTests.Domain_must_not_reference_outer_layers`, `Application_must_not_reference_infrastructure_or_wpf`, `Infrastructure_may_reference_application_and_domain`.

## 3. Layer responsibilities

- **Domain — Confirmed by source:** model và transition như `Order.MarkPaid`, `Product.IncreaseStock`/`DecreaseStock`, `PaymentIntent.MarkConfirmed`, `HeldSale.Complete`, `SalesDiscountCalculator.Resolve`; không chứa EF attributes.
- **Application — Confirmed by source:** điều phối use case qua repository/UoW, canonical fingerprint, DTO/Result, permission decorators, receipt factory và recovery semantics.
- **Infrastructure — Confirmed by source:** SQLite/EF mappings, repositories, transaction adapter, migration/backup startup, BCrypt, DPAPI-backed remembered/VietQR stores, WPF receipt renderer/printer.
- **WPF — Confirmed by source:** host/composition root, scope creation, ViewModel/dialog/window, presentation và post-commit print/preview.
- **Tests — Confirmed by source:** một executable xUnit project; test source chứng minh contract, nhưng không được chạy trong R0.5C.

## 4. Domain model map

Không type nào dưới đây được gọi là aggregate root vì source không khai báo khái niệm đó.

| Type | Kind / identity | Responsibility và relationship chính | State/snapshot đáng chú ý; enforcement | Absolute source path |
|---|---|---|---|---|
| `Entity`, `AuditableEntity` | Base entity / integer `Id` | Equality và audit timestamps | Base enforcement trong Domain và interceptor | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Common\Entity.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Common\AuditableEntity.cs` |
| `Product` | Entity / `Id` | Catalog và tồn; thuộc Category | Integer prices/stock, `TrackInventory`, `AllowNegativeStock`, archive state; EF adds an auditable shadow concurrency token | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Product.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs` |
| `InventoryMovement` | Entity / `Id` | Lịch sử biến động tồn liên kết Product/User | before/change/after, type, reference; Domain + EF checks | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\InventoryMovement.cs` |
| `Order` | Entity / `Id` | Đơn bán chứa items và discount snapshot | subtotal/discount/total, payment, cash/change/refund, lifecycle; Domain + Application transaction + EF checks | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Order.cs` |
| `OrderItem` | Entity / `Id` | Snapshot dòng bán thuộc Order | product code/name/unit, unit cost/sale price, quantity, refunded quantity; Domain + EF | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderItem.cs` |
| `OrderItemModifier` | Entity / `Id` | Snapshot modifier của dòng bán | name, price/quantity snapshot | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderItemModifier.cs` |
| `OrderDiscountSnapshot` | Entity / `Id` | Snapshot controlled discount một-một với Order | type, requested/resolved amount, reason, actor/time; Domain + unique FK/checks | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderDiscountSnapshot.cs` |
| `CheckoutRequestJournal` | Entity / `Id` | Durable prepare/completed/acknowledged/abandoned checkout | client request ID, request/quote fingerprints và JSON, Order binding; EF adds an auditable shadow concurrency token | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\CheckoutRequestJournal.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs` |
| `OrderReceiptSnapshot` | Persisted document / `OrderId` | Immutable serialized receipt source | schema version, payload, created time; constructor-only data + EF unique one-to-one | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReceiptSnapshot.cs` |
| `OrderReturn`, `OrderReturnItem` | Entities / `Id` | Return document và returned lines | client request/fingerprint, actor/reason/refund fields, original line snapshots | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReturn.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReturnItem.cs` |
| `OrderReturnBalance` | Persisted state / `OrderItemId` | Tổng quantity/amount đã return cho mỗi order item | returned/refunded counters và concurrency token | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReturnBalance.cs` |
| `HeldSale`, `HeldSaleLine` | Entities / `Id` | Durable held-cart document và line snapshots | client ID/fingerprint, active/completed/cancelled, discount/price totals, completed Order | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\HeldSale.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\HeldSaleLine.cs` |
| `PaymentIntent` | Entity / `Id` | Durable VietQR lifecycle và checkout ownership | created/presented/confirmed/completed/cancelled/expired, payload/account/quote/request snapshots, HeldSale/Order binding, concurrency token | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\PaymentIntent.cs` |
| `PaymentIntentManualResolution` | Entity / `Id` | Audit quyết định recovery thủ công | intent, actor, resolution, reason/time | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\PaymentIntentManualResolution.cs` |
| `User` | Entity / `Id` | Account, role và login state | password hash, active/lock/failed attempts, last login; Domain + BCrypt adapter | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\User.cs` |
| `Category`, `Customer`, `Discount`, `Area`, `RestaurantTable`, `Modifier`, `ModifierGroup`, `OutboxMessage` | Entity/other persisted models | Các vertical slice catalog/customer/legacy restaurant/discount/outbox | Có model/config nhưng không audit thành flow hoàn chỉnh ở R0.5C | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Category.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Customer.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Discount.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Area.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\RestaurantTable.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Modifier.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\ModifierGroup.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OutboxMessage.cs` |
| `SalesDiscountCalculator`, `OrderReturnRefundAllocator` | Domain services | Resolve discount bằng integer/basis points; phân bổ refund | Domain exceptions và checked arithmetic | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\OrderReturnRefundAllocator.cs` |

Enums chính và evidence file: `OrderStatus` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\OrderStatus.cs`; `PaymentMethod` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\PaymentMethod.cs`; `InventoryMovementType` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\InventoryMovementType.cs`; `CheckoutRequestStatus` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\CheckoutRequestStatus.cs`; `HeldSaleStatus` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\HeldSaleStatus.cs`; `SalesDiscountType` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\SalesDiscountType.cs`; `PaymentProvider` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\PaymentProvider.cs`; `PaymentIntentStatus` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\PaymentIntentStatus.cs`; `PaymentIntentManualResolutionType` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\PaymentIntentManualResolutionType.cs`; `Role` — `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Enums\Role.cs`.

## 5. Application contract và service map

| Contract | Implementation / responsibility | Consumers và dependencies | Result / transaction ownership | Evidence |
|---|---|---|---|---|
| `ICheckoutService` | `AuthorizedCheckoutService → CheckoutService`; prepare/process/recover/acknowledge checkout | `SalesViewModel`; repositories, UoW, clock, current user, canonicalizer, receipt factory/serializer | `Result`; CheckoutService mở transaction cho journal/business writes | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\AuthorizedCheckoutService.cs` |
| `IPaymentIntentService` | `AuthorizedPaymentIntentService → PaymentIntentService`; durable VietQR lifecycle/recovery | `SalesViewModel`; payment intent/held sale repositories, UoW, clock | `Result`; service mở transaction cho state transitions | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` |
| `IHeldSaleService` | `AuthorizedHeldSaleService → HeldSaleService`; hold/list/resume/cancel | `SalesViewModel`; held sale/product/payment intent repositories | `Result`; create/cancel transaction do service mở | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` |
| `IOrderReturnService` | `AuthorizedOrderReturnService → OrderReturnService`; validate/canonicalize/process return | `OrderReturnViewModel`; order/return/product/movement repositories, UoW | `Result`; toàn use case mở một transaction | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` |
| `IInventoryService` | `AuthorizedInventoryService → InventoryService`; adjustment/history | inventory/product repositories và UoW | `Result`; adjustment mở transaction | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\InventoryService.cs` |
| `IProductService`, `ICategoryService` | Authorized decorator quanh concrete service | Product/category ViewModels | `Result`; write paths gọi UoW, product opening balance có transaction | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\ProductService.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CategoryService.cs` |
| `IAuthService` | `AuthService`; login, remembered-login restore/logout | `LoginViewModel`, App session loop | `Result`; login state update gọi UoW; session lưu ở singleton current-user adapter | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\AuthService.cs` |
| `IPermissionService` | `PermissionService`; role-capability policy theo current user | Authorized decorators và WPF gates | `Result`/boolean; stateless đối với singleton current session | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PermissionService.cs` |
| `IOrderHistoryService` | `AuthorizedOrderHistoryService → OrderHistoryService`; history/details/receipt snapshot | Order history ViewModel | `Result`; read-only, không sở hữu transaction | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderHistoryService.cs` |
| `IUnitOfWork`, `IApplicationTransaction` | `EfUnitOfWork`, `EfApplicationTransaction` | Application write services | Chuyển concurrency/unique errors; rollback khi dispose chưa commit | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Abstractions\Persistence\IUnitOfWork.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfUnitOfWork.cs` |
| Receipt contracts | `ReceiptSnapshotFactory`, `IReceiptSnapshotSerializer`, repositories và `IReceiptService` | Checkout và UI preview/print | Snapshot tạo/persist trong checkout; physical print không thuộc transaction | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs` |

Repository evidence trọng yếu: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Abstractions\Persistence\IOrderRepository.cs` ↔ `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\OrderRepository.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Abstractions\Persistence\ICheckoutRequestJournalRepository.cs` ↔ `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\CheckoutRequestJournalRepository.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Abstractions\Persistence\IPaymentIntentRepository.cs` ↔ `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\PaymentIntentRepository.cs`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Abstractions\Persistence\IOrderReturnRepository.cs` ↔ `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\OrderReturnRepository.cs`.

## 6. Persistence map

`PosDbContext` là EF Core DbContext scoped do `AddDbContext` tạo. DbSets thật:

`Users`, `Categories`, `Products`, `InventoryMovements`, `Orders`, `OrderDiscountSnapshots`, `OrderItems`, `OrderItemModifiers`, `OrderReceiptSnapshots`, `OrderReturns`, `OrderReturnItems`, `OrderReturnBalances`, `CheckoutRequestJournals`, `HeldSales`, `HeldSaleLines`, `PaymentIntents`, `PaymentIntentManualResolutions`.

Configurations được auto-load bởi `PosDbContext.OnModelCreating`/`ApplyConfigurationsFromAssembly` tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\PosDbContext.cs`. Guard quan trọng:

- Unique: Product code/barcode tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\ProductConfiguration.cs`; normalized username tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\UserConfiguration.cs`; Order code tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderConfiguration.cs`; journal client request/non-null Order binding tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs`; receipt `OrderId` tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReceiptSnapshotConfiguration.cs`; return client request tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnConfiguration.cs`; held-sale client/display/completed Order tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\HeldSaleConfiguration.cs`; discount snapshot `OrderId` tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderDiscountSnapshotConfiguration.cs`; payment intent client/display/completed Order và filtered active HeldSale ownership tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs`.
- Check constraints: money/state equations tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderConfiguration.cs`; journal fingerprint/state shape tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs`; payment VND/provider/status tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs`; return totals/balances tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnConfiguration.cs` và `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnBalanceConfiguration.cs`.
- Concurrency: Product và CheckoutRequestJournal inherit shadow concurrency-token configuration from `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs`, invoked by their configuration files `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\ProductConfiguration.cs` và `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs`; return balance token tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnBalanceConfiguration.cs`; PaymentIntent token tại `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs`.
- Delete behavior: lịch sử chính dùng `Restrict`; owned line/document relationships chọn cascade có chủ ý.

Migration location: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations`.

Latest inventory file: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs`, lấy bằng filename sau khi loại các designer companion và model snapshot.

`D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\PosDbContextModelSnapshot.cs` là EF design-time snapshot của model cuối; R0.5C không sửa và không dùng nó để tuyên bố database runtime đã applied. Không mở database thật nên trạng thái migration applied, dữ liệu và integrity của database production là **Not verified**.

## 7. DI và decorator map

| Abstraction | Concrete/decorator chain | Lifetime | Registration | Notes |
|---|---|---|---|---|
| `PosDbContext` | SQLite EF context | Scoped | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, `AddDbContext<PosDbContext>` | Một context mỗi DI scope |
| Repositories, `IUnitOfWork`, `DatabaseInitializer` | EF implementations | Scoped | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, `AddScoped` registrations | Dùng cùng scoped context |
| Clock, order code, BCrypt, current user, remembered login, permission | Concrete adapters | Singleton | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, `AddSingleton` registrations | Current user giữ session trong RAM |
| Receipt serializer/store provider/builder/service | Infrastructure implementations | Singleton | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, receipt `AddSingleton` registrations | Không giữ scoped DbContext |
| VietQR gateway/image/payload/metadata/stored service | Infrastructure implementations | Singleton | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, VietQR `AddSingleton` registrations | Payload/metadata store dùng Windows protection; không phải bank callback |
| Canonicalizers | Checkout/HeldSale canonicalizers | Singleton | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure` | Deterministic/stateless |
| Product/Category/Inventory/Checkout/PaymentIntent/HeldSale | Authorized decorator → concrete service | Scoped | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.ConfigureApplicationServiceDecorators`, `AddScoped` factory registrations | Permission enforce khi resolve interface |
| Order history/return | Authorized decorator → concrete service | Scoped | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure`, `AddScoped` factory registrations | Decorator registration explicit |
| Dialog/presentation coordinators | Concrete WPF services | Chủ yếu Singleton; `ISalesWindowService` Transient | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.ConfigureDialogServices` | Tự tạo scope cho use case/window |
| ViewModels và Windows | Concrete | Transient | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.ConfigureViewModelsAndWindows` | Resolve trong window/session scope |

Không có `Decorate` package call; decorator order được tạo bằng factory explicit.

## 8. DbContext lifecycle và transaction boundaries

- `AddDbContext<PosDbContext>` đăng ký scoped; connection string được `DatabasePathResolver` dựng từ validated options.
- WPF tạo scope riêng cho database initialization, initial setup, login, shell/sales và nhiều dialog. Scope dispose context và scoped services.
- Application services mở transaction qua `IUnitOfWork.BeginTransactionAsync`, add/mutate tracked entities, gọi `SaveChangesAsync`, rồi `CommitAsync`.
- `EfApplicationTransaction.DisposeAsync` rollback nếu rời scope mà chưa commit; explicit failure paths cũng rollback với `CancellationToken.None` tại các flow quan trọng.
- Checkout transaction bao phủ Order, OrderItem, stock mutation, InventoryMovement, discount snapshot, receipt snapshot, checkout journal completion, held-sale completion và PaymentIntent completion khi tương ứng.
- Return transaction bao phủ return document/lines/balance, `Order.RegisterRefund`, `OrderItem.RegisterRefund`, `Product.RestockFromCustomerReturn` và InventoryMovement.
- Physical preview/print diễn ra sau persisted checkout/history result; lỗi in không rollback committed sale.
- External/UI side effects không nằm trong EF transaction. Automatic retry cho SQLite busy/locked không được xác minh.

## 9. Startup sequence

Confirmed call chain từ `App.OnStartup`:

1. Đặt `ShutdownMode.OnExplicitShutdown`.
2. `Host.CreateApplicationBuilder`, load `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\appsettings.json` và development config `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\appsettings.Development.json` khi phù hợp.
3. `ConfigureApplicationServices` gọi `AddInfrastructure`, đăng ký decorators, dialogs, ViewModels/Windows.
4. Build/start host.
5. Tạo async scope và gọi `DatabaseInitializer.InitializeAsync`.
6. Initializer kiểm tra migrations pending. Fresh database migrate không pre-backup; database hiện hữu có pending migrations phải tạo và verify SQLite backup trước `MigrateAsync`; backup failure chặn migration.
7. Startup database initialization/seed hoàn tất rồi session loop xét initial setup.
8. Thử restore remembered login; nếu không có session thì mở login.
9. Mở Shell trong scope; Shell mở Sales/management windows qua service/scope.
10. Logout quay về login; exit clear in-memory current user, stop và dispose host.

Evidence: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.OnStartup`, `InitializeDatabaseAsync`, `RunSessionLoopAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs` — `DatabaseInitializer.InitializeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs` — `DatabaseInitializerSafetyTests.Existing_database_with_pending_migrations_must_create_verified_backup`, `Backup_failure_must_block_migration`.

## 10. Critical runtime flows

### Cash checkout

- Entry: `SalesViewModel` → payment flow cash validation → scoped `ICheckoutService`.
- Call: authorized decorator → prepare/process `CheckoutService`.
- Transaction/writes: journal, Order/items, stock, InventoryMovement, discount/receipt snapshots; commit precedes successful result.
- UI success: ViewModel receives success, updates cart and invokes receipt presentation.
- Guards: client request/fingerprint, journal unique/concurrency guards, stock re-read, unique Order code; rollback on save/serialization/repository failure.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Concurrent_same_request_commits_exactly_one_business_result`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Failure_after_save_must_rollback_everything`.

### VietQR prepare và checkout

- Entry: `SalesViewModel` creates PaymentIntent, gateway/dialog renders persisted payload, cashier manually confirms.
- Created/presented intent creates no checkout journal/business mutation. Manual yes transitions intent to Confirmed; only then checkout prepare/process runs using persisted checkout request snapshot.
- Checkout transaction binds and completes PaymentIntent with Order. UI success occurs after commit.
- Đây là manual confirmation, không có evidence bank API auto-confirmation.
- Current R0 closeout evidence: Presented PaymentIntent persistence was verified before the QR dialog; VietQR manual acceptance PASS at `7052386`.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentApplicationTests.cs` — `PaymentIntentApplicationTests.Creating_VietQR_intent_must_not_create_checkout_journal`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Checkout_journal_is_created_only_after_manual_confirmation`, `Confirmed_intent_checkout_completes_atomically`.

### PaymentIntent recovery

- Pending intents persist across scopes. Created/Presented recover presentation; Confirmed offers retry using intent ID and persisted snapshot; unsafe legacy snapshot goes manual review.
- Concurrency token, unique IDs/bindings và service transactions guard races. Confirmed intent không được silently abandon.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Confirmed_intent_restart_offers_payment_retry`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentRecoveryActionTests.cs` — `PaymentIntentRecoveryActionTests.Retry_uses_only_payment_intent_id_and_does_not_regenerate_or_reconfirm`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentConcurrencyTests.cs` — `PaymentIntentConcurrencyTests.Concurrent_checkout_same_intent_creates_business_data_once`.

### CheckoutJournal recovery/acknowledgement

- Prepare persists canonical request/quote without business mutation.
- Process transitions Prepared → Completed and binds Order within checkout transaction.
- Recovery query is bounded/user-scoped; replay returns persisted completed result. Acknowledge is idempotent; abandon only valid pre-completion.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Restart_recovery_distinguishes_prepared_completed_acknowledged_and_abandoned`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutJournalPersistenceTests.cs` — `CheckoutJournalPersistenceTests.Journal_concurrency_token_rejects_lost_update`.

### Held Sale save/resume và ownership

- `SalesViewModel` → scoped `IHeldSaleService`. Create snapshots current product/price/discount without stock/order mutation, saves in transaction.
- Resume re-reads product availability/current price/stock and flags review; only creator’s active document is returned.
- Checkout includes HeldSale ID in fingerprint and completes held sale in the sale transaction.
- Active PaymentIntent owns at most one HeldSale through filtered unique index/policy; owned sale is hidden/blocked for resume, cash checkout and cancellation until terminal policy releases it.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs` — `HeldSaleApplicationIntegrationTests.Create_held_sale_persists_snapshot_without_business_mutation`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleCheckoutIntegrationTests.cs` — `HeldSaleCheckoutIntegrationTests.Checkout_with_active_held_sale_completes_atomically`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs` — `HeldSalePaymentOwnershipTests.Two_payment_intents_cannot_own_same_held_sale`.

### Controlled Discount

- UI creates `SalesDiscountRequest`; permission decorator/service checks capability. Domain calculator supports None, fixed integer amount và percentage basis points, validates reason/value and floors percentage.
- Checkout re-resolves against authoritative subtotal; persisted one-to-one `OrderDiscountSnapshot` carries type/value/resolved amount/reason/actor/time.
- Total cannot become negative by Domain/EF constraints. Đây chưa phải R8 Promotion Engine.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Fixed_amount_is_integer_and_bounded`, `Percentage_uses_basis_points_and_floor`, `Snapshot_has_unique_order_fk_and_integer_money`.

### Receipt snapshot, preview, print và reprint

- `ReceiptSnapshotFactory` builds immutable DTO from committed business values before commit; serializer creates versioned deterministic JSON; repository persists exactly one snapshot per Order inside checkout transaction.
- On success UI passes receipt DTO to preview/print service after commit.
- Reprint loads persisted snapshot through order-history service, deserializes and renders it; product live values are not source.
- Physical K80 pipeline exists, nhưng hardware acceptance R11 chưa chạy.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_snapshot_must_not_change_when_product_changes_after_checkout`, `Checkout_service_must_not_depend_on_print_service`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs` — `ReceiptSnapshotSerializationTests.Unsupported_snapshot_version_must_be_rejected`.

### Return/refund

- `OrderReturnViewModel` → authorized `IOrderReturnService`.
- Service canonicalizes client request, validates remaining refundable quantities/amount, opens transaction, creates return document/lines, updates per-line balance, Order refund, OrderItem refunded quantity, restores tracked stock and writes InventoryMovement, then commits.
- Client request unique index and fingerprint provide idempotent replay/conflict; balance concurrency protects double return.
- Refund is recorded as business/document state; external cash drawer/bank transfer execution is **Not verified**. Immutable printable return receipt remains R9 future scope.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Client_request_id_unique_index_must_reject_duplicate`, `Return_balance_constraints_must_reject_negative_values`.

### Stock và InventoryMovement

- Checkout re-reads tracked products, checks `CanFulfill`, adjusts stock and adds Sale movement inside same transaction as Order.
- Inventory adjustment similarly mutates product and movement in one transaction.
- Return adds stock reversal movement in return transaction.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\InventoryIntegrationTests.cs` — `InventoryIntegrationTests.Stock_in_must_commit_product_and_movement_together`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Stale_checkout_must_not_oversell_product`.

### Authentication/RBAC

- `AuthService` normalizes lookup, verifies BCrypt hash, enforces active/locked state, updates failed/success counters and current-user session.
- `RolePermissionPolicy` maps role/capability; `PermissionService` reads singleton current session.
- Authorized decorators enforce Application boundary. `ShellPermissionState` and ViewModels also gate presentation; UI gating không thay thế decorators.
- Tests: `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthServiceIntegrationTests.cs` — `AuthServiceIntegrationTests.Fifth_wrong_password_must_lock_account`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PermissionServiceTests.cs` — `PermissionServiceTests.Unauthenticated_session_must_return_unauthorized`.

## 11. Receipt architecture

| Concern | Implementation/evidence |
|---|---|
| Immutable data | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\DTOs\Printing\ReceiptRequest.cs` — `ReceiptRequest`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs` — `ReceiptSnapshotFactory.Create` |
| Serializer/version | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptSnapshotJsonSerializer.cs` — `Serialize`, `Deserialize`; rejects unsupported version, unknown members, malformed/tampered totals |
| Persistence | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\OrderReceiptSnapshotRepository.cs` — add/get; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReceiptSnapshotConfiguration.cs` — one snapshot per Order, Restrict delete |
| Renderer | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptDocumentBuilder.cs` — builds WPF `FlowDocument` from snapshot |
| Preview | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\ReceiptPreviewService.cs` — opens preview window |
| Physical print | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\WpfReceiptService.cs` — `PrintAsync`, printer options and WPF print queue |
| Reprint source | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderHistoryService.cs` and persisted JSON snapshot, not live Product |
| Failure | `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` transaction handles serialization/persistence failure; preview/print failure is post-commit presentation failure |

Snapshot tests explicitly scan out cost-price and known secret names. Hardware, paper/offline printer acceptance is **Not verified** until R11.

## 12. Return architecture

Implemented: canonical/idempotent request, permission decorator, returned-quantity/refund allocation guards, atomic return/order/item/balance/stock/movement persistence, actor/reason/reference fields và history DTO.

Boundary: physical refund side effect, immutable return-receipt snapshot/print/reprint, Cashbook và Daily Close không được source audit chứng minh hoàn chỉnh; R9 vẫn Not Started.

## 13. Authentication, authorization và security

- Password hashing: `BCryptPasswordHasher`; plaintext chỉ là input verification, không được log/persist.
- Account: normalized username, role, active flag, failed attempts, lock timestamp.
- Session: `CurrentUserService` singleton giữ actor trong RAM và clear khi logout/exit.
- Remembered credential: `WindowsRememberedLoginStore`; protected Windows storage adapter.
- Enforcement: scoped authorized Application decorators; WPF additionally controls visibility/enabled state.
- Audit: auditable entity interceptor và actor snapshots tồn tại ở các use case; comprehensive immutable audit log UI là future R4.
- Không đưa hash, account data, recipient account snapshot hoặc config secret vào Project Memory.

## 14. WPF MVVM và navigation map

| View | ViewModel | Resolution/creation | Application services | Navigation/code-behind boundary |
|---|---|---|---|---|
| `FirstRunSetupWindow` | `FirstRunSetupViewModel` | Transient trong setup scope | `IInitialSetupService` | App owns modal lifecycle; code-behind presentation |
| `LoginWindow` | `LoginViewModel` | Transient, scope mỗi login | `IAuthService` | App session loop |
| `ShellWindow` | `ShellViewModel` | Transient, shell scope | permission state, dialog/window services | Shell owns navigation commands |
| `SalesWindow` | `SalesViewModel` | Transient trong scope do `SalesWindowService` tạo | checkout, payment intent, held sale, product, history | Modal sales window; ViewModel owns use-case calls |
| `OrderHistoryWindow` | `OrderHistoryViewModel` | Transient/window service | `IOrderHistoryService`, receipt preview | Window service navigation |
| Return window | `OrderReturnViewModel` | Constructed in scope by `OrderReturnWindowService` | `IOrderReturnService` | Confirmation/presentation services |
| Product/category/inventory dialogs | Corresponding transient ViewModels | Singleton dialog service creates scope/resolve | Authorized application services | Dialog services own windows |

Code-behind chủ yếu binding, focus, keyboard/window/presentation. Một số sales/discount windows chứa input formatting và interaction logic; business persistence vẫn đi qua Application services. Việc audit toàn bộ code-behind để chứng minh không có business rule ở mọi line là **Partially verified**.

## 15. Concurrency, idempotency và restart map

| Operation | Identity/guard | Persistence constraint/recovery | Direct test evidence | Status |
|---|---|---|---|---|
| Checkout | `ClientRequestId` + canonical/quote fingerprint | Unique journal ID/Order binding, concurrency token, recovery statuses | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Concurrent_same_request_commits_exactly_one_business_result` | Confirmed by tests |
| Order code | Generated code | Unique `Orders.OrderCode`; race rolls back | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Order_code_race_must_rollback_stock_and_movement` | Confirmed by tests |
| Held sale create | client ID + fingerprint | Unique client/display IDs; replay/conflict | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleConcurrencyTests.cs` — `HeldSaleConcurrencyTests.Concurrent_same_hold_payload_creates_one_document` | Confirmed by tests |
| Held sale checkout | HeldSale ID in checkout fingerprint | unique completed Order + status/concurrency | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleConcurrencyTests.cs` — `HeldSaleConcurrencyTests.Concurrent_checkout_same_held_sale_creates_business_data_once` | Confirmed by tests |
| PaymentIntent create/checkout | client ID, quote fingerprint, intent ID | unique IDs/order, concurrency token | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentConcurrencyTests.cs` — `PaymentIntentConcurrencyTests.Concurrent_checkout_same_intent_creates_business_data_once` | Confirmed by tests |
| Payment ownership | HeldSale ID | filtered unique index for active statuses | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs` — `HeldSalePaymentOwnershipTests.Two_payment_intents_cannot_own_same_held_sale` | Confirmed by tests |
| Return | client ID + canonical fingerprint | unique client ID, balance concurrency | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Client_request_id_unique_index_must_reject_duplicate` | Confirmed by tests |
| Product stock | EF shadow concurrency token for auditable Product | EF concurrency conflict, transaction rollback | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\InventoryIntegrationTests.cs` — `InventoryIntegrationTests.Stale_product_update_must_return_concurrency_conflict` | Confirmed by tests |

## 16. Security/privacy boundaries

- Options bind config; actual secret values không được ghi vào tài liệu.
- Password hash chỉ qua `IPasswordHasher`; không đưa hash thật vào log/report.
- Receipt serializer tests exclude cost price và known secret keys; renderer hides internal VietQR metadata from customer notes.
- VietQR recipient/payload stores use Windows-specific protected storage; snapshot account fields tồn tại trong PaymentIntent nhưng không được xuất vào Project Memory.
- Authorization boundary chính là authorized Application decorators; WPF gating chỉ là presentation.
- Project Memory cấm database/customer data; log/report phải sanitize theo `AGENTS.md`.

## 17. Critical file index

### Domain

- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Order.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Product.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\CheckoutRequestJournal.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\PaymentIntent.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs`

### Application

- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs`

### Infrastructure

- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\PosDbContext.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfUnitOfWork.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptSnapshotJsonSerializer.cs`

### WPF

- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\ViewModels\SalesViewModel.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\SalesPaymentFlowService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\ReceiptPreviewService.cs`

### Tests và migrations

- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ArchitectureDependencyTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\PosDbContextModelSnapshot.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs`

## 18. Boundaries và deferred verification

- Database runtime/applied migration, production rows, real printer/scanner/cash drawer và bank settlement: **Not verified**.
- Full code-behind classification, all legacy restaurant/customer/discount/outbox flows: **Partially verified**.
- Automatic SQLite busy/locked UX, support bundle, restore wizard, hardware/load acceptance, installer/license/update/pilot thuộc R2–R13.
- Controlled Discount, Return và receipt printing hiện hữu chỉ là partial product capability; R8, R9 và R11 vẫn Not Started.
- Future roadmap gaps là deferred scope, không phải confirmed runtime bugs.
