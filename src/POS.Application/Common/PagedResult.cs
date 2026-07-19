namespace POS.Application.Common;

/// <summary>
/// Kết quả danh sách có phân trang.
///
/// PageNumber bắt đầu từ 1.
/// </summary>
public sealed class PagedResult<TItem>
{
    public PagedResult(
        IReadOnlyCollection<TItem> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Kích thước trang phải lớn hơn 0.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCount),
                "Tổng số bản ghi không được nhỏ hơn 0.");
        }

        if (items.Count > pageSize)
        {
            throw new ArgumentException(
                "Số phần tử không được lớn hơn kích thước trang.",
                nameof(items));
        }

        if (items.Count > totalCount)
        {
            throw new ArgumentException(
                "Số phần tử của trang không được lớn hơn tổng số bản ghi.",
                nameof(items));
        }

        Items = items.ToArray();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;

        TotalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(
                totalCount / (double)pageSize);
    }

    public IReadOnlyList<TItem> Items { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages { get; }

    public int ItemCount =>
        Items.Count;

    public bool HasPreviousPage =>
        PageNumber > 1;

    public bool HasNextPage =>
        PageNumber < TotalPages;

    public bool IsEmpty =>
        Items.Count == 0;

    public static PagedResult<TItem> Empty(
        int pageNumber,
        int pageSize)
    {
        return new PagedResult<TItem>(
            Array.Empty<TItem>(),
            pageNumber,
            pageSize,
            0);
    }

    /// <summary>
    /// Chuyển dữ liệu trong trang sang DTO khác
    /// nhưng giữ nguyên thông tin phân trang.
    /// </summary>
    public PagedResult<TOutput> Map<TOutput>(
        Func<TItem, TOutput> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var mappedItems = Items
            .Select(mapper)
            .ToArray();

        return new PagedResult<TOutput>(
            mappedItems,
            PageNumber,
            PageSize,
            TotalCount);
    }
}