# MASTER ROADMAP — POS ENTERPRISE RETAIL V1

## Latest checkpoint position — 2026-08-26

- R3.3 Restore Wizard and Rollback is CLOSED / COMMITTED / PUSHED at `e1f6d0dc3401b91c8674f32e18c6a2fb29ccdd49`; RST14 retains its explicit combined-evidence limitation.
- R3.4 Disaster Recovery Drill is PASS and CLOSED on `2026-08-26`: real production backup → genuinely new database → external-worker restore/restart → WPF sign-in → exact Orders/stock/receipt comparison → SQLite integrity. DR1–DR9 and canonical safety PASS.
- R3 is CLOSED. The R3.4 prerequisite blocker for pilot is removed. R4 remains NOT STARTED and R4.1 Store Setup is the exact next checkpoint.
- Final verification is PASS: focused recovery `142/142`, build `0` warnings/errors, full official Quality Gate `1317/1317`, vulnerability PASS and EF pending-model PASS.

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
| R5 | Not Started |
| R6 | Not Started |
| R7 | Not Started |
| R8 | Not Started |
| R9 | Not Started |
| R10 | Not Started |
| R11 | Not Started |
| R12 | Not Started |
| R13 | Not Started |

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

- **Status:** R4.1 Closed / Committed / Pushed; R4.2–R4.4 Not Started.
- **Exact next checkpoint:** R4.2 Employee and Account UI — NOT STARTED.
- **Objective:** vận hành cấu hình cửa hàng, nhân viên, account, role và audit an toàn.
- **Scope/checkpoints:** R4.1 Store Setup; R4.2 Employee and Account UI; R4.3 Role and Permission Management; R4.4 Audit Log UI. Gồm typed/validated store configuration; printer/scanner/cash drawer/VietQR/backup; quản lý nhân viên; reset password, lock/unlock; role/permission matrix; audit thay đổi quyền.
- **Out of scope:** product data operations R5.
- **Dependencies:** R3.
- **Entry criteria:** R3 PASS.
- **Exit criteria:** R4.1–R4.4 hoàn thành; không xóa lịch sử nhân viên đã có giao dịch; không log password/secret.
- **Manual acceptance:** cấu hình store/device, quản lý account/role và kiểm tra audit/permission bằng các vai trò thực tế.

R4.1 closeout record:

- Typed Store Setup, validation, atomic persistence, managed logo, readiness, receipt/VietQR/printing/backup integration and Administrator-only WPF navigation are implemented.
- Full Release test baseline is `1326/1326` PASS with 0 failed, 0 skipped; official Quality Gate passed without `-SkipEfCheck`.
- Database-location activation remains explicitly restart-required; physical scanner/cash-drawer actuation and physical printer success remain deferred until hardware acceptance is available.

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
