# KNOWN ISSUES — POS ENTERPRISE RETAIL V1

## Activity Log checkpoint — 2026-09-02

- The Activity Log correction is accepted by the user on the normal interactive Windows profile. Root cause was confirmed in `BulkProductOperationService`: all four Bulk operations persisted `EmployeeUpdated` because the previous audit CHECK allowed only actions `1..10`. New writes use dedicated product-Bulk action codes and the read resolver provides strict compatibility mapping for qualifying legacy rows; historical audit rows are not rewritten, deleted or migrated for text changes.
- Activity Log presentation now uses a responsive filter grid, action/business hierarchy, friendly product target, result localization/badges, ellipsis/tooltips, grouped details and collapsed technical metadata. The user opened the interface and confirmed it works well.
- Six historical DPAPI secure-storage failures are resolved as an environmental test-host limitation under `desktop-f0pkjb1\\codexsandboxoffline`; normal interactive Windows profile passed all secure-storage tests. No production DPAPI defect is established and no secure-storage code was changed.
- Physical label printer, scanner readback, real-device mm calibration, complete 100%/125%/150% DPI and unperformed Print to PDF save/overwrite/cancel scenarios remain pending. Employee Account creation/management and R6+ remain deferred/HOLD.

## R5.3–R5.4 closeout review — 2026-09-02

- User-run normal interactive Windows profile Quality Gate passed at `1455/1455`, `0 failed`, `0 skipped`, with `-SkipEfCheck` omitted. The Codex sandbox Gate was not rerun in this closeout.
- The six earlier DPAPI secure-storage failures are retained below as historical evidence and are now resolved as an environmental test-host limitation. No production DPAPI defect was established and no secure-storage production code was changed.
- R5.3 engineering/persistence/manual core acceptance and R5.4 implementation/manual UI acceptance are recorded. Physical printer, scanner readback, real-device calibration, full DPI matrix and unperformed Print to PDF save/overwrite/cancel scenarios remain pending.
- Activity Log audit-label mismatch and dense-layout issues are newly recorded below. No Audit writer, schema, Activity Log UI, localization mapping, Employee Account or Bulk pipeline change is included in this checkpoint.

## R5.4 UX closeout verification boundary — 2026-09-01

- Auto-preview and compact production controls are verified by the real STA `LabelPrintWindow` harness: `28/28 PASS`. The test uses a deterministic manual debounce scheduler; no UI-thread sleep or physical print is used.
- Invalid quantity clears the old preview and page state, hides pagination and shows `In tem`; valid quantity restores preview and `In N tem`. Validation is inline per row with one multi-row summary. Physical visual/DPI confirmation remains for the user.
- The printer capability warning is user-facing and does not use the word `capability`; it remains a warning because incomplete driver metadata does not prove that printing will fail. PDF save-dialog ownership, physical printer/scanner/calibration and DPI checks remain pending.
- Historical sandbox result: `1449 PASS / 6 FAIL / 0 SKIP`; the same two RememberedLogin and four VietQR DPAPI secure-storage failures are retained as resolved environmental test-host limitation evidence. No DPAPI/profile/credential/VietQR/RememberedLogin change was made.

## R5.4 UI hotfix verification boundary — 2026-09-01

- Production-control STA harness now verifies real `LabelPrintWindow` command bindings and ButtonAutomationPeer invocation: `26/26 PASS` with refresh, preview, Close/Esc, test print, real print, preset, error/cancel and footer hit-test coverage. The user must still re-run the five actions on the fresh Release binary because automated WPF is not physical click/UIA/DPI acceptance.
- Real `Microsoft Print to PDF` output/save-dialog ownership and cancel behavior, physical label printer spool acceptance, scanner readback, label media calibration and 100/125/150% DPI visual checks remain pending. Automated tests never send a physical job.
- The six secure-storage failures are unchanged historical DPAPI CurrentUser/profile evidence under the sandbox; no DPAPI/profile/credential/VietQR/RememberedLogin change was made. The subsequent normal interactive profile verification passed the official Quality Gate.

## R5.4 — Physical label-print acceptance boundary — 2026-09-01

- Renderer, paginator, preview, fake dispatcher, quantity, template/mm validation and production WPF composition are automated-verified. Chưa có nghiệm thu máy in tem vật lý, scanner readback, media calibration hoặc click/UIA ở DPI 100/125/150%; không tuyên bố đã in vật lý.
- Nếu Windows không trả máy in hoặc driver không trả capability đầy đủ, preview vẫn dùng được và lệnh in bị khóa/dispatcher trả lỗi phân loại; việc chọn máy in biến mất không tự fallback sang máy khác. In thử vật lý chỉ thực hiện khi người dùng chủ động chọn máy và xác nhận.
- Audit thành công/thử/lỗi chưa được nối vì audit hiện hữu chưa có contract cho job in tem; không tạo hệ thống log song song trong R5.4. Đây là gap của lượt ổn định sau R5.4, không phải lý do để ghi audit thành công giả.
- Settings tem gần nhất được lưu bằng JSON không nhạy cảm trong settings root; không đọc/ghi/reset manual/canonical database và không dùng DPAPI.

