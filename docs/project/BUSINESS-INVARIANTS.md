# BUSINESS INVARIANTS — POS ENTERPRISE RETAIL V1

## 1. Metadata và evidence boundary

- CapturedAtLocal: `2026-07-31T11:49:51.828+07:00`.
- EvidenceNormalizationReviewedAtLocal: `2026-07-31T12:24:15.171+07:00`.
- Base HEAD: `e330b616b277bde3bed2a46e71fe511cb4531ce8`.
- Current live HEAD reviewed during R0.5E: `70523861949aeb5eefe981633db33f50bc890145`.
- Evidence base files are cited individually by absolute path in every invariant; repository-wide source/test folders are not used as enforcing evidence.
- Không đọc database thật, database rows hoặc `__EFMigrationsHistory`.
- Không chạy tests trong R0.5C. “Covered by accepted test evidence” nghĩa là direct test source tồn tại trên accepted R0 baseline, không phải test vừa chạy.

Status:

- **Enforced:** source có runtime/schema guard rõ và evidence phù hợp.
- **Partially Enforced:** guard chưa bao phủ mọi path hoặc còn thiếu một lớp.
- **Policy Only:** quy tắc Project Memory/roadmap, runtime chưa enforce đầy đủ.
- **Not Verified:** chưa đủ evidence kết luận.

ID là ổn định; không renumber tùy tiện.

## A. Money and totals

### INV-MONEY-001 — Tiền persisted dùng integer

- **Statement:** Giá, subtotal, discount, total, cash, change và refund của checkout phải được biểu diễn bằng số nguyên `long`/SQLite `INTEGER`.
- **Rationale:** Tránh sai số floating-point.
- **Scope/trigger:** Tính và persist sale/return/receipt.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Order.cs` — `Order` money properties; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderItem.cs` — `OrderItem.UnitCostPrice`, `UnitSalePrice`, `LineDiscountAmount`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs` — `SalesDiscountCalculator.Resolve`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderConfiguration.cs` — money properties configured as `INTEGER`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderItemConfiguration.cs` — item money columns; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderDiscountSnapshotConfiguration.cs` — requested/resolved values as `INTEGER`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Discount_input_implementation_does_not_use_floating_point`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs` — `ReceiptSnapshotSerializationTests.Configured_snapshot_must_round_trip_unicode_and_vnd`.
- **Failure/recovery:** checked arithmetic/domain validation trả failure trước commit.
- **Status:** Enforced.
- **Gap/revisit:** Currency khác VND thuộc future scope.

### INV-MONEY-002 — Total không âm và tuân phương trình

- **Statement:** `TotalAmount = Subtotal - DiscountAmount` và không được âm.
- **Rationale:** Ngăn sai tiền và discount vượt giá trị bán.
- **Scope/trigger:** Checkout và held-sale snapshot.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Order.cs` — `Order.ApplySalesDiscount`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs` — `SalesDiscountCalculator.Resolve`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\HeldSale.cs` — constructor total equation.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderConfiguration.cs` — subtotal/discount/total check constraints; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\HeldSaleConfiguration.cs` — `CK_HeldSales_DiscountAmount`, `CK_HeldSales_TotalEquation`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Invalid_values_reason_and_zero_total_are_rejected`.
- **Failure/recovery:** Domain exception/Result failure, không persist.
- **Status:** Enforced.
- **Gap/revisit:** Promotion stacking thuộc R8.

## B. Stock and inventory

### INV-STOCK-001 — Checkout phải re-read và kiểm tra tồn

- **Statement:** Checkout không được tin stock từ UI; phải đọc Product tracked và gọi policy fulfillment trước khi bán.
- **Rationale:** Ngăn oversell do cart stale.
- **Scope/trigger:** Mọi checkout có tracked inventory.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — `CheckoutService.CheckoutAsync`/`CheckoutCoreAsync` product re-read path; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Product.cs` — `Product.CanFulfill`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\ProductConfiguration.cs` invokes `ConfigureAuditableEntity`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs` — required shadow GUID property configured by `ConfigureAuditableEntity` as concurrency token.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Stale_checkout_must_not_oversell_product`.
- **Failure/recovery:** Failure trước commit; transaction rollback.
- **Status:** Enforced.
- **Gap/revisit:** Multi-store stock không thuộc Retail V1.

