# CURRENT STATE — POS ENTERPRISE

## 1. Metadata

- Document purpose: bản tóm tắt sự thật để chuyển giao giữa các session.
- CapturedAtLocal: `2026-07-31T11:34:28.975+07:00` (Asia/Bangkok, UTC+07:00).
- ReviewedAtLocal: `2026-08-01` (Asia/Saigon, UTC+07:00).
- ReconciledAtLocal: `2026-08-01` (Asia/Saigon, UTC+07:00).
- FormalCloseoutPrepAtLocal: `2026-08-02` (Asia/Saigon, UTC+07:00).
- Live repository HEAD after the R1.3 implementation commit: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`.
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
- Live HEAD after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`
- Live origin/main after R1.3 implementation: `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`
- Ahead/behind: `0/0`

## 3. Checkpoint status

- Project: POS Enterprise Retail V1
- Current checkpoint: R1.3/R1 — Project Memory formal closeout preparation; implementation and live Jenkins verification PASS, documentation closeout commit/push and final Git-clean verification remain pending.
- Previous checkpoint: R1.2 — Repository Standards — Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`.
- Next checkpoint after R1.3 formal closeout: R2 — Platform Hardening; R2 is not authorized to start in this turn.
- R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`; R1.3 binary-name blocker is resolved by owner decision, live Jenkins build #5 passed on `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`, and R1 formal closeout remains pending only for this memory closeout commit/push and Git-clean verification.

Completed subcheckpoints in the R0.5 closeout payload:

- R0.5A — Project Memory Entry Gate — PASS.
- R0.5B — Core Project Guidance — PASS.
- R0.5C — Architecture Memory — PASS.
- R0.5D — Operating Memory — PASS.
- R0.5E — Context Exporter — PASS. Pack `project-context-20260801T0647171300576Z` was produced from baseline `70523861949aeb5eefe981633db33f50bc890145`; exporter exit code `0`, coverage `501/501` (100%), security findings `0`, excluded candidates `0`, and manifest integrity `16/16` PASS.
- R0.5F — Verification and Closeout — PASS in the closeout payload. ChatGPT Context Pack fresh-session verification PASS and Codex repository fresh-session verification PASS on `2026-08-01`.

Fresh-session provenance is intentionally later than the historical Context Pack: ChatGPT evidence is the user-supplied transcript plus manifest follow-up; Codex evidence is the independent fresh-session report against live Project Memory/Git. Neither verification claims to have committed or pushed files.

R0.5 is Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; R1.1 is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`; R1.2 is Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`. R1 remains In Progress pending formal closeout. R1.3 implementation and live Jenkins verification are PASS at `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`; no R1/R1.3 Closed claim is made before the required closeout commit/push and Git-clean verification.

Final R0.5 local verification on `2026-08-01`: `git diff --check` PASS; restore PASS; Release build PASS with 0 warnings/0 errors; Release full tests 975/975 PASS with 0 failed/0 skipped; full Quality Gate rerun PASS without `-SkipEfCheck`, including 975/975 tests, dependency vulnerability scan with no vulnerable packages, EF pending-model check PASS and Git checks PASS. Replay probe is absent and Jenkinsfile is unchanged. The first sandboxed Quality Gate invocation stopped at the vulnerability command with exit code `1` and no package result because network was unavailable; the same scan and the complete gate then passed outside the sandbox with NuGet access.

R1.2 verification on `2026-08-01`: pinned SDK `10.0.302`; restore PASS; Release build PASS with 0 warnings/0 errors; Release full tests 975/975 PASS with 0 failed/0 skipped; Quality Gate PASS with exit code `0`, vulnerability scan PASS and EF pending-model check PASS without `-SkipEfCheck`; Jenkinsfile unchanged; replay probe absent. Repository standards added/updated: `.gitattributes`, `.editorconfig`, `global.json`, `CHANGELOG.md` and `/_audit_temp/` in `.gitignore`. Existing deterministic/CI build metadata in `Directory.Build.props` was retained. Manual UI acceptance is N/A because the payload contains repository metadata and Project Memory only.

