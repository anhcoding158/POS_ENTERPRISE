using POS.Domain.Constants;

namespace POS.Application.DTOs.Products;

/// <summary>
/// Điều kiện tìm kiếm và phân trang sản phẩm.
/// </summary>
public sealed class ProductSearchRequest
{
    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 200;

    public ProductSearchRequest(
        string? searchTerm = null,
        int? categoryId = null,
        bool? isActive = null,
        bool? isLowStock = null,
        int pageNumber = 1,
        int pageSize = DefaultPageSize)
    {
        if (categoryId.HasValue &&
            categoryId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(categoryId),
                "Mã danh mục phải lớn hơn 0.");
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0 ||
            pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Kích thước trang phải từ 1 đến " +
                $"{MaximumPageSize}.");
        }

        SearchTerm = NormalizeSearchTerm(searchTerm);
        CategoryId = categoryId;
        IsActive = isActive;
        IsLowStock = isLowStock;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Tìm theo mã, mã vạch hoặc tên sản phẩm.
    /// </summary>
    public string? SearchTerm { get; }

    public int? CategoryId { get; }

    public bool? IsActive { get; }

    public bool? IsLowStock { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    private static string? NormalizeSearchTerm(
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var trimmed = searchTerm.Trim();

        if (trimmed.Length >
            BusinessRules.Products.NameMaxLength)
        {
            throw new ArgumentException(
                "Từ khóa tìm kiếm sản phẩm quá dài.",
                nameof(searchTerm));
        }

        return trimmed;
    }
}