namespace POS.Wpf.Services;

/// <summary>
/// Mở các hộp thoại thêm và sửa sản phẩm.
///
/// ViewModel màn hình chính không trực tiếp tạo Window.
/// </summary>
public interface IProductDialogService
{
    Task<bool> ShowCreateAsync();

    Task<bool> ShowEditAsync(
        int productId);
}