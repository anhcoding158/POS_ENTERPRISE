using System.Collections.Concurrent;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.HeldSales;
using POS.Application.DTOs.Checkout;
using POS.Application.Authorization;
using POS.Domain.Enums;
using POS.Domain.Services;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

public sealed class HeldSaleService(
    IHeldSaleRepository heldSales,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IClock clock,
    IHeldSaleRequestCanonicalizer canonicalizer,
    IPermissionService? permissions = null,
    IPaymentIntentRepository? paymentIntents = null) : IHeldSaleService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CreateLocks = new();

    public async Task<Result<HeldSaleDto>> CreateHeldSaleAsync(
        CreateHeldSaleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClientRequestId == Guid.Empty)
            return Failure<HeldSaleDto>("HELD_SALE.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");

        var gate = CreateLocks.GetOrAdd(
            request.ClientRequestId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await CreateCoreAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                CreateLocks.TryRemove(
                    new KeyValuePair<Guid, SemaphoreSlim>(request.ClientRequestId, gate));
        }
    }

    private async Task<Result<HeldSaleDto>> CreateCoreAsync(
        CreateHeldSaleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Failure<HeldSaleDto>("GENERAL.UNAUTHORIZED", "Không tìm thấy phiên đăng nhập hợp lệ.");
        if (request.Lines is null || request.Lines.Count == 0)
            return Failure<HeldSaleDto>("HELD_SALE.EMPTY", "Không thể giữ giỏ hàng trống.");
        if (request.Lines.Any(line => line.ProductId <= 0 || line.Quantity <= 0))
            return Failure<HeldSaleDto>("HELD_SALE.INVALID_LINE", "Dòng sản phẩm đơn giữ không hợp lệ.");

        var discount = request.SalesDiscount ?? SalesDiscountRequest.None;
        if (discount.Type != SalesDiscountType.None &&
            (permissions is null ||
             !permissions.HasPermission(SystemCapability.ApplySalesDiscount)))
            return Failure<HeldSaleDto>("GENERAL.FORBIDDEN", "Bạn không có quyền áp dụng giảm giá bán hàng.");

        var canonical = canonicalizer.Canonicalize(request);
        var existing = await heldSales.GetByClientRequestIdAsync(
            request.ClientRequestId, tracked: false, cancellationToken);
        if (existing is not null)
            return Replay(existing, canonical.Fingerprint);

        var utcNow = clock.UtcNow.ToUniversalTime();
        var normalizedLabel = string.IsNullOrWhiteSpace(request.Label)
            ? $"Đơn giữ {utcNow.ToLocalTime():HH:mm}"
            : request.Label.Trim();
        var snapshots = new List<(int ProductId, string Code, string? Barcode,
            string Name, int Quantity, long UnitPrice, int SortOrder, string? Notes)>();

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            var product = await products.GetByIdReadOnlyAsync(line.ProductId, cancellationToken);
            if (product is null || product.IsArchived || !product.IsActive)
                return Failure<HeldSaleDto>("HELD_SALE.PRODUCT_UNAVAILABLE",
                    $"Sản phẩm {line.ProductId} không còn bán.");
            if (!product.CanFulfill(line.Quantity))
                return Failure<HeldSaleDto>("HELD_SALE.INSUFFICIENT_STOCK",
                    $"Sản phẩm '{product.Name}' không đủ tồn kho tại thời điểm giữ đơn.");
            snapshots.Add((product.Id, product.Code, product.Barcode, product.Name,
                line.Quantity, product.SalePrice, index, line.Notes));
        }

        var displayCode = $"G{utcNow:yyMMddHHmm}-{request.ClientRequestId:N}"[..20].ToUpperInvariant();
        var heldSale = new HeldSale(request.ClientRequestId, canonical.Fingerprint,
            displayCode, normalizedLabel, request.Notes, actorId, utcNow, snapshots,
            discount.Type, discount.Value, discount.Reason);

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            await heldSales.AddAsync(heldSale, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(Map(heldSale));
        }
        catch (PersistenceConflictException)
        {
            var winner = await heldSales.GetByClientRequestIdAsync(
                request.ClientRequestId, tracked: false, cancellationToken);
            return winner is null
                ? Failure<HeldSaleDto>("HELD_SALE.SAVE_CONFLICT", "Không thể lưu đơn giữ do xung đột dữ liệu.")
                : Replay(winner, canonical.Fingerprint);
        }
        catch (DomainException exception)
        {
            return Failure<HeldSaleDto>(exception.Code, exception.Message);
        }
    }

    public async Task<Result<IReadOnlyList<HeldSaleDto>>> GetActiveHeldSalesAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Failure<IReadOnlyList<HeldSaleDto>>("GENERAL.UNAUTHORIZED", "Không tìm thấy phiên đăng nhập hợp lệ.");
        if (limit is <= 0 or > 100)
            return Failure<IReadOnlyList<HeldSaleDto>>("HELD_SALE.INVALID_LIMIT", "Giới hạn danh sách không hợp lệ.");
        var values = await heldSales.GetActiveAsync(actorId, limit, cancellationToken);
        if (paymentIntents is null)
            return Result.Success<IReadOnlyList<HeldSaleDto>>(values.Select(Map).ToArray());
        var resumable = new List<HeldSaleDto>(values.Count);
        foreach (var value in values)
        {
            var owner = await paymentIntents.GetActiveByHeldSaleIdAsync(
                value.Id, tracked: false, cancellationToken);
            if (HeldSalePaymentOwnershipPolicy.Evaluate(value, owner) ==
                HeldSalePaymentOwnership.Unlocked)
                resumable.Add(Map(value));
        }
        return Result.Success<IReadOnlyList<HeldSaleDto>>(resumable);
    }

    public async Task<Result<HeldSaleResumeDto>> GetHeldSaleForResumeAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Failure<HeldSaleResumeDto>("GENERAL.UNAUTHORIZED", "Không tìm thấy phiên đăng nhập hợp lệ.");
        var heldSale = await heldSales.GetByIdAsync(heldSaleId, tracked: false, cancellationToken);
        if (heldSale is null || heldSale.CreatedByUserId != actorId ||
            heldSale.Status != POS.Domain.Enums.HeldSaleStatus.Active)
            return Failure<HeldSaleResumeDto>("HELD_SALE.NOT_FOUND", "Không tìm thấy đơn đang giữ.");

        if (paymentIntents is not null)
        {
            var owner = await paymentIntents.GetActiveByHeldSaleIdAsync(
                heldSaleId, tracked: false, cancellationToken);
            if (HeldSalePaymentOwnershipPolicy.Evaluate(heldSale, owner) !=
                HeldSalePaymentOwnership.Unlocked)
                return Failure<HeldSaleResumeDto>(
                    "HELD_SALE.PAYMENT_OWNED",
                    HeldSalePaymentOwnershipPolicy.LockedMessage);
        }

        var resultLines = new List<HeldSaleResumeLineDto>();
        foreach (var line in heldSale.Lines.OrderBy(value => value.SortOrder))
        {
            var product = await products.GetByIdReadOnlyAsync(line.ProductId, cancellationToken);
            var unavailable = product is null || product.IsArchived || !product.IsActive;
            var insufficient = !unavailable && product!.TrackInventory &&
                !product.AllowNegativeStock && product.StockQuantity < line.Quantity;
            var priceChanged = !unavailable && product!.SalePrice != line.UnitPriceSnapshot;
            var status = unavailable
                ? HeldSaleResumeLineStatus.Unavailable
                : insufficient
                    ? HeldSaleResumeLineStatus.InsufficientStock
                    : priceChanged
                        ? HeldSaleResumeLineStatus.PriceChanged
                        : HeldSaleResumeLineStatus.Unchanged;
            var warning = status switch
            {
                HeldSaleResumeLineStatus.Unavailable => "Sản phẩm không còn bán.",
                HeldSaleResumeLineStatus.InsufficientStock => "Tồn kho hiện tại không đủ; hãy giảm số lượng hoặc loại dòng.",
                HeldSaleResumeLineStatus.PriceChanged => "Giá bán đã thay đổi; phải xác nhận dùng giá hiện tại.",
                _ => null
            };
            resultLines.Add(new(line.ProductId, line.ProductCodeSnapshot,
                line.ProductNameSnapshot, line.Quantity, line.UnitPriceSnapshot,
                product?.Code, product?.Name, product?.UnitName, product?.SalePrice, product?.StockQuantity,
                product?.TrackInventory ?? false, product?.AllowNegativeStock ?? false,
                status, warning, line.LineNotesSnapshot));
        }

        var currentSubtotal = resultLines
            .Where(line => line.CurrentUnitPrice.HasValue)
            .Sum(line => checked(line.CurrentUnitPrice!.Value * line.RequestedQuantity));
        var currentDiscount = 0L;
        var requiresReview = currentSubtotal != heldSale.SubtotalSnapshot;
        try
        {
            currentDiscount = SalesDiscountCalculator.Resolve(
                currentSubtotal, heldSale.DiscountType,
                heldSale.RequestedDiscountValue, heldSale.DiscountReason);
        }
        catch (DomainException)
        {
            requiresReview = true;
        }
        return Result.Success(new HeldSaleResumeDto(
            heldSale.Id, heldSale.DisplayCode, heldSale.Label, heldSale.Notes, resultLines,
            heldSale.DiscountType, heldSale.RequestedDiscountValue, heldSale.DiscountReason,
            heldSale.ResolvedDiscountAmountSnapshot, heldSale.SubtotalSnapshot,
            heldSale.TotalSnapshot, currentDiscount,
            checked(currentSubtotal - currentDiscount), requiresReview));
    }

    public async Task<Result> CancelHeldSaleAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Result.Failure(new AppError("GENERAL.UNAUTHORIZED", "Không tìm thấy phiên đăng nhập hợp lệ."));
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var heldSale = await heldSales.GetByIdAsync(heldSaleId, tracked: true, cancellationToken);
            if (heldSale is null || heldSale.CreatedByUserId != actorId)
                return Result.Failure(new AppError("HELD_SALE.NOT_FOUND", "Không tìm thấy đơn đang giữ."));
            if (paymentIntents is not null)
            {
                var owner = await paymentIntents.GetActiveByHeldSaleIdAsync(
                    heldSaleId, tracked: false, cancellationToken);
                if (HeldSalePaymentOwnershipPolicy.Evaluate(heldSale, owner) !=
                    HeldSalePaymentOwnership.Unlocked)
                    return Result.Failure(new AppError(
                        "HELD_SALE.PAYMENT_OWNED",
                        HeldSalePaymentOwnershipPolicy.LockedMessage));
            }
            if (heldSale.Status == POS.Domain.Enums.HeldSaleStatus.Cancelled)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result.Success();
            }
            heldSale.Cancel(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Failure(new AppError(exception.Code, exception.Message));
        }
        catch (PersistenceConflictException)
        {
            return Result.Failure(new AppError("HELD_SALE.CONCURRENCY_CONFLICT",
                "Đơn giữ vừa được xử lý bởi phiên khác."));
        }
    }

    private static Result<HeldSaleDto> Replay(HeldSale heldSale, string fingerprint) =>
        string.Equals(heldSale.RequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? Result.Success(Map(heldSale) with { IsIdempotentReplay = true })
            : Failure<HeldSaleDto>("HELD_SALE.IDEMPOTENCY_CONFLICT",
                "ClientRequestId đã được dùng cho nội dung đơn giữ khác.");

    private static HeldSaleDto Map(HeldSale heldSale) =>
        new(heldSale.Id, heldSale.ClientRequestId, heldSale.DisplayCode, heldSale.Label,
            heldSale.Notes, heldSale.CreatedByUserId,
            heldSale.CreatedByUser?.FullName ?? $"#{heldSale.CreatedByUserId}",
            heldSale.CreatedAtUtc, heldSale.UpdatedAtUtc, heldSale.TotalSnapshot,
            heldSale.Lines.Sum(line => line.Quantity),
            heldSale.Lines.OrderBy(line => line.SortOrder).Select(line =>
                new HeldSaleLineDto(line.ProductId, line.ProductCodeSnapshot,
                    line.BarcodeSnapshot, line.ProductNameSnapshot, line.Quantity,
                    line.UnitPriceSnapshot, line.LineTotalSnapshot, line.SortOrder,
                    line.LineNotesSnapshot)).ToArray(),
            false, heldSale.DiscountType, heldSale.RequestedDiscountValue,
            heldSale.DiscountReason, heldSale.ResolvedDiscountAmountSnapshot,
            heldSale.SubtotalSnapshot);

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new AppError(code, message));
}
