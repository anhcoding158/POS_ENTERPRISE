namespace POS.Application.DTOs.Categories;

/// <summary>
/// Một dòng danh mục dùng trong màn hình quản lý.
/// </summary>
public sealed record CategoryListItemDto(
    int Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);