## R5.4 / R5.3 verification status — 2026-09-01

- Historical sandbox full suite: `1442 PASS / 6 FAIL / 0 SKIP / 1448`; sáu failure là 2 RememberedLogin + 4 VietQR secure-storage dưới tài khoản sandbox với cùng DPAPI CurrentUser/profile-not-loaded evidence. Không sửa DPAPI, profile, credential, VietQR hoặc Remembered Login.
- Historical sandbox Quality Gate stopped at automated tests; vulnerability scan và EF pending-model riêng PASS. Normal interactive Windows profile verification later passed the full workflow at `1455/1455`.

## R5 Bulk verification boundary — 2026-08-31

- Automated Bulk persistence/readback is now covered by `BulkProductPersistenceIntegrationTests.Selected_B_and_C_preview_commit_and_readback_preserve_A_and_inventory` on an owned TEMP SQLite fixture. It verifies B/C through all four metadata operations using fresh contexts after commit; A remains unchanged, stock and inventory-movement count remain unchanged, and aggregate audit summaries report requested/changed count 2. It does not use the manual or canonical database.
- User manual evidence is limited to selection/tick riêng → mở Bulk and the supplied Ngừng bán preview/readback screenshot. Codex has not performed physical click/UIA or 100/125/150% DPI acceptance; no broader manual PASS is recorded.
- Current host fails `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)` before any store file write with `System.Security.Cryptography.CryptographicException`, HResult `0x80131501`, message that the user profile may not be loaded for the current thread/user context. The six affected tests therefore fail at their existing success assertions: 2 RememberedLogin and 4 VietQR store/pipeline tests. This is an environment/host-profile blocker evidenced by a direct ephemeral probe, not a confirmed production secure-storage defect; no DPAPI/profile/credential change was made.
- Historical full Release and Official Quality Gate were `1430 PASS / 6 FAIL / 0 SKIP` under the sandbox host. The user-run normal interactive profile workflow later passed at `1455/1455`; this resolves the environment limitation without changing tests or secure-storage behavior.

## POS-VER-008 — Bulk row-selection physical acceptance pending

- Production checkbox binding regression passes in the WPF harness for partial selection and parent UI updates. Isolated fixture persistence/readback now passes through the real Bulk service and fresh contexts; manual click/Space, reload/filter/page lifecycle and DPI acceptance remain pending; no user database was used.

## POS-VER-007 update — 2026-08-31

- Production WPF binding regression now passes for no-selection/true/false and repeated selection changes. Physical desktop click-through and DPI acceptance remain unperformed; isolated fixture persist/readback passes, while no user fixture was opened for automated verification. The six full-suite DPAPI secure-storage failures remain outside this Bulk scope and block the official gate.

## POS-VER-007 update — 2026-08-31

- Selection threshold and typed status validation are source/build verified. A production-control WPF binding regression for the status ComboBox, partial selection click-through and persist/readback is still not automated or manually closed.

## POS-VER-007 — Physical status ComboBox acceptance pending

- Typed status state and false/no-selection validation are implemented and covered by the production-window STA binding harness; physical WPF binding/click-through for both directions and mixed/no-op sets still requires manual acceptance.

## POS-VER-006 verification update — 2026-08-31

- Current Bulk snapshot source is build-verified and focused-tested. Full Release and Official Quality Gate remain open because six unrelated DPAPI secure-storage tests fail in this environment.

## POS-VER-006 — R5 Bulk/export physical acceptance remains pending

- Status: Open verification gap; no confirmed product defect after source hotfix.
- Scope: physical WPF Bulk layout at 100/125/150% DPI and Save dialog new-file/overwrite/cancel/failure flows.
- Evidence: Release WPF build succeeded and focused Bulk/ProductExport tests passed `7/7`; full build was blocked by a locked test DLL.

## R5.1–R5.3 hotfix A/B/C — 2026-08-30

- Automated composition/behavior verification đạt `29/29 PASS` Debug và Release; normal-host full Release đạt `1434/1434 PASS`, build `0/0`, Quality Gate/vulnerability/EF checks PASS. Bulk UI đã có đường vào thật và export/template caller đã nhận kết quả typed.
- Physical click-through, UIA/DPI, save dialog/file-open thực tế và người dùng chọn từng bulk operation vẫn cần manual acceptance; measure/arrange và STA composition không được coi là visual/manual PASS.
- R5.3 label printing vẫn blocked bởi không có production label renderer/printer pipeline; không dựng nút giả hoặc tái sử dụng receipt printing.