### INV-STOCK-002 — Sale stock và movement atomic với Order

- **Statement:** Stock deduction, Sale `InventoryMovement`, Order và receipt snapshot phải commit hoặc rollback cùng checkout transaction.
- **Rationale:** Không để có sale mà sai tồn hoặc movement.
- **Scope/trigger:** Checkout process.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — checkout transaction block, `IUnitOfWork.SaveChangesAsync`, `IApplicationTransaction.CommitAsync`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfUnitOfWork.cs` — `EfUnitOfWork.BeginTransactionAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\InventoryMovementConfiguration.cs` — Product/User relationships; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderConfiguration.cs` — Order relationships.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Failure_after_save_must_rollback_everything`, `Mixed_cart_failure_must_not_partially_sell_valid_product`.
- **Failure/recovery:** Explicit/Dispose rollback; journal giữ recoverable state khi phù hợp.
- **Status:** Enforced.
- **Gap/revisit:** Không có gap trong audited checkout path.

### INV-STOCK-003 — Negative-stock policy phải được tôn trọng

- **Statement:** Product không cho negative stock không được giảm dưới 0; Product cho phép negative stock có thể giảm dưới 0.
- **Rationale:** Chính sách tồn phải nhất quán theo Product.
- **Scope/trigger:** Sale và inventory stock-out.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Product.cs` — `Product.CanFulfill`, `IncreaseStock`, `DecreaseStock`, `ReconcileStock`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\InventoryService.cs` — `InventoryService.AdjustAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — stock validation/mutation path in `CheckoutCoreAsync`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\ProductConfiguration.cs` — `CK_Products_NegativeStock_Rule`, `CK_Products_AllowNegativeStock_RequiresTracking`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\InventoryIntegrationTests.cs` — `InventoryIntegrationTests.Stock_out_must_not_allow_negative_stock_when_disabled`, `Stock_out_may_create_negative_stock_when_enabled`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Negative_stock_policy_must_be_honoured`.
- **Failure/recovery:** Result failure; transaction rollback.
- **Status:** Enforced.
- **Gap/revisit:** Expiry/lot policy thuộc R6.

## C. Checkout and order

### INV-CHECKOUT-001 — Một client request không tạo duplicate Order

- **Statement:** Cùng `ClientRequestId` và cùng canonical payload phải replay một kết quả; payload khác phải conflict.
- **Rationale:** Chống double sale do retry/restart.
- **Scope/trigger:** Prepare/process/replay checkout.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutRequestCanonicalizer.cs` — `CheckoutRequestCanonicalizer.Canonicalize`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — prepare/process/replay paths; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\CheckoutRequestJournal.cs` — lifecycle members.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs` — `UX_CheckoutRequestJournals_ClientRequestId`, filtered unique `OrderId`, and invocation of `ConfigureAuditableEntity`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\AuditableEntityConfigurationExtensions.cs` — shadow GUID concurrency token.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Duplicate_prepare_same_payload_returns_existing_and_different_payload_conflicts`, `Concurrent_same_request_commits_exactly_one_business_result`.
- **Failure/recovery:** Replay persisted result hoặc explicit idempotency conflict.
- **Status:** Enforced.
- **Gap/revisit:** Cross-device sync ngoài Retail V1.

### INV-CHECKOUT-002 — UI success chỉ sau commit

