# CURRENT STATE — POS ENTERPRISE

## R5.2 — Export — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Added permission-gated CSV/XLSX export for product catalog, current stock, low-stock products, archived products, Inventory History and the exact 11-field Product import template. Product and History exports use the existing database-side filters and bounded projections; export is read-only and does not inherit a selected row as an implicit scope.
- CSV is UTF-8 BOM with quoting, newline/Unicode handling and formula-injection protection for untrusted text. XLSX is a real typed package with text code/barcode cells, numeric money/quantity cells, frozen header, filter and no formula/macro/external-link content. Output is written to an exact sibling temporary path and replaces the destination only after success.
- Cost price is included only under the existing `ManageProducts` capability because the production permission catalog has no separate cost-view/export capability; otherwise the column is omitted. The import template is available from the Import Wizard and reuses `ProductImportSchemaCatalog` directly.
- Focused Export coverage is `5/5 PASS` Debug and Release; normal-host full Release and Quality Gate after the complete in-progress diff are `1424/1424 PASS`, failed/skipped `0/0`, build `0/0`, vulnerability PASS and EF pending-model PASS. Physical WPF save-dialog and visual acceptance remain manual pending. R5.2 is implementation-complete/automated-verified but not manually closed; R5.3 remains in progress.

## Inventory History type/date correction — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Inventory History now exposes `CustomerReturn` separately as “Khách trả hàng”, matching the existing customer-return stock movement written only when `RestockQuantity > 0`; legacy `Refund` remains distinct.
- The default 30-day calendar range is now `Today.AddDays(-29)` through today, with the existing local-day-to-UTC query conversion preserved.
- Focused History/Product/return coverage is `116/116 PASS` in Debug and Release. No enum, stock-write, database schema, or manual database was changed. Physical WPF acceptance remains pending; R5.1 is still in manual acceptance and R5.2 is not started.

## Selection and global Inventory History hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The Product & Inventory selected-product rail now has a visible `× Bỏ chọn` command bound to the real `SelectedProduct`/DataGrid selection. It clears only selection, refreshes command guards and presentation, does not reload or mutate product/stock/archive data, and does not reselect the first row.
- The existing `Kho & lưu trữ → Lịch sử kho` menu command now always opens the general history route with an empty product criterion. It no longer inherits the selected product's code/name; the history service and its supported product-search behavior remain unchanged, and the selection on the Shell remains intact while the history window is opened.
- Focused selection/navigation coverage is `28/28 PASS` in Debug and `28/28 PASS` in Release; relevant Product/Inventory/Category/Import/RBAC/Shell Release coverage is `286/286 PASS`. Release solution build is `0` warnings and `0` errors. Normal-host full Release is `1413/1413 PASS`, failed/skipped `0/0`; Official Quality Gate passed with vulnerability, EF pending-model and Git checks PASS.
- The Codex sandbox full Release was `1407 PASS / 6 FAIL / 0 SKIP` only for the known CurrentUser DPAPI secure-storage/VietQR tests; the normal-host rerun passed `12/12`, with no security change. Physical WPF click/UIA/DPI acceptance remains manual pending. The persistent manual database and launcher were not touched; R5.1 remains in manual acceptance and R5.2 is NOT STARTED.

## Persistent manual database launcher — VERIFIED — 2026-08-30

- The source-tree canonical development database `data/pos-enterprise.db` was not present at preflight, so no existing canonical data needed migration or preservation. A user-authorized persistent profile was initialized at `C:\Users\pc\AppData\Local\POS Enterprise\ManualAcceptance\pos-enterprise-manual.db`; the application created it and applied the current 18 migrations without copying a source database.
- `scripts/Start-POS-PersistentManual.ps1` launches the existing Release executable with the existing `POS_RUNTIME_MODE=IsolatedTest` external-path boundary and the same fixed LocalApplicationData path on every run. It validates regular-file/reparse-point safety, does not create timestamp/GUID directories, does not delete or overwrite the database, and leaves single-instance/migration/backup/ACL behavior in the application.
- First startup reached `HostStarted`, `DatabaseInitialized`, `SessionLoopEntered` and `InitialSetupWindowReady`. No account, password, product or business data was created by the agent; the setup window remains open for the user. Automated launcher coverage is `1/1 PASS` in Debug and Release; full normal-host Release is `1411/1411 PASS`, failed/skipped `0/0`, and the official Quality Gate is PASS.
- The persistent database and its live SQLite sidecars are outside the repository and ignored; automated tests continue to use isolated TEMP databases. R5.1 remains in manual acceptance, R5.2 is NOT STARTED, and Development Freeze is ACTIVE outside authorized work.

## Inventory History navigation/filter-card hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The duplicate Shell sidebar entry and its dedicated command were removed. The single primary route remains `Sản phẩm & tồn kho → Kho & lưu trữ → Lịch sử kho`; the existing context-menu command still opens general history without inheriting a selected row, while product-context opening seeds the same visible search criterion.
- Inventory History now has one light local filter card with `Tìm`, Enter/debounce, an in-field `×`, `Xóa bộ lọc` and `Làm mới` together. The search button uses its own bounded style and Grid column; the card no longer uses the dark sidebar surface, and no shared theme was changed. Database-side search, latest-request-wins, reset/refresh, loading/error/empty/detail, paging, KPI and permission semantics are preserved.
- Focused navigation/history coverage is `22/22 PASS` in Debug and `22/22 PASS` in Release; relevant Product/Inventory/Category/Import/RBAC/Shell Release coverage is `284/284 PASS`; normal-host full Release is `1410/1410 PASS`, failed/skipped `0/0`; Release build is `0` warnings and `0` errors. Official Quality Gate passed, including vulnerability, EF pending-model and Git checks.
- Physical WPF screenshot/click/UIA and 100%/125%/150% DPI acceptance remain manual pending because this execution desktop exposes no inspectable top-level HWND and no existing render harness was available. No canonical database, runtime data, stock write, archive behavior, Import Wizard or unrelated module was changed. R5.1 remains manual acceptance, R5.2 is NOT STARTED and Development Freeze is ACTIVE outside this hotfix.

## Inventory History redesign — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Inventory History now has one explicit product criterion: the general Shell entry opens all permitted products, while a product-context entry seeds the same visible search box with that product code/name. Editing or clearing the search replaces/removes the criterion; no hidden `ProductId`, red scope banner or separate “bỏ giới hạn” flow remains.
- The screen keeps database-side name/code/barcode search, 300 ms debounce, Enter/small `Tìm`, latest-request-wins cancellation, valid-date checks, consistent reset/refresh/paging/detail states and permission/store boundaries. The compact dark sidebar and three summary cards use local styles; summary counts come from the same filtered query before paging and never add quantities across units.
- Focused Inventory History/Product navigation coverage is `21/21 PASS` in Debug and `21/21 PASS` in Release; relevant Inventory/Product/RBAC/Shell/History Release coverage is `90/90 PASS`. Normal-host full Release is `1409/1409 PASS`, failed/skipped `0/0`; Release build is `0/0` warnings/errors. The sandbox run was `1403 PASS / 6 FAIL / 0 SKIP` only for the known CurrentUser DPAPI secure-storage tests; the normal-host rerun passed all 12 tests in those classes.
- Physical WPF click/UIA and 100%/125%/150% DPI acceptance remain manual pending; automated WPF construction/measure/arrange covers `1920×1080`, `1366×768`, `1280×720`, `1180×720`, `1000×620`. Inventory write, ledger, audit, Employee, Import Wizard, Category and archive behavior were not changed. R5.1 remains manual acceptance, R5.2 is NOT STARTED and Development Freeze is ACTIVE outside this hotfix.

## Inventory History / Adjustment / Category UX hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- Inventory History giữ pipeline tìm kiếm database-side của `dc27148` nhưng trở lại bố cục cũ: sidebar bộ lọc gọn, KPI card, bảng, chi tiết và phân trang. `Tìm`, Enter và debounce dùng cùng query; `Xóa bộ lọc`/`Làm mới`, loading, empty, error, scope navigation và paging vẫn giữ semantics mới.
- Điều chỉnh tồn kho không còn tự điền `1`; ô số lượng rỗng, placeholder theo nghiệp vụ, preview không giả định dữ liệu và nút lưu chỉ bật khi request hợp lệ. Danh mục không còn hiển thị `DisplayOrder`; dữ liệu vẫn giữ trường tương thích, còn danh sách được sắp tên tiếng Việt ổn định trước phân trang.
- Focused Debug/Release: `44/44 PASS`; relevant Release: `284/284 PASS`; normal-host full Release: `1407/1407 PASS`, failed/skipped `0/0`; build sau cùng ghi nhận `0/0` warnings/errors. Không thay đổi lưu trữ sản phẩm, Import Wizard, R5.2 hoặc nghiệp vụ ghi tồn.
- Physical WPF click/UIA/DPI acceptance vẫn manual pending; automated measure/arrange và contract tests không được coi là kiểm thử vật lý. R5.1 vẫn manual acceptance, R5.2 NOT STARTED và Development Freeze ACTIVE ngoài phạm vi hotfix.

## Inventory History UX/query hotfix — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The Inventory History screen now uses one direct product search for name, code or barcode. Search is debounced, Enter applies immediately, filters update the same database-side history query, and the old product lookup plus second `Lọc lịch sử` action are removed from the user flow.
- Reset is now `Xóa bộ lọc`: it clears text, navigation product scope, extra filters, dates and selection, then performs exactly one default query. `Làm mới` preserves the current conditions and valid page. Navigation scope is visible and removable; no hidden ProductId survives reset.
- Loading, empty, failure and selected-detail states are separate; reference sources use friendly labels while unknown legacy values remain visible. Product, stock, movement and audit data are read-only in this hotfix; authorization and transaction/write workflows are unchanged.
- Focused Debug/Release history coverage is `16/16 PASS`; relevant Inventory/Product/RBAC/Audit Release coverage is `260/260 PASS`; normal-host full Release is `1399/1399 PASS`, failed/skipped `0/0`, Release build `0/0` warnings/errors. Sandbox full Release was `1393 PASS / 6 FAIL / 0 SKIP` only in the known CurrentUser DPAPI/local-secure-storage tests; normal-host rerun passed all six. Official Quality Gate is recorded after this Memory update.
- Physical WPF click/UIA/DPI acceptance remains manual because no inspectable top-level HWND is exposed. R5.1 remains IN PROGRESS; R5.1A–R5.1D and R4.2–R4.4 are preserved, R5.2 is NOT STARTED and Development Freeze remains ACTIVE outside authorized hotfix scope.