## R5.1–R5.3 nghiệm thu hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Ba lỗi được tái hiện ở boundary UI: các dialog export/bulk được tạo độc lập nhưng tham chiếu `ShellCaptionTextStyle` khai báo cục bộ trong `ShellWindow`; editor trả hàng bind trực tiếp `int` với `PropertyChanged` nên thao tác xóa/gõ bị source cũ ghi ngược; Shell vẫn giữ `SelectedProduct` và single command active trong bulk mode.
- Sửa tối thiểu: dùng `CaptionTextStyle` dùng chung đã tồn tại, thêm `ReturnQuantityText`/`RestockQuantityText` làm trạng thái nhập và chỉ chuyển sang số typed để service nhận; thêm guard/visibility/column-width presentation cho bulk mode. Không nới validation, permission, transaction, archive, inventory movement hay security.
- Automated composition/behavior coverage mới và regression liên quan đạt `1432/1432 PASS` trên normal Windows Release, build `0/0`; physical dialog save/click/UIA/DPI và return click-through vẫn cần manual acceptance. R5.3 label printing vẫn blocked bởi thiếu renderer/printer pipeline production.

## Inventory History type/date correction — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The History movement filter/display previously omitted the production `CustomerReturn` type and showed it as unknown; it now uses the business label “Khách trả hàng” without merging it with `Refund`.
- The default range previously began 31 calendar dates before today; it now begins 29 days before today, preserving the inclusive local-day boundary conversion. Focused Debug/Release is `116/116 PASS`.
- Physical WPF/UIA/DPI verification remains a manual acceptance item. No canonical or persistent manual database was used.

## Selection and global Inventory History hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The product rail previously had no user-facing way to clear the bound `SelectedProduct`, leaving product actions dependent on a selection that users could not explicitly remove. The new `ClearSelectedProductCommand` clears the binding-backed selection and notifies all dependent commands without a data reload or write.
- The global history menu previously read the selected row and passed its code/name to `ShowHistoryAsync`, so a menu action described as general history opened with an implicit product criterion. It now calls the same service with no product criterion; product search remains available inside Inventory History.
- Focused coverage is `28/28 PASS` in Debug and Release; relevant Release coverage is `286/286 PASS`; normal-host full Release is `1413/1413 PASS`, failed/skipped `0/0`, and the Quality Gate passed. Physical WPF click/UIA/DPI acceptance remains manual pending; the persistent manual database was not used or modified.

## Persistent manual database — ACTIVE USER SETUP — 2026-08-30

- The long-lived manual database is `C:\Users\pc\AppData\Local\POS Enterprise\ManualAcceptance\pos-enterprise-manual.db`. It was created by the application on first launcher use and reached the initial account setup screen; the user must create and remember the account through the UI.
- The normal isolated launcher intentionally creates a new TEMP snapshot, so it must not be used for daily persistent work. Use `scripts\Start-POS-PersistentManual.ps1` for this profile; it points directly to the same file and does not copy, reset, delete or select another database.
- The profile is currently open by the POS process. Do not run a second instance against it; the existing application single-instance boundary will report the conflict. The database, WAL/SHM sidecars, settings and logs remain outside Git and are not test fixtures.

## Inventory History navigation/filter-card hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The reported clipped `Tìm` control was caused by the search row reserving a 56-DIP column while the inherited shared button style required a larger minimum width. The fix uses a local bounded search-button style and an expanding input column, with a keyboard-accessible in-field clear button.
- The duplicate Shell navigation entry was not a second business route; it was a separate `OpenInventoryHistoryCommand`/button added beside the existing `Kho & lưu trữ → Lịch sử kho` command. It is removed without changing the existing permission-gated route.
- The filter card now uses local light surface/border styles and keeps all filter actions together. Automated WPF construction/arrange verifies the search input and button fit inside the sidebar at the minimum supported layout; physical screenshot, click-through, UIA and DPI checks remain pending because no inspectable top-level WPF HWND is exposed here.
- Normal-host full Release and the official Quality Gate pass at `1410/1410`, with no security, persistence, query or shared-style change.

