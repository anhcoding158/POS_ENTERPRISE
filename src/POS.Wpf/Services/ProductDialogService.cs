using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation của hộp thoại quản lý sản phẩm.
///
/// Ngoài lớp bảo vệ tại Application Service,
/// dialog cũng kiểm tra quyền để không mở form mà
/// người dùng không được phép sử dụng.
/// </summary>
public sealed class ProductDialogService :
    IProductDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    private readonly IPermissionService
        _permissionService;

    public ProductDialogService(
        IServiceProvider serviceProvider,
        IPermissionService permissionService)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));

        _permissionService =
            permissionService ??
            throw new ArgumentNullException(
                nameof(permissionService));
    }

    public Task<bool> ShowCreateAsync()
    {
        return ShowAuthorizedEditorAsync(
            productId: null);
    }

    public Task<bool> ShowEditAsync(
        int productId)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        return ShowAuthorizedEditorAsync(
            productId);
    }

    private async Task<bool>
        ShowAuthorizedEditorAsync(
            int? productId)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageProducts);

        if (authorization.IsFailure)
        {
            ShowAuthorizationError(
                authorization.Error.Message);

            return false;
        }

        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    ProductEditorViewModel>();

        await viewModel.InitializeAsync(
            productId);

        var window =
            new ProductEditorWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        return window.ShowDialog() == true;
    }

    private static void ShowAuthorizationError(
        string message)
    {
        var owner =
            global::System.Windows
                .Application
                .Current?
                .MainWindow;

        if (owner is null)
        {
            global::System.Windows.MessageBox.Show(
                message,
                "Không có quyền truy cập",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Warning);

            return;
        }

        global::System.Windows.MessageBox.Show(
            owner,
            message,
            "Không có quyền truy cập",
            global::System.Windows
                .MessageBoxButton.OK,
            global::System.Windows
                .MessageBoxImage.Warning);
    }
}