- **Statement:** Checkout không được trả success business trước khi EF transaction commit.
- **Rationale:** UI không được xóa cart/in receipt cho giao dịch chưa bền vững.
- **Scope/trigger:** Cash và VietQR checkout.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — checkout process calls `IUnitOfWork.SaveChangesAsync`, then `IApplicationTransaction.CommitAsync`, then returns success.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\EfApplicationTransaction.cs` — `CommitAsync`, `RollbackAsync`, rollback-on-dispose.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` — `CheckoutReliabilityIntegrationTests.Cancellation_before_commit_must_rollback_everything`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Confirmed_intent_checkout_completes_atomically`.
- **Failure/recovery:** Rollback và trả failure; journal/intents hỗ trợ retry.
- **Status:** Enforced.
- **Gap/revisit:** UI crash đúng sau commit được xử lý qua recovery, hardware print độc lập.

### INV-CHECKOUT-003 — Prepared journal không mutate business

- **Statement:** Prepare checkout chỉ persist recoverable journal/quote, không tạo Order, trừ stock, movement hoặc receipt.
- **Rationale:** Chuẩn bị/retry không được tạo side effect bán hàng.
- **Scope/trigger:** `PrepareAsync`.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — prepare path.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\CheckoutRequestJournalConfiguration.cs` — `CK_CheckoutRequestJournals_StateShape`, request/quote JSON checks.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs` — `CheckoutIdempotencyApplicationTests.Prepare_creates_recoverable_journal_without_business_mutation`.
- **Failure/recovery:** Prepared entry có thể process, abandon hoặc recover.
- **Status:** Enforced.
- **Gap/revisit:** Không có gap trong audited path.

## D. Receipt

### INV-RECEIPT-001 — Mỗi Order checkout có đúng một snapshot

- **Statement:** Checkout thành công phải persist chính xác một versioned receipt snapshot liên kết một-một với Order.
- **Rationale:** Reprint phải bền vững và không thay đổi theo catalog.
- **Scope/trigger:** Successful checkout.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs` — `ReceiptSnapshotFactory.Create`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — snapshot creation/persist path; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Repositories\OrderReceiptSnapshotRepository.cs` — `AddAsync`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReceiptSnapshotConfiguration.cs` — `OrderId` primary key and required one-to-one foreign key, `DeleteBehavior.Restrict`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Successful_checkout_must_persist_exactly_one_receipt_snapshot`, `Duplicate_snapshot_for_same_order_must_be_rejected`.
- **Failure/recovery:** Serializer/repository failure rollback toàn checkout.
- **Status:** Enforced.
- **Gap/revisit:** Return receipt riêng thuộc R9.

### INV-RECEIPT-002 — Reprint dùng persisted snapshot

- **Statement:** Reprint không được đọc live Product name/price để tái dựng hóa đơn.
- **Rationale:** Chứng từ lịch sử không đổi khi catalog đổi.
- **Scope/trigger:** History receipt preview/reprint.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderHistoryService.cs` — receipt retrieval; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptSnapshotJsonSerializer.cs` — `Deserialize`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\ReceiptPreviewService.cs` — snapshot preview.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReceiptSnapshot.cs` — persisted schema version/JSON payload; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReceiptSnapshotConfiguration.cs` — required payload.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_snapshot_must_not_change_when_product_changes_after_checkout`, `Persisted_snapshot_must_deserialize_to_original_checkout_snapshot`.
- **Failure/recovery:** Snapshot thiếu/hỏng trả failure, không tái dựng mù từ live catalog.
- **Status:** Enforced.
- **Gap/revisit:** Physical hardware acceptance R11.

### INV-RECEIPT-003 — Receipt snapshot không chứa cost hoặc known secrets

- **Statement:** Serialized receipt không được chứa cost price, password, Wi-Fi password hoặc secret-known fields.
- **Rationale:** Chứng từ khách hàng không được rò dữ liệu nội bộ.
- **Scope/trigger:** Snapshot serialization.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\DTOs\Printing\ReceiptRequest.cs` — receipt DTO shape; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Factories\ReceiptSnapshotFactory.cs` — allow-listed mapping; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Printing\ReceiptSnapshotJsonSerializer.cs` — strict serializer options/validation.
- **Persistence/schema guard:** Không có database content-redaction constraint; enforcement nằm tại DTO/factory/serializer boundary.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_payload_must_not_contain_cost_price_or_known_secrets`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs` — `ReceiptSnapshotSerializationTests.Serialization_must_be_deterministic_and_hide_internal_data`.
- **Failure/recovery:** Unsupported/tampered payload bị reject.
- **Status:** Enforced.
- **Gap/revisit:** Secret scanning toàn support bundle thuộc R2.

## E. Return/refund

### INV-RETURN-001 — Không trả quá remaining quantity