## Inventory History redesign — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The confusing behavior came from the only existing open route carrying the selected product as a hidden `ProductId`, while the visible text search was a separate criterion. The redesigned route uses a single visible product search criterion and adds a permission-gated general Shell entry; reset/clear cannot retain a stale product scope or select the first product.
- The history table, KPI cards and detail now share the applied database query state. The KPI cards count records/increase/decrease events across the full filtered result and do not sum quantities from different units. No inventory write or audit mutation is involved.
- Normal-host full Release passed `1409/1409`; the Codex sandbox remains unable to pass the six known CurrentUser DPAPI/local-secure-storage failures (`1403/1409`), while the normal-host rerun of the three affected classes passed `12/12`. No security behavior was changed.
- Physical click-through, UIA/accessibility and DPI checks remain pending because this environment exposes no inspectable top-level WPF HWND.

## Inventory History / Adjustment / Category UX hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Inventory History đã trả về bố cục sidebar/workspace cũ nhưng giữ tìm kiếm trực tiếp theo tên, mã và barcode của hotfix trước.
- Điều chỉnh tồn kho bắt đầu với số lượng rỗng; Category form/list không còn buộc người dùng nhập hoặc xem thứ tự hiển thị. `DisplayOrder` vẫn tồn tại để tương thích dữ liệu và contract persistence.
- Normal-host full Release `1407/1407 PASS`; kiểm thử click-through WPF, UIA accessibility và DPI vật lý vẫn cần thực hiện thủ công trên máy có HWND.

## Inventory History UX/query hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The reported stale-history behavior was caused by `Tìm` querying only a bounded Product dropdown; the history request still used the previously selected ProductId. The hotfix sends the text filter through the history repository query and removes the second filtering action.
- Normal-host full Release passed `1399/1399`; the sandbox still shows the six known CurrentUser DPAPI/local-secure-storage failures (`1393/1399`) and they pass on normal Windows. No security behavior was changed.
- Physical click-through, UIA/accessibility and DPI checks remain pending because this environment exposes no inspectable top-level WPF HWND.

## R5.1C/D UX remediation — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The reported `UnitName`/`SalePrice`/`CostPrice` missing-column behavior was a deterministic catalog-alias gap, not a Category lookup failure. Compact canonical headers are now recognized; a missing or inactive `Drinks`/`Food` reference is shown separately with the category name and corrective guidance.
- The wizard is simplified for store users with staged actions, progressive mapping/details, grouped issues and business-language results. Automated WPF construction/arrange and import regressions pass. Physical click-through, UIA/accessibility and DPI acceptance remain pending because this environment exposes no inspectable top-level HWND.
- Synthetic fixtures remain ignored under `D:\Projects_1\POS_Enterprise_DotNet\artifacts\R5.1C-manual-fixtures`. Manual testing must create or activate the exact categories used by a fixture in the isolated setup; no category is auto-created and no canonical database is used.

## R5.1C/D — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The Product CSV/Excel wizard is implemented and its automated import-focused and normal-host full-suite gates pass. Physical click-through, UIA accessibility inspection and DPI-equivalent visual acceptance remain manual because this environment exposes no inspectable top-level WPF HWND. The prepared synthetic fixtures are under the ignored `artifacts/R5.1C-manual-fixtures` directory.
- A fresh isolated launcher invocation reaches `InitialSetupWindowReady`; the launcher does not accept an import-file argument, so manual acceptance must choose the fixture after opening the authorized Product & Inventory wizard. No canonical database or runtime settings are used by the provided isolated smoke procedure.
- The six CurrentUser DPAPI secure-storage/VietQR failures remain a sandbox-only environment limitation and pass on the normal Windows host. No security assertion, encryption, ACL or DPAPI behavior was changed by R5.1C/D.

## R5.1B — CLOSED — 2026-08-29

- Transactional Product import is complete for the Application/Infrastructure checkpoint. The WPF Import Wizard, file picker, mapping surface and user-facing preview confirmation remain intentionally deferred to R5.1C.
- The production audit model currently has an action check constraint limited to values 1–10. R5.1B reuses the existing valid audit action with typed Product-import business/target metadata and a sanitized allowlisted summary; a new action/migration was intentionally not introduced.
- Six secure local-storage/VietQR tests still fail only in the Codex sandbox DPAPI/profile context and pass on the normal Windows host. This is an environment limitation; no security assertion or encryption boundary was changed.

## R5.1A — CLOSED — 2026-08-29

- The secure Product preview foundation intentionally does not import or mutate Product, Category, Inventory or Audit data, and has no WPF Import Wizard. Import execution, conflict policy and user-facing wizard remain deferred to R5.1B/C/D.
- The parser uses no additional CSV/XLSX package: `.xlsx` is read through the BCL ZIP/XML APIs with bounded entries and hardened XML settings. This keeps the dependency surface unchanged; formula evaluation and external resources are rejected.
- Six secure local-storage/VietQR tests fail only under the Codex sandbox DPAPI/profile context and pass on the normal Windows host. This remains an environment limitation, not an R5.1A production defect; security assertions and CurrentUser protection are unchanged.

