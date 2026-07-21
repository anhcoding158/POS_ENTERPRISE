using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.Validation;
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
/// - thanh toán;
/// - trừ kho;
/// - tạo InventoryMovement;
/// - lưu và commit.
/// </summary>
public sealed class CheckoutService :
    ICheckoutService
{
    private const int
        MaximumOrderCodeAttempts = 10;

    private const string
        InventoryReferenceType = "ORDER";

    private readonly IProductRepository
        _productRepository;

    private readonly IOrderRepository
        _orderRepository;

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

    public CheckoutService(
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IUnitOfWork unitOfWork,
        IOrderCodeGenerator orderCodeGenerator,
        ICurrentUserService currentUserService,
        IClock clock,
        ILogger<CheckoutService> logger)
    {
        _productRepository =
            productRepository ??
            throw new ArgumentNullException(
                nameof(productRepository));

        _orderRepository =
            orderRepository ??
            throw new ArgumentNullException(
                nameof(orderRepository));

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
    }

    public async Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        var validation =
            CheckoutValidator.Validate(
                request);

        if (validation.IsFailure)
        {
            return Result.Failure<CheckoutResultDto>(
                validation.Error);
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

        var utcNow =
            _clock.UtcNow.ToUniversalTime();

        await using var transaction =
            await _unitOfWork.BeginTransactionAsync(
                cancellationToken);

        try
        {
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
                    await _productRepository.GetByIdAsync(
                        requestedProduct.Key,
                        cancellationToken);

                if (product is null)
                {
                    return Failure(
                        ErrorCodes.Checkout.ProductNotFound,
                        $"Không tìm thấy sản phẩm có mã " +
                        $"{requestedProduct.Key}.");
                }

                if (!product.IsActive)
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

            var orderCodeResult =
                await GenerateUniqueOrderCodeAsync(
                    utcNow,
                    cancellationToken);

            if (orderCodeResult.IsFailure)
            {
                return Result.Failure<CheckoutResultDto>(
                    orderCodeResult.Error);
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
            foreach (var requestedLine in request.Lines)
            {
                var product =
                    products[
                        requestedLine.ProductId];

                var orderItem =
                    order.AddItem(
                        productId:
                            product.Id,

                        productCode:
                            product.Code,

                        productName:
                            product.Name,

                        unitName:
                            product.UnitName,

                        quantity:
                            requestedLine.Quantity,

                        unitCostPrice:
                            product.CostPrice,

                        unitSalePrice:
                            product.SalePrice,

                        utcNow:
                            utcNow,

                        notes:
                            requestedLine.Notes);

                /*
                 * Validator đang bắt buộc giá trị này bằng 0.
                 * Giữ nhánh code để contract sẵn sàng cho
                 * discount policy trong tương lai.
                 */
                if (requestedLine.LineDiscountAmount > 0)
                {
                    order.ApplyItemDiscount(
                        orderItem,
                        requestedLine.LineDiscountAmount,
                        utcNow);
                }
            }

            order.PrepareForPayment(
                utcNow);

            order.MarkPaid(
                request.PaymentMethod,
                request.CashReceived,
                utcNow);

            order.Complete(
                utcNow);

            /*
             * Chỉ trừ tồn sau khi Order đã vượt qua toàn bộ
             * validation giá và thanh toán.
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

                await _inventoryMovementRepository.AddAsync(
                    movement,
                    cancellationToken);
            }

            await _orderRepository.AddAsync(
                order,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Result.Success(
                CreateResult(
                    order,
                    cashierName));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException exception)
        {
            return Result.Failure<CheckoutResultDto>(
                new Error(
                    exception.Code,
                    exception.Message));
        }
        catch (PersistenceConflictException exception)
        {
            _logger.LogWarning(
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
            _logger.LogError(
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
                await _orderRepository.CodeExistsAsync(
                    candidate,
                    cancellationToken);

            if (!exists)
            {
                return Result.Success(
                    candidate);
            }
        }

        return Result.Failure<string>(
            new Error(
                ErrorCodes.Checkout.OrderCodeConflict,
                "Không thể tạo mã đơn hàng duy nhất. " +
                "Vui lòng thử lại."));
    }

    private static Dictionary<int, int>
        BuildRequestedQuantities(
            CheckoutRequest request)
    {
        var result =
            new Dictionary<int, int>();

        foreach (var line in request.Lines)
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
                    ErrorCodes.Checkout.InvalidQuantity,
                    "Tổng số lượng sản phẩm vượt giới hạn.",
                    exception);
            }

            if (totalQuantity >
                POS.Domain.Constants.BusinessRules
                    .Orders.MaximumLineQuantity)
            {
                throw new DomainException(
                    ErrorCodes.Checkout.InvalidQuantity,
                    "Tổng số lượng của một sản phẩm vượt giới hạn.");
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
                ErrorCodes.Checkout.ConcurrencyConflict,
                "Tồn kho hoặc dữ liệu sản phẩm vừa được thay đổi " +
                "bởi giao dịch khác. Vui lòng tải lại giỏ hàng.");
        }

        if (string.Equals(
                exception.Target,
                PersistenceConflictTargets.OrderCode,
                StringComparison.Ordinal))
        {
            return Failure(
                ErrorCodes.Checkout.OrderCodeConflict,
                "Mã đơn hàng vừa bị trùng. Vui lòng thử lại.");
        }

        return Failure(
            ErrorCodes.Checkout.SaveFailed,
            "Dữ liệu giao dịch bị xung đột với bản ghi hiện có.");
    }

    private static CheckoutResultDto CreateResult(
        Order order,
        string cashierName)
    {
        var paymentMethod =
            order.PaymentMethod ??
            throw new InvalidOperationException(
                "Order đã hoàn tất nhưng thiếu phương thức thanh toán.");

        var paidAtUtc =
            order.PaidAtUtc ??
            throw new InvalidOperationException(
                "Order đã hoàn tất nhưng thiếu thời điểm thanh toán.");

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
                lines);
    }

    private static Result<CheckoutResultDto> Failure(
        string code,
        string message)
    {
        return Result.Failure<CheckoutResultDto>(
            new Error(
                code,
                message));
    }
}