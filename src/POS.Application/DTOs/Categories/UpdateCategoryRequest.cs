namespace POS.Application.DTOs.Categories;

/// <summary>
/// Dữ liệu cập nhật danh mục.
/// </summary>
public sealed class UpdateCategoryRequest
{
    public UpdateCategoryRequest(
        int categoryId,
        string? name,
        int displayOrder,
        bool isActive,
        string? description = null)
    {
        CategoryId =
            categoryId;

        Name =
            NormalizeRequiredText(
                name);

        Description =
            NormalizeOptionalText(
                description);

        DisplayOrder =
            displayOrder;

        IsActive =
            isActive;
    }

    public int CategoryId { get; }

    public string Name { get; }

    public string? Description { get; }

    public int DisplayOrder { get; }

    public bool IsActive { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ??
               string.Empty;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
                ? null
                : value.Trim();
    }
}