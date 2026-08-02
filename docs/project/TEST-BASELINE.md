# TEST BASELINE — POS ENTERPRISE RETAIL V1

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
