namespace POS.Application.DTOs.Products;

/// <summary>
/// Dữ liệu tạo sản phẩm mới.
///
/// Request chỉ chuẩn hóa chuỗi.
/// ProductService và Domain chịu trách nhiệm kiểm tra nghiệp vụ.
/// </summary>
public sealed class CreateProductRequest
{
    public CreateProductRequest(
        int categoryId,
        string? code,
        string? barcode,
        string? name,
        string? description,
        string? unitName,
        string? imagePath,
        long costPrice,
        long salePrice,
        int initialStockQuantity,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock)
    {
        CategoryId = categoryId;
        Code = NormalizeRequiredText(code);
        Barcode = NormalizeOptionalText(barcode);
        Name = NormalizeRequiredText(name);
        Description = NormalizeOptionalText(description);
        UnitName = NormalizeRequiredText(unitName);
        ImagePath = NormalizeOptionalText(imagePath);

        CostPrice = costPrice;
        SalePrice = salePrice;
        InitialStockQuantity = initialStockQuantity;
        MinimumStock = minimumStock;
        TrackInventory = trackInventory;
        AllowNegativeStock = allowNegativeStock;
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