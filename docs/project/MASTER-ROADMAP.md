# MASTER ROADMAP — POS ENTERPRISE RETAIL V1

## MASTER ROADMAP V2 — CURRENT EXECUTION POSITION — 2026-09-03

This section supersedes older status/order statements below; historical entries remain preserved as history.

| Stage | Current status |
| --- | --- |
| R0 VietQR Closeout | Main implementation complete; acceptance evidence retained separately |
| R1 CI/Repository | Main implementation complete; `R1.4` Release correctness implemented, verified and committed |
| R2 Platform Hardening | Complete |
| R3 Backup/Restore | Core and tests complete |
| R4.1 Store Setup | Core complete |
| R4.2 Employee Account | Automated and manual A–I PASS; committed/pushed |
| R4.3 Role/Permission | Core complete |
| R4.4 Audit Log | Core complete |
| R5.1/R5.2 Import/Export | Core complete |
| R5.3 Bulk Operations | Complete |
| R5.4 Barcode/Label | Code/test complete; hardware evidence pending |
| Receipt logo | Commit `cf22086`; preview manual PASS; PDF/reprint/K80 pending |
| R6–R13 | Not started as new implementation; follow the V2 order below |

### V2 execution order

```text
C0 close Employee/worktree
R1.4 Quality Gate Release Correctness
R5.5/R11.0 Receipt/Label/Scanner/DPI acceptance
R6A Supplier + Purchase + Goods Receipt
R6B Batch/Expiry if pilot requires it
R9A Cash Session + Cashbook + Daily Close
R10A Pilot reports
R11 Hardware/UI/Performance
R12A Installer/Support/Pilot build
R13A Core Pilot 0.9.0
R7 Customer + Loyalty
R8 Coupon + Voucher + Promotion
R10B Full reports
R12B License + Commercial Release
R13B Retail V1 1.0.0
```

Current evidence: focused compatibility tests `89/89 PASS`; post-R1.4 normal-profile official Release Quality Gate `1561/1561 PASS`; sandbox `1555 PASS / 6 FAIL / 0 SKIP / 1561` with environment-specific DPAPI/VietQR/Remembered Login failures; EF pending-model and vulnerability PASS. Employee manual A–I is PASS. R1.4 is verified and committed; R6 implementation has not started.

R1.4 is the current process closeout. After the worktree is clean, the first new code checkpoint remains `R6.1 — Supplier Master`; do not start R6 in this checkpoint.

### R1.4 — Quality Gate Release Correctness

- Implemented and verified on `2026-09-03`; official build, tests and EF pending-model all select Release, with fail-fast preserved and no final PASS after a failed step.
- The pre-R1.4 Debug-based official result and the separate Release result remain historical evidence; the post-change normal-profile official result is `1561/1561 PASS`.
- No R6 implementation has started. Push verification is handled by the closeout procedure.

## Historical snapshot — Employee Account & Security — 2026-09-02

The following dated snapshot is retained for history and is superseded by the C0 closeout and Roadmap V2 position above.

- Implemented account state separation, one create CTA, modal temporary-password reset, first-login mandatory change return-to-Login behavior, persisted consecutive failed-login tracking, 5-attempt/15-minute lockout, admin lifecycle guards, ID-based selection restoration, feedback and no-hard-delete lifecycle.
- Added forward-only `20260902074505_EmployeeSecurityAuditHardening` for `LastFailedLoginAtUtc` and `LoginFailed`; no migration was applied to the manual database.
- Focused verification is `22/22 PASS`; full sandbox Release is `1510 PASS / 6 FAIL / 0 SKIP`, with the six known DPAPI environmental failures separated. A fresh normal-profile Quality Gate and user physical acceptance are still required; the earlier `1514/1514` evidence is not inherited.
- Next checkpoint: user acceptance and normal-profile Gate for Employee Account, then Employee hardening follow-up. R6+ remains `HOLD`.

## Receipt logo rendering — IMPLEMENTED / MANUAL PRINT FOLLOW-UP — 2026-09-02

- New receipts capture the persisted managed logo as bounded self-contained PNG content; preview, print and reprint consume the same immutable receipt snapshot, while legacy/no-logo/corrupt snapshots retain the `PE` fallback.
- User accepted the latest Release preview alignment. Focused receipt verification is `55/55 PASS`, dedicated logo tests are `10/10 PASS`; full sandbox is `1541 PASS / 6 FAIL / 0 SKIP` with the six known secure-storage profile failures separated. A fresh normal-profile Gate and final PDF/physical-printer/reprint acceptance remain pending.
- This is a separate logical closeout from Store Settings success feedback and Employee/Audit work; no migration or historical receipt rewrite was introduced.

## Bulk UX V2 + Numeric Entry closeout — 2026-09-02

- Bulk UX V2 and Numeric Entry are `ACCEPTED / COMMITTED LOCALLY` after user physical acceptance and the user-run normal interactive Quality Gate: `1514/1514 PASS`, `0 failed`, `0 skipped`, exit code `0`.
- The closeout preserves Bulk selection/preview/commit/audit/persistence invariants and Product Editor stock semantics, including conditional negative opening stock. No migration, Activity Log, DPAPI, VietQR, Remembered Login or unrelated numeric screen was changed.
- The six historical sandbox secure-storage failures remain a resolved environmental test-host limitation and are not used to overwrite the normal-profile Gate result. IME, undo/redo and complete DPI matrix remain pending where not evidenced.
- The next authorized workstream is Employee Account creation and management. Keep its implementation unstaged/uncommitted for user acceptance; do not begin R6 and do not push.

## Activity Log stabilization closeout — 2026-09-02

- Activity Log audit semantics and presentation correction is implemented: Bulk price/category/status/minimum-stock operations now have distinct persisted action codes, strict legacy read compatibility, friendly product targets and Vietnamese result labels. The writer root cause was confirmed and employee audit actions remain unchanged.
- The Activity Log UI now uses a responsive filter card, compact action/business presentation, ellipsis/tooltips, result badges, grouped detail sections and a collapsed technical metadata section. No Shell/sidebar, Employee Account, Label Printing or unrelated inventory/export flow was changed.
- Focused changed-source verification is `12/12 PASS`; fresh Release build is `0 warning / 0 error`; full sandbox Release is `1450 PASS / 6 FAIL / 0 SKIP / 1456`, with only the historical DPAPI host limitation failing. The user-run normal interactive Quality Gate for this current source passed at `1456/1456`, and the user confirmed the Activity Log interface works well.
- R5.3 audit semantic correctness is accepted and committed locally. R5.4 physical printer/scanner/calibration/DPI/PDF acceptance remains deferred. R6+ remains `HOLD`; next product work is Bulk UX closeout, followed by Employee Account creation/management.

## Latest checkpoint position — R5.3–R5.4 closeout — 2026-09-02

- R5.3 is `ACCEPTED / COMMITTED LOCALLY` for engineering, persistence and manual core acceptance. User evidence covers partial selection → Bulk, “Ngừng bán” preview/status, and isolated SQLite persistence/readback proving only selected products change while stock and InventoryMovement do not. Audit correctness remains open pending Activity Log semantic/action tracing.
- R5.4 is `ACCEPTED / COMMITTED LOCALLY` for implementation and manual UI acceptance. The user confirmed automatic preview, compact footer, consolidated validation, size adjustment and test print inside the calibration group. Physical printer, scanner readback, real-device calibration, full 100%/125%/150% DPI and unperformed Print to PDF save/overwrite/cancel scenarios remain deferred.
- User-run official `scripts/Test-QualityGate.ps1` without `-SkipEfCheck` passed under a normal interactive Windows profile on 2026-09-02: `1455/1455 PASS`, `0 failed`, `0 skipped`, exit `0`; restore, build (`0/0`), vulnerability, EF pending-model and Git checks passed. The Codex sandbox Gate was not rerun after this evidence; historical six DPAPI failures remain documented as a resolved environmental test-host limitation.
- R6+ remains `HOLD` by Product Owner decision. Post-R5 stabilization queue: Activity Log semantic/action mapping, Activity Log UX/layout, Employee Account creation/management, then other user-review issues. No new roadmap ID is introduced.