- **Statement:** Tổng quantity đã return cho một OrderItem không được vượt quantity đã bán.
- **Rationale:** Ngăn hoàn tiền và cộng tồn quá mức.
- **Scope/trigger:** Create return.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — `OrderReturnService.ProcessAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderItem.cs` — `OrderItem.RegisterRefund`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\OrderReturnBalance.cs` — `OrderReturnBalance.Register`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Product.cs` — `Product.RestockFromCustomerReturn`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnItemConfiguration.cs` — quantity checks; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnBalanceConfiguration.cs` — `CK_OrderReturnBalances_ReturnedQuantity`, `CK_OrderReturnBalances_RefundedAmount`, concurrency token.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Order_return_item_constraints_must_reject_invalid_quantities`, `Return_balance_constraints_must_reject_negative_values`.
- **Failure/recovery:** Validation/conflict rollback toàn return.
- **Status:** Enforced.
- **Gap/revisit:** Direct concurrent over-return test cần theo dõi khi mở rộng R9.

### INV-RETURN-002 — Return request idempotent

- **Statement:** Cùng return `ClientRequestId`/fingerprint phải replay; cùng ID khác payload phải conflict.
- **Rationale:** Retry không tạo duplicate refund/stock reversal.
- **Scope/trigger:** Order return.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — canonical fingerprint, existing-request replay/conflict path.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnConfiguration.cs` — unique index `UX_OrderReturns_ClientRequestId`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs` — `OrderReturnPersistenceTests.Client_request_id_unique_index_must_reject_duplicate`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnApplicationTests.cs` — `OrderReturnApplicationTests.Changed_canonical_payload_must_produce_different_fingerprint`.
- **Failure/recovery:** Replay hoặc conflict; transaction rollback.
- **Status:** Enforced.
- **Gap/revisit:** External refund provider idempotency chưa có.

### INV-RETURN-003 — Refund external và return receipt không được giả định hoàn tất

- **Statement:** Persisted return/refund record không được diễn giải thành bằng chứng cash drawer/bank refund hoặc immutable printed return receipt đã hoàn tất.
- **Rationale:** Source audit chỉ chứng minh business state, không chứng minh external side effect/document R9.
- **Scope/trigger:** Reporting và UI claims.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\OrderReturnService.cs` — persists refund method/reference without external provider invocation.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderReturnConfiguration.cs` — refund method/reference columns only; không có external-side-effect constraint.
- **Direct test evidence:** Không có direct test evidence trong source audit hiện tại cho cash-drawer/bank refund hoặc immutable printed return receipt.
- **Failure/recovery:** External handling/reconciliation chưa được source chứng minh.
- **Status:** Partially Enforced.
- **Gap/revisit:** R9.1 Return Receipt, R9.2 Cashbook, R9.3 Daily Close.

## F. Held Sale

### INV-HELD-001 — Hold không mutate sale/stock

- **Statement:** Tạo HeldSale chỉ persist cart snapshot; không tạo Order, movement, receipt hoặc giảm stock.
- **Rationale:** “Giữ đơn” không phải bán hàng.
- **Scope/trigger:** Create held sale.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — `HeldSaleService.CreateCoreAsync`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\HeldSaleConfiguration.cs` — HeldSale snapshot/state mappings; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\HeldSaleLineConfiguration.cs` — line snapshot mappings.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs` — `HeldSaleApplicationIntegrationTests.Create_held_sale_persists_snapshot_without_business_mutation`.
- **Failure/recovery:** Replay/conflict theo client request.
- **Status:** Enforced.
- **Gap/revisit:** Retention policy future.

### INV-HELD-002 — Resume phải revalidate live eligibility

- **Statement:** Resume phải đối chiếu product active/archive, stock và current price; khác biệt phải được báo/review.
- **Rationale:** Snapshot cũ không được checkout mù.
- **Scope/trigger:** Resume held sale.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — `HeldSaleService.GetHeldSaleForResumeAsync`.
- **Persistence/schema guard:** Không có database constraint cho live revalidation; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\HeldSaleLineConfiguration.cs` persists original product/price snapshots, enforcement comparison nằm tại Application boundary.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs` — `HeldSaleApplicationIntegrationTests.Resume_reports_price_stock_and_unavailable_without_mutation`, `Resume_archived_product_reports_unavailable`.
- **Failure/recovery:** Return status/warning, không business mutation.
- **Status:** Enforced.
- **Gap/revisit:** Promotion rule revalidation thuộc R8.

### INV-HELD-003 — Một active PaymentIntent sở hữu tối đa một HeldSale

- **Statement:** HeldSale có active PaymentIntent không được đồng thời resume/cash-checkout/cancel và không được có hai active owners.
- **Rationale:** Ngăn hai UI/payment paths bán cùng cart.
- **Scope/trigger:** Create intent, list/resume/cancel held sale, checkout.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSalePaymentOwnershipPolicy.cs` — `Evaluate`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — list/resume/cancel checks; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — checkout ownership check; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — intent ownership transitions.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs` — filtered unique index `UX_PaymentIntents_Active_HeldSaleOwner`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs` — `HeldSalePaymentOwnershipTests.Two_payment_intents_cannot_own_same_held_sale`, `Cash_checkout_is_blocked_for_confirmed_payment_owned_held_sale`.
- **Failure/recovery:** Terminal cancelled/expired intent releases theo policy; stale action returns `PAYMENT_OWNED`.
- **Status:** Enforced.
- **Gap/revisit:** Multi-terminal cloud ownership ngoài V1.

