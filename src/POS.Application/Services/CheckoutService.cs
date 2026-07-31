using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Application.DTOs.Printing;
using POS.Application.Factories;
using POS.Application.Validation;
using POS.Application.Authorization;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Hoàn tất một giao dịch bán hàng bằng một transaction.
///
/// Quy trình:
/// - kiểm tra request;
/// - lấy thu ngân từ session;
/// - tải Product có tracking;
/// - kiểm tra trạng thái và tồn kho;
/// - lấy giá từ Product trong database;
/// - tạo Order snapshot;
/// - tính lại tổng tiền từ dữ liệu database;
/// - đối chiếu số tiền VietQR đã xác nhận;
/// - thanh toán;
/// - trừ kho;
/// - tạo InventoryMovement;
/// - lưu thay đổi;
/// - tạo receipt snapshot bất biến;
/// - commit transaction;
/// - trả kết quả cho Presentation.
///
/// CheckoutService không preview và không gọi máy in.
/// </summary>
public sealed class CheckoutService :
    ICheckoutService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim>
        ProcessLocks = new();

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim>
        PrepareLocks = new();

    private const int
        MaximumOrderCodeAttempts = 10;

    private const string
        InventoryReferenceType = "ORDER";

    private readonly IProductRepository
        _productRepository;

    private readonly IOrderRepository
        _orderRepository;

    private readonly IOrderReceiptSnapshotRepository
        _orderReceiptSnapshotRepository;

    private readonly IInventoryMovementRepository
        _inventoryMovementRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IOrderCodeGenerator
        _orderCodeGenerator;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IClock
        _clock;

    private readonly ILogger<CheckoutService>
        _logger;

    private readonly IReceiptStoreSnapshotProvider?
        _receiptStoreSnapshotProvider;

    private readonly IReceiptSnapshotSerializer
        _receiptSnapshotSerializer;

    private readonly ICheckoutRequestJournalRepository?
        _checkoutJournals;

    private readonly ICheckoutRequestCanonicalizer
        _canonicalizer;

    private readonly IHeldSaleRepository?
        _heldSales;

    private readonly IPermissionService? _permissionService;

    private readonly IPaymentIntentRepository? _paymentIntents;

    public CheckoutService(
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IOrderReceiptSnapshotRepository
            orderReceiptSnapshotRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IUnitOfWork unitOfWork,
        IOrderCodeGenerator orderCodeGenerator,
        ICurrentUserService currentUserService,
        IClock clock,
        ILogger<CheckoutService> logger,
        IReceiptSnapshotSerializer
            receiptSnapshotSerializer,
        IReceiptStoreSnapshotProvider?
            receiptStoreSnapshotProvider = null,
        ICheckoutRequestJournalRepository?
            checkoutJournals = null,
        ICheckoutRequestCanonicalizer?
            canonicalizer = null,
        IHeldSaleRepository?
            heldSales = null,
        IPermissionService? permissionService = null,
        IPaymentIntentRepository? paymentIntents = null)
    {
        _productRepository =
            productRepository ??
            throw new ArgumentNullException(
                nameof(productRepository));

        _orderRepository =
            orderRepository ??
            throw new ArgumentNullException(
                nameof(orderRepository));

        _orderReceiptSnapshotRepository =
            orderReceiptSnapshotRepository ??
            throw new ArgumentNullException(
                nameof(orderReceiptSnapshotRepository));

        _inventoryMovementRepository =
            inventoryMovementRepository ??
            throw new ArgumentNullException(
                nameof(inventoryMovementRepository));

        _unitOfWork =
            unitOfWork ??
            throw new ArgumentNullException(
                nameof(unitOfWork));

        _orderCodeGenerator =
            orderCodeGenerator ??
            throw new ArgumentNullException(
                nameof(orderCodeGenerator));

        _currentUserService =
            currentUserService ??
            throw new ArgumentNullException(
                nameof(currentUserService));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _receiptSnapshotSerializer =
            receiptSnapshotSerializer ??
            throw new ArgumentNullException(
                nameof(receiptSnapshotSerializer));

        _checkoutJournals = checkoutJournals;
        _canonicalizer = canonicalizer ?? new CheckoutRequestCanonicalizer();
        _heldSales = heldSales;
        _permissionService = permissionService;
        _paymentIntents = paymentIntents;

        /*
         * Nullable tạm thời để các unit test cũ đang tự tạo
         * CheckoutService không bị vỡ constructor.
         *
         * Composition root production đã đăng ký provider,
         * nên ứng dụng thật luôn nhận store snapshot cấu hình.
         */
        _receiptStoreSnapshotProvider =
            receiptStoreSnapshotProvider;
    }

    public async Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClientRequestId == Guid.Empty)
            return await CheckoutCoreAsync(request, cancellationToken: cancellationToken);

        var gate = ProcessLocks.GetOrAdd(
            request.ClientRequestId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await CheckoutCoreAsync(request, cancellationToken: cancellationToken);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                ProcessLocks.TryRemove(
                    new KeyValuePair<Guid, SemaphoreSlim>(
                        request.ClientRequestId,
                        gate));
        }
    }

    public async Task<Result<CheckoutResultDto>> RetryConfirmedPaymentIntentAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (_paymentIntents is null || paymentIntentId <= 0 ||
            _currentUserService.UserId is not int actorId)
            return Failure("PAYMENT_INTENT.NOT_FOUND", "Không tìm thấy yêu cầu VietQR.");
        var intent = await _paymentIntents.GetByIdAsync(
            paymentIntentId, tracked: false, cancellationToken);
        if (intent is null || intent.CreatedByUserId != actorId)
            return Failure("PAYMENT_INTENT.NOT_FOUND", "Không tìm thấy yêu cầu VietQR.");
        ConfirmedCheckoutSnapshot snapshot;
        try
        {
            snapshot = ConfirmedCheckoutSnapshotJson.Deserialize(intent.CheckoutRequestJson);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            global::POS.Application.Common.PosLog.Warning(_logger, exception,
                "PaymentIntent {PaymentIntentId} có checkout snapshot không đọc được.",
                paymentIntentId);
            return Failure("PAYMENT_INTENT.SNAPSHOT_MANUAL_REVIEW",
                "Giao dịch đã xác nhận nhận tiền nhưng dữ liệu đơn thuộc phiên bản cũ và không thể tự động hoàn tất. Không yêu cầu khách chuyển thêm. Vui lòng liên hệ quản trị viên.");
        }
        if (snapshot.PaymentIntentId != intent.Id ||
            snapshot.HeldSaleId != intent.HeldSaleId ||
            snapshot.Total != intent.Amount ||
            snapshot.QuoteFingerprint != intent.QuoteFingerprint)
            return Failure("PAYMENT_INTENT.SNAPSHOT_INTEGRITY",
                "Dữ liệu đơn VietQR không khớp yêu cầu thanh toán; cần xử lý thủ công.");
        if (intent.HeldSaleId is int ownedHeldSaleId && _heldSales is not null)
        {
            var ownedHeldSale = await _heldSales.GetByIdAsync(
                ownedHeldSaleId, tracked: false, cancellationToken);
            if (ownedHeldSale?.Status == HeldSaleStatus.Completed &&
                ownedHeldSale.CompletedOrderId != intent.CompletedOrderId)
                return Failure(
                    "PAYMENT_INTENT.CROSS_PAYMENT_CONFLICT",
                    "Đơn giữ liên quan đã được hoàn tất bởi một giao dịch khác. Không thể tự động hoàn tất VietQR mà không có nguy cơ tạo trùng đơn. Không yêu cầu khách chuyển thêm. Vui lòng để quản trị viên xử lý.");
        }
        var request = new CheckoutRequest(
            snapshot.Lines.Select(line => new CheckoutLineRequest(
                line.ProductId, line.Quantity, line.Modifiers,
                line.LineDiscountAmount, line.Notes)),
            PaymentMethod.VietQr, 0, notes: snapshot.Notes,
            confirmedPaymentAmount: snapshot.Total,
            clientRequestId: snapshot.ClientRequestId,
            heldSaleId: snapshot.HeldSaleId,
            salesDiscount: snapshot.SalesDiscount,
            paymentIntentId: intent.Id);
        var gate = ProcessLocks.GetOrAdd(
            snapshot.ClientRequestId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await CheckoutCoreAsync(request, snapshot, cancellationToken);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                ProcessLocks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(
                    snapshot.ClientRequestId, gate));
        }
    }

    private async Task<Result<CheckoutResultDto>> CheckoutCoreAsync(
        CheckoutRequest request,
        ConfirmedCheckoutSnapshot? lockedSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var validation =
            CheckoutValidator.Validate(
                request);

        if (validation.IsFailure)
        {
            return Result.Failure<CheckoutResultDto>(
                validation.AppError);
        }

        if (request.HeldSaleId.HasValue &&
            (request.HeldSaleId.Value <= 0 ||
             request.ClientRequestId == Guid.Empty ||
             _heldSales is null))
        {
            return Failure(
                "CHECKOUT.HELD_SALE_INVALID",
                "Checkout đơn giữ yêu cầu HeldSaleId và durable ClientRequestId hợp lệ.");
        }

        var cashierUserId =
            _currentUserService.UserId;

        var cashierName =
            _currentUserService.FullName;

        if (!cashierUserId.HasValue ||
            cashierUserId.Value <= 0 ||
            string.IsNullOrWhiteSpace(
                cashierName))
        {
            return Failure(
                ErrorCodes.General.Unauthorized,
                "Không tìm thấy phiên đăng nhập hợp lệ.");
        }

        if (request.SalesDiscount.Type != SalesDiscountType.None &&
            (_permissionService is null ||
             !_permissionService.HasPermission(SystemCapability.ApplySalesDiscount)))
            return Failure(
                ErrorCodes.General.Forbidden,
                "Bạn không có quyền áp dụng giảm giá bán hàng.");

        /*
         * The durable checkout boundary starts only after manual VietQR
         * confirmation.  This check must happen before PrepareCheckoutAsync:
         * otherwise a Created/Presented intent leaves a Prepared journal that
         * startup recovery mistakes for an orphaned checkout.
         *
         * The same state is checked again inside the business transaction
         * below, so a concurrent transition cannot bypass checkout safety.
         */
        var paymentBoundary = await ValidatePaymentIntentCheckoutBoundaryAsync(
            request,
            cashierUserId.Value,
            cancellationToken);
        if (paymentBoundary.IsFailure)
            return Result.Failure<CheckoutResultDto>(paymentBoundary.AppError);

        CheckoutRequestJournal? checkoutJournal = null;
        if (request.ClientRequestId != Guid.Empty &&
            _checkoutJournals is not null)
        {
            var canonical = _canonicalizer.Canonicalize(request);
            checkoutJournal = await _checkoutJournals.GetTrackedAsync(
                request.ClientRequestId, cancellationToken);

            if (checkoutJournal is null)
            {
                var preparation = lockedSnapshot is null
                    ? await PrepareCheckoutAsync(request, cancellationToken)
                    : await PrepareCoreAsync(request, lockedSnapshot, cancellationToken);
                if (preparation.IsFailure)
                    return Result.Failure<CheckoutResultDto>(preparation.AppError);
                checkoutJournal = await _checkoutJournals.GetTrackedAsync(
                    request.ClientRequestId, cancellationToken);
            }

            if (checkoutJournal is null)
                return Failure("CHECKOUT.JOURNAL_MISSING", "Không thể tải checkout đã chuẩn bị.");
            if (checkoutJournal.PreparedByUserId != cashierUserId.Value)
                return Failure(ErrorCodes.General.Unauthorized, "Checkout không thuộc phiên người dùng hiện tại.");
            if (!string.Equals(
                checkoutJournal.RequestFingerprint, canonical.Fingerprint, StringComparison.Ordinal))
                return Failure("CHECKOUT.IDEMPOTENCY_CONFLICT", "ClientRequestId đã được dùng cho payload khác.");
            if (checkoutJournal.Status == CheckoutRequestStatus.Abandoned)
                return Failure("CHECKOUT.ABANDONED", "Checkout này đã bị bỏ và không thể dùng lại.");
            if (checkoutJournal.Status == CheckoutRequestStatus.Completed)
                return await ReplayAsync(checkoutJournal, cancellationToken);
        }

        var utcNow =
            _clock.UtcNow.ToUniversalTime();

        await using var transaction =
            await _unitOfWork.BeginTransactionAsync(
                cancellationToken);

        try
        {
            HeldSale? heldSale = null;
            PaymentIntent? paymentIntent = null;
            if (checkoutJournal is not null)
            {
                await _checkoutJournals!.ReloadTrackedAsync(
                    checkoutJournal,
                    cancellationToken);

                if (checkoutJournal.PreparedByUserId != cashierUserId.Value)
                    return Failure(ErrorCodes.General.Unauthorized, "Checkout không thuộc phiên người dùng hiện tại.");
                if (checkoutJournal.Status == CheckoutRequestStatus.Completed)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return await ReplayAsync(checkoutJournal, cancellationToken);
                }
                if (checkoutJournal.Status != CheckoutRequestStatus.Prepared)
                    return Failure("CHECKOUT.ABANDONED", "Checkout này không còn có thể xử lý.");
                var transactionCanonical = _canonicalizer.Canonicalize(request);
                if (!string.Equals(
                    checkoutJournal.RequestFingerprint,
                    transactionCanonical.Fingerprint,
                    StringComparison.Ordinal))
                    return Failure("CHECKOUT.IDEMPOTENCY_CONFLICT", "ClientRequestId đã được dùng cho payload khác.");
            }

            if (request.HeldSaleId is int heldSaleId)
            {
                heldSale = await _heldSales!.GetByIdAsync(
                    heldSaleId,
                    tracked: true,
                    cancellationToken);

                if (heldSale is null ||
                    heldSale.CreatedByUserId != cashierUserId.Value)
                {
                    return Failure(
                        "CHECKOUT.HELD_SALE_NOT_FOUND",
                        "Không tìm thấy đơn đang giữ thuộc phiên người dùng hiện tại.");
                }

                await _heldSales.ReloadTrackedAsync(
                    heldSale,
                    cancellationToken);

                if (heldSale.Status != HeldSaleStatus.Active ||
                    heldSale.CompletedOrderId.HasValue)
                {
                    return Failure(
                        "CHECKOUT.HELD_SALE_NOT_ACTIVE",
                        "Đơn giữ đã được hoàn tất hoặc hủy bởi phiên khác.");
                }
                if (_paymentIntents is not null)
                {
                    var activeOwner = await _paymentIntents.GetActiveByHeldSaleIdAsync(
                        heldSaleId, tracked: false, cancellationToken);
                    if (activeOwner is not null &&
                        request.PaymentIntentId != activeOwner.Id)
                        return Failure(
                            "CHECKOUT.HELD_SALE_PAYMENT_OWNED",
                            HeldSalePaymentOwnershipPolicy.LockedMessage);
                }
            }

            if (request.PaymentMethod == PaymentMethod.VietQr && _paymentIntents is not null)
            {
                if (request.PaymentIntentId is not int paymentIntentId || paymentIntentId <= 0)
                    return Failure("PAYMENT_INTENT.REQUIRED", "Thanh toán VietQR yêu cầu PaymentIntentId.");

                paymentIntent = await _paymentIntents.GetByIdAsync(
                    paymentIntentId, tracked: true, cancellationToken);
                if (paymentIntent is null ||
                    paymentIntent.CreatedByUserId != cashierUserId.Value)
                    return Failure("PAYMENT_INTENT.NOT_FOUND", "Không tìm thấy yêu cầu VietQR thuộc phiên hiện tại.");

                await _paymentIntents.ReloadTrackedAsync(paymentIntent, cancellationToken);
                if (paymentIntent.Status == PaymentIntentStatus.Completed)
                    return Failure("PAYMENT_INTENT.ALREADY_COMPLETED", "Yêu cầu VietQR đã hoàn tất bởi đơn khác.");
                if (paymentIntent.Status != PaymentIntentStatus.Confirmed)
                    return Failure("PAYMENT_INTENT.NOT_CONFIRMED", "Yêu cầu VietQR chưa được thu ngân xác nhận.");
                if (paymentIntent.HeldSaleId != request.HeldSaleId)
                    return Failure("PAYMENT_INTENT.HELD_SALE_MISMATCH", "Yêu cầu VietQR không khớp đơn đang giữ.");
            }

            /*
             * Gom tổng số lượng theo ProductId trước khi
             * thay đổi entity để xử lý đúng các dòng trùng sản phẩm.
             */
            var requestedQuantities =
                BuildRequestedQuantities(
                    request);

            var products =
                new Dictionary<int, Product>();

            foreach (var requestedProduct in
                     requestedQuantities)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var product =
                    await _productRepository
                        .GetByIdAsync(
                            requestedProduct.Key,
                            cancellationToken);

                if (product is not null &&
                    checkoutJournal is not null)
                {
                    await _productRepository.ReloadTrackedAsync(
                        product,
                        cancellationToken);
                }

                if (product is null)
                {
                    return Failure(
                        ErrorCodes.Checkout.ProductNotFound,
                        $"Không tìm thấy sản phẩm có mã " +
                        $"{requestedProduct.Key}.");
                }

                if (product.IsArchived)
                {
                    if (lockedSnapshot is null)
                        return Failure(
                            ErrorCodes.Products.Archived,
                            $"Sản phẩm '{product.Name}' đã được lưu trữ " +
                            "và không thể thanh toán.");
                }

                if (!product.IsActive && lockedSnapshot is null)
                {
                    return Failure(
                        ErrorCodes.Checkout.ProductInactive,
                        $"Sản phẩm '{product.Name}' đã ngừng bán.");
                }

                if (!product.CanFulfill(
                        requestedProduct.Value))
                {
                    return Failure(
                        ErrorCodes.Checkout.InsufficientStock,
                        $"Sản phẩm '{product.Name}' không đủ tồn kho. " +
                        $"Tồn hiện tại: {product.StockQuantity:N0}, " +
                        $"yêu cầu: {requestedProduct.Value:N0}.");
                }

                products.Add(
                    product.Id,
                    product);
            }

            var currentQuote = lockedSnapshot is null
                ? CreatePreparedQuote(request, products)
                : CreatePreparedQuote(lockedSnapshot, products);
            var currentQuoteFingerprint = CheckoutRequestCanonicalizer.Hash(currentQuote);

            if (checkoutJournal is not null)
            {
                if (!string.Equals(
                    checkoutJournal.PreparedQuoteFingerprint,
                    currentQuoteFingerprint,
                    StringComparison.Ordinal))
                    return Failure(
                        "CHECKOUT.PREPARATION_STALE",
                        "Giá hoặc kết quả checkout đã thay đổi. Vui lòng kiểm tra và tạo giao dịch mới.");
            }

            if (paymentIntent is not null &&
                !string.Equals(paymentIntent.QuoteFingerprint, currentQuoteFingerprint, StringComparison.Ordinal))
                return Failure(
                    "PAYMENT_INTENT.STALE",
                    "Thông tin đơn hàng đã thay đổi. Hãy tạo mã VietQR mới.");

            var orderCodeResult =
                await GenerateUniqueOrderCodeAsync(
                    utcNow,
                    cancellationToken);

            if (orderCodeResult.IsFailure)
            {
                return Result.Failure<CheckoutResultDto>(
                    orderCodeResult.AppError);
            }

            var order =
                new Order(
                    orderCode:
                        orderCodeResult.Value,

                    cashierUserId:
                        cashierUserId.Value,

                    utcNow:
                        utcNow,

                    customerId:
                        null,

                    restaurantTableId:
                        null,

                    notes:
                        request.Notes);

            /*
             * Giá vốn và giá bán luôn lấy từ Product đã đọc
             * trong database, không dùng giá từ giao diện.
             */
            foreach (var requestedLine in
                     request.Lines)
            {
                var product =
                    products[
                        requestedLine.ProductId];
                var lockedLine = lockedSnapshot?.Lines.First(line =>
                    line.ProductId == requestedLine.ProductId);

                var orderItem =
                    order.AddItem(
                        productId:
                            product.Id,

                        productCode:
                            lockedLine?.ProductCode ?? product.Code,

                        productName:
                            lockedLine?.ProductName ?? product.Name,

                        unitName:
                            lockedLine?.UnitName ?? product.UnitName,

                        quantity:
                            requestedLine.Quantity,

                        unitCostPrice:
                            product.CostPrice,

                        unitSalePrice:
                            lockedLine?.UnitPrice
                            ?? product.SalePrice,

                        utcNow:
                            utcNow,

                        notes:
                            requestedLine.Notes);

                /*
                 * Validator đang bắt buộc giá trị này bằng 0.
                 * Giữ nhánh code để contract sẵn sàng cho
                 * discount policy trong tương lai.
                 */
                if (requestedLine.LineDiscountAmount >
                    0)
                {
                    order.ApplyItemDiscount(
                        orderItem,
                        requestedLine
                            .LineDiscountAmount,
                        utcNow);
                }
            }

            if (request.SalesDiscount.Type != SalesDiscountType.None)
                order.ApplySalesDiscount(
                    request.SalesDiscount.Type,
                    request.SalesDiscount.Value,
                    request.SalesDiscount.Reason!,
                    cashierUserId.Value,
                    utcNow);

            /*
             * PrepareForPayment tính lại toàn bộ:
             * - Subtotal;
             * - DiscountAmount;
             * - TotalAmount.
             *
             * Đây là tổng tiền tin cậy được xây từ Product
             * vừa đọc trong database.
             */
            order.PrepareForPayment(
                utcNow);

            /*
             * =================================================
             * VIETQR CONFIRMED AMOUNT GATE
             * =================================================
             *
             * Phải kiểm tra trước:
             * - MarkPaid;
             * - giảm tồn kho;
             * - tạo InventoryMovement;
             * - Add Order;
             * - SaveChanges;
             * - Commit.
             *
             * Sai dù chỉ một đồng cũng dừng giao dịch.
             */
            var confirmedAmountValidation =
                ValidateConfirmedPaymentAmount(
                    request,
                    order.TotalAmount);

            if (confirmedAmountValidation.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Checkout VietQR bị dừng do số tiền " +
                    "không khớp. CashierUserId: {CashierUserId}, " +
                    "OrderCode: {OrderCode}, " +
                    "ConfirmedAmount: {ConfirmedAmount}, " +
                    "ActualTotalAmount: {ActualTotalAmount}",
                    cashierUserId.Value,
                    order.OrderCode,
                    request.ConfirmedPaymentAmount,
                    order.TotalAmount);

                return Result.Failure<
                    CheckoutResultDto>(
                        confirmedAmountValidation
                            .AppError);
            }

            if (paymentIntent is not null && paymentIntent.Amount != order.TotalAmount)
                return Failure(
                    "PAYMENT_INTENT.AMOUNT_MISMATCH",
                    "Số tiền VietQR không khớp tổng thanh toán hiện tại.");

            order.MarkPaid(
                request.PaymentMethod,
                request.CashReceived,
                utcNow);

            order.Complete(
                utcNow);

            /*
             * Chỉ trừ tồn sau khi Order đã vượt qua:
             * - validation giá;
             * - đối chiếu số tiền VietQR;
             * - validation thanh toán của Domain.
             */
            foreach (var requestedProduct in
                     requestedQuantities)
            {
                var product =
                    products[
                        requestedProduct.Key];

                if (!product.TrackInventory)
                {
                    continue;
                }

                var quantityBefore =
                    product.StockQuantity;

                product.DecreaseStock(
                    requestedProduct.Value,
                    utcNow);

                var movement =
                    new InventoryMovement(
                        productId:
                            product.Id,

                        movementType:
                            InventoryMovementType.Sale,

                        quantityDelta:
                            -requestedProduct.Value,

                        quantityBefore:
                            quantityBefore,

                        quantityAfter:
                            product.StockQuantity,

                        reason:
                            $"Bán hàng {order.OrderCode}",

                        occurredAtUtc:
                            utcNow,

                        referenceType:
                            InventoryReferenceType,

                        referenceId:
                            order.OrderCode,

                        performedByUserId:
                            cashierUserId.Value);

                await _inventoryMovementRepository
                    .AddAsync(
                        movement,
                        cancellationToken);
            }

            await _orderRepository.AddAsync(
                order,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            /*
             * Sau SaveChanges:
             * - Order và OrderItem đã có database identity;
             * - toàn bộ giá, tên và số tiền đã được chốt;
             * - transaction vẫn chưa commit.
             *
             * Nếu tạo receipt snapshot thất bại,
             * code sẽ đi vào catch và DisposeAsync của transaction
             * sẽ rollback Order, tồn kho và InventoryMovement.
             */
            var checkoutResult =
                CreateResult(
                    order,
                    cashierName,
                    request.SalesDiscount) with
                {
                    CheckoutClientRequestId = request.ClientRequestId,
                    PaymentIntentId = paymentIntent?.Id,
                    PaymentIntentDisplayCode = paymentIntent?.DisplayCode,
                    PaymentConfirmedAtUtc = paymentIntent?.ConfirmedAtUtc
                };

            var receiptSnapshot =
                CreateReceiptSnapshot(
                    checkoutResult,
                    request.Notes);

            if (receiptSnapshot.OrderId !=
                order.Id)
            {
                throw new InvalidOperationException(
                    "Receipt snapshot OrderId does not match " +
                    "the persisted order.");
            }

            checkoutResult =
                checkoutResult with
                {
                    ReceiptSnapshot =
                        receiptSnapshot
                };

            var payloadJson =
                _receiptSnapshotSerializer.Serialize(
                    receiptSnapshot);

            var persistedSnapshot =
                new OrderReceiptSnapshot(
                    orderId:
                        order.Id,

                    snapshotVersion:
                        receiptSnapshot.SnapshotVersion,

                    payloadJson:
                        payloadJson,

                    createdAtUtc:
                        utcNow);

            await _orderReceiptSnapshotRepository
                .AddAsync(
                    persistedSnapshot,
                    cancellationToken);

            checkoutJournal?.Complete(
                order.Id,
                utcNow);

            heldSale?.Complete(
                order.Id,
                utcNow);

            paymentIntent?.Complete(
                order.Id,
                utcNow);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Result.Success(
                checkoutResult);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException exception)
        {
            return Result.Failure<
                CheckoutResultDto>(
                    new AppError(
                        exception.Code,
                        exception.Message));
        }
        catch (PersistenceConflictException exception)
        {
            global::POS.Application.Common.PosLog.Warning(_logger,
                exception,
                "Checkout gặp xung đột persistence. " +
                "CashierUserId: {CashierUserId}, Kind: {Kind}, " +
                "Target: {Target}",
                cashierUserId.Value,
                exception.Kind,
                exception.Target);

            return MapPersistenceConflict(
                exception);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger,
                exception,
                "Checkout không thể hoàn thành. " +
                "CashierUserId: {CashierUserId}",
                cashierUserId.Value);

            return Failure(
                ErrorCodes.Checkout.SaveFailed,
                "Không thể lưu giao dịch bán hàng. " +
                "Không có dữ liệu dở dang được ghi nhận.");
        }
    }

    public async Task<Result<CheckoutPreparationDto>> PrepareCheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClientRequestId == Guid.Empty)
            return await PrepareCoreAsync(request, cancellationToken: cancellationToken);
        var gate = PrepareLocks.GetOrAdd(
            request.ClientRequestId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await PrepareCoreAsync(request, cancellationToken: cancellationToken);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                PrepareLocks.TryRemove(
                    new KeyValuePair<Guid, SemaphoreSlim>(
                        request.ClientRequestId,
                        gate));
        }
    }

    private async Task<Result<CheckoutPreparationDto>> PrepareCoreAsync(
        CheckoutRequest request,
        ConfirmedCheckoutSnapshot? lockedSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = CheckoutValidator.Validate(request);
        if (validation.IsFailure)
            return Result.Failure<CheckoutPreparationDto>(validation.AppError);
        if (request.ClientRequestId == Guid.Empty)
            return Result.Failure<CheckoutPreparationDto>(
                new AppError("CHECKOUT.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ."));
        if (_checkoutJournals is null)
            return Result.Failure<CheckoutPreparationDto>(
                new AppError("CHECKOUT.JOURNAL_UNAVAILABLE", "Durable checkout journal chưa được cấu hình."));
        if (_currentUserService.UserId is not int actorId || actorId <= 0)
            return Result.Failure<CheckoutPreparationDto>(
                new AppError(ErrorCodes.General.Unauthorized, "Không tìm thấy phiên đăng nhập hợp lệ."));
        if (request.SalesDiscount.Type != SalesDiscountType.None &&
            (_permissionService is null ||
             !_permissionService.HasPermission(SystemCapability.ApplySalesDiscount)))
            return Result.Failure<CheckoutPreparationDto>(
                new AppError(ErrorCodes.General.Forbidden, "Bạn không có quyền áp dụng giảm giá bán hàng."));

        var paymentBoundary = await ValidatePaymentIntentCheckoutBoundaryAsync(
            request,
            actorId,
            cancellationToken);
        if (paymentBoundary.IsFailure)
            return Result.Failure<CheckoutPreparationDto>(paymentBoundary.AppError);

        var canonical = _canonicalizer.Canonicalize(request);
        var existing = await _checkoutJournals.GetTrackedAsync(request.ClientRequestId, cancellationToken);
        if (existing is not null)
            return MapPreparation(existing, canonical.Fingerprint);

        var products = new Dictionary<int, Product>();
        foreach (var productId in request.Lines.Select(line => line.ProductId).Distinct().Order())
        {
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            if (product is null ||
                (lockedSnapshot is null && (product.IsArchived || !product.IsActive)))
                return Result.Failure<CheckoutPreparationDto>(
                    new AppError(ErrorCodes.Checkout.ProductNotFound, $"Sản phẩm {productId} không khả dụng."));
            products.Add(productId, product);
        }

        var quoteJson = lockedSnapshot is null
            ? CreatePreparedQuote(request, products)
            : CreatePreparedQuote(lockedSnapshot, products);
        var journal = new CheckoutRequestJournal(
            request.ClientRequestId, canonical.Fingerprint, canonical.Json,
            CheckoutRequestCanonicalizer.Hash(quoteJson), quoteJson, actorId, _clock.UtcNow);
        try
        {
            await using var transaction =
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _checkoutJournals.AddAsync(journal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(ToPreparation(journal));
        }
        catch (PersistenceConflictException)
        {
            var winner = await _checkoutJournals.GetReadOnlyAsync(request.ClientRequestId, cancellationToken);
            return winner is null
                ? Result.Failure<CheckoutPreparationDto>(
                    new AppError("CHECKOUT.PREPARE_CONFLICT", "Không thể chuẩn bị checkout."))
                : MapPreparation(winner, canonical.Fingerprint);
        }
    }

    public async Task<Result<IReadOnlyList<CheckoutRecoveryDto>>> GetCheckoutRecoveryAsync(
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        if (_checkoutJournals is null || _currentUserService.UserId is not int actorId || actorId <= 0)
            return Result.Failure<IReadOnlyList<CheckoutRecoveryDto>>(
                new AppError(ErrorCodes.General.Unauthorized, "Không tìm thấy phiên đăng nhập hợp lệ."));
        var journals = await _checkoutJournals.GetActiveRecoveryAsync(actorId, limit, cancellationToken);
        var recoveries = new List<CheckoutRecoveryDto>(journals.Count);
        foreach (var journal in journals)
        {
            var preparedRequest = journal.Status == CheckoutRequestStatus.Prepared
                ? _canonicalizer.Deserialize(
                    journal.CanonicalRequestJson,
                    journal.ClientRequestId)
                : null;
            PaymentIntent? paymentIntent = null;
            if (preparedRequest?.PaymentIntentId is int paymentIntentId &&
                _paymentIntents is not null)
                paymentIntent = await _paymentIntents.GetByIdAsync(
                    paymentIntentId,
                    tracked: false,
                    cancellationToken);
            else if (journal.Status == CheckoutRequestStatus.Completed &&
                     journal.OrderId is int completedOrderId &&
                     _paymentIntents is not null)
                paymentIntent = await _paymentIntents.GetByCompletedOrderIdAsync(
                    completedOrderId,
                    cancellationToken);
            recoveries.Add(MapRecovery(
                journal,
                preparedRequest,
                paymentIntent));
        }
        return Result.Success<IReadOnlyList<CheckoutRecoveryDto>>(
            recoveries);
    }

    public Task<Result> AcknowledgeCheckoutAsync(
        Guid clientRequestId, CancellationToken cancellationToken = default) =>
        ChangeJournalWithLockAsync(clientRequestId, abandon: false, cancellationToken);

    public Task<Result> AbandonCheckoutAsync(
        Guid clientRequestId, CancellationToken cancellationToken = default) =>
        ChangeJournalWithLockAsync(clientRequestId, abandon: true, cancellationToken);

    private async Task<Result> ChangeJournalWithLockAsync(
        Guid clientRequestId,
        bool abandon,
        CancellationToken cancellationToken)
    {
        if (clientRequestId == Guid.Empty)
            return Result.Failure(new AppError(
                "CHECKOUT.REQUEST_ID_REQUIRED",
                "ClientRequestId không hợp lệ."));
        var gate = ProcessLocks.GetOrAdd(
            clientRequestId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ChangeJournalAsync(
                clientRequestId,
                abandon,
                cancellationToken);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                ProcessLocks.TryRemove(
                    new KeyValuePair<Guid, SemaphoreSlim>(
                        clientRequestId,
                        gate));
        }
    }

    private async Task<Result> ChangeJournalAsync(
        Guid clientRequestId, bool abandon, CancellationToken cancellationToken)
    {
        if (_checkoutJournals is null || _currentUserService.UserId is not int actorId || actorId <= 0)
            return Result.Failure(new AppError(ErrorCodes.General.Unauthorized, "Không tìm thấy phiên đăng nhập hợp lệ."));
        if (clientRequestId == Guid.Empty)
            return Result.Failure(new AppError("CHECKOUT.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ."));
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var journal = await _checkoutJournals.GetTrackedAsync(clientRequestId, cancellationToken);
            if (journal is null || journal.PreparedByUserId != actorId)
                return Result.Failure(new AppError("CHECKOUT.JOURNAL_NOT_FOUND", "Không tìm thấy checkout."));
            if (abandon)
            {
                var preparedRequest = _canonicalizer.Deserialize(
                    journal.CanonicalRequestJson,
                    journal.ClientRequestId);
                if (preparedRequest.PaymentIntentId is int paymentIntentId &&
                    _paymentIntents is not null)
                {
                    var paymentIntent = await _paymentIntents.GetByIdAsync(
                        paymentIntentId,
                        tracked: false,
                        cancellationToken);
                    if (paymentIntent?.Status is
                        PaymentIntentStatus.Confirmed or
                        PaymentIntentStatus.Completed)
                        return Result.Failure(new AppError(
                            "CHECKOUT.CONFIRMED_PAYMENT_CANNOT_ABANDON",
                            "Đã xác nhận nhận tiền nhưng đơn hàng chưa hoàn tất. Hãy thử hoàn tất đơn."));
                }
                journal.Abandon(actorId, _clock.UtcNow);
            }
            else
                journal.Acknowledge(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Failure(new AppError(exception.Code, exception.Message));
        }
    }

    private static Result<CheckoutPreparationDto> MapPreparation(
        CheckoutRequestJournal journal, string fingerprint)
    {
        if (!string.Equals(journal.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return Result.Failure<CheckoutPreparationDto>(
                new AppError("CHECKOUT.IDEMPOTENCY_CONFLICT", "ClientRequestId đã được dùng cho payload khác."));
        if (journal.Status == CheckoutRequestStatus.Abandoned)
            return Result.Failure<CheckoutPreparationDto>(
                new AppError("CHECKOUT.ABANDONED", "Checkout này đã bị bỏ và không thể dùng lại."));
        return Result.Success(ToPreparation(journal));
    }

    private async Task<Result> ValidatePaymentIntentCheckoutBoundaryAsync(
        CheckoutRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        if (_paymentIntents is null)
            return Result.Success();

        if (request.HeldSaleId is int heldSaleId)
        {
            var activeOwner = await _paymentIntents.GetActiveByHeldSaleIdAsync(
                heldSaleId, tracked: false, cancellationToken);
            if (request.PaymentIntentId is null && activeOwner is not null)
            {
                var message = activeOwner.Status == PaymentIntentStatus.Confirmed
                    ? "Đơn giữ đã xác nhận nhận tiền VietQR; không thể thanh toán bằng phương thức khác. Vui lòng retry VietQR hoặc xử lý thủ công."
                    : "Đơn giữ đang thuộc giao dịch VietQR. Hãy hủy giao dịch VietQR trước khi đổi phương thức thanh toán.";
                return Result.Failure(new AppError(
                    "CHECKOUT.HELD_SALE_PAYMENT_OWNED", message));
            }

            if (request.PaymentIntentId is int requestedOwnerId &&
                activeOwner is not null &&
                activeOwner.Id != requestedOwnerId)
                return Result.Failure(new AppError(
                    "PAYMENT_INTENT.HELD_SALE_OWNER_MISMATCH",
                    "Giao dịch VietQR không phải owner của đơn giữ này."));
        }

        if (request.PaymentMethod != PaymentMethod.VietQr)
            return request.PaymentIntentId is null
                ? Result.Success()
                : Result.Failure(new AppError(
                    "PAYMENT_INTENT.METHOD_MISMATCH",
                    "PaymentIntent chỉ được dùng cho checkout VietQR."));

        if (request.PaymentIntentId is not int paymentIntentId ||
            paymentIntentId <= 0)
            return Result.Failure(new AppError(
                "PAYMENT_INTENT.REQUIRED",
                "Thanh toán VietQR yêu cầu PaymentIntentId."));

        var intent = await _paymentIntents.GetByIdAsync(
            paymentIntentId,
            tracked: false,
            cancellationToken);

        if (intent is null || intent.CreatedByUserId != actorId)
            return Result.Failure(new AppError(
                "PAYMENT_INTENT.NOT_FOUND",
                "Không tìm thấy yêu cầu VietQR thuộc phiên hiện tại."));

        if (intent.Status == PaymentIntentStatus.Completed)
        {
            /*
             * A retry of the same durable checkout must still replay its
             * original Order.  A different checkout request is rejected
             * before it can prepare another journal for the completed intent.
             */
            if (_checkoutJournals is not null &&
                intent.CompletedOrderId is int completedOrderId)
            {
                var completedJournal =
                    await _checkoutJournals.GetByOrderIdReadOnlyAsync(
                        completedOrderId,
                        cancellationToken);

                if (completedJournal?.ClientRequestId ==
                    request.ClientRequestId)
                    return Result.Success();
            }

            return Result.Failure(new AppError(
                "PAYMENT_INTENT.ALREADY_COMPLETED",
                "Yêu cầu VietQR đã hoàn tất bởi đơn khác."));
        }

        if (intent.Status != PaymentIntentStatus.Confirmed)
            return Result.Failure(new AppError(
                "PAYMENT_INTENT.NOT_CONFIRMED",
                "Yêu cầu VietQR chưa được thu ngân xác nhận."));

        if (intent.HeldSaleId != request.HeldSaleId)
            return Result.Failure(new AppError(
                "PAYMENT_INTENT.HELD_SALE_MISMATCH",
                "Yêu cầu VietQR không khớp đơn đang giữ."));

        return Result.Success();
    }

    private static CheckoutPreparationDto ToPreparation(CheckoutRequestJournal journal) =>
        new(journal.ClientRequestId, journal.Status, journal.RequestFingerprint,
            journal.PreparedQuoteFingerprint, journal.PreparedQuoteJson, journal.OrderId);

    private static CheckoutRecoveryDto MapRecovery(
        CheckoutRequestJournal journal,
        CheckoutRequest? preparedRequest,
        PaymentIntent? paymentIntent)
    {
        using var quote = JsonDocument.Parse(journal.PreparedQuoteJson);
        var root = quote.RootElement;
        var lines = root.GetProperty("lines").EnumerateArray().Select(line =>
            new CheckoutRecoveryLineDto(
                line.GetProperty("productId").GetInt32(),
                line.GetProperty("productCode").GetString() ?? string.Empty,
                line.GetProperty("productName").GetString() ?? string.Empty,
                line.GetProperty("unitName").GetString() ?? string.Empty,
                line.GetProperty("quantity").GetInt32(),
                line.GetProperty("unitSalePrice").GetInt64(),
                line.GetProperty("lineTotal").GetInt64())).ToArray();
        var prepared = journal.Status == CheckoutRequestStatus.Prepared;
        var hasConfirmedPayment = paymentIntent?.Status is
            PaymentIntentStatus.Confirmed or
            PaymentIntentStatus.Completed;
        return new CheckoutRecoveryDto(
            journal.ClientRequestId,
            journal.Status,
            journal.CreatedAtUtc,
            journal.OrderId,
            journal.Order?.OrderCode,
            root.GetProperty("total").GetInt64(),
            root.GetProperty("paymentMethod").Deserialize<PaymentMethod>(),
            lines,
            preparedRequest,
            prepared,
            prepared && !hasConfirmedPayment)
        {
            PaymentIntentId = preparedRequest?.PaymentIntentId ?? paymentIntent?.Id,
            PaymentIntentStatus = paymentIntent?.Status
        };
    }

    public static string CreatePreparedQuote(
        CheckoutRequest request, IReadOnlyDictionary<int, Product> products)
    {
        var lines = request.Lines.Select(line =>
        {
            var product = products[line.ProductId];
            return new
            {
                productId = product.Id,
                productCode = product.Code,
                productName = product.Name,
                unitName = product.UnitName,
                quantity = line.Quantity,
                unitSalePrice = product.SalePrice,
                lineDiscountAmount = line.LineDiscountAmount,
                lineTotal = checked(product.SalePrice * line.Quantity - line.LineDiscountAmount),
                notes = line.Notes,
                trackInventory = product.TrackInventory,
                modifiers = line.Modifiers.OrderBy(x => x.ModifierId).ThenBy(x => x.Quantity)
            };
        }).OrderBy(line => line.productId).ThenBy(line => line.quantity).ToArray();
        var subtotal = lines.Sum(line => checked(line.unitSalePrice * line.quantity));
        var discountAmount = POS.Domain.Services.SalesDiscountCalculator.Resolve(
            subtotal,
            request.SalesDiscount.Type,
            request.SalesDiscount.Value,
            request.SalesDiscount.Reason);
        var total = checked(subtotal - discountAmount);
        return JsonSerializer.Serialize(new
        {
            version = 1,
            paymentMethod = request.PaymentMethod,
            notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : string.Join(' ', request.Notes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            heldSaleId = request.HeldSaleId,
            cashReceived = request.CashReceived,
            confirmedPaymentAmount = request.ConfirmedPaymentAmount,
            discountCode = request.DiscountCode,
            subtotal,
            salesDiscount = new
            {
                type = request.SalesDiscount.Type,
                value = request.SalesDiscount.Value,
                reason = request.SalesDiscount.Reason,
                resolvedAmount = discountAmount
            },
            discountAmount,
            total,
            change = request.PaymentMethod == PaymentMethod.Cash ? Math.Max(0, request.CashReceived - total) : 0,
            lines
        });
    }

    private static string CreatePreparedQuote(
        ConfirmedCheckoutSnapshot snapshot,
        Dictionary<int, Product> products)
    {
        var lines = snapshot.Lines.Select(line => new
        {
            productId = line.ProductId,
            productCode = line.ProductCode,
            productName = line.ProductName,
            unitName = line.UnitName,
            quantity = line.Quantity,
            unitSalePrice = line.UnitPrice,
            lineDiscountAmount = line.LineDiscountAmount,
            lineTotal = checked(line.UnitPrice * line.Quantity - line.LineDiscountAmount),
            notes = line.Notes,
            trackInventory = products[line.ProductId].TrackInventory,
            modifiers = line.Modifiers.OrderBy(x => x.ModifierId).ThenBy(x => x.Quantity)
        }).OrderBy(line => line.productId).ThenBy(line => line.quantity).ToArray();
        return JsonSerializer.Serialize(new
        {
            version = 1,
            paymentMethod = snapshot.PaymentMethod,
            notes = snapshot.Notes,
            heldSaleId = snapshot.HeldSaleId,
            cashReceived = 0L,
            confirmedPaymentAmount = snapshot.Total,
            discountCode = (string?)null,
            subtotal = snapshot.Subtotal,
            salesDiscount = new
            {
                type = snapshot.SalesDiscount.Type,
                value = snapshot.SalesDiscount.Value,
                reason = snapshot.SalesDiscount.Reason,
                resolvedAmount = snapshot.DiscountAmount
            },
            discountAmount = snapshot.DiscountAmount,
            total = snapshot.Total,
            change = 0L,
            lines
        });
    }

    private async Task<Result<CheckoutResultDto>> ReplayAsync(
        CheckoutRequestJournal journal,
        CancellationToken cancellationToken)
    {
        if (journal.OrderId is not int orderId)
            return Failure("CHECKOUT.REPLAY_INVALID", "Checkout đã hoàn tất nhưng thiếu Order.");

        var order = await _orderRepository.GetByIdReadOnlyAsync(orderId, cancellationToken);
        var snapshot = await _orderReceiptSnapshotRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (order is null || snapshot is null)
            return Failure("CHECKOUT.REPLAY_INVALID", "Không thể phục hồi dữ liệu checkout đã hoàn tất.");

        var receipt = _receiptSnapshotSerializer.Deserialize(snapshot.PayloadJson);
        var result = CreateResult(
            order,
            order.CashierUser?.FullName ?? $"#{order.CashierUserId}") with
        {
            CheckoutClientRequestId = journal.ClientRequestId,
            ReceiptSnapshot = receipt,
            PaymentIntentId = receipt.PaymentIntentId,
            PaymentIntentDisplayCode = receipt.PaymentIntentDisplayCode,
            PaymentConfirmedAtUtc = receipt.PaymentConfirmedAtUtc,
            IsIdempotentReplay = true
        };
        return Result.Success(result);
    }

    private ReceiptRequest CreateReceiptSnapshot(
        CheckoutResultDto checkoutResult,
        string? receiptNotes)
    {
        /*
         * Provider null chỉ xảy ra ở những test cũ tự tạo
         * CheckoutService bằng constructor.
         *
         * Ứng dụng production resolve qua DI và luôn đi qua
         * overload có store snapshot đã cấu hình.
         */
        if (_receiptStoreSnapshotProvider is null)
        {
            return ReceiptSnapshotFactory.Create(
                checkoutResult:
                    checkoutResult,

                receiptNotes:
                    receiptNotes);
        }

        var storeSnapshot =
            _receiptStoreSnapshotProvider
                .GetCurrentSnapshot();

        return ReceiptSnapshotFactory.Create(
            checkoutResult:
                checkoutResult,

            store:
                storeSnapshot,

            receiptNotes:
                receiptNotes);
    }

    private async Task<Result<string>>
        GenerateUniqueOrderCodeAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken)
    {
        for (var attempt = 1;
             attempt <= MaximumOrderCodeAttempts;
             attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var candidate =
                _orderCodeGenerator.Generate(
                    utcNow);

            if (string.IsNullOrWhiteSpace(
                    candidate))
            {
                continue;
            }

            var exists =
                await _orderRepository
                    .CodeExistsAsync(
                        candidate,
                        cancellationToken);

            if (!exists)
            {
                return Result.Success(
                    candidate);
            }
        }

        return Result.Failure<string>(
            new AppError(
                ErrorCodes.Checkout
                    .OrderCodeConflict,

                "Không thể tạo mã đơn hàng duy nhất. " +
                "Vui lòng thử lại."));
    }

    /// <summary>
    /// Đối chiếu số tiền Presentation đã xác nhận với
    /// tổng tiền Application vừa tính lại từ database.
    ///
    /// Cash không dùng ConfirmedPaymentAmount.
    ///
    /// VietQR bắt buộc:
    /// ConfirmedPaymentAmount == actualTotalAmount.
    /// </summary>
    private static Result
        ValidateConfirmedPaymentAmount(
            CheckoutRequest request,
            long actualTotalAmount)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (actualTotalAmount < 0)
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .InvalidAmount,

                    "Tổng tiền đơn hàng không hợp lệ."));
        }

        switch (request.PaymentMethod)
        {
            case PaymentMethod.Cash:

                /*
                 * CheckoutValidator đã bảo đảm:
                 * ConfirmedPaymentAmount == 0.
                 */
                return Result.Success();

            case PaymentMethod.VietQr:

                if (request.ConfirmedPaymentAmount ==
                    actualTotalAmount)
                {
                    return Result.Success();
                }

                return Result.Failure(
                    new AppError(
                        ErrorCodes.Payments
                            .VietQrAmountMismatch,

                        "Số tiền VietQR đã xác nhận không khớp " +
                        "với tổng đơn hiện tại. " +
                        $"Đã xác nhận: " +
                        $"{request.ConfirmedPaymentAmount:N0} ₫; " +
                        $"tổng đơn: {actualTotalAmount:N0} ₫. " +
                        "Giao dịch chưa được lưu."));

            case PaymentMethod.BankTransfer:
            case PaymentMethod.Card:

                return Result.Failure(
                    new AppError(
                        ErrorCodes.Checkout
                            .PaymentMethodNotSupported,

                        "Phương thức thanh toán chưa được hỗ trợ."));

            default:

                return Result.Failure(
                    new AppError(
                        ErrorCodes.Checkout
                            .InvalidPaymentMethod,

                        "Phương thức thanh toán không hợp lệ."));
        }
    }

    private static Dictionary<int, int>
        BuildRequestedQuantities(
            CheckoutRequest request)
    {
        var result =
            new Dictionary<int, int>();

        foreach (var line in
                 request.Lines)
        {
            result.TryGetValue(
                line.ProductId,
                out var currentQuantity);

            int totalQuantity;

            try
            {
                totalQuantity =
                    checked(
                        currentQuantity +
                        line.Quantity);
            }
            catch (OverflowException exception)
            {
                throw new DomainException(
                    ErrorCodes.Checkout
                        .InvalidQuantity,

                    "Tổng số lượng sản phẩm vượt giới hạn.",
                    exception);
            }

            if (totalQuantity >
                POS.Domain.Constants.BusinessRules
                    .Orders.MaximumLineQuantity)
            {
                throw new DomainException(
                    ErrorCodes.Checkout
                        .InvalidQuantity,

                    "Tổng số lượng của một sản phẩm " +
                    "vượt giới hạn.");
            }

            result[line.ProductId] =
                totalQuantity;
        }

        return result;
    }

    private static Result<CheckoutResultDto>
        MapPersistenceConflict(
            PersistenceConflictException exception)
    {
        if (exception.Kind ==
            PersistenceConflictKind.Concurrency)
        {
            return Failure(
                ErrorCodes.Checkout
                    .ConcurrencyConflict,

                "Tồn kho hoặc dữ liệu sản phẩm vừa được thay đổi " +
                "bởi giao dịch khác. Vui lòng tải lại giỏ hàng.");
        }

        if (string.Equals(
                exception.Target,
                PersistenceConflictTargets.OrderCode,
                StringComparison.Ordinal))
        {
            return Failure(
                ErrorCodes.Checkout
                    .OrderCodeConflict,

                "Mã đơn hàng vừa bị trùng. Vui lòng thử lại.");
        }

        return Failure(
            ErrorCodes.Checkout.SaveFailed,
            "Dữ liệu giao dịch bị xung đột với bản ghi hiện có.");
    }

    private static CheckoutResultDto CreateResult(
        Order order,
        string cashierName,
        SalesDiscountRequest? checkoutDiscount = null)
    {
        var paymentMethod =
            order.PaymentMethod ??
            throw new InvalidOperationException(
                "Order đã hoàn tất nhưng thiếu " +
                "phương thức thanh toán.");

        var paidAtUtc =
            order.PaidAtUtc ??
            throw new InvalidOperationException(
                "Order đã hoàn tất nhưng thiếu " +
                "thời điểm thanh toán.");

        var lines =
            order.Items
                .Select(
                    item =>
                        new CheckoutLineResultDto(
                            OrderItemId:
                                item.Id,

                            ProductId:
                                item.ProductId,

                            ProductCode:
                                item.ProductCode,

                            ProductName:
                                item.ProductName,

                            UnitName:
                                item.UnitName,

                            Quantity:
                                item.Quantity,

                            UnitCostPrice:
                                item.UnitCostPrice,

                            UnitSalePrice:
                                item.UnitSalePrice,

                            ModifierAmountPerUnit:
                                item.ModifierAmountPerUnit,

                            FinalUnitPrice:
                                item.FinalUnitPrice,

                            GrossAmount:
                                item.GrossAmount,

                            LineDiscountAmount:
                                item.LineDiscountAmount,

                            NetAmount:
                                item.NetAmount,

                            Notes:
                                item.Notes,

                            Modifiers:
                                item.Modifiers
                                    .Select(
                                        modifier =>
                                            new CheckoutLineModifierResultDto(
                                                ModifierId:
                                                    modifier.ModifierId,

                                                ModifierGroupId:
                                                    modifier.ModifierGroupId,

                                                ModifierGroupName:
                                                    modifier.ModifierGroupName,

                                                ModifierName:
                                                    modifier.ModifierName,

                                                Quantity:
                                                    modifier.Quantity,

                                                UnitAdditionalPrice:
                                                    modifier.UnitAdditionalPrice,

                                                AmountPerProductUnit:
                                                    modifier.AmountPerProductUnit))
                                    .ToArray()))
                .ToArray();

        return new CheckoutResultDto(
            OrderId:
                order.Id,

            OrderCode:
                order.OrderCode,

            CashierUserId:
                order.CashierUserId,

            CashierName:
                cashierName,

            CustomerId:
                order.CustomerId,

            CustomerName:
                null,

            RestaurantTableId:
                order.RestaurantTableId,

            RestaurantTableName:
                null,

            DiscountCode:
                order.DiscountCode,

            Status:
                order.Status,

            PaymentMethod:
                paymentMethod,

            Subtotal:
                order.Subtotal,

            DiscountAmount:
                order.DiscountAmount,

            TotalAmount:
                order.TotalAmount,

            CashReceived:
                order.CashReceived,

            ChangeAmount:
                order.ChangeAmount,

            CreatedAtUtc:
                order.CreatedAtUtc,

            PaidAtUtc:
                paidAtUtc,

            Lines:
                lines)
        {
            SalesDiscountType = checkoutDiscount?.Type ??
                order.DiscountSnapshot?.Type ?? SalesDiscountType.None,
            RequestedDiscountValue = checkoutDiscount?.Value ??
                order.DiscountSnapshot?.RequestedValue ?? 0,
            DiscountReason = checkoutDiscount?.Reason ??
                order.DiscountSnapshot?.Reason,
            DiscountAppliedByUserId = order.DiscountSnapshot?.AppliedByUserId,
            DiscountAppliedAtUtc = order.DiscountSnapshot?.AppliedAtUtc
        };
    }

    private static Result<CheckoutResultDto> Failure(
        string code,
        string message)
    {
        return Result.Failure<CheckoutResultDto>(
            new AppError(
                code,
                message));
    }
}