## R5.1C/D — Store-user UX remediation — AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The import wizard now presents three user-facing stages: `Chọn file`, `Kiểm tra và nhập` and `Kết quả`. Technical mapping details, full 11-field preview and row diagnostics are progressive disclosures; the primary action is contextual (`Chọn file Excel/CSV`, `Kiểm tra lại`, `Nhập sản phẩm`, or the result action). The old secure-preview/service/schema wording and competing confirmation buttons are no longer in the main surface.
- The proven mapping defect was incomplete alias coverage for compact canonical headers such as `UnitName`, `SalePrice`, `CostPrice`, `InitialStock`, `MinimumStock` and `IsActive`. The single catalog now recognizes these deterministically without fuzzy matching. Missing `Drinks`/`Food` categories remain a separate active-reference issue and are explained by name; categories and units are never created by preview.
- The wizard auto-reads a selected file, requires an explicit choice for multiple Excel sheets, hides the mapping table when all columns are recognized, prioritizes common product columns, keeps a full-information view available, groups issues while distinguishing error rows from issue count, and gives separate empty/header-only, blocking, success and rollback presentation. The R5.1A/B parser, permissions, snapshot, transaction and stock semantics are unchanged.
- Remediation coverage is `27/27 PASS` in Debug and Release, relevant Product/Inventory/RBAC/Audit Release is `250/250 PASS`, normal-host full Release is `1389/1389 PASS`, failed/skipped `0/0`, and Release build is `0/0` warnings/errors. Official Quality Gate without `-SkipEfCheck` is PASS, including vulnerability, EF pending-model and Git checks. Physical click/UIA/DPI and end-user fixture click-through remain manual because no inspectable top-level HWND is exposed; R5.1 remains IN PROGRESS and R5.1C/D are AUTOMATED VERIFIED / MANUAL PENDING.

## R5.1C/D — Product CSV/Excel Import Wizard — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING — 2026-08-30

- The WPF Product & Inventory surface now exposes `Nhập CSV/Excel` through the existing `ManageProducts` authorization boundary. The wizard uses the R5.1A preview/parser and R5.1B transactional service; it provides file selection, explicit multi-worksheet selection, visible/editable 11-field mapping, full-file validation with bounded preview, row issues, duplicate policy, explicit confirmation, cancellation/busy protection and typed committed/rollback results. No second parser, Product model, database mutation path, package or migration was introduced.
- Mapping, worksheet, policy and file changes invalidate the previous preview. Preview remains read-only; only explicit confirmation calls `IProductImportService`, and successful commit refreshes the Product list. The wizard preserves A/B security limits and semantics, including leading-zero text fields, full-batch validation beyond 100 preview rows, safe errors and no automatic retry/re-import.
- R5.1 import-focused WPF/application coverage is `25/25 PASS` in Debug and Release. Relevant Product/Inventory/RBAC/Audit regressions passed `248/248` in Release; the normal-host full Release suite passed `1387/1387`, failed/skipped `0/0`. Release build is `0/0` warnings/errors and the official Quality Gate without `-SkipEfCheck` passed with vulnerability, EF pending-model and Git checks PASS. Isolated startup reached the safe `InitialSetupWindowReady` milestone using an owned TEMP source database; no canonical data was used.
- Automated construction/measure/arrange and integration coverage is complete. Physical click/UIA/DPI acceptance and an end-user import against the prepared synthetic fixtures remain manual because this environment exposes no inspectable top-level HWND. Therefore R5.1C and R5.1D are IMPLEMENTED / AUTOMATED VERIFIED / MANUAL ACCEPTANCE PENDING; R5.1 remains IN PROGRESS, R5.2 is NOT STARTED, R5.1A/B and R4.2–R4.4 remain CLOSED/PRESERVED, and Development Freeze is ACTIVE outside R5.1.

## R5.1B — Transactional Product Import — CLOSED / VERIFIED — 2026-08-29

- R5.1B adds an Application transactional import use case over the single R5.1A 11-field Product schema. It re-parses and fingerprints the preview file before confirmation, resolves only existing active Categories, keeps UnitName as Product text, and has no WPF Import Wizard or database schema change.
- `Skip` preserves duplicates, `Update` changes the permitted Product fields while preserving Product ID/history and rejects non-zero opening stock on updates, and `Error` rejects duplicates. ProductCode/Barcode conflicts, same-file duplicates, stale reference snapshots and invalid categories fail safely before mutation.
- Creation applies positive `Tồn đầu` through the existing Product domain reconciliation plus append-only `OpeningBalance` InventoryMovement inside the same EF database transaction. The committed batch writes a sanitized audit summary using the existing valid audit action/check constraint; rollback/cancellation/constraint failures do not write success audit.
- Focused R5.1B integration tests pass `9/9` in Debug and `9/9` in Release; relevant Product/Inventory/RBAC regressions pass `32/32`. Release build is `0/0` warnings/errors. Normal-host full Release is `1383/1383 PASS`, failed/skipped `0/0`; official Quality Gate without `-SkipEfCheck` passes `1383/1383`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- R5.1A remains CLOSED/PRESERVED; R5.1 is IN PROGRESS and R5.1C (WPF Import Wizard) is NEXT. R4.2/R4.3/R4.4 remain CLOSED/PRESERVED. Development Freeze is ACTIVE outside this R5.1B exception. No canonical database or production data was used.

## R5.1A — Product CSV/Excel secure preview foundation — CLOSED / VERIFIED — 2026-08-29

- The single Application schema catalog covers exactly these 11 production-backed fields, in order: `ProductCode`, `Barcode`, `Tên`, `Danh mục`, `Đơn vị tính`, `Giá bán`, `Giá vốn`, `Tồn đầu`, `Tồn tối thiểu`, `Trạng thái` and `Ghi chú`. They map to `Product.Code`, `Barcode`, `Name`, `Category`/`CategoryId`, `UnitName`, `SalePrice`, `CostPrice`, opening `StockQuantity`, `MinimumStock`, `IsActive` and `Description`. Product stores unit as text; no parallel Unit model was introduced.
- The read-only Application contract and Infrastructure parser accept only regular `.csv`/`.xlsx` files, validate signatures and bounded CSV/XLSX content, normalize typed preview rows, preserve barcode leading zeroes, reject formulas/external links/macros, report header/row issues and keep preview rows bounded while retaining full summary counts. No Product, Category, Inventory or Audit database write, migration, WPF wizard or authorization change was made.
- Focused import tests pass `12/12` in Debug and `12/12` in Release. Normal-host full Release is `1374/1374 PASS`, failed/skipped `0/0`; Release build is `0/0` warnings/errors. Official Quality Gate without `-SkipEfCheck` passed with `1374/1374`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- Six known secure local-storage/VietQR failures reproduce only in the Codex sandbox; the normal Windows host passed the secure-store classes and the full suite. No security weakening or test filtering was used. R5.1A is CLOSED, R5.1 is IN PROGRESS, R5.1B is NEXT; R4.2/R4.3/R4.4 remain CLOSED/PRESERVED and Development Freeze is ACTIVE outside this scoped checkpoint.

## Test portability and new-PC handover — ACTIVE FREEZE — 2026-08-29

