namespace POS.Application.DTOs.Categories;

/// <summary>
/// Điều kiện tìm kiếm danh mục.
///
/// Request tự bảo vệ thông số phân trang để tránh
/// tải dữ liệu quá lớn từ giao diện hoặc API.
/// </summary>
public sealed class CategorySearchRequest
{
    public const int MaximumPageSize = 200;

    public CategorySearchRequest(
        string? searchTerm = null,
        bool? isActive = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0 ||
            pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Kích thước trang phải nằm trong khoảng " +
                $"1 đến {MaximumPageSize}.");
        }

        SearchTerm =
            NormalizeOptionalText(
                searchTerm);

        IsActive = isActive;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public string? SearchTerm { get; }

    public bool? IsActive { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
                ? null
                : value.Trim();
    }
}