## Final Employee visual correction — CLOSED — 2026-08-29

- Resolved the Employee DataGrid right-edge/header clipping and inconsistent column alignment with Employee-local styles, a wrapping tooltip header, automatic horizontal scrolling and a safe trailing inset. Identity, filter, action and footer surfaces remain responsive without changing commands or business behavior.
- The six DPAPI secure-storage/VietQR failures were environment-only: the Codex sandbox user context cannot use `CurrentUser` DPAPI, while the normal Windows user passed all six tests and the full Release/official gate. No secure-storage production change was made.
- Audit Log was not changed. Development Freeze remains ACTIVE; R4.2/R4.3/R4.4 remain CLOSED/PRESERVED and R5.1 remains NOT STARTED.

## Employee + Audit UI/runtime follow-up — CLOSED — 2026-08-29

- Resolved the remaining Employee presentation gap: the redundant header range is gone, footer pagination remains useful, and the identity/status header is bounded by explicit responsive Grid columns. Long employee IDs wrap at constrained widths and retain a complete tooltip.
- Resolved the remaining Audit UX gap: users can explicitly search, clear/reload filters, retry failures and distinguish loading, true-empty, filtered-empty, failure and no-selection detail states. Invalid date ranges are rejected before the Application query boundary.
- Existing `ViewAuditLog` authorization, database-side filtering/paging, newest-first stable ordering, cancellation, append-only audit and allowlisted redaction are preserved. No migration was needed.
- Remaining limitation: this desktop exposes no top-level HWND/UIA, so physical click and DPI smoke for the requested display matrix remain manual and are not claimed here. R5.1 remains NOT STARTED and Development Freeze is ACTIVE.

## Post-R4.4 handover-freeze hotfix — CLOSED — 2026-08-29

- The observed Audit load dialog was caused by the missing `IAuditLogService` registration in the WPF composition root, not by an invalid audit query or migration. The service is now registered and the Audit ViewModel contains a sanitized module-level load/detail boundary with retry.
- Employee summary and identity/status layout now use bounded WPF regions, so the list summary and long identity metadata cannot cross the master/detail boundary or overlap the status card. Real WPF bounds regression coverage is included.
- Release isolated startup reached `DatabaseInitialized` and `ShellWindowReady` with the current executable. This environment still exposes no top-level HWND for external UIA; physical Audit navigation and DPI smoke remain manual checks and are not claimed as observed.
- R4.3 and R4.4 are closed, R5.1 is not started, and development freeze is active again after this narrowly scoped hotfix.

## R4.4 Secure Audit Log UI — CLOSED — 2026-08-29

- The secure audit viewer is append-only and permission-gated by `ViewAuditLog`. It uses the existing audit store, typed action metadata, historical actor/target snapshots, a safe terminal abstraction (`TERM-ISOLATED` for isolated runs), and an allowlisted change-set contract. Passwords, hashes, tokens, connection strings and raw exceptions are not stored or displayed.
- Filtering and paging remain database-side with stable UTC ordering; local-time conversion is presentation-only. Existing historical rows with empty new metadata use explicit unknown/fallback labels rather than invented before/after values.
- Real WPF construction/layout and Release isolated startup passed. External UIA exposes no top-level HWND in this environment, so physical click and DPI smoke remain manual checks.
- R4.3/R4.4 are closed; development freeze is active and R5.1 Product CSV/Excel Import is recorded as the next checkpoint, not started.

## R4.3 Role and Permission Management — CLOSED — 2026-08-29

- Built-in role management is intentionally read-only in this checkpoint because the current production model is an enum-backed role policy, not database-backed custom roles. Assignment continues through the existing Employee and Account boundary.
- The new matrix is permission-gated by `AssignRolesPermissions`; non-Administrator roles retain the existing least-privilege policy. R4.4 secure audit viewing is the next checkpoint.

## R4.2 final Employee detail visual polish — CLOSED — 2026-08-28

- Resolved the selected-detail presentation gap: identity metadata and employment/account states are now composed in one bordered header, profile values are grouped in a warm-neutral information card, and lifecycle/profile commands sit in a deliberate action strip.
- Empty optional phone/email values render as `Chưa cập nhật`; long employee codes remain bounded with ellipsis and retain the complete value in a tooltip. Existing commands, authorization and confirmations remain unchanged.
- Real WPF layout verification passed at `1180×720`, `1366×768`, `1280×720` and `1000×620`. This desktop still has no inspectable top-level HWND for external UIA, so physical 100%/125%/150% DPI smoke remains an honest manual limitation.
- R4.3 and R4.4 remain NOT STARTED.