- The portability checkpoint is committed and pushed as `34d886681e494e30c5dd6b704b9afad8a5958eb1` (`test(portability): remove canonical database dependency`). Audit runtime, Employee UI and Shell/sidebar inventory tests now create unique migrated SQLite databases under owned `%TEMP%` children; they do not copy or modify the canonical database.
- The shared test helper uses the current EF migration path, deterministic test-only data where required, exact TEMP ownership/reparse guards and bounded cleanup after provider disposal. The canonical database remained unchanged at `937984` bytes, timestamp `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with WAL/SHM/journal absent.
- The post-portability official Quality Gate passed without `-SkipEfCheck`: `1360/1360 PASS`, failed/skipped `0/0`, build warnings/errors `0/0`, vulnerability PASS, EF pending-model PASS and Git checks PASS. New-PC instructions are tracked at `docs/project/NEW-PC-HANDOVER.md`.
- Development Freeze is ACTIVE. R4.2, R4.3 and R4.4 remain CLOSED. R5.1 Product CSV/Excel Import remains NOT STARTED.

## Final Employee visual correction — CLOSED / VERIFIED — 2026-08-29

- Employee DataGrid now has local presentation styles with consistent header/cell padding and vertical centering, aligned left/center contracts, light separators, a wrapping `Đăng nhập sai` header with tooltip, safe trailing inset, and automatic horizontal scrolling. Long employee codes and names retain ellipsis/tooltips; the identity avatar/text/status groups share a top-aligned responsive layout and the footer uses separate pagination columns.
- Focused Employee WPF coverage is `6/6 PASS` in Debug and Release, including real WPF measure/arrange at `1920×1080`, `1366×768`, `1280×720`, `1180×720` and `1000×620`. Audit Log production files and behavior were not changed.
- Sandbox-only failures in six DPAPI secure-storage tests were reproduced deterministically under `codexsandboxoffline`; the same six tests passed under the normal Windows user with `DataProtectionScope.CurrentUser`. Normal-host full Release and official Quality Gate both passed `1362/1362`, failed/skipped `0/0`, build `0/0`, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- Development Freeze is ACTIVE; R4.2, R4.3 and R4.4 remain CLOSED/PRESERVED and R5.1 remains NOT STARTED.

## Employee + Audit UI/runtime follow-up — CLOSED / VERIFIED — 2026-08-29

- The scoped follow-up removes the redundant Employee list-header range text; row count, page size and navigation remain in the footer. The detail identity header uses explicit avatar/identity/status columns, a zero-minimum identity column, wrapped long employee IDs and full-value tooltips, with real WPF bounds coverage at `1920×1080`, `1366×768`, `1280×720` and the supported minimum window size.
- Audit now has a primary `Tìm kiếm` command, default-button Enter behavior, disabled/loading command state, explicit `Làm mới`, filter reset/reload, safe retry, local date-range validation and distinct loading/database-empty/filtered-empty/failure/no-selection presentation. Search remains application-authorized and database-side; paging/order, cancellation, append-only audit and redaction boundaries are unchanged.
- Focused Debug and Release coverage is `10/10 PASS`; relevant Employee/Audit/RBAC/Shell/Store Setup/Sales Release regressions are `182/182 PASS`; full Release is `1362/1362 PASS` with failed/skipped `0/0`. Release rebuild and official Quality Gate both report `0/0` warnings/errors, vulnerability PASS, EF pending-model PASS and Git checks PASS.
- No physical click/UIA or DPI `100%/125%/150%` observation is claimed because this desktop exposes no inspectable top-level HWND; those visual checks remain manual. Development Freeze is ACTIVE after this exception; R4.2, R4.3 and R4.4 remain CLOSED/PRESERVED and R5.1 remains NOT STARTED.

## Post-R4.4 handover-freeze hotfix — CLOSED / COMMITTED / PUSHED — 2026-08-29

- The Audit runtime failure was reproduced in the real Release composition: `AuditLogWindow` loaded, then `AuditLogViewModel` failed while resolving `IAuditLogService` because the WPF composition root had omitted that registration. The async module load consequently escaped to the global startup-safety message. No query, migration, mapping or schema defect was involved.
- The production fix registers the existing `IAuditLogService`/`AuditLogService` boundary and gives list/detail loading a module-specific sanitized error state with retry and cancellation-aware single-flight loading. Application authorization, database-side filtering/paging, append-only audit behavior and secret exclusions are unchanged.
- Employee master/detail clipping is corrected with bounded summary presentation and explicit avatar/identity/status Grid columns; identity metadata cannot overlap the status card, and long values retain ellipsis/tooltips. Existing Employee commands and behavior are unchanged.
- Final focused coverage is `10/10 PASS`; Release build is `0` warnings / `0` errors. The official Quality Gate passed with `1360/1360 PASS`, failed/skipped `0`, vulnerability PASS, EF pending-model PASS and Git checks PASS. R4.3 and R4.4 remain closed; R5.1 remains not started and development freeze is reactivated.

## R4.4 Secure Audit Log UI — CLOSED / COMMITTED / PUSHED — 2026-08-29

- R4.4 adds a permission-gated, append-only audit viewer under the existing dark grouped Shell. It reuses the current `SecurityAuditEvent` writer/repository boundary, adds safe actor/target snapshots, typed business metadata, privacy-safe terminal identity and allowlisted before/after changes; credentials, hashes, tokens and raw exceptions are excluded.
- Audit queries are asynchronous, cancellation-aware, database-filtered and stably paged newest-first. The WPF screen provides initialized date/actor/business/action filters, friendly Vietnamese action text, local-time display, detail and safe before/after surfaces, with real WPF layout/virtualization coverage.
- The additive migration `20260828173138_AddSecureAuditLogMetadata` preserves existing audit rows with honest empty metadata defaults and adds supporting indexes. `ViewAuditLog` is authoritative at the Application boundary; Administrator is granted, other built-in roles remain denied by default.
- R4.4 isolated startup used the current Release executable and an owned temporary database; migration and host startup completed without startup/XAML/resource errors. External UIA exposes no top-level HWND on this desktop, so physical click/DPI claims remain manual limitations. Evidence is outside the repository.
- R4.4 Quality Gate is `1359/1359` PASS with 0 failed/skipped, 0 build warnings/errors, vulnerability PASS, EF pending-model PASS and Git checks PASS. Development is frozen for machine handover. Exact next roadmap checkpoint is R5.1 Product CSV/Excel Import — recorded, not started.

## R4.3 Role and Permission Management — CLOSED / COMMITTED / PUSHED — 2026-08-29

## R4.3 Role and Permission Management — CLOSED / COMMITTED / PUSHED — 2026-08-29

- R4.3 adds a permission-gated WPF Role and Permission screen under the existing dark grouped Shell. It presents the four stable enum-backed built-in roles, Vietnamese labels, database-side account usage counts and the centralized effective-permission matrix.
- Custom roles are deferred: the live architecture has no persisted Role aggregate or permission-assignment store; introducing one would be a new authorization model requiring additive lifecycle, assignment, concurrency, final-Administrator and audit contracts. Built-in role assignments remain through the existing authorized EmployeeAccountService.
- `ViewAuditLog` is a new typed capability reserved for R4.4. No WPF visibility is treated as an authorization boundary; Application permission evaluation remains authoritative and unknown values deny by default.
- R4.3 isolated acceptance and final gate evidence are recorded outside the repository: focused `3/3` and official gate `1355/1355` PASS. R4.4 is now closed; the next roadmap checkpoint is R5.1 Product CSV/Excel Import and remains not started.

## R4.2 final Employee detail visual polish — CLOSED / COMMITTED / PUSHED — 2026-08-28

- This tightly scoped presentation checkpoint preserves the completed Employee master-detail behavior, filters, commands, persistence, authorization, security safeguards, dark grouped sidebar, Store Setup UX and Inventory navigation hotfix.
- The selected-employee detail now has one identity header containing avatar, real name/code/username and an intentional contextual status area (`Nhân viên` and `Tài khoản`); the Hồ sơ tab uses a grouped information card with friendly optional-value fallbacks, safe long-code trimming/tooltips and a connected action strip.
- Real WPF construction/layout coverage passes at normal and supported reduced sizes; profile/action surfaces remain reachable without overlap. No Employee business behavior or shared global theme was changed.
- R4.3 and R4.4 remain NOT STARTED. Physical DPI visual smoke remains a manual check on this execution desktop because external UIA exposes no top-level HWND.

## R4.2 final manual UI correction — CLOSED / COMMITTED / PUSHED — 2026-08-28

- The live entry baseline is `93da2045ced774e8bd02e34777bfc145aac682db`, descendant of `cea5c4230487d95c379c9e18d3a7437e91dc7133`, with `main` aligned `0/0` to `origin/main`, clean/staged `0`, SDK `10.0.302`, and exact canonical database identity.
- The final correction keeps the modern Employee master-detail screen, dark grouped Shell/sidebar, Store Setup UX and Inventory hotfix. It makes true-empty and filtered-empty states mutually exclusive using global and filtered counts, gives each visible action one authoritative command, fixes the real WPF Role selected-content binding, and makes create mode profile-first with explicit post-save account continuation.
- Release reproduction reached `ShellWindowReady` from the current executable after `DatabaseInitialized`; the old generic readiness string is absent from current Sales production routes. External UIA does not expose a top-level HWND in this desktop, so no unobserved visual clicks or DPI/manual screenshot claims are made.
- Final official Quality Gate is `1352/1352` PASS with `0` failed/skipped, `0/0` build warnings/errors, vulnerability PASS, EF pending-model PASS and Git checks PASS. Real WPF construction/layout tests and focused Release coverage are `8/8` PASS.
- R4.3 and R4.4 remain NOT STARTED. Store Readiness is in scope only for route verification in this correction; Store Setup UX remains preserved.

## Post-modernization manual UX hotfix — CLOSED / COMMITTED / PUSHED — 2026-08-28

- Sales first-click readiness was repaired without weakening authorization: Sales now reloads the authoritative external Store Setup snapshot before evaluation, and missing/unavailable backup storage is a warning rather than a sales blocker. Core store/database validity remains blocking. A typed owner-correct “Cửa hàng chưa sẵn sàng” dialog lists friendly blockers and can open the existing authorized Store Setup dialog.
- The exact IsolatedTest finding is intentional fresh-scenario behavior: the launcher copies only the database; Store Setup is external `store-settings.json` under each scenario-owned temporary root. Separate isolated invocations do not share prior temporary settings, so production settings/secrets are not copied into evidence or scenarios.
- Employee filters now bind a stable typed “Tất cả vai trò” sentinel immediately, notify filtered-state properties when filters change, distinguish true empty database from filtered/search no-result, and expose a separate filtered-empty Add command that enters the existing create mode. The empty card is content-safe at reduced height, the clear-filter glyph uses a vector Path, and long employee codes use bounded text with full-code tooltips.
- New manual UX hotfix coverage is `8/8` PASS in Debug after real WPF dialog/window construction. Existing modernization behavior is preserved; no schema, migration, Shell/sidebar, Store Setup redesign, authentication, RBAC or security policy change was introduced.
- Exact isolated launcher evidence used a fresh Release copy with `POS_RUNTIME_MODE=IsolatedTest` and scenario-owned paths. The execution desktop exposed no inspectable HWND/UIA and the bounded launcher did not reach `ShellWindowReady`; no unsupported visual click or DPI-scaling claim is made. Manual visual/interaction checks remain required.
- Store Readiness is now treated as blocking only for core operational errors; optional backup/tax issues are warnings. R4.3 remains NOT STARTED.

## R4.2 Employee and Account Management UI modernization — CLOSED / COMMITTED / PUSHED — 2026-08-28

- The approved dark grouped production Shell sidebar, grouped navigation, Inventory route hotfix, Store Setup UX and existing routes remain unchanged. Only the Employee and Account content surface was modernized with native WPF XAML, shared POS resources, existing MVVM/service boundaries and stable AutomationIds; no schema or migration was added.
- The Employee window now uses a centered, resize-safe master-detail layout with a responsive approximately 63/37 list/detail split, header actions, three aggregate summary cards backed by database-side count queries, wrapped search/filter toolbar, friendly Vietnamese status mappings, virtualized DataGrid presentation and controlled horizontal scrolling.
- Selection is deterministic: the first visible employee is selected after a successful load, selection is preserved by ID where possible, stale detail is cleared on filtering, and a genuine no-selection state contains no editor, default role, password or stale data. Detail is read-only first and organized into Hồ sơ, Tài khoản & bảo mật and Vai trò & quyền hạn tabs; editing, account creation and sensitive actions remain explicit and authorized.
- Existing authorization, password hashing, lockout, force-password-change, final-Administrator protection, optimistic concurrency, audit behavior, historical retention and owner-correct dirty-state guards remain application-owned. Summary attention is defined from existing typed account states and failed-login state without loading the full employee table.
- New Employee UI contract/behavior coverage is `6/6` PASS in Release, including real WPF window/resource construction, initial selection, no-selection/no-result behavior, friendly typed security presentation and existing isolated production-service wiring. Release build is `0` warnings/`0` errors. Official Quality Gate without `-SkipEfCheck` is PASS with `1350/1350`, `0` failed, `0` skipped, vulnerability scan PASS, EF pending-model PASS and Git checks PASS.
- Deterministic visual evidence is limited honestly: the approved reference was inspected; production XAML/resources and real ViewModel behavior were constructed, while this execution desktop exposed no inspectable top-level HWND/UIA element and the bounded exact isolated launcher run did not reach `ShellWindowReady` before its 90-second observation window. No unsupported visual click, scaling or pixel-perfect claim is made; manual visual smoke remains required. Temporary isolated roots were validated and removed, and canonical data remained protected.
- Store Readiness “Cấu hình cửa hàng chưa sẵn sàng.” remains a separate manual observation and is not claimed fixed. R4.3 remains NOT STARTED.

## Shell sidebar and inventory navigation hotfix — CLOSED / COMMITTED / PUSHED — 2026-08-27

- The grouped sidebar introduced at `0088b6f1a9185c125756bea352c2353108107573` rendered the Products child as permanently selected but attached no command, while Shell had no authoritative route state. Category therefore opened correctly, but returning to `Sản phẩm & tồn kho` executed nothing and could not update selected/expanded state.
- Shell now uses one `ShellRoute` state for Overview, Products and Categories. The Products command reactivates the existing inventory surface, expands Hàng hóa and does not recreate the view or duplicate the initial query; repeated Category ↔ Products transitions are covered behaviorally.
- The sidebar is `276` logical pixels at normal desktop widths and `76` in compact mode below `1180`, with full-label tooltips, one icon/label/chevron group row, balanced child rows, a fixed status footer and session-only one-group expansion. Final labels use `Sản phẩm & tồn kho`, `Danh mục sản phẩm`, `Đơn hàng`, `Nhân viên & tài khoản`, `Dữ liệu & hỗ trợ` and `Màn hình VietQR`.
- Shell minimum width is `1024`; compact mode leaves at least `948` logical pixels before workspace margins. Inventory summary cards switch from four to two columns, and the product DataGrid keeps its established star rhythm with minimum column widths plus an automatic horizontal scrollbar instead of silent right-edge clipping.
- New hotfix coverage is `4/4` PASS; focused Product/Category/Shell/Store Setup/Employee/Orders coverage is `219/219`, broader navigation/auth/Sales/VietQR/backup/restore/isolated coverage is `532/532`, and the final suite is `1348/1348` PASS. Release build and official Quality Gate without `-SkipEfCheck` passed with zero warnings/errors, vulnerability scan PASS and EF pending-model PASS.
- The Release IsolatedTest launcher migrated only a fresh `%TEMP%` copy, reached `ShellWindowReady` and completed the production product query. External UIA again exposed no top-level HWND, so final click/spacing/scaling observation remains manual. Store Setup UX is preserved, R4.2 remains CLOSED and R4.3 remains NOT STARTED.

## Store Setup UX polish and Shell navigation UX — CLOSED / COMMITTED / PUSHED — 2026-08-27

- Deferred R4.1 visual polish was completed without changing persisted schema: Store Setup is now organized as Thông tin cửa hàng, Hóa đơn và in ấn, Thiết bị bán hàng, with administration-only paths in a collapsed advanced section.
- Currency and region are presented as Việt Nam đồng (VND) and Việt Nam (UTC+7) automatic settings; invalid legacy time-zone values fall back safely. Raw enum, Windows ID, scanner mode, cash-drawer mode, VietQR fields and booleans are not exposed in the normal UI. VietQR remains owned by its dedicated module and existing data is preserved; cash-drawer UI is deferred while compatibility remains.
- Printer discovery now reports busy/result/missing-selection states, printer testing remains through the production abstraction, and In thử uses the real receipt service. Scanner testing uses the same bounded normalization boundary as Sales, with success/timeout/cancel behavior and no product persistence.
- Shell navigation now uses one-level task groups with permission-filtered children, stable AutomationIds and preserved Employee, Sales, Orders, VietQR, backup/restore and storage destinations. Dead Customer navigation is not presented.
- New focused UX coverage is `8/8` PASS; related focused regressions are `384/384` PASS; final full suite is `1344/1344` PASS with `0` failed and `0` skipped. Release build and official Quality Gate without `-SkipEfCheck` passed with zero warnings/errors, vulnerability scan PASS and EF pending-model PASS.
- Isolated Release launcher reached `ShellWindowReady` on a fresh copied database and created scenario-owned startup artifacts only. UIA exposed no top-level HWND in this execution desktop, so visual interaction is not claimed; real WPF Store Setup construction/resource loading and production initialization behavior are covered deterministically. R4.3 remains NOT STARTED.

## R4.2 Employee and Account UI hotfix — CLOSED / COMMITTED / PUSHED — 2026-08-27

- The automated R4.2 closeout at `bb93417034ea049aa9ab5e3ce8d4f2ca6fcd7536` was followed by a real manual failure when opening `Nhan vien va tai khoan`; startup, Administrator authentication and Shell opening still succeeded.
- The recovered production exception was `System.Windows.Markup.XamlParseException` from `EmployeeManagementWindow.InitializeComponent`, with inner `System.Exception: Cannot find resource named 'AuthLabelStyle'. Resource names are case sensitive.` The missing shared resource was referenced by the Employee window XAML but existed only in a different window's local resources.
- The fix adds the three shared authentication styles (`AuthLabelStyle`, `AuthPasswordBoxStyle`, `PasswordRequirementStyle`) to the merged Typography theme, and adds a logged, sanitized module-loading boundary with a module-specific Vietnamese message. Authorization and the existing scoped initialization/query path are unchanged.
- New behavioral coverage constructs the real production Employee window on an isolated migrated database, validates defaults/roles/permissions, loads populated and empty pages, checks unauthorized rejection, and verifies Store Setup/Sales composition. The navigation-boundary regression verifies exception-chain logging and no startup-error classification.
- Release isolated proof reached `ShellWindowReady`; production authentication restored the existing Administrator, the real employee query returned data, the real window was constructed and initialized with `count=1`, `total=1`. External UIA still exposed no top-level HWND, so visual click confirmation remains a manual limitation.
- Official Quality Gate without `-SkipEfCheck` passed: full suite `1338/1338`, failed `0`, skipped `0`, build `0/0`, vulnerability scan PASS and EF pending-model PASS. R4.1 visual polish remains deferred; R4.3 remains NOT STARTED.

## R4.2 Employee and Account Management — CLOSED / COMMITTED / PUSHED — 2026-08-26

- R4.2 adds a separate `Employee` identity record with an optional linked `User` login account, typed lifecycle/account status, centralized role capabilities, password policy, failed-login/lock state, force-password-change flow, last-login reporting, optimistic concurrency and sanitized security audit events.
- Existing User IDs and business history remain intact. The additive EF migration backfills existing Users into deterministic Employee records, uses restrict/no-cascade employee-account behavior, and was verified by an isolated upgrade test.
- Application services enforce View/Manage Employees, Manage Accounts, Reset Passwords, Lock/Unlock and Assign Roles capabilities before mutation. The final usable Administrator is protected transactionally; deactivation disables the linked account and reactivation does not silently unlock it.
- WPF adds one Administrator/permission-controlled Employee and Account window with search/filter/paging, create/edit/link-account, reset/lock/deactivate/reactivate/role actions, Vietnamese status text, stable AutomationIds, dirty-state and unsaved-close guard. Forced password change is presented before Shell access.
- R4.2 focused staff/migration/UI-contract coverage is `9/9` PASS; Debug full suite is `1336/1336` PASS; Release build and official Quality Gate are PASS with 0 warnings/errors, 0 failed/skipped, vulnerability scan PASS and EF pending-model PASS.
- Isolated Release startup reached `ShellWindowReady` under the exact launcher and all effective database/settings/logo/backup paths were scenario-owned. The execution desktop exposed no inspectable Win32 top-level window, so no unsupported visual UIA claim is made; production-service, persisted-state and source contract evidence remain recorded.
- R4.1 visual polish remains explicitly deferred. Exact next checkpoint: **R4.3 Role and Permission Management — READY TO START**. R4.3 has not started.

## R4.1 isolated-startup hotfix — CLOSED / COMMITTED / PUSHED — 2026-08-26

- Manual isolated smoke exposed a real startup blocker after R4.1. The exact chain was `AggregateException` from `ValidateOnBuild`, caused by missing `ILoggerFactory`/`ILogger<T>` while constructing R4.1 logger-dependent Store Setup, receipt, VietQR and database-initializer services.
- Root cause: the three existing pre-startup restore/worker `ServiceCollection` compositions called `AddInfrastructure` without `AddLogging`; the main Host provider had logging, so ordinary DI tests missed this exact path.
- Repair: centralized those compositions in `App.CreatePreStartupInfrastructureServices`, which adds logging before Infrastructure while retaining `ValidateOnBuild=true` and `ValidateScopes=true`. The IsolatedTest launcher now starts the built Release `POS.Enterprise.exe` directly and reports effective settings/logo paths inside the scenario boundary.
- Regression: `IsolatedStartupTests` exercises production configuration, isolated absolute database path, no Store Setup JSON, no printer, eager-service resolution and no production-path leakage. Sanitized isolated milestones prove Host, database initialization and LoginWindow construction/readiness.
- Final hotfix verification: Release build `0/0`; focused hotfix/Store Setup suite `11/11`; final full suite `1327/1327`; official Quality Gate, vulnerability scan and EF pending-model check PASS. R4.2 remains READY TO START.

## R4.1 Store Setup — CLOSED / COMMITTED / PUSHED — 2026-08-26

- R4.1 implements one Administrator-only Store Setup surface backed by an immutable, versioned typed `StoreSettingsSnapshot`, centralized validation/readiness, atomic external JSON persistence and managed logo storage.
- Production consumers now read the typed snapshot for receipt store data, VietQR, printer/paper/copies/auto-print, backup root/state and GFS retention. Register entry evaluates the same typed readiness contract and gives Administrator guidance for blocking issues.
- Database-directory changes are pending/restart-required and never replace the active database in place. IsolatedTest always derives database/settings/logo/backup paths inside its verified `%TEMP%` boundary and ignores persisted production paths.
- Release build, focused regressions, full suite `1327/1327` (0 failed, 0 skipped), vulnerability scan, EF pending-model check and official Quality Gate all PASS. R3 remains CLOSED at `d74bced32a74555bf36d57dea18f0e8d70708f71`; R4.1 hotfix remains part of the closed checkpoint.
- Isolated acceptance used only a temporary copied database; the exact scenario root was removed after evidence capture. No physical printer was available, so printer acceptance records typed unavailable/not-configured outcomes and does not claim hardware success.
- Exact next checkpoint: **R4.2 Employee and Account UI — READY TO START**.

## R3.4 Disaster Recovery Drill — CLOSED — 2026-08-26

- R3.4 PASS on a controlled `%TEMP%` boundary using the real production flow: verified manual backup of database A → normal initialization of a genuinely new database B → production restore preparation and external worker replacement → exactly one normal restart → real WPF sign-in → exact Orders, stock and receipt comparisons → SQLite integrity verification.
- DR1–DR9 machine evidence is complete. The backup artifact was production-inspected as restorable; the durable restore operation reached `Verified`; the parent, worker and restarted process lifecycle had no duplicate or orphan; the restored application reached the authenticated Shell; production repository reads and independent read-only SQLite projections matched the source baseline exactly; `PRAGMA integrity_check` returned only `ok` and `PRAGMA foreign_key_check` returned zero rows.
- The canonical database remained `937984` bytes with timestamp `2026-08-09T14:26:03.9619805Z` and SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`. Canonical automatic/restore/pre-restore roots and WAL/SHM/journal sidecars remained absent; the exact scenario root was safely removed and owned process count returned to zero.
- No production defect was found. One Windows-portability defect in a restore test fixture was corrected by matching production's `File.Replace(..., ignoreMetadataErrors: true)` behavior without weakening assertions. Focused recovery tests are `142/142` PASS. The final official Quality Gate is PASS: build `0` warnings/`0` errors, full suite `1317/1317`, `0` failed, `0` skipped, vulnerability scan PASS, and EF pending-model check PASS.
- R3.1–R3.4 and R3 are CLOSED. The R3.4 prerequisite blocker for pilot is removed. R4 remains NOT STARTED; R4.1 Store Setup is the exact next checkpoint.

