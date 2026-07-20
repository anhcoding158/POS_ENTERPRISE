namespace POS.Application.DTOs.Categories;

/// <summary>
/// Dữ liệu tạo danh mục mới.
///
/// DTO chỉ chuẩn hóa chuỗi.
/// Domain Category vẫn là nơi kiểm tra luật nghiệp vụ chính thức.
/// </summary>
public sealed class CreateCategoryRequest
{
    public CreateCategoryRequest(
        string? name,
        int displayOrder,
        string? description = null)
    {
        Name =
            NormalizeRequiredText(
                name);

        Description =
            NormalizeOptionalText(
                description);

        DisplayOrder =
            displayOrder;
    }

    public string Name { get; }

    public string? Description { get; }

    public int DisplayOrder { get; }

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