namespace POS.Application.DTOs.Products;

/// <summary>
/// Thông tin đầy đủ của một sản phẩm.
/// </summary>
public sealed record ProductDetailsDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Code,
    string? Barcode,
    string Name,
    string? Description,
    string UnitName,
    string? ImagePath,
    long CostPrice,
    long SalePrice,
    long ProfitPerUnit,
    int StockQuantity,
    int MinimumStock,
    bool TrackInventory,
    bool AllowNegativeStock,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);