## R3.3 Restore Wizard and Rollback — CLOSED — 2026-08-25

- R3.3B/C/D/E implementation and verification are complete. Production now provides fail-closed artifact inspection, durable restore planning/state, a startup restore worker with replacement/rollback, and the authenticated WPF Restore Wizard/recovery flow.
- RST1–RST13 are PASS using retained isolated runtime evidence. RST14 is **ACCEPTED — combined runtime, durable-state and automated contract evidence; independent external UIA/PID timeline incomplete**. The retired external harness limitation is recorded honestly and is not represented as a machine-assisted timeline PASS.
- RST15 cumulative canonical-safety audit is PASS. Every destructive acceptance path was isolated beneath `%TEMP%`; the canonical database remained `937984` bytes, timestamp `2026-08-09T14:26:03.9619805Z`, SHA-256 `C1F4BCCF022F896DD0948F2E25AFABE831DF3EF9CE1B289E9D933F9A33BDDBED`, with canonical automatic/restore/pre-restore roots and SQLite sidecars absent.
- Final verification is PASS: restore targeted `89/89`, backup/restore UI `89/89`, Release rebuild `0` warnings/`0` errors, full Release `1317/1317` with `0` failed/`0` skipped, vulnerability scan PASS, EF pending-model check PASS, and official Quality Gate PASS without `-SkipEfCheck`.
- The WAL checkpoint/hash defect is repaired and regression-covered. RST7 uses a real regular-file reparse fixture. R3.3 is Closed / Committed / Pushed at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`. The newer R3.4 closeout section above supersedes the checkpoint position recorded here.

## R3.2 Automatic Backup and Retention — CLOSED — 2026-08-23

- R3.2 is complete locally: 24-hour daily policy, verified automatic artifacts, shared singleton coordination, and deterministic GFS retention of 7 latest, 4 UTC ISO-week and 3 UTC monthly snapshots. The 2 GiB quota is secondary and returns `SuccessWithRetentionWarning` rather than deleting required GFS snapshots.
- ABM1/ABM2 PASS with human + machine evidence. ABM3-A/B, ABM4-A/B, ABM5-A/B and ABM6 PASS with user-approved machine-assisted production-runtime evidence on isolated `%TEMP%` resources. Evidence: `C:\Users\Dell\AppData\Local\Temp\POS-Enterprise-R3.2C-R2M-Evidence-20260823T150820552Z-5c42379a87ee40f1ab7e694a67a8dc1e`.
- Targeted R3.2 regression is `76/76` PASS; official outside-sandbox Quality Gate is `1240/1240` PASS with vulnerability, EF pending-model and Git checks PASS. Store Setup configurability remains R4.1, end-of-day remains R9, and shutdown-triggered backup is not claimed as R3.2 scope.
- **Historical R3.2 closeout status on 2026-08-23:** R3.3 Restore Wizard was NOT STARTED and was the exact next checkpoint. Current R3.3/R3.4 status is recorded in the newer section above.

## R3.1 Manual Backup — CLOSED / COMMITTED / PUSHED — 2026-08-09

- R3.1 formal closeout audit is PASS at product checkpoint commit `1cbabbbb8928a29c520897c849c405f6ad6e16de`, subject `feat(r3.1): complete verified manual backup workflow`. The commit is pushed to `origin/main`; HEAD equals origin/main and the post-push working tree is clean. The authenticated Shell exposes an owner-modal manual-backup workflow with explicit destination selection and consent, single-flight execution, typed fail-closed results, deferred close while work is running, and success presentation containing the final path, UTC completion time, exact byte length and SHA-256.
- `ManualBackupService` resolves the active runtime database from `InfrastructureOptions`; validates source and destination; requires source integrity and schema compatibility; delegates to the existing SQLite backup primitive that creates a unique temporary file, verifies it before a no-overwrite final move, verifies the final file and cleans operation-owned failed output; then rechecks final integrity/schema, positive length and SHA-256 before returning `Success`. No automatic scheduling or retention behavior is included.
- Official Quality Gate on the exact product/script working tree PASSed with exit code `0`: restore PASS; Debug build `0` warnings/`0` errors; `1183/1183` tests PASS with `0` failed/`0` skipped; vulnerability scan PASS for `5/5` projects; local tools restore PASS; EF pending-model check PASS; Git checks PASS.
- MB1 backup action = PASS — Independently Verified. The isolated synthetic fixture remained `372736` bytes with SHA-256 `0B7CF7803264B3F909FCB8ECA97607124D40066EA9E23DCE27FF4DEA354AA34E`. Destination contained exactly one final artifact and no partial/temp output; the artifact was `372736` bytes with SHA-256 `96D79B539267E762096A5B594363D78C9CBB71A36112E04D1BCA4A0FEBF23EB8`, matching the UI evidence.
- Independent arbitrary-artifact SQLite integrity/schema probe = **NOT RUN** because the repository has no safe command-line probe for an arbitrary artifact and this closeout did not create a harness or open the application. This is non-blocking for R3.1 because the live product path and the executed happy-path regression prove integrity/schema verification before success; it is not recorded as an independent probe PASS.
- No official MB2 contract exists in live source or Project Memory. R3.1 is Closed, Committed and Pushed; the separate post-push Project Memory synchronization commit does not contain the product implementation. R3.2 Automatic Backup and Retention is **NOT STARTED** and is the exact next checkpoint.

## R2.4D Database Runtime Hardening — CLOSED — 2026-08-09

- R2.4A, R2.4B, R2.4C and R2.4D are complete. R2.4 and R2 are Closed / Committed / Pushed at `ff91ab515507666a3fb3b01fc28e7ad0f6241d59`.
- Runtime path resolution keeps source/test build output on the repository database, keeps EF tooling semantics, and routes standard publish output to `%LocalAppData%\\POS Enterprise`. Publish output under the repository, outside it, with a renamed executable, or with a different working directory does not select the repository database.
- An external `Infrastructure__DatabasePath` is blocked before host start, database identity acquisition, `DbContext`, SQLite open/create, integrity checking or migration unless the child process is an explicit source-output `IsolatedTest` with an absolute non-canonical path. Published output is blocked even if `DOTNET_ENVIRONMENT=Development` or the test mode variable is present.
- Startup diagnostics are allow-listed metadata only: safe environment name, provider category, runtime mode and override-present boolean. No database path, executable path, working directory or raw environment value is logged.
- The child-process scripts use direct process environment construction, retain isolated snapshots, propagate exit codes and do not mutate the parent PowerShell environment. The Windows PowerShell fixed-size environment-collection failure was corrected by initializing and copying the mutable legacy child collection; clean M1 and M3 launches completed without that error.
- Manual acceptance passed: M1 clean Normal source run, M2 stale override block, M3 valid Isolated Test, M4-A standard publish with different cwd, M4-B relocated publish, M4-C renamed executable, and M5 published override with `DOTNET_ENVIRONMENT=Development`. M6 filesystem/hash audit also passed.
- Fresh verification: targeted runtime/database/storage regressions `105/105` PASS; Release build `0` warnings/`0` errors; Release full regression `1164/1164` PASS with `0` failed/`0` skipped; outside-sandbox Quality Gate Debug regression `1164/1164` PASS with EF pending-model check, vulnerability scan `5/5`, and Git checks PASS.
- The publish helper produced the expected POS.Enterprise payload; `appsettings.Development.json`, database artifacts and PDBs were absent. The full artifact contract was not rerun in this publish-only probe because TRX/log/report groups were not generated.

## R2.4D closeout evidence

- User-observed manual evidence is recorded for M1–M5. M1 was rerun from a clean state with exactly one WPF instance after an earlier duplicate-launch observation; only the clean rerun is accepted. M2 and M5 showed the safety block without exposing path/cwd/executable/raw environment values. M6 found no new repository or publish database/WAL/SHM artifacts, and protected database hashes/metadata remained unchanged.
- The checkpoint closeout is recorded at `ff91ab515507666a3fb3b01fc28e7ad0f6241d59`.

## R2.4C Authenticated Storage Status UI — COMPLETED — 2026-08-08

- R2.4 remains **IN PROGRESS**. R2.4A, R2.4B and R2.4C are completed; R2.4D is the next checkpoint and remains **NOT STARTED**. R3 and R4 remain **NOT STARTED**.
- The authenticated Shell now exposes a compact `Dung lượng` status entry and an owner-centered detail modal. The UI reuses the production `IDatabaseStorageMonitor` contract and does not replace R2.4B startup-preflight authority.
- Presentation is typed and fail-safe: initial, loading, healthy, warning, insufficient and unavailable states are explicit; refresh is asynchronous, single-flight and retryable after unavailable results. Main database size and total SQLite footprint are labelled separately, and unavailable values are not shown as zero.
- The surface contains no database/backup path, connection string, raw exception, backup/restore/cleanup/delete control or new permission/schema. Existing authenticated Shell authorization, modal ownership and DI conventions are reused.
- Independent review and automated re-verification passed. R2.4C targeted tests: `18/18` PASS; combined regression: `166/166` PASS; POS.Wpf Release build: 0 warnings/0 errors; outside-sandbox Full Quality Gate: `1142/1142` PASS; vulnerability scan PASS; EF pending-model check PASS.
- Manual acceptance passed for Shell entry, authorization, modal ownership/single-instance behavior, repeated open/close, layout and Vietnamese wording, displayed metrics, refresh, privacy/forbidden controls, Shell navigation and application shutdown. No manual acceptance remains pending for R2.4C.
- A separate authentication incident remains under investigation and is not a confirmed R2.4C defect or root-cause finding.

## R2.4B Startup Migration Storage Preflight — COMPLETED — 2026-08-08

- R2.4 remains **IN PROGRESS**. R2.4A, R2.4B and R2.4C are completed; R2.4D is **NOT STARTED**. R3 and R4 remain **NOT STARTED**.
- Startup storage preflight runs only after pending migrations are confirmed and before integrity checking, the existing verified pre-migration backup, `MigrateAsync`, or any schema mutation. With no pending migration, the monitor and backup remain untouched.
- Existing databases use the production footprint-plus-padding estimate; unknown existing footprint fails safe when free-space metrics are available. Fresh/missing databases request zero additional backup bytes and do not create a pre-migration backup. `Allowed`, `AllowedWithWarning`, and `MetricsUnavailable` continue; `Insufficient` throws a typed safe exception before mutation. Unknown status values fail closed.
- Cancellation is propagated unchanged. New logs contain only typed status and no database/backup path, volume root, connection string, identity, or raw exception detail. No UI, new backup/restore workflow, retention, package, schema, migration, or ModelSnapshot change is included. Manual acceptance is **NOT APPLICABLE — no manual surface in R2.4B**.
- Independent review corrected fail-open unknown-status handling and a zero-byte estimate for an existing database with unavailable footprint metadata. Fresh verification: `DatabaseInitializerSafetyTests` `15/15` PASS; combined storage/path/options/DI/architecture/configuration/SQLite regressions `99/99` PASS; Release build 0 warnings/0 errors; complete outside-sandbox Quality Gate `1124/1124` PASS with 0 failed/0 skipped, vulnerability scan PASS for 5/5 projects, EF pending-model check PASS and Git checks PASS. The sandbox run reached 1122 PASS/2 environment-only R2.1 named-pipe failures; production IPC was unchanged.

## R2.4A Typed Storage Snapshot Foundation — COMPLETED — 2026-08-08

- R2.4 remains **IN PROGRESS**. R2.4A is completed; R2.4B is the next checkpoint and is **NOT STARTED**. R3 remains **NOT STARTED**.
- Application now owns typed database-storage snapshot and operation-preflight contracts. Infrastructure provides a metadata-only monitor over the canonical database path, exact SQLite WAL/SHM/journal siblings and containing-volume capacity without opening or reading database content.
- Production policy warns at 5 GiB absolute free space or 10% available capacity, reserves 512 MiB operational headroom, and estimates a future pre-migration backup as footprint plus `max(256 MiB, ceil(10% of footprint))`. Arithmetic is overflow-safe; unavailable metrics remain permitted for the future startup-migration integration.
- R2.4A does not connect the monitor to `DatabaseInitializer`, change startup behavior, add UI, implement backup/restore or cleanup, or modify packages/schema/migrations. Manual acceptance is **NOT APPLICABLE — no manual surface in R2.4A**.
- Independent-review fixes prevent inaccessible metadata from being reported as a missing file, revalidate parent reparse points before returning a snapshot, observe cancellation between metadata reads, and derive `CanProceed` from typed preflight status.
- Fresh verification: exact `DatabaseStorageMonitorTests` `44/44` PASS; targeted storage/path/options/DI/architecture/security/initializer/SQLite regressions `89/89` PASS; Release build 0 warnings/0 errors; complete outside-sandbox Quality Gate `1114/1114` PASS with 0 failed/0 skipped, vulnerability scan PASS for 5/5 projects, EF pending-model check PASS and Git checks PASS.

## R2.3 Logging and Support Bundle — COMPLETE — 2026-08-08

- R2.3A Safe Logging Foundation, R2.3B Safe Support Bundle Composition & Export Service and R2.3C Support Bundle UI all have automated verification PASS. R2.3D manual acceptance M01–M09 is PASS with no observed issue.
- The locally generated manual archive was inspected read-only outside the repository: ZIP structure, fixed JSON entries, dynamic managed-log entry boundary, database-artifact exclusion, path safety, manifest database exclusion and temporary-file absence all PASS. Raw bundle content and the machine-specific archive path are not retained in Project Memory.
- Final verification: R2.3C `21/21`, R2.3B `12/12`, R2.3A `11/11`, exact corrective tests `2/2`, R2.2 live `26/26`, architecture/privacy/security `22/22`; all PASS with 0 failed/0 skipped. POS.Wpf Release build PASS with 0 warnings/0 errors.
- Final Quality Gate outside sandbox PASS without `-SkipEfCheck`: `1070/1070` PASS, 0 failed/0 skipped; gate build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model and Git checks PASS. The accepted sandbox provenance remains 1068 PASS/2 environment-only R2.1 named-pipe failures; production IPC was not changed.
- The Support Bundle excludes the sales database and database sidecars/backups, uses safe diagnostic filtering, and never automatically uploads or sends the archive. No package or migration was added and the main database was not read, copied, hashed, modified or deleted during closeout.
- R2.3 is **COMPLETED**. R2 remains IN PROGRESS because R2.4 Disk Space and Database Growth is **NOT STARTED**; no R2.4 implementation occurred.

## R2.3C Support Bundle UI, consent, progress and cancellation UX — automated verification — 2026-08-08

- Current checkpoint: R2.3 — Logging and Support Bundle — **IN PROGRESS**. R2.3A, R2.3B and R2.3C have automated verification PASS; R2.3C manual acceptance and R2.3D are not performed, so R2.3 is not complete.
- The authenticated Shell now has a minimal `Gói hỗ trợ` entry available to signed-in users without inventing a permission or schema. It opens an owner-centered modal through the existing WPF dialog/DI conventions.
- The Vietnamese disclosure states the fixed technical contents, database/WAL/SHM/journal/backup exclusion, no automatic upload/send, user-selected destination and shared diagnostic filtering. Consent defaults false and is reset after every terminal operation.
- `SupportBundleViewModel` depends only on `ISupportBundleService` and a thin WPF folder-picker adapter. Its finite states are Idle, Ready, Running, Cancelling, Success, Cancelled and Failed. It enforces one async operation, indeterminate progress, typed safe result mapping, fixed `IncludeDatabase=false`, per-operation CTS lifecycle and close-after-terminal cancellation behavior.
- Verification: targeted R2.3C `21/21` PASS; R2.3B `12/12`; R2.3A `11/11`; corrective ownership `2/2`; R2.2 live `26/26`; architecture/privacy/security `22/22`; POS.Wpf Release build PASS with 0 warnings/0 errors.
- Full Quality Gate outside sandbox PASS without `-SkipEfCheck`: `1070/1070` PASS, 0 failed/0 skipped; vulnerability scan PASS for 5/5 projects; EF pending-model and Git checks PASS. The sandbox run reached 1068 PASS/2 FAIL only in the accepted R2.1 named-pipe environment boundary; IPC was unchanged.
- No WPF application/manual UI run, package change, migration/database update, main database access, R2.3D or R2.4 work occurred. No stage/commit/push/merge occurred.

## R2.3B Safe Support Bundle Composition & Export Service — automated verification — 2026-08-08

- Current checkpoint: R2.3 — Logging and Support Bundle — **IN PROGRESS**. R2.3A and R2.3B have automated verification PASS; R2.3C/D, UI and manual R2.3 acceptance are not performed, so R2.3 is not complete.
- `ISupportBundleService` exposes typed request/result contracts in Application. Database inclusion defaults false and `IncludeDatabase=true` fails closed before destination, collector, temporary file, ZIP or database access.
- Infrastructure creates only a fixed, versioned diagnostic schema plus top-level managed logs. It uses a unique destination-local `CreateNew` temporary file, closes and flushes the ZIP before a no-overwrite final move, removes only an operation-owned temp on failure/cancellation and never exposes a partial final filename.
- Diagnostics are allow-listed and normalized: product/version, EF known/applied/pending migration identifiers, bounded SQLite `PRAGMA quick_check`, safe configuration limits and OS/runtime facts. No migration is applied, SQL generated, business row read, raw configuration/environment serialized, or user/machine/path/command-line value exported.
- Log export reuses the R2.3A managed-file ownership policy and shared sanitizer. It is top-level-only, newest-first, snapshot- and 20 MiB-budget-bounded, revalidates before/after open and before read, streams complete UTF-8 records with bounded overlong-record discard, and tolerates active append/source size changes without following reparse points.
- Verification: targeted R2.3B `12/12` PASS; targeted R2.3A `11/11` PASS; corrective ownership tests `2/2` PASS; live R2.2 class `26/26` PASS; related architecture/privacy/security `22/22` PASS; POS.Wpf Release build PASS with 0 warnings/0 errors.
- Full Quality Gate outside sandbox PASS without `-SkipEfCheck`: `1049/1049` PASS, build 0 warnings/0 errors, vulnerability scan PASS for 5/5 projects, EF pending-model PASS and Git checks PASS. The sandbox run had only the accepted R2.1 named-pipe environment failures (1047 passed/2 failed); production IPC was not changed.
- No WPF UI/application launch, package change, migration, database update, main database read/copy/hash, R2.3C/D or R2.4 work occurred. No stage/commit/push/merge occurred.

## R2.3A Safe Logging Foundation — automated verification — 2026-08-08

- Current checkpoint: R2.3 — Logging and Support Bundle — **IN PROGRESS**. R2.3A Safe Logging Foundation is implemented and automated-verification PASS; R2.3B/C/D are not started and R2.3 is not complete.
- Corrective security verification: managed-log enumeration now accepts only canonical direct-child regular files, rejects directories/reparse points before reading size/time metadata, and revalidates the same ownership guard immediately before delete. New-file creation uses `CreateNew`, so a foreign/reparse entry occupying a managed-looking sequence is never followed or overwritten.
- The Microsoft Extensions logging stack now has one DI-registered, fail-safe rotating file provider. Production logs resolve to `%LocalAppData%\POS Enterprise\logs`; no current-working-directory, database/data, backup, source, bin or obj location is used.
- Policy defaults are typed and configurable: 5 MiB per segment, 10 managed segments, 50 MiB total and 14 days. UTC-day or size starts a new system-named segment; oldest managed files are removed first and foreign files are never deleted.
- `PosLog` redacts sensitive property names/values and never forwards raw exceptions. SQLite diagnostics retain exception type and numeric primary/extended codes without provider message, SQL or database path. Trace fallbacks likewise emit exception type only.
- Verification after corrective pass: targeted R2.3A `11/11` PASS; live R2.2 class regression `26/26` PASS; related privacy/architecture `14/14` PASS; POS.Wpf Release build PASS with 0 warnings/0 errors; complete outside-sandbox Quality Gate PASS without `-SkipEfCheck`, full `1037/1037`, vulnerability scan PASS for 5/5 projects, EF pending-model PASS and Git whitespace check PASS. The two-test increase is the reparse/ownership regression coverage.
- No WPF application, migration, database update or main database read/copy/modification/deletion occurred. No Support Bundle UI, R2.4 work, package change, commit, push or merge occurred.

## Corrective Order History and Product List UX closeout — 2026-08-08

- This is a narrow corrective UX closeout after R2.2; it does not start or complete R2.3 and does not change the roadmap checkpoint state.
- Order History corrective source commit: `9211b9e0fbbb8c020c55377916048c1dd1244ff4`; integrated `main` commit: `1c048ec381d5ccd48b1433aaa77d61a91ec1c262`. Product List UX source commit: `f09377525e9b6a5f83229fdcdc7a8c15af15c18b`; integrated `main` commit: `352d2814ba8b194c8abf875852462f16f05153d4`.
- User manual acceptance: **PASS** for the Order History panel proportions, financial summary, table height, action bar and small-window layout; **PASS** for stable Product search width during focus/text entry, revised column proportions and light vertical separators.
- Integrated-main verification: `OrderHistoryUiContractTests` 43/43 PASS; `ProductArchivingUiContractTests` 9/9 PASS; POS.Wpf Release build PASS with 0 warnings/0 errors; full Release suite 1026/1026 PASS with 0 failed/0 skipped.
- Full Quality Gate PASS without `-SkipEfCheck`: repeated tests 1026/1026 PASS, build 0 warnings/0 errors, dependency vulnerability scan PASS for 5/5 projects, EF pending-model check PASS, and Git checks PASS.
- No WPF application, migration, database update or database-row inspection was performed during integration verification. No push was performed.

## R2.2 closeout — 2026-08-06

- Current checkpoint: R2.3 — Logging and Support Bundle — IN PROGRESS; R2.3A automated verification PASS.
- Next subcheckpoint: R2.3B — NOT STARTED; no R2.3B work is authorized by this handoff.
- R2.2 classifies SQLite busy, locked, disk-full, corruption/not-a-database and unknown provider failures by numeric base code; persistence boundaries translate failures while preserving the technical cause for logging. Checkout writes are not retried blindly. Retry is bounded and restricted to explicitly safe operations.
- Sales checkout presents sanitized, actionable messages and preserves cart, quantity, payment input and method after database failure. Checkout state is released and receipt/success flow does not run after failure. Startup presents a sanitized blocking error before the session loop when the database cannot be opened safely.
- Acceptance Test A: **Manual PASS**. A real `BEGIN IMMEDIATE` lock produced the expected bounded failure UX, preserved two cart lines/quantity 3, `85.000 đ` cash input and Cash method, opened no receipt/success flow, restored checkout UI, persisted `0/0/0/0` while locked, and after lock release/retry persisted exactly Orders `1`, OrderItems `2`, InventoryMovements `2`, ReceiptSnapshots `1`. Evidence directory `POS-R22-Acceptance-20260806-Manual1` remains outside Git.
- Acceptance Tests B/C/D: **NOT MANUALLY RUN**. **Covered by equivalent deterministic automated acceptance: PASS**. They must not be described as Manual PASS.
- Automated acceptance uses only isolated databases under TEMP, creates its own data, acquires/releases a real SQLite lock, verifies before/after atomic counts and duplicate rejection, simulates `SQLITE_FULL` by error code without consuming disk, hashes a separate not-a-database file with SHA-256 before/after, verifies startup blocking/window ordering and cleans its generated TEMP directories deterministically.
- Final verification: targeted R2.2 acceptance `27/27` PASS; full Release `1019/1019` PASS with 0 failed/0 skipped; Quality Gate PASS without `-SkipEfCheck`; gate build 0 warnings/0 errors; vulnerability scan PASS for 5/5 projects; EF pending-model consistency PASS; changed-file security/secret scan PASS with 0 high-confidence findings; `git diff --check` PASS (CRLF/LF notices are not whitespace errors).
- Residual boundary: manual B/C/D were intentionally not run; their acceptance is equivalent deterministic automation, not manual evidence. Logging/Support Bundle remains R2.3 and disk-space monitoring/growth remains R2.4; neither is implemented by R2.2.

## 1. Metadata

- Document purpose: bản tóm tắt sự thật để chuyển giao giữa các session.
- CapturedAtLocal: `2026-07-31T11:34:28.975+07:00` (Asia/Bangkok, UTC+07:00).
- ReviewedAtLocal: `2026-08-01` (Asia/Saigon, UTC+07:00).
- ReconciledAtLocal: `2026-08-02` (Asia/Saigon, UTC+07:00).
- FormalCloseoutPrepAtLocal: `2026-08-02` (Asia/Saigon, UTC+07:00).
- R2.1CloseoutAtLocal: `2026-08-02` (Asia/Saigon, UTC+07:00).
- Current repository HEAD after the R1 formal-closeout commit: `b9e382550e2e4abcf7a93ed6c5352322dc967668`.
- R0.5 formal closeout commit: `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.
- R1.1 formal repository closeout commit: `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`.
- R0.5 Context Pack baseline commit: `70523861949aeb5eefe981633db33f50bc890145`.
- Base commit for the original R0.5B capture: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Tài liệu này được tạo trong R0.5B; R0.5 Project Memory được formal-closeout trong `dfb0eb7a000054664aa7feccb51778fe80aa32a7`.

