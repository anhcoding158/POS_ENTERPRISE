namespace POS.Wpf.Services;

/// <summary>
/// Mở hộp thoại thêm và chỉnh sửa danh mục.
///
/// ViewModel màn hình quản lý danh mục không trực tiếp
/// khởi tạo Window, giúp tách nghiệp vụ trình bày khỏi WPF View.
/// </summary>
public interface ICategoryDialogService
{
    /// <summary>
    /// Mở cửa sổ tạo danh mục mới.
    ///
    /// Trả về true khi người dùng lưu thành công.
    /// </summary>
    Task<bool> ShowCreateAsync();

    /// <summary>
    /// Mở cửa sổ chỉnh sửa một danh mục.
    ///
    /// Trả về true khi người dùng lưu thành công.
    /// </summary>
    Task<bool> ShowEditAsync(
        int categoryId);
}