## R4.2 final manual UI correction — CLOSED — 2026-08-28

- The prior manual observations were reproduced from the current source: the filtered-empty card combined a dynamic Clear Filters button with a second Add button, the filtered state was derived from the visible page only, and the Role ComboBox had a valid selected object but no selected-content text after real WPF layout.
- The correction uses authoritative global/filtered counts, separate clear/add commands, a stable Role sentinel rendered by its friendly display value, a profile-first create form with X/Hủy dirty protection, and content-driven responsive empty/create regions.
- The current Release binary reached `ShellWindowReady` from the exact post-baseline build. No old `Cấu hình cửa hàng chưa sẵn sàng` production source route remains; external UIA still exposes no top-level HWND, so final physical click/DPI visual acceptance remains a manual limitation and is not claimed here.
- R4.3 and R4.4 remain NOT STARTED. Store Setup UX and the dark grouped sidebar remain preserved.

## Post-modernization manual UX hotfix — 2026-08-28

- Resolved: Sales no longer blocks on a missing/unavailable optional backup directory or stale Store Setup snapshot. Core store/database errors remain blocking; optional backup and tax issues are surfaced as warnings.
- Resolved: the readiness path now reloads the external settings snapshot before evaluation and uses an actionable owner-correct dialog with friendly blockers and an authorized Store Setup action.
- Resolved: Employee Role filter blank-first-render, filtered-empty misclassification, empty-state Add behavior, reduced-height empty-card layout, malformed clear-filter glyph and long-code overflow presentation.
- IsolatedTest finding: each launcher invocation intentionally owns a fresh external settings root and copies only the database. Prior isolated Store Setup state is not expected to persist across invocations; no production settings or secrets were copied.
- Remaining limitation: this execution desktop exposes no top-level HWND/UIA for the isolated WPF process; the bounded launcher did not reach `ShellWindowReady`, so visual click, DPI and pixel-comparison acceptance remain manual. R4.3 remains NOT STARTED.

## R4.2 Employee and Account UI modernization — 2026-08-28

- No confirmed production defect was introduced by the modernization. The implementation preserves the Application authorization/security boundary and uses production data/state only.
- Verification limitation: the current execution desktop exposes no inspectable Win32 top-level HWND/UIA element for the isolated WPF process, and the bounded exact `Start-POS-IsolatedTest.ps1` run did not reach `ShellWindowReady` within 90 seconds while initializing the copied scenario database. Real WPF resource construction, ViewModel behavior and the official Quality Gate passed; visual clicks, DPI scaling and pixel-level comparison remain manual and are not claimed here.
- The two Release full-suite named-pipe failures remain the known restricted-environment condition (`UnauthorizedAccessException`/activation assertion in `SingleInstanceInfrastructureTests`); the official outside-sandbox Quality Gate passed `1350/1350` without weakening the current-user-only IPC ACL.
- Store Readiness remains a separate manual observation and is not claimed fixed. R4.3 remains NOT STARTED.

## Shell sidebar and inventory navigation hotfix — 2026-08-27

- Resolved: the grouped `ShellProductsNavigationButton` had no command and a hard-coded selected style. `ShellRoute`, `NavigateToProductsCommand` and route-derived selected/parent expansion state now make Dashboard/Category/other-module → Products transitions deterministic without duplicate product loads.
- Resolved: the fixed `232`-pixel sidebar, default Expander indicator plus manual chevron, four-column KPI layout and unconstrained star-only product grid caused cramped labels and right-edge clipping. The production UI now uses deliberate `276`/`76` responsive modes, one chevron, compact tooltips, a fixed footer, two-column narrow KPI layout, column minimums and horizontal grid scrolling.
- The Store Setup production layout and behavior were not changed. No schema, migration, authorization boundary, DI lifetime or persisted data was changed.
- Remaining limitation: the execution desktop exposed neither a Win32 top-level HWND nor a UIA top-level element for the isolated process. `ShellWindowReady`, real WPF construction/bindings, production isolated product loading and automated width/route contracts passed, but final visual checks at 100%/125%/150% scaling and physical click transitions remain manual.
- Official Quality Gate passed with `1348/1348`, failed `0`, skipped `0`, zero build warnings/errors, vulnerability scan PASS and EF pending-model PASS. R4.3 remains NOT STARTED.

## Store Setup UX polish and Shell navigation UX — 2026-08-27