## 2. Repository

- Repository root: `D:\Projects_1\POS_Enterprise_DotNet`
- Solution: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`
- Branch: `main`
- Upstream: `origin/main`
- R2.1 implementation base HEAD: `b437be5de3a3f6deb03b142c88dd913610ebc834`
- R2.1 source and Project Memory are committed together by the closeout commit containing this document.
- Ahead/behind: `0/0`

## 3. Checkpoint status

- Project: POS Enterprise Retail V1
- Current checkpoint: R2.3 — Logging and Support Bundle — IN PROGRESS; R2.3A, R2.3B and R2.3C have automated verification PASS.
- Previous checkpoint: R1.2 — Repository Standards — Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`.
- Next subcheckpoint: R2.3D — NOT STARTED; R2.3C manual acceptance is not performed.
- R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`; R1.3 implementation and live Jenkins build #5 passed on `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`; R1.3 and the entire R1 are Closed by formal-closeout commit `b9e382550e2e4abcf7a93ed6c5352322dc967668`.

Completed subcheckpoints in the R0.5 closeout payload:

- R0.5A — Project Memory Entry Gate — PASS.
- R0.5B — Core Project Guidance — PASS.
- R0.5C — Architecture Memory — PASS.
- R0.5D — Operating Memory — PASS.
- R0.5E — Context Exporter — PASS. Pack `project-context-20260801T0647171300576Z` was produced from baseline `70523861949aeb5eefe981633db33f50bc890145`; exporter exit code `0`, coverage `501/501` (100%), security findings `0`, excluded candidates `0`, and manifest integrity `16/16` PASS.
- R0.5F — Verification and Closeout — PASS in the closeout payload. ChatGPT Context Pack fresh-session verification PASS and Codex repository fresh-session verification PASS on `2026-08-01`.

Fresh-session provenance is intentionally later than the historical Context Pack: ChatGPT evidence is the user-supplied transcript plus manifest follow-up; Codex evidence is the independent fresh-session report against live Project Memory/Git. Neither verification claims to have committed or pushed files.

R0.5 is Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; R1.1 is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`; R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`; R1.3 and the entire R1 are Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`.

