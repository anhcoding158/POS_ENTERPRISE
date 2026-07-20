namespace POS.Application.DTOs.Categories;

/// <summary>
/// Thông tin đầy đủ của một danh mục.
/// </summary>
public sealed record CategoryDetailsDto(
    int Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);