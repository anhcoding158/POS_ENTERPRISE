namespace POS.Application.DTOs.Products;

/// <summary>
/// Read model tối thiểu cho quầy bán hàng.
/// Không chứa giá vốn hoặc lợi nhuận.
/// </summary>
public sealed record SalesCatalogProductDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Code,
    string? Barcode,
    string Name,
    string UnitName,
    long SalePrice,
    int StockQuantity,
    int MinimumStock,
    bool TrackInventory,
    bool AllowNegativeStock,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsActive,
    bool IsArchived)
{
    public string? ImagePath { get; init; }
}
