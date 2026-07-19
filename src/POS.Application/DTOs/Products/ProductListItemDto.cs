namespace POS.Application.DTOs.Products;

/// <summary>
/// Dữ liệu sản phẩm dùng cho bảng danh sách,
/// tìm kiếm và màn hình chọn sản phẩm bán hàng.
/// </summary>
public sealed record ProductListItemDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Code,
    string? Barcode,
    string Name,
    string UnitName,
    long CostPrice,
    long SalePrice,
    long ProfitPerUnit,
    int StockQuantity,
    int MinimumStock,
    bool TrackInventory,
    bool AllowNegativeStock,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsActive);