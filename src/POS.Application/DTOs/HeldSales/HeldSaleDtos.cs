namespace POS.Application.DTOs.HeldSales;

public sealed record CreateHeldSaleLineRequest(
    int ProductId,
    int Quantity,
    string? Notes = null);

public sealed record CreateHeldSaleRequest(
    Guid ClientRequestId,
    string? Label,
    string? Notes,
    IReadOnlyList<CreateHeldSaleLineRequest> Lines,
    POS.Application.DTOs.Checkout.SalesDiscountRequest? SalesDiscount = null);

public sealed record HeldSaleLineDto(
    int ProductId,
    string ProductCodeSnapshot,
    string? BarcodeSnapshot,
    string ProductNameSnapshot,
    int Quantity,
    long UnitPriceSnapshot,
    long LineTotalSnapshot,
    int SortOrder,
    string? Notes);

public sealed record HeldSaleDto(
    int Id,
    Guid ClientRequestId,
    string DisplayCode,
    string Label,
    string? Notes,
    int CreatedByUserId,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long TotalSnapshot,
    int TotalQuantity,
    IReadOnlyList<HeldSaleLineDto> Lines,
    bool IsIdempotentReplay = false,
    POS.Domain.Enums.SalesDiscountType DiscountType = POS.Domain.Enums.SalesDiscountType.None,
    long RequestedDiscountValue = 0,
    string? DiscountReason = null,
    long ResolvedDiscountAmountSnapshot = 0,
    long SubtotalSnapshot = 0);

public enum HeldSaleResumeLineStatus
{
    Unchanged = 1,
    PriceChanged = 2,
    InsufficientStock = 3,
    Unavailable = 4
}

public sealed record HeldSaleResumeLineDto(
    int ProductId,
    string ProductCodeSnapshot,
    string ProductNameSnapshot,
    int RequestedQuantity,
    long UnitPriceSnapshot,
    string? CurrentProductCode,
    string? CurrentProductName,
    string? CurrentUnitName,
    long? CurrentUnitPrice,
    int? CurrentStock,
    bool TrackInventory,
    bool AllowNegativeStock,
    HeldSaleResumeLineStatus Status,
    string? Warning,
    string? Notes);

public sealed record HeldSaleResumeDto(
    int Id,
    string DisplayCode,
    string Label,
    string? Notes,
    IReadOnlyList<HeldSaleResumeLineDto> Lines,
    POS.Domain.Enums.SalesDiscountType DiscountType = POS.Domain.Enums.SalesDiscountType.None,
    long RequestedDiscountValue = 0,
    string? DiscountReason = null,
    long ResolvedDiscountAmountSnapshot = 0,
    long SubtotalSnapshot = 0,
    long TotalSnapshot = 0,
    long CurrentResolvedDiscountAmount = 0,
    long CurrentTotal = 0,
    bool DiscountRequiresReview = false);
