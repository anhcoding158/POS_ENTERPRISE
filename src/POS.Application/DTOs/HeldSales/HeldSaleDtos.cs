namespace POS.Application.DTOs.HeldSales;

public sealed record CreateHeldSaleLineRequest(
    int ProductId,
    int Quantity,
    string? Notes = null);

public sealed record CreateHeldSaleRequest(
    Guid ClientRequestId,
    string? Label,
    string? Notes,
    IReadOnlyList<CreateHeldSaleLineRequest> Lines);

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
    bool IsIdempotentReplay = false);

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
    IReadOnlyList<HeldSaleResumeLineDto> Lines);