Final R0.5 local verification on `2026-08-01`: `git diff --check` PASS; restore PASS; Release build PASS with 0 warnings/0 errors; Release full tests 975/975 PASS with 0 failed/0 skipped; full Quality Gate rerun PASS without `-SkipEfCheck`, including 975/975 tests, dependency vulnerability scan with no vulnerable packages, EF pending-model check PASS and Git checks PASS. Replay probe is absent and Jenkinsfile is unchanged. The first sandboxed Quality Gate invocation stopped at the vulnerability command with exit code `1` and no package result because network was unavailable; the same scan and the complete gate then passed outside the sandbox with NuGet access.

R1.2 verification on `2026-08-01`: pinned SDK `10.0.302`; restore PASS; Release build PASS with 0 warnings/0 errors; Release full tests 975/975 PASS with 0 failed/0 skipped; Quality Gate PASS with exit code `0`, vulnerability scan PASS and EF pending-model check PASS without `-SkipEfCheck`; Jenkinsfile unchanged; replay probe absent. Repository standards added/updated: `.gitattributes`, `.editorconfig`, `global.json`, `CHANGELOG.md` and `/_audit_temp/` in `.gitignore`. Existing deterministic/CI build metadata in `Directory.Build.props` was retained. Manual UI acceptance is N/A because the payload contains repository metadata and Project Memory only.

