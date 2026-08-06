# ARCHITECTURE DECISIONS — POS ENTERPRISE RETAIL V1

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
- R2: In Progress overall. R2.1 is COMPLETE under `DEC-023`; R2.2 is COMPLETE/CLOSED under `DEC-024`; R2.3–R2.4 and R3–R13 remain Not Started.
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