- The Store Setup UX now hides implementation values and keeps persisted compatibility fields intact. VietQR remains configured only in the dedicated VietQR module; cash-drawer hardware integration is intentionally deferred.
- Printer discovery/test and scanner test workflows are deterministic and report actionable Vietnamese states. Automated acceptance uses a fake receipt adapter; no physical printer is claimed. The real production receipt abstraction is used by the In thử command.
- The execution desktop does not expose a top-level Win32 HWND to external UIA for the isolated launcher, so no visual click/navigation PASS is claimed. Real WPF view construction, resources, ViewModel initialization and isolated startup milestones passed; manual visual smoke remains the honest final visual check.
- Official Quality Gate passed outside the restricted IPC sandbox with `1344/1344` tests, `0` failed, `0` skipped, zero build warnings/errors, vulnerability scan PASS and EF pending-model PASS. R4.3 remains NOT STARTED.

## R4.2 Employee and Account UI hotfix — 2026-08-27

- Resolved the real post-closeout navigation defect: `EmployeeManagementWindow.xaml` referenced `AuthLabelStyle` without a merged/shared resource, causing `XamlParseException` during the production window constructor. The shared authentication styles are now available through the Typography theme; no schema or migration change was required.
- The navigation boundary now logs a sanitized complete exception chain and presents a module-loading message instead of the generic startup-failure message. Raw exception details remain hidden from users.
- Isolated production construction and first-page loading pass after real migration/authentication. The execution desktop still exposes no inspectable Win32 top-level HWND to external UIA; no visual UIA PASS is claimed. R4.1 visual polish remains deferred and R4.3 is NOT STARTED.

## R4.2 Employee and Account Management closeout — 2026-08-26

- R4.2 uses predefined roles and a centralized typed permission catalog. Effective permissions are displayed read-only in the Employee UI; dedicated role/permission administration remains the planned R4.3 checkpoint and is not claimed here.
- No employee hard-delete command is exposed. Existing User IDs and order/receipt/audit references are retained; deactivation is the supported lifecycle operation. Physical authentication hardware is outside this module.
- The real isolated Release process reached `ShellWindowReady`, but this execution desktop exposed no Win32 top-level window handle to external UIA. The closeout therefore uses production-service, persistence, exact-PID and compiled XAML/AutomationId evidence and does not claim visual interaction that was not observed.
- R4.1 visual polish remains deferred by scope. No Store Setup redesign was made during R4.2.

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

### POS-VER-009 — Sandbox DPAPI secure-storage failures were an environmental test-host limitation