R1.3 local verification on `2026-08-01`: SDK `10.0.302`; restore PASS; Release build 0 warnings/0 errors; 975/975 tests PASS; TRX semantic validation PASS; vulnerability JSON parse and semantic validation PASS across 5 projects with no vulnerable package; Quality Gate PASS with exit code `0` and EF pending-model check PASS; native publish PASS with 50 files and 11,807,483 bytes; exact five publish-root paths PASS; PDB count `0`, denylist count `0`, and secret/customer-data scan PASS; controlled failure probe preserved exact exit code `23`; `_ci_artifacts` was safely removed.

R1.3 live Jenkins verification supplied for job `POS_ENTERPRISE_R1_1_CI`, build `#5`, URL `http://localhost:8080/job/POS_ENTERPRISE_R1_1_CI/5/`, on exact SCM revision `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`: SUCCESS; Release build 0 warnings/0 errors; tests total/executed/passed `975/975/975`, failed `0`, skipped/notExecuted `0`; Quality Gate PASS; vulnerability scan PASS; EF pending model changes none; publish win-x64 PASS; artifact contract validator PASS; archive/fingerprint PASS; Publish `50` files / `11,807,483` bytes; complete archived contract `56` files / `13,291,155` bytes; artifact ZIP SHA-256 `38adf83096b23b19b3e17ee5fc143025cbf15bcbbb12cdb688efe160414c848d`. This was supplied live evidence; the pipeline was not rerun in this preparation turn.

