namespace POS.Wpf.Services;

/// <summary>
/// Mở màn hình bán hàng.
///
/// Shell không trực tiếp khởi tạo SalesWindow,
/// nhờ đó việc tạo ViewModel vẫn do DI quản lý.
/// </summary>
public interface ISalesWindowService
{
    Task ShowAsync();
}