- Stable ID: `POS-VER-009`.
- Title: Sandbox DPAPI secure-storage failures were an environmental test-host limitation.
- Classification: Known Operational Limitation.
- Status: Resolved.
- Severity: Medium.
- Affected area: normal-profile versus Codex sandbox execution of existing RememberedLogin and VietQR secure-storage tests.
- Evidence: user-run official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck` under a normal interactive Windows profile on `2026-09-02`, with `1455/1455 PASS`, `0 failed`, `0 skipped`, exit code `0`; prior sandbox runs recorded six DPAPI failures and are retained above as historical evidence.
- Observed or known condition: the six failures occurred only under the sandbox test host; no production secure-storage failure was established.
- Expected condition or intended boundary: secure-storage behavior is evaluated under an interactive Windows profile with the user profile loaded; the sandbox limitation must not be treated as a production defect.
- User/business impact: sandbox-only verification was initially blocked, but the accepted normal-profile Quality Gate is now green.
- Workaround or recovery behavior: run the unchanged tests and Quality Gate under the approved normal interactive Windows profile.
- Related invariant/decision/roadmap checkpoint: `INV-SECURITY-001`, `INV-SECURITY-002`; R5.3–R5.4 closeout.
- Owner checkpoint: R5.3–R5.4 closeout.
- Closure criteria: normal-profile full suite and official Gate pass without changing secure-storage production code, tests or assertions. Met by the user-supplied `1455/1455` result on `2026-09-02`.
- Revalidation trigger: change to secure-storage code/tests, Windows profile/host contract or Quality Gate script.
- Last verified base: R5.3–R5.4 closeout working tree; accepted evidence time `2026-09-02`.
- Notes: resolved environmental test-host limitation; not evidence of a production DPAPI defect.

### POS-VER-010 — Activity Log Bulk audit action label mismatch requires semantic tracing

- Stable ID: `POS-VER-010`.
- Title: Activity Log Bulk audit action label mismatch requires semantic tracing.
- Classification: Verification Gap.
- Status: Open.
- Severity: Medium.
- Affected area: Activity Log audit-row semantic/action presentation for Bulk Product operations.
- Evidence: user physical UI observation on `2026-09-02`: rows with `Nghiệp vụ = Sản phẩm và thao tác hàng loạt` display `Hành động = Cập nhật nhân viên`.
- Observed or known condition: Bulk Product audit rows appear with an action label that may not match the displayed business area.
- Expected condition or intended boundary: persisted event contract, action code and localized/presentation mapping must resolve to the correct semantic label for the operation.
- User/business impact: Activity Log readers may misunderstand what operation occurred; R5.3 audit correctness is not fully accepted until this is traced.
- Trigger or reproduction precondition: inspect affected Activity Log rows after Bulk Product operations; no root cause is concluded by this record.
- Workaround or recovery behavior: none; preserve the evidence and investigate before changing data or labels.
- Related invariant/decision/roadmap checkpoint: audit invariants; R5.3; post-R5 stabilization.
- Owner checkpoint: post-R5 stabilization queue.
- Closure criteria: trace persisted event contract through audit writer, action code and localization/presentation mapping, determine root cause, add targeted regression evidence and apply an authorized correction if required.
- Revalidation trigger: any change to audit writer, audit action enum/constraint, localization mapping, Activity Log query or Bulk audit contract.
- Last verified time: `2026-09-02` user physical observation.
- Notes: do not modify Audit writer, database/schema, Activity Log XAML/ViewModel, localization mapping, Employee Account or Bulk pipeline in this closeout.

### POS-VER-011 — Activity Log table/detail layout is visually dense

- Stable ID: `POS-VER-011`.
- Title: Activity Log table/detail layout is visually dense.
- Classification: Verification Gap.
- Status: Open.
- Severity: Low.
- Affected area: Activity Log table/detail presentation and long target/operation text.
- Evidence: user physical UI observation on `2026-09-02`: the layout is wide and text is spread horizontally; long values such as `Batch ...` and business names crowd columns; `Success` is not sufficiently user-friendly.
- Observed or known condition: long target and operation text lacks compact hierarchy, wrapping/truncation and friendly presentation.
- Expected condition or intended boundary: Activity Log should remain readable at the supported layout while preserving full detail through an appropriate compact presentation.
- User/business impact: operators must scan a dense table and may have difficulty identifying the important event fields.
- Trigger or reproduction precondition: open Activity Log with long Bulk Product targets/operation names.
- Workaround or recovery behavior: none recorded; retain full values until a presentation correction is authorized.
- Related invariant/decision/roadmap checkpoint: R4.4 Activity Log; post-R5 stabilization.
- Owner checkpoint: post-R5 stabilization queue.
- Closure criteria: authorized UX correction demonstrates readable compact layout, friendly labels and preserved full-detail access with targeted automated/manual evidence.
- Revalidation trigger: Activity Log XAML/ViewModel, localization or audit-detail presentation change.
- Last verified time: `2026-09-02` user physical observation.
- Notes: no Activity Log XAML/ViewModel or localization mapping change is included in this closeout.

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
| Total records | 16 |
| Confirmed Defect | 0 |
| Known Operational Limitation | 4 |
| Verification Gap | 8 |
| Deferred Roadmap Capability | 4 |
| Resolved Historical Issue | 0 |
| Open | 4 |
| Monitoring | 3 |
| In Progress | 1 |
| Deferred | 4 |
| Resolved | 2 |
| Not Revalidated | 2 |
| Critical | 0 |
| High | 0 |
| Medium | 7 |
| Low | 1 |
| Informational | 5 |

The counts above were recounted directly from these 16 stable IDs: `POS-OPS-001`, `POS-OPS-002`, `POS-OPS-003`, `POS-VER-001`, `POS-VER-002`, `POS-VER-003`, `POS-VER-004`, `POS-VER-005`, `POS-VER-009`, `POS-VER-010`, `POS-VER-011`, `POS-ROAD-001`, `POS-ROAD-002`, `POS-ROAD-003`, `POS-ROAD-004`, `POS-ROAD-005`. There are no confirmed open runtime defects in this register.
- R5.2 Export — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Export reports and the blank import template are implemented and covered by typed writer/service tests. Physical save-dialog, Excel-open and DPI visual checks remain manual; no claim of manual acceptance is made here.
- CSV cannot carry cell types and spreadsheet applications may reinterpret long numeric-looking codes when opened. XLSX keeps ProductCode and Barcode as text cells; the UI/documentation should continue to recommend XLSX when preserving identifiers matters.
- R5.3 label printing remains a separate dependency: the current repository has receipt printing but no production label renderer/printer pipeline.
- R5.3 Bulk Operations — PARTIAL / AUTOMATED VERIFIED — 2026-08-30

- Bulk price/category/status/minimum-stock changes are available only for the explicitly selected rows on the current page and require preview/confirmation. “Select all filtered results” is intentionally deferred until a bounded immutable selection snapshot exists.
- Bulk label printing is blocked by the absence of a production label renderer/printer pipeline. Existing receipt printing is not silently reused; R5.3 is not closed while printing remains unresolved.