## G. Controlled Discount

### INV-DISCOUNT-001 — Discount type/value/reason phải hợp lệ

- **Statement:** Controlled discount chỉ dùng None, fixed integer amount hoặc percentage basis points; value/reason phải hợp lệ và resolved amount không vượt subtotal.
- **Rationale:** Ngăn total âm và nhập phần trăm mơ hồ.
- **Scope/trigger:** Hold/checkout discount.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Services\SalesDiscountCalculator.cs` — `SalesDiscountCalculator.Resolve`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Validation\CheckoutValidator.cs` — discount validation; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\SalesDiscountInputFormatter.cs` — UI parsing only.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderDiscountSnapshotConfiguration.cs` — `CK_OrderDiscountSnapshots_Type`, requested/resolved/reason checks.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Fixed_amount_is_integer_and_bounded`, `Percentage_uses_basis_points_and_floor`, `Invalid_values_reason_and_zero_total_are_rejected`.
- **Failure/recovery:** Domain/App failure trước commit.
- **Status:** Enforced.
- **Gap/revisit:** Line discount/coupon/voucher/promotion R8.

### INV-DISCOUNT-002 — Discount phải có permission và actor snapshot

- **Statement:** Áp controlled discount phải qua capability check và persisted actor/reason/time snapshot.
- **Rationale:** Giảm giá là hành vi tài chính cần truy vết.
- **Scope/trigger:** Checkout/hold với sales discount.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\AuthorizedCheckoutService.cs` — discount permission path; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\HeldSaleService.cs` — discount capability check; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\Order.cs` — `Order.ApplySalesDiscount`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\OrderDiscountSnapshotConfiguration.cs` — required `AppliedByUserId`, reason, unique `OrderId`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs` — `SalesDiscountTests.Snapshot_has_unique_order_fk_and_integer_money`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthorizedCheckoutServiceTests.cs` — `AuthorizedCheckoutServiceTests.Anonymous_user_must_not_checkout`, `Inventory_staff_must_not_checkout`, `Cashier_must_reach_checkout_core`.
- **Failure/recovery:** Forbidden result hoặc transaction rollback.
- **Status:** Enforced.
- **Gap/revisit:** Promotion audit policy R8.

## H. VietQR and PaymentIntent

### INV-PAYMENT-001 — VietQR confirmation là manual boundary

- **Statement:** QR presented không được xem là đã nhận tiền; chỉ thao tác xác nhận thủ công mới chuyển intent sang Confirmed và cho tạo checkout journal.
- **Rationale:** Không có bank callback/API reconciliation trong source.
- **Scope/trigger:** VietQR presentation/confirmation.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — confirm lifecycle; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\ViewModels\SalesViewModel.cs` — manual confirmation flow; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\Services\VietQrPaymentDialogService.cs` — dialog result.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs` — `CK_PaymentIntents_Status`, `CK_PaymentIntents_StateShape`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Confirmation_no_creates_no_checkout_journal`, `Checkout_journal_is_created_only_after_manual_confirmation`.
- **Failure/recovery:** No giữ intent chưa confirmed; Confirmed recovery bắt buộc retry/review.
- **Status:** Enforced.
- **Gap/revisit:** Bank auto-reconciliation ngoài Retail V1.

### INV-PAYMENT-002 — Create/present intent không mutate sale

- **Statement:** Created/Presented PaymentIntent không được tạo Order, stock movement, receipt hoặc checkout journal.
- **Rationale:** QR preparation không phải checkout.
- **Scope/trigger:** Create/present.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — create/present methods.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs` — intent state/shape; không có Order/movement/receipt write in create/present path.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentApplicationTests.cs` — `PaymentIntentApplicationTests.Creating_intent_creates_no_order_stock_movement_or_receipt`, `Presenting_intent_creates_no_order_stock_movement_or_receipt`.
- **Failure/recovery:** Pending intent recoverable qua scope/restart.
- **Status:** Enforced.
- **Gap/revisit:** Không có gap trong audited path.

Current R0 closeout evidence confirms the Presented transition is persisted before the VietQR dialog; this does not change the manual-confirmation boundary in `INV-PAYMENT-001`.

### INV-PAYMENT-003 — Confirmed intent phải recoverable và complete một lần

- **Statement:** Confirmed intent phải persist request snapshot và không được silently abandon; retry concurrent chỉ tạo business data một lần.
- **Rationale:** Tránh mất tiền hoặc duplicate Order sau restart.
- **Scope/trigger:** Confirm/restart/retry checkout.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\PaymentIntent.cs` — confirm/complete/cancel transitions; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PaymentIntentService.cs` — recovery/retry state; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\CheckoutService.cs` — confirmed-intent completion; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\ViewModels\SalesViewModel.cs` — recovery actions.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\PaymentIntentConfiguration.cs` — unique client/display/completed Order indexes, concurrency token, required checkout JSON/fingerprint.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs` — `PaymentIntentCheckoutTests.Confirmed_intent_restart_offers_payment_retry`, `Confirmed_payment_intent_cannot_be_silently_abandoned`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentConcurrencyTests.cs` — `PaymentIntentConcurrencyTests.Concurrent_checkout_same_intent_creates_business_data_once`.
- **Failure/recovery:** Retry bằng intent ID/persisted snapshot; unsafe legacy state đi manual review.
- **Status:** Enforced.
- **Gap/revisit:** Operator reconciliation workflow mở rộng future.

