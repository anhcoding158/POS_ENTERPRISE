using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// Mở cửa sổ quản lý danh mục.
///
/// Chỉ tài khoản có quyền ManageCategories mới được
/// tạo ViewModel và mở màn hình quản trị.
/// </summary>
public sealed class CategoryManagementDialogService :
    ICategoryManagementDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    private readonly IPermissionService
        _permissionService;

    public CategoryManagementDialogService(
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

    public async Task ShowAsync()
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageCategories);

        if (authorization.IsFailure)
        {
            ShowAuthorizationError(
                authorization.Error.Message);

            return;
        }

        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    CategoryManagementViewModel>();

        await viewModel.InitializeAsync();

        var window =
            new CategoryManagementWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        window.ShowDialog();
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