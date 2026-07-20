namespace POS.Application.DTOs.Products;

/// <summary>
/// Dữ liệu cập nhật thông tin sản phẩm.
///
/// Tồn kho thực tế không được chỉnh qua request này.
/// Sau này tồn kho sẽ được điều chỉnh qua nghiệp vụ
/// nhập kho, xuất kho hoặc kiểm kê.
/// </summary>
public sealed class UpdateProductRequest
{
    public UpdateProductRequest(
        int productId,
        int categoryId,
        string? code,
        string? name,
        string? unitName,
        long costPrice,
        long salePrice,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock,
        bool isActive,
        string? barcode = null,
        string? description = null,
        string? imagePath = null)
    {
        ProductId = productId;
        CategoryId = categoryId;

        Code =
            NormalizeRequiredText(code)
                .ToUpperInvariant();

        Name = NormalizeRequiredText(name);

        UnitName = NormalizeRequiredText(unitName);

        CostPrice = costPrice;
        SalePrice = salePrice;

        MinimumStock = minimumStock;

        TrackInventory = trackInventory;

        AllowNegativeStock =
            trackInventory &&
            allowNegativeStock;

        IsActive = isActive;

        Barcode = NormalizeOptionalText(barcode);
        Description = NormalizeOptionalText(description);
        ImagePath = NormalizeOptionalText(imagePath);
    }

    public int ProductId { get; }

    public int CategoryId { get; }

    public string Code { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string? Description { get; }

    public string UnitName { get; }

    public string? ImagePath { get; }

    public long CostPrice { get; }

    public long SalePrice { get; }

    public int MinimumStock { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    public bool IsActive { get; }

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