## I. Migration/startup/schema

### INV-MIGRATION-001 — Startup dùng EF migrations, không EnsureCreated/Delete

- **Statement:** Runtime schema upgrade phải dùng `MigrateAsync`; không dùng `EnsureCreated`/`EnsureDeleted` thay migration.
- **Rationale:** Bảo toàn upgrade path và dữ liệu.
- **Scope/trigger:** App startup.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs` — `DatabaseInitializer.InitializeAsync`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\PosDbContextModelSnapshot.cs` — EF model snapshot; latest forward migration `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentMigrationTests.cs` — `PaymentIntentMigrationTests.Previously_applied_payment_intent_migration_is_upgraded_forward`, `Fresh_database_reaches_the_same_final_schema`.
- **Failure/recovery:** Migration exception blocks startup.
- **Status:** Enforced.
- **Gap/revisit:** Applied state database thật không được kiểm tra R0.5C.

### INV-BACKUP-001 — Existing database phải verified-backup trước pending migration

- **Statement:** Database hiện hữu có pending migrations chỉ được migrate sau khi verified backup thành công; fresh/no-pending database không tạo backup không cần thiết.
- **Rationale:** Có recovery point trước schema change.
- **Scope/trigger:** Startup database initialization.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\DatabaseInitializer.cs` — `DatabaseInitializer.InitializeAsync`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\SqliteDatabaseSafetyService.cs` — `CreateVerifiedBackup`.
- **Persistence/schema guard:** Không có database constraint; file/integrity verification nằm trong `SqliteDatabaseSafetyService.CreateVerifiedBackup`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs` — `DatabaseInitializerSafetyTests.Existing_database_with_pending_migrations_must_create_verified_backup`, `Backup_failure_must_block_migration`, `Fresh_database_must_migrate_without_pre_migration_backup`.
- **Failure/recovery:** Backup failure chặn migration/startup; verified backup được giữ khi migration fail.
- **Status:** Enforced.
- **Gap/revisit:** Restore wizard/drill thuộc R3.

### INV-MIGRATION-002 — Migration đã áp là forward-only policy

- **Statement:** Không sửa migration đã áp làm upgrade mechanism; schema correction phải bằng migration mới.
- **Rationale:** EF không rerun migration đã recorded.
- **Scope/trigger:** Mọi schema change.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md` — “Database và migration” forward-only governance; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs` — current latest forward migration evidence.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\PosDbContextModelSnapshot.cs` và migration ID trong `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Migrations\20260730103954_AddHeldSalePaymentOwnershipGuard.cs`; policy không có universal runtime constraint.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentMigrationTests.cs` — `PaymentIntentMigrationTests.Applied_migration_is_not_assumed_to_rerun_after_its_file_changes`.
- **Failure/recovery:** Vi phạm phải dừng checkpoint và tạo forward plan.
- **Status:** Policy Only.
- **Gap/revisit:** Enforce bằng CI repository policy tại R1.

## J. Authentication/RBAC/security

### INV-AUTH-001 — Password phải verify bằng BCrypt và không persist plaintext

- **Statement:** Login phải verify input qua `IPasswordHasher`/BCrypt; User chỉ giữ password hash.
- **Rationale:** Không lưu mật khẩu thật.
- **Scope/trigger:** Setup/login/password change.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Authentication\BCryptPasswordHasher.cs` — `BCryptPasswordHasher.HashPassword`, `VerifyPassword`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\AuthService.cs` — login verification; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\User.cs` — `User.PasswordHash`.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\UserConfiguration.cs` — required password-hash property; plaintext không thuộc `User`.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthServiceIntegrationTests.cs` — `AuthServiceIntegrationTests.Wrong_password_must_increment_failed_attempts`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthenticationInfrastructureTests.cs` — `AuthenticationInfrastructureTests.Bcrypt_must_hash_and_verify_password`, `Bcrypt_must_fail_safely_for_invalid_hash`.
- **Failure/recovery:** Invalid credentials không tạo session.
- **Status:** Enforced.
- **Gap/revisit:** Password reset/admin UI R4.

### INV-AUTH-002 — Locked/inactive account không được tạo session

- **Statement:** Account locked hoặc inactive phải bị từ chối kể cả password đúng.
- **Rationale:** Account state phải thắng credential.
- **Scope/trigger:** Login/remembered restore.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\AuthService.cs` — login state checks; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Domain\Entities\User.cs` — `RegisterFailedLogin`, `IsLocked`, active state.
- **Persistence/schema guard:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\Persistence\Configurations\UserConfiguration.cs` — failed-attempt/lock/active mappings and checks.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthServiceIntegrationTests.cs` — `AuthServiceIntegrationTests.Locked_account_must_reject_correct_password`, `Inactive_account_must_be_rejected`.
- **Failure/recovery:** Failure; current session không set.
- **Status:** Enforced.
- **Gap/revisit:** Account management UI R4.

### INV-AUTH-003 — Permission phải enforce ở Application boundary

- **Statement:** UI visibility không đủ; use case protected phải qua authorized decorator/permission service.
- **Rationale:** Ngăn gọi service vượt quyền từ alternate UI path.
- **Scope/trigger:** Product/category/inventory/checkout/payment/held sale/order history/return.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Services\PermissionService.cs` — `PermissionService.HasPermission`, `Authorize`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Wpf\App.xaml.cs` — `App.ConfigureApplicationServiceDecorators`; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Infrastructure\DependencyInjection.cs` — `DependencyInjection.AddInfrastructure` authorized order-history/return factory registrations.
- **Persistence/schema guard:** Không có database constraint; enforcement nằm tại Application decorator boundary.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PermissionServiceTests.cs` — `PermissionServiceTests.Denied_permission_must_return_forbidden`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthorizedCheckoutServiceTests.cs` — `AuthorizedCheckoutServiceTests.Anonymous_user_must_not_checkout`, `Inventory_staff_must_not_checkout`.
- **Failure/recovery:** Unauthorized/forbidden Result, không mutation.
- **Status:** Enforced.
- **Gap/revisit:** Comprehensive audit-log UI và role editor thuộc R4.