## Historical checkpoint record — R5.4 UX closeout — 2026-09-01

- R5.4 label UX closeout is `IMPLEMENTED / AUTOMATED CONTROL VERIFIED / MANUAL RECHECK PENDING`: footer is reduced to `Đóng`/`In N tem`, preview is automatic with cancellable 300 ms debounce, invalid state is empty/no pagination/`In tem`, validation is consolidated, refresh is compact, and calibration/test print is retained in a collapsed group.
- Evidence: fresh Release solution/WPF/test builds with `UseAppHost=true` `0/0`; focused production R5.4 + Shell/Product regression `28/28 PASS`; full Release `1449 PASS / 6 FAIL / 0 SKIP`; vulnerability, EF pending-model and diff-check PASS. Official Gate without `-SkipEfCheck` is NOT PASS because it stops at the same six DPAPI secure-storage failures.
- Historical R5.3 status before normal-profile closeout: implemented/persistence-verified with its existing user evidence and sandbox DPAPI limitation. The accepted normal-profile Gate and current R5.3/R5.4 status are recorded in the closeout section above. R6, Audit Log and Employee Account work remain HOLD; no new roadmap ID is introduced.

## Historical checkpoint record — R5.4 UI hotfix — 2026-09-01

- Production `LabelPrintWindow` đã được hotfix ở đúng control boundary: DataContext/command bindings, RequestClose/Close/Esc lifecycle, preset projection, refresh feedback, preview rebuild feedback, `In N tem`, printer disappearance/error handling và footer/scroll layout. `R5.4` hiện `IMPLEMENTED / AUTOMATED VERIFIED / MANUAL RECHECK PENDING`.
- Bằng chứng lượt này: fresh Release solution/WPF/test build `0/0` với `UseAppHost=true`; production-control + R5.4/Shell/Product focused `26/26 PASS`; full Release `1447 PASS / 6 FAIL / 0 SKIP`; vulnerability riêng PASS; EF pending-model PASS; `git diff --check` PASS. Sáu lỗi là secure-storage DPAPI cũ; Official Quality Gate không `-SkipEfCheck` dừng ở test stage với exit `1`, nên Gate NOT PASS.
- R5.3 vẫn được ghi là hoàn thiện triển khai và persistence/readback với user evidence chính; blocker DPAPI giữ nguyên, cần xác minh trên normal interactive Windows profile. Không sửa DPAPI, profile, credential, VietQR hoặc Remembered Login trong hotfix này.
- R6 trở đi HOLD theo quyết định ngày 01/09/2026. Sau R5.4, dừng chờ người dùng nghiệm thu lại năm thao tác UI/in thử; lượt ổn định/correction sau đó ưu tiên Nhật ký hoạt động, tạo/quản lý Tài khoản nhân viên và lỗi UI/contract. Không đặt các mục này Completed và không tạo roadmap ID mới.

## Historical checkpoint record — R5 Bulk verification — 2026-08-31

- User evidence now accepts the previously failing individual-row selection → Bulk opening path and the supplied Ngừng bán preview/readback display only. This does not close all R5 manual acceptance criteria.
- Historical R5.3 Bulk evidence: isolated SQLite persist/readback for B/C versus untouched A across price, category, active state and minimum-stock. The sandbox full Release was `1430 PASS / 6 FAIL / 0 SKIP` because of CurrentUser DPAPI tests; the later normal-profile closeout passed `1455/1455`.
- At that historical point, R5.3 remained open for final acceptance and R5.4 was not started. The current closeout above supersedes that position for R5.3–R5.4; remaining hardware/DPI and Activity Log stabilization boundaries are explicitly retained.
- Exact next permitted checkpoint remains **R5.3 — Bulk Operations** (continue closeout only): bounded current-page selection, preview/confirmation for price/category/status/minimum-stock, permission/concurrency/transaction/audit/no-stock-write invariants, required focused/full/Gate evidence and approved manual acceptance. Prerequisites are the current source/diff and user-selected scope retained, with the secure-storage Gate blocker resolved or explicitly dispositioned; completion requires all R5.3 exit evidence and no claim of PASS for unrun physical/DPI criteria.

## Historical checkpoint record — R5 Bulk row selection — 2026-08-31

- Individual production row checkbox changes now publish immediately to the Shell selection source; partial selection opens Bulk at the existing minimum of two and retains only checked IDs.
- Focused verification is complete; full Release/Quality Gate remain not-pass due six secure-storage tests, and physical/manual acceptance remains required.

## Historical checkpoint record — R5 Bulk selection/preview binding — 2026-08-31

- Bulk selection threshold and production status binding are implemented and focused-verified. No-op preview is non-writable at the UI command guard; no stock or unrelated workflow changed.
- Full Release and Official Quality Gate remain not-pass because of six DPAPI secure-storage failures; physical UI/DPI and manual persistence acceptance remain required.

## Historical checkpoint record — R5 Bulk reference snapshot — 2026-08-31

- Bulk now opens with a read-only reference snapshot for every selected product and reuses the same table for current values and validated preview results. Export, History, returns and R5.4 remain unchanged.
- Automated focused verification is complete; final Release/Quality Gate and manual desktop/DPI acceptance remain required before checkpoint closeout.

## Historical checkpoint record — R5 hotfix — 2026-08-31

- Authorized implementation is complete for Bulk form progressive disclosure and export result timing. R5.1/R5.2/R5.3 remain manual-pending/partial as previously recorded; R5.4 has not started.
- Next evidence is physical manual acceptance of Bulk at normal/maximized/DPI sizes and all Save dialog outcomes, followed by full Release and Official Quality Gate.

## Latest checkpoint position — R5.1–R5.3 hotfix A/B/C — 2026-08-30

- Hotfix đã nối chọn nhiều từ Product & Inventory UI tới dialog preview/confirm thật bằng tập checkbox theo trang, bổ sung header select-all và hướng dẫn trạng thái chưa chọn; không fallback sang `SelectedProduct`, không thay đổi stock/movement và giữ nguyên bốn bulk operations hiện có.
- History chung hiện có một lối mở duy nhất tại `Hàng hóa → Lịch sử kho`, không kế thừa selection sản phẩm; mục trùng trong menu sản phẩm đã bỏ. Export/template dialog trả trạng thái typed sau writer và caller hiển thị feedback thành công/hủy/lỗi đúng cửa sổ khởi phát.
- Focused A/B/C `29/29 PASS` Debug và Release; full normal-host Release `1434/1434 PASS`, failed/skipped `0/0`; Release build `0/0`; Official Quality Gate đầy đủ PASS. Physical click/UIA/DPI, save-dialog/file-open và manual bulk interaction vẫn pending, vì vậy không đóng manual acceptance.
- Giữ R5.1A/B CLOSED/PRESERVED, R5.1C/D và toàn R5.1 theo bằng chứng manual thực tế; R5.2 automated verified/manual pending; R5.3 vẫn PARTIAL do label-printing pipeline production chưa tồn tại. R4.2–R4.4 CLOSED/PRESERVED, R5.4 chưa bắt đầu.

## Latest checkpoint position — R5.1–R5.3 nghiệm thu hotfix — 2026-08-30

- Hotfix giới hạn đã nối lại ba entry point export/template qua dialog có resource đúng phạm vi, sửa text-editing state của nhập số trả hàng và tách command single-product khỏi tập chọn nhiều trong Product & Inventory. Không thay đổi transaction, stock movement, audit, RBAC, importer, archive hoặc schema.
- Bằng chứng tự động hiện tại: focused Debug `58/58 PASS`, focused Release `185/185 PASS`, full normal-host Release `1432/1432 PASS`, failed/skipped `0/0`, Release build `0/0`; Official Quality Gate không dùng `-SkipEfCheck` PASS với vulnerability, local tool restore, EF pending-model và Git checks. Physical click/UIA/DPI và file-open/manual end-to-end vẫn pending.
- R5.1 vẫn IN PROGRESS/manual acceptance pending; R5.2 IMPLEMENTED/AUTOMATED VERIFIED/MANUAL PENDING; R5.3 PARTIAL/AUTOMATED VERIFIED vì production label-printing pipeline chưa có. R4.2–R4.4 CLOSED/PRESERVED, R5.4 chưa bắt đầu và Development Freeze ACTIVE ngoài ngoại lệ hotfix.

