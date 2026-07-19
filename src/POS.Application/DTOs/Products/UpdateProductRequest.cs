namespace POS.Application.DTOs.Products;

/// <summary>
/// Dữ liệu cập nhật sản phẩm.
///
/// Việc thay đổi tồn kho thực tế không nằm trong request này.
/// Tồn kho phải được điều chỉnh bằng nghiệp vụ nhập, xuất
/// hoặc kiểm kê riêng để có thể lưu lịch sử.
/// </summary>
public sealed class UpdateProductRequest
{
    public UpdateProductRequest(
        int productId,
        int categoryId,
        string? code,
        string? barcode,
        string? name,
        string? description,
        string? unitName,
        string? imagePath,
        long costPrice,
        long salePrice,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock,
        bool isActive)
    {
        ProductId = productId;
        CategoryId = categoryId;

        Code = NormalizeRequiredText(code);
        Barcode = NormalizeOptionalText(barcode);
        Name = NormalizeRequiredText(name);
        Description = NormalizeOptionalText(description);
        UnitName = NormalizeRequiredText(unitName);
        ImagePath = NormalizeOptionalText(imagePath);

        CostPrice = costPrice;
        SalePrice = salePrice;
        MinimumStock = minimumStock;
        TrackInventory = trackInventory;
        AllowNegativeStock = allowNegativeStock;
        IsActive = isActive;
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