### INV-SECURITY-001 — Project Memory không chứa dữ liệu thật hoặc secret

- **Statement:** Project Memory không được chứa database rows, password/hash thật, token, secret, số tài khoản đầy đủ hoặc dữ liệu khách hàng.
- **Rationale:** Tài liệu repository không phải data export.
- **Scope/trigger:** Mọi checkpoint documentation/export.
- **Enforcing code:** Policy tại `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md`.
- **Persistence/schema guard:** Không có runtime schema guard.
- **Direct test evidence:** Không áp dụng cho tài liệu; review/secret scan là gate.
- **Failure/recovery:** Dừng, sanitize và review trước commit.
- **Status:** Policy Only.
- **Gap/revisit:** R0.5E exporter secret/exclusion scan PASS; R0.5F fresh-session verification and formal closeout PASS; R0.5 was committed/pushed at `dfb0eb7a000054664aa7feccb51778fe80aa32a7`. Future Project Memory/export changes must rerun the secret/privacy gate.

### INV-SECURITY-002 — Log/audit phải sanitize sensitive values

- **Statement:** Log/report không được xuất password, secret hoặc full customer/payment-sensitive values.
- **Rationale:** Hạn chế disclosure.
- **Scope/trigger:** Logging, support/report/export.
- **Enforcing code:** `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\Common\PosLog.cs` — centralized log-message helpers; `D:\Projects_1\POS_Enterprise_DotNet\src\POS.Application\DTOs\Printing\ReceiptRequest.cs` — allow-listed receipt DTO; `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md` — Security và privacy policy.
- **Persistence/schema guard:** Không có universal redaction guard được xác minh.
- **Direct test evidence:** `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` — `ReceiptSnapshotPersistenceTests.Persisted_payload_must_not_contain_cost_price_or_known_secrets`; không có direct global log-sanitization test trong source audit hiện tại.
- **Failure/recovery:** Review/sanitize output.
- **Status:** Partially Enforced.
- **Gap/revisit:** R0.5F exporter/closeout scan PASS; universal logging/support-bundle redaction remains a gap for R2.3.