Manual artifact smoke test: Jenkins ZIP download, complete extraction and `POS.Enterprise.exe` launch/basic operation PASS using an existing Windows user profile. Clean Windows profile/clean-machine first-run behavior remains Not Revalidated and customer clean-install acceptance is not claimed. The artifact `appsettings.json` has blank `Payment.BankBin`, `Payment.AccountNumber`, `Payment.AccountName` and `Infrastructure.DefaultAdminPassword` values and the ZIP contains no database. Previously displayed VietQR values came from configuration persisted under the existing Windows user profile, not from the ZIP. Forgot password and change password are not implemented; first-run store/VietQR setup and account/password-management gaps remain deferred to R4 under the current roadmap.

Historical R2.1A discovery/baseline completed without implementation changes: Release build 0 warnings/0 errors; 975/975 tests PASS; final Quality Gate rerun PASS; vulnerability scan PASS; EF pending model changes none. At that capture time, implementation and acceptance were Not Started; discovery established that ownership must be acquired before `Host.StartAsync` and before `InitializeDatabaseAsync`/SQLite access. The completed implementation is recorded below.

R2.1 implementation and closeout on `2026-08-02`: database paths are canonicalized into a SHA-256-scoped identity; a current-user Windows mutex and ACL-restricted named pipe enforce one owner per database while allowing different database identities to run independently; ownership is acquired before `Host.StartAsync`, database initialization and SQLite access. A second same-identity launch requests foreground activation and exits without opening the database. Window activation supports setup, login and shell targets, restores minimized windows and uses taskbar attention as a best-effort fallback.

R2.1 automated verification: `git diff --check` PASS; Release rebuild PASS with 0 warnings/0 errors; both named-pipe tests PASS individually; `SingleInstanceInfrastructureTests` PASS 11/11; the two IPC tests PASS 10/10 finite stability rounds; full Release tests PASS 992/992 with 0 failed/0 skipped. The earlier sandboxed full run failed 2/992 because the execution sandbox denied even a minimal local named-pipe client connection; the same tests passed outside the sandbox. The test harness was made deterministic by signaling readiness only after `StartActivationListener` returned, removing the arbitrary post-malformed-payload delay and disposing request cancellation deterministically. No production ACL or current-user isolation was weakened.

R2.1 final Quality Gate PASS without `-SkipEfCheck`: restore PASS; build PASS with 0 warnings/0 errors; 992/992 tests PASS; all five projects report no vulnerable packages; local tool restore PASS; EF pending-model check reports no changes since the last migration; Git checks PASS. Final post-memory Project Context export also PASSed with 100% coverage, final manifest verification PASS and `SecurityFindingCount=0`; generated packs remain ignored and are not staged.

R2.1 manual multi-process/crash acceptance: Tests A/B/C PASS. Test D attempt 1 remains INCOMPLETE / NOT PROVEN. Test D attempt 2 PASS at `C:\Users\Dell\AppData\Local\Temp\POS-R21-Acceptance-20260802-Final`: old Store A PID `23292` at `2026-08-02T20:40:18.4019720+07:00` used the expected Release executable and Store A database identity, then exited with `HANDLE_SIGNALED_AND_OS_PID_ABSENT`; Store A relaunched as PID `15524` at `2026-08-02T20:40:56.1570299+07:00` and returned to Login; Store B remained PID `13056` with unchanged start time/path and GUI state. ETL SHA-256 is `DF08062C04E37BFAD920A2405713DA59DC6E17AD63A673F65C9F22340FB7560C`; bounded tracerpt records `buffers=808`, `events=3119706`, `lostEvents=0` and `skippedEvents=0`. Zero is claimed only for those two explicit loss counters; full multi-gigabyte XML/FileIO export was not an R2.1 acceptance requirement.

## 4. Working-tree state

R2.1 entry base was clean at `b437be5de3a3f6deb03b142c88dd913610ebc834`. The closeout payload contains only reviewed R2.1 source, tests and affected Project Memory. It contains no package, SDK, target-framework, schema, migration, database, customer data or R2.2 implementation change. Generated Project Context packs and manual evidence remain outside Git.

## 5. R0 authoritative baseline

- R0 commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`
- Commit subject: `feat(checkout): add controlled discounts and durable VietQR recovery`
- Manual acceptance: PASS.
- Build: PASS — 0 warnings, 0 errors.
- Full tests: 969/969 PASS — 0 failed, 0 skipped.
- Quality Gate: PASS.
- EF pending-model check: ran and passed; Quality Gate không dùng `-SkipEfCheck`.

Các kết quả trên là accepted R0 baseline. Build, tests và Quality Gate không được chạy lại trong R0.5B.

Historical R0.5 verification evidence at Context Pack baseline `70523861949aeb5eefe981633db33f50bc890145`: sequential build PASS with 0 warnings/0 errors; full tests 975/975 PASS; Quality Gate PASS with EF pending-model check and exit code 0; VietQR manual acceptance PASS. The Context Pack/exporter evidence is recorded separately above and is not relabeled as evidence for live HEAD `afdda252ce124413b9190607a96a0046cf5097e7`.

## 6. Latest migration

- Migration ID: `20260730103954_AddHeldSalePaymentOwnershipGuard`
- Filename: `20260730103954_AddHeldSalePaymentOwnershipGuard.cs`
- Absolute path: `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs`
- Evidence: migration inventory từ repository live, loại `PosDbContextModelSnapshot.cs` và các file `*.Designer.cs`, sau đó sắp xếp theo filename.

R0.5B không đọc `__EFMigrationsHistory`; vì vậy không tuyên bố migration này đã applied vào database thật.

## 7. Architecture summary

Project references live xác nhận hướng lõi:

`POS.Domain → POS.Application → POS.Infrastructure → POS.Wpf`

- `POS.Domain` không có production project reference.
- `POS.Application` tham chiếu `POS.Domain`.
- `POS.Infrastructure` tham chiếu `POS.Application` và `POS.Domain`.
- `POS.Wpf` tham chiếu `POS.Application` và `POS.Infrastructure`.

Architecture audit chi tiết, gồm service map, transaction map và business invariants, đã được thực hiện trong R0.5C.

## 8. Active issues

- Không còn lỗi manual R0 đang mở theo authoritative R0 closeout.
- Comprehensive issue inventory exists locally in `KNOWN-ISSUES.md`; POS-VER-005 remediation was committed and closed in `dfb0eb7a000054664aa7feccb51778fe80aa32a7`, without recording its former value.
- Không biến future roadmap gaps thành confirmed runtime bugs.
- Phase 1 không phát hiện mâu thuẫn kỹ thuật buộc phải dừng; đường dẫn migration thực tế đã được lấy trực tiếp từ source live.

## 9. Files changed in current subcheckpoint

- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseIdentity.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabasePathResolver.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Platform\WindowsSingleInstanceCoordinator.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\WindowActivationService.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SingleInstanceInfrastructureTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\WindowActivationCoordinatorTests.cs`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CHECKPOINT-WORKFLOW.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`

- Production source changed: Yes — database identity, database-scoped Windows ownership/activation and WPF activation composition only.
- Tests changed: Yes — single-instance identity, mutex, ACL, activation, lifecycle, crash takeover and window activation coverage.
- Migrations changed: No.
- Package/SDK/target framework changed: No.
- Repository standards changed in R1.3: No.
- CI artifact configuration and Project Memory changed: CI artifact implementation is already committed at the current HEAD; only Project Memory is changed in this preparation turn.
- Product behavior changed: Yes — one owner per database identity and second-instance activation behavior for R2.1.

## 10. Prohibited next actions

- Không đổi `POS.Wpf.csproj` assembly identity; R1.3 implementation must use native `POS.Enterprise.*` output and remains uncommitted/unpushed until review.
- Không sửa production source, tests, POS.Wpf.csproj hoặc Quality Gate script; Jenkinsfile changes are limited to the R1.3 artifact contract.
- Context Pack đã tạo trong `artifacts/project-context/`, thư mục này bị ignore và không được stage.
- Không chạy database update.
- Không đọc dữ liệu database thật.
- R2.1 closeout commit/push đã được cho phép sau khi review chính xác staged scope; không amend hoặc force-push.
- R2.1 and R2.2 are COMPLETE/CLOSED. R2.3 is IN PROGRESS with R2.3A/B/C automated verification PASS; R2.3C manual acceptance and R2.3D are NOT STARTED.

## 11. Closeout note

R1 remains Closed at `b9e382550e2e4abcf7a93ed6c5352322dc967668`. R2.1 and R2.2 are COMPLETE/CLOSED with the distinct acceptance provenance recorded above. R2.3 is IN PROGRESS; R2.3A/B/C have automated verification PASS, while R2.3C manual acceptance and R2.3D remain outstanding.
