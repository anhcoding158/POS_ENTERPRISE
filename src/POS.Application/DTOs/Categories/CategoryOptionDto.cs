namespace POS.Application.DTOs.Categories;

/// <summary>
/// Danh mục tối giản dùng cho ComboBox
/// hoặc danh sách lựa chọn sản phẩm.
/// </summary>
public sealed record CategoryOptionDto(
    int Id,
    string Name,
    int DisplayOrder);