R1.3 local verification on `2026-08-01`: SDK `10.0.302`; restore PASS; Release build 0 warnings/0 errors; 975/975 tests PASS; TRX semantic validation PASS; vulnerability JSON parse and semantic validation PASS across 5 projects with no vulnerable package; Quality Gate PASS with exit code `0` and EF pending-model check PASS; native publish PASS with 50 files and 11,807,483 bytes; exact five publish-root paths PASS; PDB count `0`, denylist count `0`, and secret/customer-data scan PASS; controlled failure probe preserved exact exit code `23`; `_ci_artifacts` was safely removed.

R1.3 live Jenkins verification supplied for job `POS_ENTERPRISE_R1_1_CI`, build `#5`, URL `http://localhost:8080/job/POS_ENTERPRISE_R1_1_CI/5/`, on exact SCM revision `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`: SUCCESS; Release build 0 warnings/0 errors; tests total/executed/passed `975/975/975`, failed `0`, skipped/notExecuted `0`; Quality Gate PASS; vulnerability scan PASS; EF pending model changes none; publish win-x64 PASS; artifact contract validator PASS; archive/fingerprint PASS; Publish `50` files / `11,807,483` bytes; complete archived contract `56` files / `13,291,155` bytes; artifact ZIP SHA-256 `38adf83096b23b19b3e17ee5fc143025cbf15bcbbb12cdb688efe160414c848d`. This was supplied live evidence; the pipeline was not rerun in this preparation turn.

Manual artifact smoke test: Jenkins ZIP download, complete extraction and `POS.Enterprise.exe` launch/basic operation PASS using an existing Windows user profile. Clean Windows profile/clean-machine first-run behavior remains Not Revalidated and customer clean-install acceptance is not claimed. The artifact `appsettings.json` has blank `Payment.BankBin`, `Payment.AccountNumber`, `Payment.AccountName` and `Infrastructure.DefaultAdminPassword` values and the ZIP contains no database. Previously displayed VietQR values came from configuration persisted under the existing Windows user profile, not from the ZIP. Forgot password and change password are not implemented; first-run store/VietQR setup and account/password-management gaps remain deferred to R4 under the current roadmap.

## 4. Working-tree state

Entry state before R1.3/R1 formal closeout preparation: **Clean**. The current authorized payload is Project Memory only; source, tests, projects, packages, migrations, Jenkins implementation and scripts are not changed. `HEAD` and `origin/main` are `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`, ahead/behind is `0/0`, `_ci_artifacts` is absent, and `artifacts/project-context/` remains ignored and is not staged. After exact-path staging, the expected state is six staged Project Memory files with no unstaged or untracked files; commit and push are explicitly outside this turn.

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

- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CHECKPOINT-WORKFLOW.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`

- Production source changed: No.
- Tests changed: No.
- Migrations changed: No.
- Package/SDK/target framework changed: No.
- Repository standards changed in R1.3: No.
- CI artifact configuration and Project Memory changed: CI artifact implementation is already committed at the current HEAD; only Project Memory is changed in this preparation turn.
- Product behavior changed: No.

## 10. Prohibited next actions

- Không đổi `POS.Wpf.csproj` assembly identity; R1.3 implementation must use native `POS.Enterprise.*` output and remains uncommitted/unpushed until review.
- Không sửa production source, tests, POS.Wpf.csproj hoặc Quality Gate script; Jenkinsfile changes are limited to the R1.3 artifact contract.
- Context Pack đã tạo trong `artifacts/project-context/`, thư mục này bị ignore và không được stage.
- Không chạy database update.
- Không đọc dữ liệu database thật.
- Không commit hoặc push trong lượt này; formal closeout commit/push requires separate authorization after staged review.
- R2 remains Not Started until the R1 formal-closeout commit/push and Git-clean verification are complete.

## 11. Closeout note

R1.1 closeout is the repository baseline at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`; R1.2 closeout is at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`; current R1.3 implementation HEAD is `8bebb3ebc2b61c4de2fd8d97dc4c0b6944281bb6`. Live Jenkins build #5 and artifact smoke test PASS, with the smoke test explicitly limited to an existing profile. R1.3/R1 Project Memory formal closeout is ready for staged review and commit authorization, but R1 is not yet Closed and R2 remains Not Started until commit/push and Git-clean verification.