## Latest checkpoint position — Inventory History type/date correction — 2026-08-30

- The authorized History follow-up is complete at the read/presentation boundary: `CustomerReturn` is labeled and filterable independently from `Refund`, and the approved “30 ngày gần nhất” contract is 30 calendar days including today (`Today.AddDays(-29)` through today).
- Debug/Release focused verification is `116/116 PASS`; no write workflow, enum values, schema, or other checkpoint was changed. R5.1 remains in manual acceptance; R5.2 is the next authorized checkpoint and is not started.

## Latest checkpoint position — Selection and global Inventory History hotfix — 2026-08-30

- This authorized R5.1 manual-acceptance hotfix adds an explicit `× Bỏ chọn` action to the existing Product selection rail and decouples the single `Kho & lưu trữ → Lịch sử kho` route from the currently selected Product row. No stock write, archive behavior, inventory-history query/service, RBAC boundary, persistent manual profile or unrelated module changed.
- The clear-selection action updates the real selection and command state without reload or mutation. The global history route passes no product criterion, opens the default general history view, and does not change selection in the Shell.
- Automated evidence is `28/28 PASS` focused in Debug and Release, `286/286 PASS` relevant Release, normal-host full Release `1413/1413 PASS`, failed/skipped `0/0`, Release build `0/0`, and Official Quality Gate PASS including vulnerability, EF pending-model and Git checks. Physical WPF click/UIA/DPI acceptance remains manual pending.
- R5.1 remains in manual acceptance and is not closed by this hotfix. R5.1A–R5.1D and R4.2–R4.4 remain preserved, R5.2 is NOT STARTED, and Development Freeze remains ACTIVE outside this authorized exception.

## Latest checkpoint position — Inventory History navigation/filter-card hotfix — 2026-08-30

- This limited exception corrects the final Inventory History presentation/navigation defects while preserving the accepted database-side search and query semantics. There is one primary Shell route through `Kho & lưu trữ → Lịch sử kho`; the duplicate sidebar command is removed, and the screen uses one light local filter card with a safe `Tìm`/clear-search layout plus colocated reset/refresh actions.
- Focused history/navigation coverage is `22/22 PASS` in Debug and Release, relevant Release coverage is `284/284 PASS`, normal-host full Release is `1410/1410 PASS`, failed/skipped `0/0`, and Release build is `0/0`. Official Quality Gate passed with vulnerability, EF pending-model and Git checks PASS.
- Physical WPF screenshot/UIA/DPI acceptance remains manual pending. R5.1 remains in manual acceptance and is not closed by this hotfix; R5.1A–R5.1D and R4.2–R4.4 are preserved, R5.2 is NOT STARTED, and Development Freeze remains ACTIVE outside this authorized exception.

## Latest checkpoint position — Inventory History redesign — 2026-08-30

- This authorized Inventory History hotfix keeps the completed search/query behavior while replacing the confusing hidden-product-scope presentation with one visible product criterion and a general Shell entry. The screen now uses a compact filter sidebar, business-language summary cards, a spacious history table and on-demand detail without changing inventory writes, ledger, audit, RBAC or other modules.
- Focused coverage is `21/21 PASS` in Debug and Release, relevant Release coverage is `90/90 PASS`, normal-host full Release is `1409/1409 PASS`, failed/skipped `0/0`, and Release build is `0/0`. The six sandbox DPAPI failures passed in normal-host verification; physical WPF/UIA/DPI acceptance remains pending.
- R5.1 remains in manual acceptance and is not closed by this hotfix. R5.1A–R5.1D and R4.2–R4.4 remain preserved; R5.2 is NOT STARTED. Development Freeze remains ACTIVE outside this authorized Inventory History exception.

## Latest checkpoint position — Inventory History / Adjustment / Category UX hotfix — 2026-08-30

- Authorized hotfix is limited to presentation/read-query behavior for Inventory History, empty quantity UX for manual stock adjustment, and category name ordering/display-order form removal. Product archiving/storage, Import Wizard and later checkpoints are unchanged.
- Inventory History uses the existing direct search pipeline; category search/list order is Vietnamese-name-first before paging with stable Id tie-breaker. `DisplayOrder` remains persisted and is still passed internally to existing create/update contracts.
- Focused `44/44` Debug and Release, relevant Release `284/284`, normal-host full Release `1407/1407`, failed/skipped `0/0`; physical manual acceptance remains pending. R5.1 remains in manual acceptance, R5.2 is NOT STARTED.

## Latest checkpoint position — 2026-08-30 (Inventory History UX/query hotfix — AUTOMATED VERIFIED / MANUAL PENDING)

- This authorized hotfix stays within the existing Inventory History capability. Product name/code/barcode search now reaches the history query directly; filters, paging, selection, loading/error/empty states and visible navigation scope share one typed ViewModel pipeline.
- The old product lookup/search-two-step flow is removed from the main surface. `Xóa bộ lọc` clears all user and navigation conditions, while `Làm mới` preserves them. No inventory write, movement creation/deletion, audit mutation, permission boundary or Import Wizard behavior changed.
- Focused history coverage is `16/16 PASS` in Debug and Release, relevant Release is `260/260 PASS`, normal-host full Release is `1399/1399 PASS`, failed/skipped `0/0`, Release build `0/0`; the six sandbox DPAPI failures passed on the normal host. Official Quality Gate is recorded after this documentation update.
- Manual WPF click/UIA/DPI acceptance remains pending. R5.1 remains IN PROGRESS, R5.2 is NOT STARTED, R5.1A–R5.1D and R4.2–R4.4 remain preserved, and Development Freeze remains ACTIVE outside the authorized hotfix.

## Latest checkpoint position — 2026-08-30 (R5.1C/D UX remediation — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING)

- The store-user remediation keeps R5.1C/D within the authorized Product import checkpoint. The wizard now uses a three-stage, progressive-disclosure flow with one contextual primary action, Vietnamese business wording, compact canonical-header auto-mapping and distinct reference-data guidance. Full validation, 11-field preservation, duplicate semantics and transaction confirmation remain intact.
- Regression evidence is `27/27 PASS` focused in Debug/Release, `250/250 PASS` relevant Release, full normal-host Release `1389/1389 PASS`, failed/skipped `0/0`, Release build `0/0`, and official Quality Gate without `-SkipEfCheck` is PASS, including vulnerability, EF pending-model and Git checks. The six known sandbox DPAPI tests remain normal-host verified; no security boundary changed.
- Physical WPF click/UIA/DPI and manual fixture click-through remain pending due to the environment limitation. R5.1C/D are IMPLEMENTED/AUTOMATED VERIFIED/MANUAL PENDING, R5.1 remains IN PROGRESS, R5.2 is NOT STARTED, R5.1A/B and R4.2–R4.4 remain CLOSED/PRESERVED, and Development Freeze remains ACTIVE outside R5.1.

## Latest checkpoint position — 2026-08-30 (R5.1C/D — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING)

- R5.1C connects the existing Product & Inventory route to a real WPF import wizard. It reuses the R5.1A parser/schema and R5.1B service for file selection, explicit worksheet/mapping, full validation, bounded preview, duplicate-policy confirmation, cancellation and typed transaction results. R5.1D hardening covers the stale-preview, re-entrant, rollback, result-display and parser/security integration paths without adding a package, migration, parallel model or UI-side mutation.
- The current automated evidence is `25/25 PASS` for the import-focused Debug/Release coverage, `248/248 PASS` for relevant Release regressions, normal-host full Release `1387/1387 PASS`, failed/skipped `0/0`, Release build `0/0`, and official Quality Gate PASS including vulnerability and EF pending-model checks. Isolated startup reached `InitialSetupWindowReady` with an owned TEMP database.
- R5.1C/D implementation is complete, but physical WPF click/UIA/DPI acceptance remains pending because no inspectable top-level HWND is exposed in this environment. R5.1 remains IN PROGRESS; R5.1A/B and R4.2/R4.3/R4.4 are CLOSED/PRESERVED; R5.2 is NOT STARTED; Development Freeze remains ACTIVE outside R5.1.

