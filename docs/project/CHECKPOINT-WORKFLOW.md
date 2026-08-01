# CHECKPOINT WORKFLOW — POS ENTERPRISE RETAIL V1

## 1. Metadata

- Document: `CHECKPOINT-WORKFLOW.md`.
- Purpose: mandatory operating workflow for Codex/ChatGPT sessions and project checkpoints after R0.5.
- RepositoryRoot: `D:\Projects_1\POS_Enterprise_DotNet`.
- Solution: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`.
- CapturedAtLocal: `2026-07-31T14:32:44.841+07:00`.
- ReviewedAtLocal: `2026-08-01` (Asia/Saigon, UTC+07:00).
- Live HEAD before the R0.5 closeout commit: `afdda252ce124413b9190607a96a0046cf5097e7`.
- Live origin/main before the R0.5 closeout commit: `afdda252ce124413b9190607a96a0046cf5097e7`.
- Context Pack baseline: `70523861949aeb5eefe981633db33f50bc890145`.
- Current checkpoint: R1.2 — Repository Standards implementation/closeout. R1.1 is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`; R1 is In Progress; R1.3 remains NOT STARTED.
- AppliesTo: every new Codex/ChatGPT session and every checkpoint after R0.5, subject to a narrower compatible instruction file.
- Authority: `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md`, accepted Project Memory, the active checkpoint contract and explicit user authorization.

Source-of-truth order:

1. Live repository source.
2. Live Git status, diff and history.
3. Committed Project Memory.
4. User-supplied evidence/log.
5. Old snapshot or Context Pack.

An explicit user decision may correct uncommitted Project Memory state, but source or Git must never be overwritten blindly from chat.

## 2. New-session bootstrap

Before editing code, a new session must read these files in order:

1. `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md`
2. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`
3. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
4. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md`
5. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md`
6. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md`
7. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`
8. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`
9. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CHECKPOINT-WORKFLOW.md`

After reading, establish:

- branch;
- HEAD;
- origin/main;
- ahead/behind;
- working-tree and staged state;
- current checkpoint;
- the only checkpoint authorized for work;
- entry and exit criteria;
- files allowed to read, modify and create;
- forbidden files and actions;
- required tests and gates;
- expected manual acceptance;
- commit/push boundary;
- expected next checkpoint.

If a required document is missing during R0.5 construction, report the exact missing document and continue only under the subcheckpoint identified by `CURRENT-STATE.md` and the user. Do not invent replacement content or jump to R1.

## 3. Git preflight

Run read-only:

```powershell
Set-Location "D:\Projects_1\POS_Enterprise_DotNet"

