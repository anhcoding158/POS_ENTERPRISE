using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation của hộp thoại thêm/sửa danh mục.
///
/// Kiểm tra quyền trước khi tạo ViewModel hoặc mở Window.
/// Application decorator vẫn là lớp bảo vệ cuối cùng.
/// </summary>
public sealed class CategoryDialogService :
    ICategoryDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    private readonly IPermissionService
        _permissionService;

    public CategoryDialogService(
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
            categoryId: null);
    }

    public Task<bool> ShowEditAsync(
        int categoryId)
    {
        if (categoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(categoryId),
                categoryId,
                "Mã danh mục phải lớn hơn 0.");
        }

        return ShowAuthorizedEditorAsync(
            categoryId);
    }

    private async Task<bool>
        ShowAuthorizedEditorAsync(
            int? categoryId)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageCategories);

        if (authorization.IsFailure)
        {
            ShowAuthorizationError(
                authorization.Error.Message);

            return false;
        }

        /*
         * ViewModel là transient.
         *
         * Mỗi lần mở editor nhận một instance mới,
         * tránh giữ dữ liệu và validation từ cửa sổ cũ.
         */
        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    CategoryEditorViewModel>();

        await viewModel.InitializeAsync(
            categoryId);

        var window =
            new CategoryEditorWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        return window.ShowDialog() ==
               true;
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