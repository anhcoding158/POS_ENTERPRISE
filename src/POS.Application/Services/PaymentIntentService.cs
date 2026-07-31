using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Payments;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Application.Validation;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Services;

namespace POS.Application.Services;

public sealed class PaymentIntentService(
    IPaymentIntentRepository paymentIntents,
    IProductRepository products,
    IHeldSaleRepository heldSales,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IClock clock,
    ICheckoutRequestCanonicalizer canonicalizer,
    IVietQrPaymentGateway gateway) : IPaymentIntentService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public async Task<Result<PaymentIntentDto>> CreateAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = canonicalizer;
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Checkout);
        if (request.ClientRequestId == Guid.Empty)
            return Failure<PaymentIntentDto>("PAYMENT_INTENT.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Unauthorized<PaymentIntentDto>();

        var quoteResult = await ResolveQuoteAsync(request.Checkout, cancellationToken);
        if (quoteResult.IsFailure)
            return Result.Failure<PaymentIntentDto>(quoteResult.AppError);
        var quote = quoteResult.Value;

        var existing = await paymentIntents.GetByClientRequestIdAsync(
            request.ClientRequestId, tracked: false, cancellationToken);
        if (existing is not null)
            return Replay(existing, quote.Fingerprint);

        if (quote.Request.HeldSaleId is int heldSaleId)
        {
            var activeOwner = await paymentIntents.GetActiveByHeldSaleIdAsync(
                heldSaleId, tracked: false, cancellationToken);
            if (activeOwner is not null)
                return Failure<PaymentIntentDto>(
                    "PAYMENT_INTENT.HELD_SALE_ALREADY_OWNED",
                    HeldSalePaymentOwnershipPolicy.LockedMessage);
        }

        var displayCode = $"VQ{request.ClientRequestId:N}"[..14].ToUpperInvariant();
        var payload = gateway.Build(quote.Total, displayCode);
        if (payload.IsFailure)
            return Result.Failure<PaymentIntentDto>(payload.AppError);

        var now = clock.UtcNow.ToUniversalTime();
        var intent = new PaymentIntent(
            request.ClientRequestId,
            displayCode,
            quote.Total,
            payload.Value.TransferContent,
            payload.Value.PayloadText,
            Hash(payload.Value.PayloadText),
            payload.Value.BankCode,
            payload.Value.AccountNumber,
            payload.Value.AccountName,
            quote.Fingerprint,
            BuildSnapshotJson(quote, paymentIntentId: null),
            actorId,
            now,
            now.Add(Lifetime),
            quote.Request.HeldSaleId);

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            if (intent.HeldSaleId is int transactionalHeldSaleId)
            {
                var owner = await paymentIntents.GetActiveByHeldSaleIdAsync(
                    transactionalHeldSaleId, tracked: false, cancellationToken);
                if (owner is not null)
                    return Failure<PaymentIntentDto>(
                        "PAYMENT_INTENT.HELD_SALE_ALREADY_OWNED",
                        HeldSalePaymentOwnershipPolicy.LockedMessage);
                var heldSale = await heldSales.GetByIdAsync(
                    transactionalHeldSaleId, tracked: false, cancellationToken);
                if (heldSale?.Status != HeldSaleStatus.Active)
                    return Failure<PaymentIntentDto>(
                        "PAYMENT_INTENT.HELD_SALE_INVALID",
                        "Đơn giữ không còn hợp lệ.");
            }
            await paymentIntents.AddAsync(intent, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            intent.LockCheckoutSnapshot(BuildSnapshotJson(quote, intent.Id));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(intent, false));
        }
        catch (PersistenceConflictException exception)
            when (exception.Kind == PersistenceConflictKind.UniqueConstraint)
        {
            var winner = await paymentIntents.GetByClientRequestIdAsync(
                request.ClientRequestId, tracked: false, cancellationToken);
            return winner is null
                ? Failure<PaymentIntentDto>("PAYMENT_INTENT.CREATE_CONFLICT", "Không thể tạo yêu cầu VietQR.")
                : Replay(winner, quote.Fingerprint);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentDto>(new AppError(exception.Code, exception.Message));
        }
    }

    public Task<Result<PaymentIntentDto>> MarkPresentedAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        MutateAsync(paymentIntentId, (intent, now, _) => intent.MarkPresented(now), cancellationToken);

    public async Task<Result<PaymentIntentDto>> ConfirmReceivedAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Unauthorized<PaymentIntentDto>();
        var intent = await OwnedTrackedAsync(paymentIntentId, actorId, cancellationToken);
        if (intent is null)
            return NotFound<PaymentIntentDto>();
        if (intent.Status is PaymentIntentStatus.Confirmed or PaymentIntentStatus.Completed)
            return Result.Success(Map(intent, true));
        if (intent.Status != PaymentIntentStatus.Presented)
            return Failure<PaymentIntentDto>("PAYMENT_INTENT.INVALID_TRANSITION", "Yêu cầu VietQR không thể xác nhận.");

        CheckoutRequest storedRequest;
        try
        {
            storedRequest = ToCheckoutRequest(
                ConfirmedCheckoutSnapshotJson.Deserialize(intent.CheckoutRequestJson));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return Failure<PaymentIntentDto>(
                "PAYMENT_INTENT.SNAPSHOT_UNREADABLE",
                "Dữ liệu đơn VietQR thuộc phiên bản cũ và cần được xử lý thủ công.");
        }
        var quote = await ResolveQuoteAsync(storedRequest, cancellationToken);
        if (quote.IsFailure || quote.Value.Fingerprint != intent.QuoteFingerprint ||
            quote.Value.Total != intent.Amount)
            return Failure<PaymentIntentDto>("PAYMENT_INTENT.STALE", "Thông tin đơn hàng đã thay đổi. Hãy tạo mã VietQR mới.");

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            await paymentIntents.ReloadTrackedAsync(intent, cancellationToken);
            if (intent.Status is PaymentIntentStatus.Confirmed or PaymentIntentStatus.Completed)
                return Result.Success(Map(intent, true));
            if (intent.HeldSaleId is int heldSaleId)
            {
                var heldSale = await heldSales.GetByIdAsync(
                    heldSaleId, tracked: false, cancellationToken);
                if (heldSale?.Status != HeldSaleStatus.Active)
                    return Failure<PaymentIntentDto>(
                        "PAYMENT_INTENT.HELD_SALE_CONFLICT",
                        "Đơn giữ đã được xử lý bởi giao dịch khác; không thể xác nhận VietQR tự động.");
            }
            intent.MarkConfirmed(actorId, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(intent, false));
        }
        catch (PersistenceConflictException)
        {
            return Failure<PaymentIntentDto>("PAYMENT_INTENT.CONCURRENCY_CONFLICT", "Yêu cầu VietQR vừa được xử lý ở phiên khác.");
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentDto>(new AppError(exception.Code, exception.Message));
        }
    }

    public Task<Result<PaymentIntentDto>> CancelAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        MutateAsync(paymentIntentId, (intent, now, _) => intent.Cancel(now), cancellationToken);

    public Task<Result<PaymentIntentDto>> ExpireAsync(
        int paymentIntentId, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(paymentIntentId, (intent, now, _) => intent.Expire(now, reason), cancellationToken);

    public async Task<Result<PaymentIntentDto>> GetByIdAsync(int paymentIntentId, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Unauthorized<PaymentIntentDto>();
        var intent = await paymentIntents.GetByIdAsync(paymentIntentId, tracked: false, cancellationToken);
        return intent is null || intent.CreatedByUserId != actorId
            ? NotFound<PaymentIntentDto>()
            : Result.Success(Map(intent, false));
    }

    public async Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> GetPendingAsync(
        int limit = 25, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Unauthorized<IReadOnlyList<PaymentIntentPendingDto>>();
        var bounded = Math.Clamp(limit, 1, 100);
        var values = await paymentIntents.GetPendingAsync(actorId, bounded, cancellationToken);
        return Result.Success<IReadOnlyList<PaymentIntentPendingDto>>(
            values.Select(value => MapPending(value, stale: false)).ToArray());
    }

    public async Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> RecoverPendingAsync(
        int limit = 25, CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingAsync(limit, cancellationToken);
        if (pending.IsFailure)
            return pending;
        var recovered = new List<PaymentIntentPendingDto>(pending.Value.Count);
        foreach (var summary in pending.Value)
        {
            var entity = await paymentIntents.GetByIdAsync(summary.Id, tracked: false, cancellationToken);
            if (entity is null)
                continue;
            var stale = true;
            try
            {
                var snapshot = ConfirmedCheckoutSnapshotJson.Deserialize(entity.CheckoutRequestJson);
                ValidateSnapshotIdentity(snapshot, entity);
                if (entity.Status == PaymentIntentStatus.Confirmed)
                {
                    if (entity.HeldSaleId is int heldSaleId)
                    {
                        var heldSale = await heldSales.GetByIdAsync(
                            heldSaleId, tracked: false, cancellationToken);
                        if (heldSale?.Status == HeldSaleStatus.Completed &&
                            heldSale.CompletedOrderId is int completedOrderId)
                        {
                            var completedOrder = await orders.GetByIdReadOnlyAsync(
                                completedOrderId, cancellationToken);
                            if (completedOrder is not null &&
                                completedOrder.PaymentMethod != PaymentMethod.VietQr)
                            {
                                recovered.Add(MapPending(
                                    entity, stale: true, completedOrder));
                                continue;
                            }
                        }
                    }
                    recovered.Add(MapPending(entity, stale: false));
                    continue;
                }
                var request = ToCheckoutRequest(snapshot);
                var quote = await ResolveQuoteAsync(request, cancellationToken);
                stale =
                    quote.IsFailure ||
                    quote.Value.Fingerprint != entity.QuoteFingerprint ||
                    quote.Value.Total != entity.Amount;
            }
            catch (Exception exception)
                when (exception is
                    ArgumentException or
                    InvalidOperationException or
                    System.Text.Json.JsonException)
            {
                /*
                 * Snapshot legacy không thể reconstruct được giữ nguyên
                 * để manual review; không dùng giá/config live và không
                 * tự confirm, cancel hoặc checkout.
                 */
            }
            recovered.Add(MapPending(entity, stale));
        }
        return Result.Success<IReadOnlyList<PaymentIntentPendingDto>>(recovered);
    }

    public async Task<Result<PaymentIntentManualResolutionDto>> ResolveManuallyAsync(
        ResolvePaymentIntentManuallyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!currentUser.IsInRole(Role.Administrator) ||
            currentUser.UserId is not int actorId || actorId <= 0)
            return Failure<PaymentIntentManualResolutionDto>(
                ErrorCodes.General.Unauthorized,
                "Chỉ Quản trị viên được xử lý thủ công giao dịch VietQR.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Failure<PaymentIntentManualResolutionDto>(
                "PAYMENT_INTENT_RESOLUTION.REASON_REQUIRED", "Lý do xử lý là bắt buộc.");

        var existing = await paymentIntents.GetResolutionAsync(
            request.PaymentIntentId, cancellationToken);
        if (existing is not null)
            return await MapResolutionAsync(existing, cancellationToken);

        var intent = await paymentIntents.GetByIdAsync(
            request.PaymentIntentId, tracked: true, cancellationToken);
        if (intent is null || intent.Status != PaymentIntentStatus.Confirmed)
            return Failure<PaymentIntentManualResolutionDto>(
                "PAYMENT_INTENT_RESOLUTION.NOT_MANUAL_REVIEW",
                "Chỉ giao dịch đã xác nhận và chưa hoàn tất mới được xử lý thủ công.");

        Order? linkedOrder = null;
        if (request.ResolutionType == PaymentIntentManualResolutionType.LinkExistingOrder)
        {
            if (request.LinkedOrderId is not int orderId || orderId <= 0)
                return Failure<PaymentIntentManualResolutionDto>(
                    "PAYMENT_INTENT_RESOLUTION.ORDER_REQUIRED", "Phải chọn chính xác hóa đơn.");
            linkedOrder = await orders.GetByIdReadOnlyAsync(orderId, cancellationToken);
            if (linkedOrder is null ||
                linkedOrder.PaymentMethod != PaymentMethod.VietQr ||
                linkedOrder.TotalAmount != intent.Amount)
                return Failure<PaymentIntentManualResolutionDto>(
                    "PAYMENT_INTENT_RESOLUTION.ORDER_MISMATCH",
                    "Hóa đơn không tồn tại hoặc không khớp chính xác VietQR và số tiền.");
            var otherIntent = await paymentIntents.GetByCompletedOrderIdAsync(orderId, cancellationToken);
            if (otherIntent is not null && otherIntent.Id != intent.Id)
                return Failure<PaymentIntentManualResolutionDto>(
                    "PAYMENT_INTENT_RESOLUTION.ORDER_ALREADY_LINKED",
                    "Hóa đơn đã liên kết với giao dịch VietQR khác.");
        }

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            await paymentIntents.ReloadTrackedAsync(intent, cancellationToken);
            existing = await paymentIntents.GetResolutionAsync(request.PaymentIntentId, cancellationToken);
            if (existing is not null)
                return await MapResolutionAsync(existing, cancellationToken);
            var now = clock.UtcNow;
            if (linkedOrder is not null)
                intent.Complete(linkedOrder.Id, now);
            var resolution = new PaymentIntentManualResolution(
                intent.Id, request.ResolutionType, actorId, request.Reason, now,
                request.ExternalReference, request.LinkedOrderId);
            await paymentIntents.AddResolutionAsync(resolution, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await MapResolutionAsync(resolution, cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            existing = await paymentIntents.GetResolutionAsync(request.PaymentIntentId, cancellationToken);
            return existing is not null
                ? await MapResolutionAsync(existing, cancellationToken)
                : Failure<PaymentIntentManualResolutionDto>(
                    "PAYMENT_INTENT_RESOLUTION.CONFLICT", "Giao dịch vừa được xử lý ở phiên khác.");
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentManualResolutionDto>(
                new AppError(exception.Code, exception.Message));
        }
    }

    public async Task<Result<IReadOnlyList<PaymentIntentManualResolutionDto>>> GetManualResolutionHistoryAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsInRole(Role.Administrator))
            return Unauthorized<IReadOnlyList<PaymentIntentManualResolutionDto>>();
        var history = await paymentIntents.GetResolutionHistoryAsync(limit, cancellationToken);
        var result = new List<PaymentIntentManualResolutionDto>(history.Count);
        foreach (var resolution in history)
        {
            var mapped = await MapResolutionAsync(resolution, cancellationToken);
            if (mapped.IsSuccess)
                result.Add(mapped.Value);
        }
        return Result.Success<IReadOnlyList<PaymentIntentManualResolutionDto>>(result);
    }

    private async Task<Result<PaymentIntentManualResolutionDto>> MapResolutionAsync(
        PaymentIntentManualResolution resolution, CancellationToken cancellationToken)
    {
        var intent = await paymentIntents.GetByIdAsync(
            resolution.PaymentIntentId, tracked: false, cancellationToken);
        if (intent is null)
            return Failure<PaymentIntentManualResolutionDto>(
                "PAYMENT_INTENT.NOT_FOUND", "Không tìm thấy yêu cầu VietQR.");
        var linkedOrder = resolution.LinkedOrderId is int orderId
            ? await orders.GetByIdReadOnlyAsync(orderId, cancellationToken)
            : null;
        return Result.Success(MapResolution(
            resolution, intent.DisplayCode, linkedOrder?.OrderCode, intent.Amount));
    }

    private static PaymentIntentManualResolutionDto MapResolution(
        PaymentIntentManualResolution value, string displayCode,
        string? linkedOrderCode, long amount) =>
        new(value.Id, value.PaymentIntentId, displayCode, value.ResolutionType,
            value.ResolvedAtUtc, value.ResolvedByUserId, value.Reason,
            value.ExternalReference, value.LinkedOrderId, linkedOrderCode, amount);

    private async Task<Result<PaymentIntentDto>> MutateAsync(
        int id, Action<PaymentIntent, DateTimeOffset, int> mutation, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Unauthorized<PaymentIntentDto>();
        var intent = await OwnedTrackedAsync(id, actorId, cancellationToken);
        if (intent is null)
            return NotFound<PaymentIntentDto>();
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            await paymentIntents.ReloadTrackedAsync(intent, cancellationToken);
            mutation(intent, clock.UtcNow, actorId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(intent, false));
        }
        catch (PersistenceConflictException)
        {
            return Failure<PaymentIntentDto>("PAYMENT_INTENT.CONCURRENCY_CONFLICT", "Yêu cầu VietQR vừa được xử lý ở phiên khác.");
        }
        catch (DomainException exception)
        {
            return Result.Failure<PaymentIntentDto>(new AppError(exception.Code, exception.Message));
        }
    }

    private async Task<PaymentIntent?> OwnedTrackedAsync(int id, int actorId, CancellationToken token)
    {
        if (id <= 0) return null;
        var intent = await paymentIntents.GetByIdAsync(id, tracked: true, token);
        return intent?.CreatedByUserId == actorId ? intent : null;
    }

    private async Task<Result<ResolvedQuote>> ResolveQuoteAsync(CheckoutRequest source, CancellationToken token)
    {
        if (source.Lines.Count == 0)
            return Failure<ResolvedQuote>(ErrorCodes.Checkout.EmptyCart, "Giỏ hàng không được để trống.");
        if (source.PaymentMethod != PaymentMethod.VietQr)
            return Failure<ResolvedQuote>("PAYMENT_INTENT.METHOD_INVALID", "PaymentIntent chỉ hỗ trợ VietQR.");

        var productMap = new Dictionary<int, Product>();
        foreach (var id in source.Lines.Select(x => x.ProductId).Distinct().Order())
        {
            var product = await products.GetByIdReadOnlyAsync(id, token);
            if (product is null || product.IsArchived || !product.IsActive)
                return Failure<ResolvedQuote>(ErrorCodes.Checkout.ProductNotFound, $"Sản phẩm {id} không khả dụng.");
            productMap.Add(id, product);
        }

        if (source.HeldSaleId is int heldSaleId)
        {
            var heldSale = await heldSales.GetByIdAsync(heldSaleId, tracked: false, token);
            if (heldSale is null || heldSale.Status != HeldSaleStatus.Active ||
                heldSale.CreatedByUserId != currentUser.UserId)
                return Failure<ResolvedQuote>("PAYMENT_INTENT.HELD_SALE_INVALID", "Đơn giữ không còn hợp lệ.");
        }

        try
        {
            var subtotal = source.Lines.Sum(line =>
                checked(productMap[line.ProductId].SalePrice * line.Quantity));
            var discount = SalesDiscountCalculator.Resolve(
                subtotal, source.SalesDiscount.Type, source.SalesDiscount.Value, source.SalesDiscount.Reason);
            var total = checked(subtotal - discount);
            if (total <= 0)
                return Failure<ResolvedQuote>(ErrorCodes.Payments.InvalidAmount, "Tổng thanh toán VietQR không hợp lệ.");
            var normalized = new CheckoutRequest(
                source.Lines, PaymentMethod.VietQr, 0, source.CustomerId, source.RestaurantTableId,
                source.DiscountCode, source.Notes, total, Guid.NewGuid(), source.HeldSaleId,
                source.SalesDiscount, paymentIntentId: null);
            var quoteJson = CheckoutService.CreatePreparedQuote(normalized, productMap);
            return Result.Success(new ResolvedQuote(
                normalized, total, CheckoutRequestCanonicalizer.Hash(quoteJson),
                productMap));
        }
        catch (DomainException exception)
        {
            return Result.Failure<ResolvedQuote>(new AppError(exception.Code, exception.Message));
        }
        catch (OverflowException)
        {
            return Failure<ResolvedQuote>(ErrorCodes.Payments.InvalidAmount, "Tổng thanh toán vượt giới hạn.");
        }
    }

    private static Result<PaymentIntentDto> Replay(PaymentIntent value, string fingerprint) =>
        value.QuoteFingerprint == fingerprint
            ? Result.Success(Map(value, true))
            : Failure<PaymentIntentDto>("PAYMENT_INTENT.IDEMPOTENCY_CONFLICT", "ClientRequestId đã được dùng cho nội dung khác.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static PaymentIntentDto Map(PaymentIntent value, bool replay) =>
        new(value.Id, value.DisplayCode, value.Status, value.Amount, value.Currency,
            value.TransferContent, value.PayloadText, value.BankCodeSnapshot,
            value.AccountNumberSnapshot, value.AccountNameSnapshot, value.CreatedAtUtc,
            value.UpdatedAtUtc, value.ExpiresAtUtc, value.HeldSaleId, value.CompletedOrderId, replay);

    private static PaymentIntentPendingDto MapPending(
        PaymentIntent value, bool stale, Order? conflictingOrder = null)
    {
        var crossPaymentConflict = conflictingOrder is not null;
        var usable = !stale && !crossPaymentConflict;
        return new(value.Id, value.DisplayCode, value.Status, value.Amount,
            value.Currency, value.TransferContent, value.PayloadText,
            value.BankCodeSnapshot, value.AccountNumberSnapshot, value.AccountNameSnapshot,
            value.CreatedAtUtc,
            value.UpdatedAtUtc, value.ExpiresAtUtc, value.HeldSaleId,
            usable && value.Status == PaymentIntentStatus.Created,
            usable && value.Status is PaymentIntentStatus.Created or PaymentIntentStatus.Presented,
            usable && value.Status == PaymentIntentStatus.Presented,
            value.Status is PaymentIntentStatus.Created or PaymentIntentStatus.Presented,
            usable && value.Status == PaymentIntentStatus.Confirmed,
            stale,
            crossPaymentConflict
                ? "Đơn giữ liên quan đã được hoàn tất bởi một giao dịch khác. Không thể tự động hoàn tất VietQR mà không có nguy cơ tạo trùng đơn. Không yêu cầu khách chuyển thêm. Vui lòng để quản trị viên xử lý."
                : stale && value.Status == PaymentIntentStatus.Confirmed
                ? "Yêu cầu đã xác nhận nhận tiền nhưng snapshot cũ không thể khôi phục. Cần quản lý xử lý thủ công; không tạo mã mới."
                : stale
                    ? "Thông tin đơn hàng cũ không thể khôi phục an toàn. Không tự xác nhận hoặc checkout."
                    : value.Status == PaymentIntentStatus.Confirmed
                        ? "Đã lưu xác nhận nhận tiền nhưng đơn hàng chưa hoàn tất."
                        : null)
        {
            ConfirmedAtUtc = value.ConfirmedAtUtc,
            ConfirmedByUserId = value.ConfirmedByUserId,
            IsCrossPaymentConflict = crossPaymentConflict,
            ConflictingOrderId = conflictingOrder?.Id,
            ConflictingOrderCode = conflictingOrder?.OrderCode,
            ConflictingPaymentMethod = conflictingOrder?.PaymentMethod
        };
    }

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new AppError(code, message));
    private static Result<T> Unauthorized<T>() =>
        Failure<T>(ErrorCodes.General.Unauthorized, "Không tìm thấy phiên đăng nhập hợp lệ.");
    private static Result<T> NotFound<T>() =>
        Failure<T>("PAYMENT_INTENT.NOT_FOUND", "Không tìm thấy yêu cầu VietQR.");

    private static CheckoutRequest ToCheckoutRequest(ConfirmedCheckoutSnapshot snapshot) =>
        new(snapshot.Lines.Select(line => new CheckoutLineRequest(
                line.ProductId, line.Quantity, line.Modifiers,
                line.LineDiscountAmount, line.Notes)),
            PaymentMethod.VietQr, 0, notes: snapshot.Notes,
            confirmedPaymentAmount: snapshot.Total,
            clientRequestId: snapshot.ClientRequestId,
            heldSaleId: snapshot.HeldSaleId,
            salesDiscount: snapshot.SalesDiscount,
            paymentIntentId: snapshot.PaymentIntentId);

    private static void ValidateSnapshotIdentity(
        ConfirmedCheckoutSnapshot snapshot, PaymentIntent intent)
    {
        if (snapshot.PaymentIntentId != intent.Id ||
            snapshot.HeldSaleId != intent.HeldSaleId ||
            snapshot.Total != intent.Amount ||
            snapshot.QuoteFingerprint != intent.QuoteFingerprint)
            throw new InvalidOperationException("PaymentIntent checkout snapshot không khớp entity.");
    }

    private static string BuildSnapshotJson(ResolvedQuote quote, int? paymentIntentId)
    {
        var lines = quote.Request.Lines.Select(line =>
        {
            var product = quote.Products[line.ProductId];
            return new ConfirmedCheckoutLineSnapshot(
                product.Id, product.Code, product.Name, product.UnitName,
                line.Quantity, product.SalePrice, line.LineDiscountAmount,
                line.Notes, line.Modifiers);
        }).OrderBy(line => line.ProductId).ThenBy(line => line.Quantity).ToArray();
        var subtotal = lines.Sum(line => checked(line.UnitPrice * line.Quantity));
        var discount = SalesDiscountCalculator.Resolve(
            subtotal, quote.Request.SalesDiscount.Type,
            quote.Request.SalesDiscount.Value, quote.Request.SalesDiscount.Reason);
        return ConfirmedCheckoutSnapshotJson.Serialize(new ConfirmedCheckoutSnapshot(
            ConfirmedCheckoutSnapshotJson.CurrentVersion,
            quote.Request.ClientRequestId, PaymentMethod.VietQr,
            paymentIntentId, quote.Request.HeldSaleId, quote.Request.Notes,
            quote.Request.SalesDiscount, subtotal, discount, quote.Total,
            quote.Fingerprint, lines));
    }

    private sealed record ResolvedQuote(
        CheckoutRequest Request, long Total, string Fingerprint,
        IReadOnlyDictionary<int, Product> Products);
}