git status --short --branch
git status --porcelain=v1 -uall
git diff --check
git diff --cached --check
git diff --cached --name-only
git rev-parse --abbrev-ref HEAD
git rev-parse HEAD
git rev-parse origin/main
git rev-list --left-right --count origin/main...HEAD
git log -5 --oneline --decorate
```

Compare the output with the active checkpoint contract. Stop when:

- branch is unexpected;
- HEAD is unexpected;
- origin/main is unexpected;
- ahead/behind is unexpected;
- a conflict exists;
- a staged file is outside expected scope;
- a tracked modification/deletion/rename is outside expected scope;
- an untracked file is unexplained;
- a target already exists when creation is required;
- a target is missing when modification is required;
- Project Memory and baseline evidence conflict;
- the single authorized checkpoint cannot be identified;
- a secret or customer data location is discovered;
- required authority for database, migration, external coordination or destructive action is absent.

At a stop condition:

- do not run `git reset`;
- do not run `git clean`;
- do not run `git restore`;
- do not run `git stash`;
- do not use checkout to discard work;
- do not repair Git state automatically;
- report the raw relevant status/reference output and wait for user direction.

## 4. Scope lock

Before any edit, record:

- Checkpoint ID.
- Objective.
- In scope.
- Out of scope.
- Files allowed for read-only audit.
- Files allowed to modify.
- Files allowed to create.
- Files forbidden to modify.
- Tests to add or update.
- Gates to run.
- Manual acceptance required.
- Project Memory files to evaluate/update.
- Exact staging boundary.
- Commit/push boundary.
- Expected next checkpoint.

Only one checkpoint may be `In Progress`. Do not implement a nearby module, unrelated warning, formatting cleanup or future checkpoint. Do not start the next checkpoint before current acceptance.

## 5. Read-before-write

For every change:

1. Read the exact live file before editing.
2. Use live repository source as the primary truth.
3. Do not use source copied from old chat, ZIP or snapshot when live source differs.
4. For a long, recently changed or uncertain file, re-read the complete relevant section and its current callers before replacement.
5. Do not overwrite a long file from an old version.
6. Inspect relevant call sites, tests, DI registrations, configuration, persistence mapping and migration boundary.
7. Verify every type, member, test and path; do not infer names.
8. Check current Git state again if the session was interrupted or external changes may have occurred.

## 6. Small-batch implementation

Default to batches of approximately two or three files when practical:

- give each batch one explicit objective;
- review compile and test impact after the batch;
- keep behavior changes separate from broad refactoring unless inseparable;
- do not perform repository-wide formatting;
- do not fix unrelated warnings;
- do not add or upgrade packages outside checkpoint scope;
- do not change target framework, namespaces, DI lifetime, transaction boundary or authorization policy without explicit scope/evidence;
- do not create a migration unless the checkpoint authorizes it;
- do not open, modify or replace a real database.

Existing user changes are preserved. Never revert unrelated work.

## 7. Test-first and regression protection

For a proven bug fix or new business behavior:

1. Identify the governing invariant in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md`.
2. Add or update a regression test that proves the failure mode and expected behavior.
3. Cover recovery, idempotency, restart, concurrency and transaction behavior when the checkpoint touches checkout/payment or those boundaries are relevant.
4. Do not test only the happy path when failure/recovery is part of the contract.
5. Do not delete a test to obtain PASS.
6. Do not weaken an assertion merely to match incorrect implementation.
7. If automated proof is impractical, define explicit manual acceptance and record the verification gap in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`.
8. Treat test source as source evidence only until the test command actually succeeds.

## 8. Verification order

In a checkpoint authorized to execute runtime verification:

1. Git status review.
2. Direct file/content review.
3. `git diff --check`.
4. Restore only if the checkpoint/environment requires it.
5. Rebuild.
6. Filtered tests if the checkpoint calls for them.
7. Full automated tests.
8. Quality Gate without skipping required EF checks.
9. Manual acceptance.
10. Diff summary and scope review.
11. Project Memory update.
12. Final verification after memory update.
13. Exact staging.
14. Full staged-diff review.
15. Commit.
16. Push.
17. Clean-worktree/reference verification.

Never write PASS for a command that did not run successfully. Report which command ran directly and which ran inside `D:\Projects_1\POS_Enterprise_DotNet\scripts\Test-QualityGate.ps1`. If the Quality Gate repeats restore/build/tests, record both executions rather than collapsing them into one claim.

Canonical closeout commands and the accepted historical baseline live in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`.

## 9. Manual acceptance

Each executed manual scenario must record:

- Scenario ID.
- Preconditions.
- Exact steps.
- Expected result.
- Actual result.
- PASS or FAIL.
- Evidence/observation.
- Tester.
- Time, if known.
- Environment, including relevant hardware/software.
- Recovery result when failure is induced or observed.

Do not claim manual PASS without performing the steps. Keep automated tests, WPF smoke tests, hardware acceptance and store/pilot acceptance distinct.

Manual acceptance is mandatory when the checkpoint exit criteria require it. A historical manual PASS cannot be silently reused for changed behavior.

## 10. Project Memory update

After implementation and verification PASS, evaluate whether the checkpoint changed:

- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`

Update only affected documents:

- preserve history and stable IDs;
- create a new decision when superseding an old decision;
- close an issue only with closure evidence;
- update the baseline only with newly executed and accepted results;
- never replace original `CapturedAtLocal` merely to make an old document appear new;
- add reviewed/updated metadata when a later review needs a timestamp;
- commit memory with the checkpoint code so documentation and code identify the same commit.

`CURRENT-STATE.md` may move to the next checkpoint only when:

- current exit criteria PASS;
- required automated gates PASS;
- required manual acceptance PASS;
- Project Memory is consistent and updated;
- commit/push is complete when the checkpoint requires it;
- final Git state matches the checkpoint contract.

R0.5 is a special case governed by `DEC-017`: R0.5B/C/D/E remain uncommitted until R0.5F review/gates/acceptance PASS; local readiness is not the same as accepted, committed or closed state.

## 11. Exact staging

Before staging:

1. Write the exact expected file list.
2. Compare it with `git status --porcelain=v1 -uall`.
3. Confirm every unrelated file remains unstaged.
4. Use exact paths only.

Do not use `git add .` or `git add -A` when exact-path staging is required.

After staging run:

```powershell
git diff --cached --name-only
git diff --cached --check
git diff --cached --stat
git diff --cached
```

Review the full staged diff. Stop without committing if the staged name list differs from the expected list, if whitespace/conflict/secret/runtime artifacts appear, or if any file is not explained by the checkpoint.

For untracked files, direct content readback is required before staging; an empty `git diff` does not review them.

## 12. Commit and push gate

Commit only when:

- scope review PASS;
- required rebuild/tests/Quality Gate PASS;
- required manual acceptance PASS;
- Project Memory is consistent;
- staged diff is exactly in scope;
- no secret or customer data is present;
- no database, WAL, SHM, journal, backup, generated binary or runtime artifact is staged;
- commit message identifies the checkpoint/behavior correctly;
- the checkpoint explicitly authorizes commit.

After commit:

1. Record and verify the commit hash.
2. Verify the commit file list.
3. Push only when authorized and to the expected branch.
4. Verify HEAD and origin/main.
5. Verify ahead/behind.
6. Verify a clean worktree or only explicitly permitted retained files.

Do not amend, force-push, rebase or rewrite history without an explicit user request and an approved recovery plan.

## 13. Failure and interruption workflow

If a command fails or the session is interrupted:

1. Do not guess current state.
2. Re-run Git preflight.
3. Read `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`.
4. Read `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`.
5. Read `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`.
6. Identify all changed/staged/untracked files.
7. Identify the last gate that actually passed from retained evidence.
8. Do not use chat reports as a replacement for live source.
9. Do not resume after an unproven step.
10. Record an unresolved issue when appropriate.
11. Do not mark the checkpoint completed.

If a future gate fails, follow the failure procedure in `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`.

## 14. Database and migration safety

- Do not open, read or update a real database unless the checkpoint explicitly authorizes it.
- Do not read customer/business rows to create documentation or Context Packs.
- Do not claim a migration was applied merely because migration source or ModelSnapshot exists.
- Keep source/model/pending-model evidence separate from real database applied state.
- SQLite/EF changes are forward-only; do not modify an applied migration as the upgrade mechanism.
- Do not modify ModelSnapshot independently of a valid model/migration.
- Backup, restore and migration execution must follow the authorized checkpoint and its recovery plan.
- Do not delete database, WAL, SHM, journal or backup files to make tests pass.
- Do not copy a runtime database or sidecar into Project Memory or a Context Pack.
- Do not expose a connection string or secret in reports.

## 15. Secret and privacy safety

Project Memory, reports, logs and exports must not contain:

- real passwords;
- real password hashes;
- tokens;
- secrets;
- API keys;
- real account identifiers;
- full bank account numbers;
- customer data;
- database rows;
- backup data;
- WAL/SHM/journal content;
- machine-specific private credentials.

If a secret is detected, stop. Report its location safely without printing the value, sanitize in the authorized scope and require review before commit.

## 16. Checkpoint completion states

- **Not Started:** no authorized checkpoint implementation has begun.
- **In Progress:** work is authorized and incomplete.
- **Completed Locally:** scoped artifacts are locally complete, but later review/gates/commit may remain.
- **Pending User Review:** local result awaits explicit user acceptance.
- **Verification Failed:** a required gate or acceptance failed; checkpoint cannot advance.
- **Blocked:** progress requires missing authority, evidence, dependency or external state; the blocker is recorded.
- **Accepted:** required reviewer/user acceptance has been given.
- **Committed:** accepted scope is recorded in a verified commit.
- **Pushed:** the verified commit is present on the expected remote branch.
- **Closed:** all exit criteria, required gates, acceptance, Project Memory, commit/push and final Git-state requirements have passed.

Do not use ambiguous `Completed`. A checkpoint is not `Closed` while acceptance, commit/push or clean-state requirements remain.

## 17. Evidence requirements by claim

| Claim | Minimum evidence |
|---|---|
| Source implemented | Exact live file and type/member/call path |
| Test coverage exists | Exact test file and test method; state whether it ran |
| Automated PASS | Exact command, successful exit and result summary |
| Quality Gate PASS | Exact script command, successful exit, stage outcome and EF skip state |
| Manual PASS | Complete scenario record with actual result/environment |
| Hardware PASS | Target device/environment plus manual scenario evidence |
| Migration source current | Migration file, ModelSnapshot and model check |
| Migration applied | Authorized real database evidence; source alone is insufficient |
| Issue resolved | Closure criteria plus accepted regression/manual evidence |
| Checkpoint closed | Exit criteria, gates, acceptance, memory, commit/push and final Git evidence |

## 18. Handoff report

Every checkpoint handoff must include:

- Checkpoint ID.
- Objective.
- Entry Git state.
- Branch, HEAD, origin/main and ahead/behind.
- Files read.
- Files created.
- Files modified.
- Files explicitly not modified.
- Implementation summary.
- Tests added/modified.
- Commands run.
- Exact gate results.
- Manual acceptance.
- Known issues and verification gaps.
- Project Memory updates.
- Final Git status.
- Staged state.
- Commit and push state.
- Current checkpoint.
- Next proposed checkpoint.
- Explicit stop.

The report must also list gates not run and explain why. It must not call an accepted historical baseline a fresh run.

## 19. Reconciled R0.5/R1 boundary

For this document capture:

- R0 is Completed.
- R0.5A, R0.5B, R0.5C and R0.5D are PASS.
- R0.5E is PASS: pack `project-context-20260801T0647171300576Z`, baseline `70523861949aeb5eefe981633db33f50bc890145`, exporter exit code `0`, source coverage `501/501`, security findings `0`, excluded candidates `0`, and manifest integrity `16/16` PASS.
- R0.5F is PASS in the closeout payload: ChatGPT Context Pack and Codex repository fresh-session checks both PASS on `2026-08-01`. The Context Pack predates those checks and is not expected to contain their later results.
- Final local closeout gates on `2026-08-01` PASS: restore; Release build with 0 warnings/0 errors; Release full tests 975/975; complete Quality Gate without `-SkipEfCheck`, including dependency vulnerability and EF pending-model checks; replay-probe absence; Jenkinsfile unchanged. A first sandboxed gate attempt stopped at the network-dependent vulnerability command; the complete rerun with NuGet access passed and is the accepted gate evidence.
- R0.5 is Closed / Committed / Pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; HEAD and origin/main were aligned and the post-commit worktree was clean.
- R1.1 entry criterion from R0.5 was satisfied at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`; its repository closeout is Closed / Committed / Pushed / Git-clean at `9e96ff2409e97bd8bbb3a3455bf398a283f23ca4`, while Jenkins runtime acceptance remains attributed to `afdda252ce124413b9190607a96a0046cf5097e7`.
- R1.2 is the current checkpoint. Its reviewed implementation/closeout payload adds minimal repository standards: `.gitattributes`, `.editorconfig`, `global.json` SDK `10.0.302`, `CHANGELOG.md` and `/_audit_temp/` protection in `.gitignore`; existing deterministic/CI build metadata and fail-fast Jenkins behavior are retained. Verification PASS: restore, Release build 0 warnings/0 errors, 975/975 tests, Quality Gate exit `0`, vulnerability scan and EF pending-model check.
- R1 is In Progress. R1.2 formal repository baseline remains pending its own commit/push and post-commit Git-clean verification. R1.3 is NOT STARTED; R2–R13 remain Not Started.

The R0.5 Project Memory files were committed in the R0.5F closeout at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`. Status text must preserve the distinction between local completion, blocked verification, user acceptance and closed/pushed state.

The historical Context Pack is export-only under ignored `artifacts/project-context/`. The current R1.2 turn stops after exact staging and staged-diff review; it does not commit or push and does not begin R1.3.