## 3. Traceability summary

| Invariant group | Total | Enforced | Partially Enforced | Policy Only | Not Verified | Main absolute test files | Revisit checkpoint |
|---|---:|---:|---:|---:|---:|---|---|
| Money and totals | 2 | 2 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs` | R8 |
| Stock and inventory | 3 | 3 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\InventoryIntegrationTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` | R6 |
| Checkout and order | 3 | 3 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutIdempotencyApplicationTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\CheckoutReliabilityIntegrationTests.cs` | R2 |
| Receipt | 3 | 3 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotSerializationTests.cs` | R11 |
| Return/refund | 3 | 2 | 1 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnPersistenceTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\OrderReturnApplicationTests.cs` | R9 |
| Held Sale | 3 | 3 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSaleApplicationIntegrationTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\HeldSalePaymentOwnershipTests.cs` | R8 |
| Controlled Discount | 2 | 2 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\SalesDiscountTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthorizedCheckoutServiceTests.cs` | R8 |
| VietQR/PaymentIntent | 3 | 3 | 0 | 0 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentApplicationTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentCheckoutTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentConcurrencyTests.cs` | R13 |
| Migration/startup/backup | 3 | 2 | 0 | 1 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\DatabaseInitializerSafetyTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PaymentIntentMigrationTests.cs` | R1/R3 |
| Authentication/RBAC/security | 5 | 3 | 1 | 1 | 0 | `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\AuthServiceIntegrationTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\PermissionServiceTests.cs`; `D:\Projects_1\POS_Enterprise_DotNet\tests\POS.Architecture.Tests\ReceiptSnapshotPersistenceTests.cs` | R2/R4/R0.5F |
| **Total** | **30** | **26** | **2** | **2** | **0** | Direct test source nêu trên | Theo từng group |

Các con số được đếm từ 30 ID trong chính file này. Status không phải kết quả test vừa chạy.