## Latest checkpoint position — 2026-08-29 (R5.1B — Transactional Product Import — CLOSED / COMMITTED / PUSHED)

- R5.1B is closed on the Product CSV/XLSX foundation. The Application/Infrastructure use case revalidates the typed preview snapshot and imports the exact 11 production-backed fields atomically without introducing a parallel Product model, UI wizard, package or migration.
- Duplicate policy is explicit per batch: `Skip` preserves existing rows, `Update` preserves Product identity/history and never overwrites current stock through `Tồn đầu`, and `Error` rolls back/rejects duplicates. Category resolution is active-ID-only; UnitName remains text; opening stock uses the existing domain and append-only inventory movement workflow.
- Normal-host full Release and Quality Gate evidence is `1383/1383 PASS`, failed/skipped `0/0`, build `0/0`, vulnerability PASS and EF pending-model PASS. R5.1A is CLOSED/PRESERVED, R5.1 is IN PROGRESS, R5.1C is NEXT, R4.2/R4.3/R4.4 remain CLOSED/PRESERVED and Development Freeze is ACTIVE outside this checkpoint.

## Latest checkpoint position — 2026-08-29 (R5.1A — Product CSV/Excel secure preview foundation — CLOSED / COMMITTED / PUSHED)

- R5.1A establishes the read-only Product CSV/XLSX preview foundation from the live Product model. The exact 11-field catalog covers code, barcode, name, category, unit, sale/cost price, opening/minimum stock, status and notes; no parallel product schema or database mutation was introduced.
- Secure parsing, typed validation, bounded preview and summary behavior are complete. The foundation rejects unsupported/signature-mismatched files, formulas, external links/macros and unsafe/unbounded content; R5.1B is the next checkpoint for the authorized import workflow/UI.
- Normal-host full Release and Quality Gate evidence is `1374/1374 PASS`, failed/skipped `0/0`, build `0/0`, vulnerability PASS and EF pending-model PASS. R4.2/R4.3/R4.4 remain CLOSED/PRESERVED; R5.1 is IN PROGRESS, R5.1A is CLOSED, R5.1B is NEXT and Development Freeze is ACTIVE outside this checkpoint.

## Latest checkpoint position — 2026-08-29 (post-R4.4 handover-freeze hotfix — CLOSED / COMMITTED / PUSHED)

- The narrowly scoped hotfix repairs the proven missing-Audit-service composition registration, adds a module-specific sanitized load boundary, and bounds Employee master/detail summary and identity/status layout. R4.3/R4.4 behavior and security contracts remain preserved.
- Development freeze is active for machine handover. **Exact next checkpoint:** R5.1 Product CSV/Excel Import — NOT STARTED.

## Latest checkpoint position — 2026-08-28 (R4.2 final manual UI correction — CLOSED / COMMITTED / PUSHED)

- The final R4.2 manual correction is CLOSED against live `93da2045ced774e8bd02e34777bfc145aac682db`. It is limited to Employee empty/create states, Role filter selected presentation and verification of the existing Sales readiness route.
- Employee modernization, dark grouped Shell/sidebar, Store Setup UX and Inventory navigation hotfix remain preserved. No schema/migration or R4.3/R4.4 work is introduced.
- **Exact next checkpoint:** R4.3 Role and Permission Management — NOT STARTED. R4.4 is also NOT STARTED.

## Latest checkpoint position — 2026-08-28 (post-modernization manual UX hotfix)

- The focused manual UX hotfix is CLOSED / COMMITTED / PUSHED within R4.2. It repairs Store Readiness first-click behavior, optional warning classification, actionable readiness dialog, Employee filter initialization, filtered/true empty semantics, create transition, reduced-height empty layout, clear icon and long-code presentation.
- Employee modernization, Store Setup UX, dark grouped Shell/sidebar and Inventory hotfix are preserved. Isolated settings remain scenario-owned and non-persistent across separate temporary invocations; visual UIA limitations remain explicit.
- **Exact next checkpoint:** R4.3 Role and Permission Management — NOT STARTED.

## Latest checkpoint position — 2026-08-28 (R4.2 Employee UI modernization)

- R4.2 Employee and Account Management UI modernization is CLOSED / COMMITTED / PUSHED. The approved visual boundary is applied to Employee content only: master-detail administration layout, aggregate summaries, modern search/filter/list, deterministic selection/no-selection behavior, read-only-first tabbed detail and responsive WPF presentation.
- Functional/security behavior remains under the existing R4.2 Application boundary. Official Quality Gate passed `1350/1350`; Release build passed with `0` warnings/errors; EF pending-model and vulnerability checks passed. The explicit isolated/UIA visual limitation is recorded in Project Memory and does not become a false acceptance claim.
- Store Setup UX, dark grouped Shell/sidebar and Inventory navigation hotfix are preserved. Store Readiness remains a separate manual observation and is not claimed fixed.
- **Exact next checkpoint:** R4.3 Role and Permission Management — NOT STARTED.

## Latest checkpoint position — 2026-08-26 (R4.2 closeout)

- R4.2 Employee and Account Management is CLOSED / COMMITTED / PUSHED after additive migration, application authorization/security services, forced-password-change flow, WPF Employee UI, isolated evidence and final Quality Gate PASS.
- Final R4.2 baseline is `1336/1336` PASS, Release build `0` warnings/errors, vulnerability scan PASS and EF pending-model PASS. R4.1 visual polish remains deferred.
- **Exact next checkpoint:** R4.3 Role and Permission Management — READY TO START. R4.3 has not started.

## Latest checkpoint position — 2026-08-26

