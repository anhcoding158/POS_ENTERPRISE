# CURRENT STATE — POS ENTERPRISE

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
- Current checkpoint: R2.1 — Single-instance Application — COMPLETE.
- Previous checkpoint: R1.2 — Repository Standards — Closed / Committed / Pushed / Git-clean at `7490e87a2b5381f6e030ef0948b5b6be0dd2e77d`.
- Next checkpoint: R2.2 — SQLite Busy/Locked UX — Not Started.
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
- R2.1 is COMPLETE. R2.2 — SQLite Busy/Locked UX is next and remains Not Started.

## 11. Closeout note

R1 remains Closed at `b9e382550e2e4abcf7a93ed6c5352322dc967668`. R2.1 is COMPLETE with source, automated gates and manual Tests A/B/C/D recorded above. R2.2 — SQLite Busy/Locked UX is next and has not started.
