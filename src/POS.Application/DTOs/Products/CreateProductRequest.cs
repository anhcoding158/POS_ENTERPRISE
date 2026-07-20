namespace POS.Application.DTOs.Products;

/// <summary>
/// Dữ liệu tạo sản phẩm mới.
///
/// DTO chỉ chuẩn hóa dữ liệu văn bản.
/// Các quy tắc nghiệp vụ chính thức vẫn được kiểm tra
/// trong Product Domain và ProductService.
/// </summary>
public sealed class CreateProductRequest
{
    public CreateProductRequest(
        int categoryId,
        string? code,
        string? name,
        string? unitName,
        long costPrice,
        long salePrice,
        int initialStockQuantity,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock,
        string? barcode = null,
        string? description = null,
        string? imagePath = null)
    {
        CategoryId = categoryId;

        Code =
            NormalizeRequiredText(code)
                .ToUpperInvariant();

        Name = NormalizeRequiredText(name);

        UnitName = NormalizeRequiredText(unitName);

        CostPrice = costPrice;
        SalePrice = salePrice;

        InitialStockQuantity =
            initialStockQuantity;

        MinimumStock = minimumStock;

        TrackInventory = trackInventory;

        AllowNegativeStock =
            trackInventory &&
            allowNegativeStock;

        Barcode = NormalizeOptionalText(barcode);
        Description = NormalizeOptionalText(description);
        ImagePath = NormalizeOptionalText(imagePath);
    }

    public int CategoryId { get; }

    public string Code { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string? Description { get; }

    public string UnitName { get; }

    public string? ImagePath { get; }

    public long CostPrice { get; }

    public long SalePrice { get; }

    public int InitialStockQuantity { get; }

    public int MinimumStock { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}