- R3.3 Restore Wizard and Rollback is CLOSED / COMMITTED / PUSHED at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`; RST14 retains its explicit combined-evidence limitation.
- R3.4 Disaster Recovery Drill is PASS and CLOSED on `2026-08-26`: real production backup → genuinely new database → external-worker restore/restart → WPF sign-in → exact Orders/stock/receipt comparison → SQLite integrity. DR1–DR9 and canonical safety PASS.
- R3 is CLOSED. The R3.4 prerequisite blocker for pilot is removed. R4.1 Store Setup and its isolated-startup hotfix are CLOSED / COMMITTED / PUSHED; R4.2 Employee and Account UI is the exact next checkpoint and READY TO START.
- R4.1 hotfix verification is PASS: focused regression `11/11`, full Release suite `1327/1327`, build `0` warnings/errors, official Quality Gate, vulnerability scan and EF pending-model check PASS.

## Latest checkpoint position — 2026-08-23

- R2.4 is Closed / Committed / Pushed at `ff91ab515507666a3fb3b01fc28e7ad0f6241d59`.
- R3.1 Manual Backup is Closed / Committed / Pushed at product checkpoint `1cbabbbb8928a29c520897c849c405f6ad6e16de` with formal acceptance PASS: official Quality Gate `1183/1183` and MB1 independently verified. A later docs-only synchronization commit records this post-push state.
- **Historical checkpoint position on 2026-08-23:** R3.2 Automatic Backup and Retention was Closed locally; R3.3 Restore Wizard was NOT STARTED and was the exact next checkpoint. This dated statement is superseded by the current position above.

## Governance

- Roadmap gốc: 29/07/2026.
- Project Memory foundation được bổ sung ngày 31/07/2026.
- Đây là một Master Roadmap duy nhất và là nguồn sự thật cho thứ tự checkpoint.
- Không thay đổi phạm vi tùy hứng.
- Mọi thay đổi roadmap sau khi `DECISIONS.md` tồn tại phải được ghi lại ở đó.

## Mục tiêu sản phẩm

Bản cài đầu tiên đủ an toàn cho một cửa hàng bán lẻ, một chi nhánh, hoạt động offline trên Windows.

### Phạm vi Retail V1

- Sản phẩm, danh mục và tồn kho.
- Barcode và in tem.
- Nhân viên, tài khoản và phân quyền.
- Bán tiền mặt và VietQR.
- Giữ đơn, giảm giá và promotion.
- Khách hàng, thành viên và tích điểm.
- Trả hàng và chứng từ.
- Backup/restore.
- Chốt ngày.
- Báo cáo doanh thu, hàng hóa và lợi nhuận.
- Import/export.
- Cấu hình cửa hàng và thiết bị.
- Installer, license, Jenkins và update process.

### Ngoài phạm vi Retail V1

- Kế toán Nợ/Có đầy đủ.
- Hóa đơn điện tử thuế.
- Đa chi nhánh và cloud sync.
- Nhà hàng có bàn, bếp hoặc KDS.
- API ngân hàng tự động đối soát.
- Mobile app.

## Trạng thái tổng quan

| Stage | Status |
|---|---|
| R0 | Completed |
| R0.5 | Closed / Committed / Pushed |
| R1 | Closed |
| R2 | Closed |
| R3 | In Progress — R3.1 Closed / Committed / Pushed |
| R4 | Not Started |
| R5 | R5.3–R5.4 Accepted / Committed Locally; other R5 acceptance remains scoped separately |
| R6 | HOLD — Product Owner decision |
| R7 | HOLD — Product Owner decision |
| R8 | HOLD — Product Owner decision |
| R9 | HOLD — Product Owner decision |
| R10 | HOLD — Product Owner decision |
| R11 | HOLD — Product Owner decision |
| R12 | HOLD — Product Owner decision |
| R13 | HOLD — Product Owner decision |

Không đánh dấu stage hoàn thành chỉ vì một phần tính năng đã tồn tại. Controlled Discount đã có nhưng R8 vẫn Not Started vì còn line discount, coupon, voucher và Promotion Engine. Return đã có nhưng R9 vẫn Not Started vì còn immutable return receipt, Cashbook Lite và Daily Close. Receipt printing đã có nhưng R11 vẫn Not Started vì hardware acceptance thực tế chưa hoàn thành. R1 đã Closed tại `b9e382550e2e4abcf7a93ed6c5352322dc967668`; R2 đang In Progress overall, R2.1 đã COMPLETE và R2.2 là checkpoint tiếp theo nhưng chưa bắt đầu.

Manual acceptance của stage tương lai chỉ là tiêu chí phải đạt; không được ghi PASS trước khi chạy thật. R1 formal closeout is Closed at `b9e382550e2e4abcf7a93ed6c5352322dc967668`. R2.1 Single-instance Application is COMPLETE with 992/992 full Release tests, final Quality Gate/vulnerability/EF/security checks and manual Tests A/B/C/D PASS. R2.2 SQLite Busy/Locked UX is COMPLETE/CLOSED: Test A Manual PASS; Tests B/C/D NOT MANUALLY RUN and covered by equivalent deterministic automated acceptance PASS. R2.3 Logging and Support Bundle is COMPLETE: R2.3A/B/C automated verification, R2.3D manual acceptance M01–M09, local read-only ZIP inspection and final 1070/1070 Quality Gate all PASS. R2.4 is IN PROGRESS: R2.4A, R2.4B and R2.4C are COMPLETED; R2.4D is the next checkpoint and remains NOT STARTED.

## R0 — VIETQR RUNTIME CLOSEOUT

- **Status:** Completed.
- **Objective:** đóng an toàn runtime VietQR và recovery của checkout.
- **Scope/checkpoints:** R0.1 — Schema Compatibility; R0.2 — Startup Ordering; R0.3 — PaymentIntent Recovery; R0.4 — Manual Acceptance.
- **Out of scope:** các stage Retail V1 sau R0.
- **Dependencies:** baseline trước R0.
- **Entry criteria:** source và schema R0 sẵn sàng cho closeout.
- **Exit criteria:** schema compatibility, startup ordering, durable recovery, automated gates và manual acceptance đều PASS.
- **Manual acceptance:** PASS theo authoritative R0 closeout.

Evidence hoàn thành:

- Commit: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Commit subject: `feat(checkout): add controlled discounts and durable VietQR recovery`.
- Manual acceptance PASS.
- Build PASS — 0 warnings, 0 errors.
- Full suite 969/969 PASS — 0 failed, 0 skipped.
- Quality Gate PASS, có EF pending-model check.
- Commit/push thành công.
- Git sạch và `main` đồng bộ `origin/main` tại closeout.

## R0.5 — PROJECT MEMORY FOUNDATION

- **Status:** Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; HEAD and origin/main were aligned and the post-commit worktree was clean.
- **Objective:** tạo bộ nhớ kỹ thuật bền vững trong repository, không phụ thuộc lịch sử ChatGPT/Codex.
- **Scope/checkpoints:**
  - R0.5A — Project Memory Entry Gate: PASS.
  - R0.5B — Core Project Guidance: PASS.
  - R0.5C — Architecture Memory: PASS.
  - R0.5D — Operating Memory: PASS.
  - R0.5E — Context Exporter: PASS. Pack `project-context-20260801T0647171300576Z`, baseline `70523861949aeb5eefe981633db33f50bc890145`, exporter exit code `0`, coverage `501/501`, security findings `0`, excluded candidates `0`, manifest integrity `16/16` PASS.
  - R0.5F — Verification and Closeout: PASS in the closeout payload. ChatGPT and Codex individual fresh-session acceptance checks both PASS on `2026-08-01`; commit/push and final Git-clean confirmation remain closeout mechanics rather than unperformed fresh-session evidence.
- **Out of scope:** production features và R1–R13.
- **Dependencies:** R0 Completed.
- **Entry criteria:** R0 closeout PASS và R0.5A entry gate PASS.
- **Exit criteria:** toàn bộ A–F PASS, Project Memory đã commit/push, Git sạch và `CURRENT-STATE.md` chuyển next checkpoint sang R1.
- **Manual acceptance:** session Codex mới và ChatGPT Context Pack mới xác định đúng repository, trạng thái, quy tắc và next checkpoint; chỉ đánh PASS tại R0.5F.

Reconciled verification evidence:

- Context Pack baseline: `70523861949aeb5eefe981633db33f50bc890145`.
- Live HEAD/origin/main before the R0.5 closeout commit: `afdda252ce124413b9190607a96a0046cf5097e7`.
- Sequential build: PASS — 0 warnings, 0 errors.
- Full automated tests: 975/975 PASS.
- Quality Gate: PASS, including EF pending-model check, exit code 0.
- VietQR manual acceptance: PASS; Presented state persisted before the QR dialog.
- R0.5E exporter pack details and both R0.5F fresh-session PASS results are recorded above with distinct provenance; the historical pack was not rewritten after the tests.
- Final R0.5 local verification on `2026-08-01`: restore PASS; Release build 0 warnings/0 errors; Release full tests 975/975 PASS; Quality Gate PASS without `-SkipEfCheck`, with vulnerability and EF pending-model checks PASS; replay probe absent; Jenkinsfile unchanged. Exact staging and staged-diff review are authorized. Commit/push remain outside the current turn.

## R1 — JENKINS CI VÀ CHUẨN REPOSITORY

- **Status:** In Progress by reconciliation; R1 is not completed.
- **Objective:** mỗi push lên `main` tự động chạy toàn bộ gate đáng tin cậy.
- **Scope/checkpoints:** R1.1 Jenkins CI Pipeline: Checkout → repository validation → restore → build → filtered/full tests → Quality Gate → vulnerability scan → EF pending-model check → reports/artifacts; R1.2 Repository Standards: line endings, versioning, build metadata, changelog convention, không cho CI xanh giả khi test fail; R1.3 CI Artifacts: test results, build logs, gate logs, vulnerability report và published binaries thử nghiệm.
- **Out of scope:** platform hardening và product features R2–R13.
- **Dependencies:** R0.5.
- **Entry criteria:** R0.5 closeout PASS và next checkpoint là R1.
- **Exit criteria:** mỗi push lên `main` tự động chạy toàn bộ gate và xuất đúng artifacts.
- **Manual acceptance:** quan sát một pipeline thật xử lý thành công và chứng minh test/gate fail làm pipeline fail.

Checkpoint state after reconciliation:

- R1.1 — Jenkins CI Pipeline runtime E2E: PASS from supplied Jenkins evidence at `afdda252ce124413b9190607a96a0046cf5097e7`; SCM checkout, Windows agent, .NET SDK `10.0.302`, Release build/test, Quality Gate, vulnerability scan, EF pending-model check, intentional failure propagation and final normal rerun were verified. Repository closeout is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`.
- R1.2 — Repository Standards: Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. Added minimal text/binary and LF policy (`.gitattributes`), editor policy (`.editorconfig`), SDK pin (`global.json` at `10.0.302`), changelog convention (`CHANGELOG.md`) and `_audit_temp` ignore protection. Existing deterministic/CI build metadata was retained; no product version was invented or changed. Restore, Release build, full tests and Quality Gate all PASS.
- R1.3 — CI Artifacts: implementation/local verification PASS and live Jenkins verification PASS on job `POS_ENTERPRISE_R1_1_CI` build `#5` at `http://localhost:8080/job/POS_ENTERPRISE_R1_1_CI/5/`, exact SCM revision `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`. Build/tests/gates/artifact validation/archive all passed; the complete archived contract was `56` files / `13,291,155` bytes and the artifact ZIP SHA-256 was `38adf83096b23b19b3e17ee5fc143025cbf15bcbbb12cdb688efe160414c848d`. Download/extraction/application smoke test PASS used an existing Windows profile only; clean-profile first-run remains Not Revalidated. R1 formal closeout is Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`.

R1.3 owner-approved artifact contract:

- Artifact root is `D:\Projects_1\POS_Enterprise_DotNet\_ci_artifacts` (`_ci_artifacts/` in Jenkins), generated-only, ignored by Git and safely cleaned only at the exact root before preparation.
- Test results: one valid TRX/XML per full-test project at `_ci_artifacts/test-results/<ProjectName>.trx`; the current solution has only `POS.Architecture.Tests`, so the required file is `POS.Architecture.Tests.trx`. Full-test selection remains unchanged, and a runner-started test failure must still produce TRX.
- Build logs: non-empty plain text, console-visible and exit-code preserving at `_ci_artifacts/logs/restore.log`, `build-release.log` and `publish-win-x64.log`.
- Quality Gate log: `_ci_artifacts/logs/quality-gate.log`, console-visible, non-empty and exit-code preserving; Quality Gate runs without `-SkipEfCheck`.
- Vulnerability report: valid SDK-supported JSON from the full-solution, transitive-inclusive vulnerability command at `_ci_artifacts/reports/vulnerability/vulnerabilities.json`; existing vulnerability enforcement remains active.
- Experimental publish: producer `src/POS.Wpf/POS.Wpf.csproj`, native application identity `POS.Enterprise` with root files `POS.Enterprise.exe`, `POS.Enterprise.dll`, `POS.Enterprise.deps.json` and `POS.Enterprise.runtimeconfig.json`; Release/net10.0-windows, win-x64, framework-dependent, not single-file/R2R/signed, output at `_ci_artifacts/publish/POS.Wpf/win-x64/`. The validator checks those four native identity files plus `appsettings.json` directly at the publish root, then applies the approved allowlist and denylist. CI does not override `AssemblyName`/`TargetName` or copy/rename binaries.
- Publication uses Declarative Pipeline `post { always { ... } }` and `archiveArtifacts`: only `*.exe` and `*.dll` use `fingerprint: true`; every `*.json` uses `fingerprint: false`, including `POS.Enterprise.deps.json`, `POS.Enterprise.runtimeconfig.json` and `appsettings.json`. TRX, logs, Quality Gate log and vulnerability JSON also use `fingerprint: false`. Binary and JSON archive calls use `onlyIfSuccessful: false`; `allowEmptyArchive: true` only protects post/always publication when the pipeline fails before publish. Successful pipelines still require the validator to prove all five artifact groups are present and valid.
- Retention keeps metadata/console for 30 builds and archived artifacts for 10 builds via the equivalent `buildDiscarder(logRotator(...))` policy; no new plugin is introduced.
- All safe artifacts are archived on success or failure when created; missing artifacts from stages not reached are not fabricated. Publication or validation failure fails the build and never changes a prior failure to success.
- Databases, backups, secrets, credentials, customer data, source archives, workspace-wide files, coverage/JUnit/HTML reports and installers are excluded.
- Formal R1.3 closeout required a successful live Jenkins run on the exact pushed implementation commit with all five artifact groups validated; that live requirement is PASS at build #5. The Project Memory formal-closeout commit/push and final Git-clean verification are complete at `b9e382550e2e4abcf7a93ed6c5352322dc967668`.

## R2 — PLATFORM HARDENING

- **Status:** Closed / Committed / Pushed at `ff91ab515507666a3fb3b01fc28e7ad0f6241d59`.
- **Objective:** harden runtime Windows/SQLite và khả năng support.
- **Scope/checkpoints:** R2.1 Single-instance Application; R2.2 SQLite Busy/Locked UX; R2.3 Logging and Support Bundle; R2.4 Disk Space and Database Growth. Gồm mutex theo database/store identity; phiên thứ hai có thông báo rõ; không kill process mù; phân biệt busy, locked, disk full và corruption; retry có giới hạn, không retry checkout mù; không làm mất cart; log rotation/sanitize; Support Bundle an toàn; disk-space monitoring; không tự xóa dữ liệu bán hàng.
- **Out of scope:** backup/restore workflow R3.
- **Dependencies:** R1.
- **Entry criteria:** R1 PASS and formally Closed / Committed / Pushed / Git-clean at `b9e382550e2e4abcf7a93ed6c5352322dc967668`.
- **Exit criteria:** hoàn thành R2.1–R2.4 và đáp ứng toàn bộ guardrail nêu trên.
- **Completed checkpoints:** R2.1 Single-instance Application, R2.2 SQLite Busy/Locked UX, R2.3 Logging and Support Bundle, and R2.4 Disk Space and Database Growth are complete. R2.4D and its M1–M6 manual evidence closed with R2 at `ff91ab515507666a3fb3b01fc28e7ad0f6241d59`.
- **Manual acceptance:** thử phiên thứ hai, busy/locked, disk full/corruption presentation, cart preservation, log/support bundle và cảnh báo disk space.

## R3 — BACKUP VÀ RESTORE

- **Status:** Closed — R3.1–R3.4 complete; R3.3 is Closed / Committed / Pushed at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`; R3.4 DR1–DR9 PASS on `2026-08-26`.
- **Objective:** bảo vệ dữ liệu bằng backup/restore có verify và recovery.
- **Scope/checkpoints:** R3.1 Manual Backup; R3.2 Automatic Backup and Retention; R3.3 Restore Wizard; R3.4 Disaster Recovery Drill.
- **Completed checkpoint:** R3.1 Manual Backup — Closed / Committed / Pushed at product checkpoint `1cbabbbb8928a29c520897c849c405f6ad6e16de`; formal acceptance PASS with official Quality Gate `1183/1183` and MB1 independently verified. The product verifies integrity/schema, positive byte length and SHA-256 before success; the independent arbitrary-artifact SQLite probe remains NOT RUN and is not represented as PASS. The later synchronization commit is docs-only.
- **Completed checkpoint:** R3.2 Automatic Backup and Retention — daily 24-hour policy, verified single-flight automatic backup, GFS 7 latest / 4 weekly / 3 monthly, secondary 2 GiB quota warning semantics, and mixed human + user-approved machine-assisted acceptance PASS.
- **Completed checkpoint:** R3.3 Restore Wizard and Rollback — Closed / Committed / Pushed at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`; RST14 remains accurately classified as combined-evidence acceptance.
- **Completed checkpoint:** R3.4 Disaster Recovery Drill — production backup, genuine database switch, external-worker restore/restart, authenticated WPF Shell, exact Orders/stock/receipt recovery, SQLite integrity and canonical safety PASS. The R3.4 prerequisite blocker for pilot is removed.
- **Out of scope:** store/employee management R4.
- **Dependencies:** R2.
- **Entry criteria:** R2 PASS.
- **Exit criteria:** backup verify trước khi báo thành công; backup trước migration theo policy; restore verify integrity/schema; backup database hiện tại trước restore; đóng DbContext trước khi thay database; restore lỗi có rollback; disaster recovery drill PASS.
- **Manual acceptance:** thực hiện backup, restore và disaster recovery drill trên dữ liệu kiểm thử; drill là blocker trước pilot.

## R4 — STORE SETUP VÀ EMPLOYEE MANAGEMENT

- **Status:** R4.1 Closed / Committed / Pushed; R4.2 Closed / Committed / Pushed; R4.3–R4.4 Not Started.
- **Exact next checkpoint:** R4.3 Role and Permission Management — READY TO START.
- **Objective:** vận hành cấu hình cửa hàng, nhân viên, account, role và audit an toàn.
- **Scope/checkpoints:** R4.1 Store Setup; R4.2 Employee and Account UI; R4.3 Role and Permission Management; R4.4 Audit Log UI. Gồm typed/validated store configuration; printer/scanner/cash drawer/VietQR/backup; quản lý nhân viên; reset password, lock/unlock; role/permission matrix; audit thay đổi quyền.
- **Out of scope:** product data operations R5.
- **Dependencies:** R3.
- **Entry criteria:** R3 PASS.
- **Exit criteria:** R4.1–R4.4 hoàn thành; không xóa lịch sử nhân viên đã có giao dịch; không log password/secret.
- **Manual acceptance:** cấu hình store/device, quản lý account/role và kiểm tra audit/permission bằng các vai trò thực tế.

R4.1 closeout record:

- Typed Store Setup, validation, atomic persistence, managed logo, readiness, receipt/VietQR/printing/backup integration and Administrator-only WPF navigation are implemented.
- R4.1 isolated-startup hotfix: the exact pre-startup `ILoggerFactory`/`ILogger<T>` DI failure was repaired by centralizing logging-safe pre-startup Infrastructure composition and launching the compiled executable from the isolated launcher. The focused regression and real isolated smoke reached `LoginWindowReady` with no startup-failure log; this execution desktop exposed no inspectable Win32 top-level window, so no visual UIA claim is made.
- Final hotfix Release baseline is `1327/1327` PASS with 0 failed, 0 skipped; official Quality Gate passed without `-SkipEfCheck`. R4.1 remains CLOSED and R4.2 remains READY TO START.
- Database-location activation remains explicitly restart-required; physical scanner/cash-drawer actuation and physical printer success remain deferred until hardware acceptance is available.

R4.2 closeout record:

- Employee and optional account separation, centralized typed capabilities, BCrypt-compatible password policy, reset/force-change, lockout state, last-login display, optimistic concurrency, sanitized security audit and final-Administrator safeguards are implemented.
- Existing Users and history are preserved by additive migration/backfill. Deactivation disables linked accounts; reactivation does not silently unlock or reactivate accounts. Role effective permissions are displayed read-only; dedicated role/permission administration is R4.3.
- The real IsolatedTest Release process reached `ShellWindowReady` with scenario-owned paths. External UIA could not observe a top-level window in the execution desktop, so acceptance is recorded as combined production-service/persistence/PID/source-contract evidence rather than an unsupported visual claim.
- Final R4.2 baseline is `1336/1336` PASS with 0 failed/skipped, Release/Quality Gate build 0/0, vulnerability and EF checks PASS. R4.2 is CLOSED; R4.3 is READY TO START.

## R5 — PRODUCT DATA OPERATIONS

- **Status:** Not Started.
- **Objective:** nhập, xuất, bulk-update và in barcode/price label đáng tin cậy.
- **Scope/checkpoints:** R5.1 CSV/Excel Product Import; R5.2 Export; R5.3 Bulk Operations; R5.4 Barcode and Price Label Printing. Có mapping, validation, preview, lỗi từng dòng, duplicate policy, transaction/rollback theo batch, template, bulk price/category/status/minimum stock và kích thước tem theo mm.
- **Out of scope:** supplier, purchase, batch và expiry R6.
- **Dependencies:** R4.
- **Entry criteria:** R4 PASS.
- **Exit criteria:** R5.1–R5.4 hoàn thành và barcode in ra scan lại được.
- **Manual acceptance:** import/export round-trip, batch rollback, bulk operations, preview/in tem và scan barcode thật.

## R6 — SUPPLIER, PURCHASE, BATCH VÀ EXPIRY

- **Status:** Not Started.
- **Objective:** quản lý nguồn nhập, lot và hạn dùng với lịch sử bất biến.
- **Scope/checkpoints:** R6.1 Supplier; R6.2 Purchase Receipt; R6.3 Batch/Lot; R6.4 Expiry Management; R6.5 Lot Stocktake. Gồm supplier/history, phiếu nhập atomic với InventoryMovement, lot code/quantity/cost/manufacture-expiry date, expiry warning, FEFO, policy hàng hết hạn và điều chỉnh có reason/audit.
- **Out of scope:** customer/loyalty R7.
- **Dependencies:** R5.
- **Entry criteria:** R5 PASS.
- **Exit criteria:** R6.1–R6.5 hoàn thành; không sửa lịch sử lot sau giao dịch.
- **Manual acceptance:** nhập hàng theo lot, theo dõi/điều chỉnh tồn lot, FEFO và cảnh báo/policy expiry.

## R7 — CUSTOMER VÀ LOYALTY

- **Status:** Not Started.
- **Objective:** customer, membership và loyalty ledger idempotent, auditable.
- **Scope/checkpoints:** R7.1 Customer Master; R7.2 Attach Customer to Order; R7.3 Loyalty Ledger; R7.4 Membership Tiers; R7.5 Point Redemption; R7.6 Basic CRM; R7.7 Customer Debt chỉ sau khi Customer và Daily Close ổn định. Gồm search/history, order customer snapshot, Earn/Redeem/Adjustment/Expire/Return reversal, tier policy và audit.
- **Out of scope:** pricing/promotion R8; Customer Debt trước khi điều kiện ổn định được đáp ứng.
- **Dependencies:** R6.
- **Entry criteria:** R6 PASS.
- **Exit criteria:** R7.1–R7.6 hoàn thành; R7.7 chỉ theo điều kiện; không dùng điểm hai lần và không dùng trường Points sửa tự do làm nguồn lịch sử.
- **Manual acceptance:** attach customer, earn/redeem/reversal, tier/audit và thử lại thao tác để chứng minh idempotency.

## R8 — PRICING, COUPON VÀ PROMOTION ENGINE

- **Status:** Not Started.
- **Objective:** pricing adjustments có quyền, policy, audit và snapshot bất biến.
- **Scope/checkpoints:** R8.1 Line Discount; R8.2 Coupon; R8.3 Voucher; R8.4 Promotion Engine V1; R8.5 Promotion Priority/Stacking Policy; R8.6 Promotion Audit. Gồm line discount amount/percentage với actor/reason/permission; coupon conditions/limits; voucher lifecycle chống dùng hai lần; promotions theo product/category/order/time/customer; stacking/priority và immutable snapshot.
- **Out of scope:** documents, cashbook và daily close R9.
- **Dependencies:** R7.
- **Entry criteria:** R7 PASS.
- **Exit criteria:** R8.1–R8.6 hoàn thành; reprint/return không đọc rule live; total không âm.
- **Manual acceptance:** áp dụng từng loại discount/coupon/voucher/promotion, kiểm tra permission, limits, stacking, audit, reprint/return và chống double-use.

## R9 — DOCUMENTS, CASHBOOK VÀ DAILY CLOSE

- **Status:** Not Started.
- **Objective:** chứng từ trả hàng bất biến và kiểm soát tiền mặt/chốt ngày mức Retail V1.
- **Scope/checkpoints:** R9.1 Immutable Return Receipt; R9.2 Cashbook Lite; R9.3 Daily Close Lite; R9.4 Reopen Policy. Gồm preview/print/reprint return receipt; cash sales/refunds/other receipt/payment; theoretical/counted cash; daily revenue/discount/refund/payment methods; difference/reason/actor; reopen cần permission/audit.
- **Out of scope:** kế toán đầy đủ.
- **Dependencies:** R8.
- **Entry criteria:** R8 PASS.
- **Exit criteria:** R9.1–R9.4 hoàn thành và dữ liệu close/reopen có permission/audit đúng.
- **Manual acceptance:** return receipt print/reprint, đối chiếu theoretical/counted cash, close và reopen theo quyền.

## R10 — REPORTS V1 VÀ ACCOUNTING EXPORT

- **Status:** Not Started.
- **Objective:** báo cáo Retail V1 đúng lịch sử và export dùng được.
- **Scope/checkpoints:** R10.0 Historical Data Audit; R10.1 Today Dashboard; R10.2 Revenue Reports; R10.3 Product Reports; R10.4 Gross Profit Reports; R10.5 Operations Reports; R10.6 Customer Reports; R10.7 CSV/Excel/PDF and Accounting Export.
- **Out of scope:** full General Ledger.
- **Dependencies:** R9.
- **Entry criteria:** R9 PASS.
- **Exit criteria:** audit UnitCostSnapshot trước gross profit; không dùng `Product.CostPrice` live cho lịch sử; không gọi gross profit là net profit; return/discount allocation đúng; hoàn thành R10.0–R10.7.
- **Manual acceptance:** đối chiếu báo cáo với bộ giao dịch kiểm thử có sale, discount và return; kiểm tra CSV/Excel/PDF/accounting export.

## R11 — STORE VÀ HARDWARE ACCEPTANCE

- **Status:** Not Started.
- **Objective:** chứng minh hệ thống vận hành ổn định với hardware, display và tải mục tiêu.
- **Scope/checkpoints:** R11.1 Scanner; R11.2 K80 Printer; R11.3 Label Printer; R11.4 Cash Drawer; R11.5 Display/DPI Acceptance; R11.6 Performance Acceptance.
- **Out of scope:** release engineering, installer và license R12.
- **Dependencies:** R10.
- **Entry criteria:** R10 PASS và có hardware/môi trường acceptance.
- **Exit criteria:** toàn bộ R11.1–R11.6 đạt manual acceptance.
- **Manual acceptance:** scanner liên tục và barcode lỗi/trùng/hết hàng; K80 tiếng Việt, discount, VietQR, return, offline/hết giấy; lỗi in không checkout trùng; label calibration và scan được; cash drawer failure không làm checkout fail; 1366×768, 1920×1080, 125%, 150%; 10.000 sản phẩm và 100.000 Orders; startup/search/report/database growth/memory.

## R12 — RELEASE ENGINEERING, INSTALLER VÀ LICENSE

- **Status:** Not Started.
- **Objective:** tạo release x64 có installer, license và update/rollback an toàn.
- **Scope/checkpoints:** R12.1 Publish; R12.2 Installer; R12.3 Jenkins Release; R12.4 License V1; R12.5 Code Signing; R12.6 Update Policy.
- **Out of scope:** pilot R13.
- **Dependencies:** R11.
- **Entry criteria:** R11 PASS.
- **Exit criteria:** version metadata; không kèm development config/test database; data ngoài installation directory; uninstall không xóa customer database; upgrade giữ dữ liệu và backup trước migration; pipeline có tests/gate/hash/smoke test; offline license không phá/khóa dữ liệu; license hết hạn vẫn xem/export theo policy; update có backup/migration/verify/rollback.
- **Manual acceptance:** fresh install, uninstall giữ data, upgrade/migration/rollback, license states, signature/hash và release smoke test.

## R13 — PILOT CỬA HÀNG ĐẦU TIÊN

- **Status:** Not Started.
- **Objective:** hoàn tất pilot cửa hàng và phát hành 1.0 dựa trên evidence vận hành.
- **Scope/checkpoints:** R13.1 Pilot Preparation; R13.2 Pilot Acceptance; R13.3 Pilot Monitoring; R13.4 Release 1.0.
- **Out of scope:** các hạng mục ngoài Retail V1.
- **Dependencies:** R12.
- **Entry criteria:** R12 PASS và pilot đã chuẩn bị.
- **Exit criteria:** không mất dữ liệu, duplicate Order, sai tồn hoặc sai tiền; backup/restore drill PASS; Daily Close khớp; scanner/printer ổn định; migration upgrade PASS; installer upgrade PASS; pilot đạt thời gian vận hành đã thống nhất.
- **Manual acceptance:** Cash; VietQR; Discount; Coupon; Customer/Loyalty; Held Sale; Return; Print/reprint; Import/export; Daily Close; Reports; Backup/restore; Power loss/restart; Database locked; Low disk.

Release 1.0 chỉ được thực hiện sau khi toàn bộ exit criteria R13 đạt.

## PRODUCT MILESTONES

### Mốc A — Pilot Candidate 0.9.0

- R0–R5.
- Backup/restore.
- Store Setup/Employee UI.
- Import/export.
- Barcode label.
- Return Receipt.
- Daily Close Lite.
- Basic revenue report.
- Hardware acceptance.
- Trial installer.

Ước lượng gốc sau R0: **5–8 tuần**. Đây là estimate, không phải cam kết.

### Mốc B — Retail V1 1.0.0

- Supplier/Purchase.
- Basic Batch/Expiry.
- Customer/Loyalty.
- Line Discount.
- Coupon/Voucher.
- Promotion Engine V1.
- Full V1 reports.
- Accounting export.
- License.
- Release pipeline.
- Pilot PASS.

Ước lượng gốc sau R0: **10–16 tuần**. Đây là estimate, không phải cam kết.
- Latest checkpoint position — 2026-08-30 (R5.2 — Export — IMPLEMENTED / AUTOMATED VERIFIED / MANUAL PENDING)

- R5.2 export now provides five approved read reports (Product catalog, current stock, low stock, archived products and Inventory History) plus the schema-derived blank Product import template in CSV/XLSX. It preserves production filtering, permission, ordering and CustomerReturn semantics; no database mutation, migration or new package was added.
- File output is typed and bounded: UTF-8 BOM CSV and formula-safe, macro-free XLSX are written through an atomic temporary sibling path. Cost data is omitted when the existing `ManageProducts` capability is absent. Manual save/open and visual acceptance remain pending; R5.2 is not marked closed until the checkpoint workflow accepts those criteria.
- R5.1A/B remain CLOSED/PRESERVED, R5.1C/D remain manual-acceptance pending, R5.3 is the active next implementation block, R5.4 and R5.2-adjacent features outside this scope are not started, and Development Freeze remains active outside authorized R5 work.
- Latest checkpoint position — 2026-08-30 (R5.3 — Bulk Operations — partial / automated verified)

- R5.3 non-printing product operations are implemented for an explicit, bounded selection on the current Product page: preview then confirmation for prices, category, status and minimum-stock threshold. The service rechecks ManageProducts, optimistic timestamps, domain invariants and transaction consistency; no stock movement is created for these operations. Focused UI/service verification is `6/6 PASS` in Debug and Release; final normal-host full verification is `1426/1426 PASS`.
- Bulk label printing remains a real dependency, not a placeholder: source tracing found receipt printing but no label renderer/printer/settings pipeline to reuse. R5.3 therefore remains open; R5.4 is not started and no fake print action was added. Final manual WPF and hardware acceptance are pending.
