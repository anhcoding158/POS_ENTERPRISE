using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation mở cửa sổ bán hàng.
///
/// Quyền UseCheckout được kiểm tra tại đây để:
/// - không chỉ dựa vào trạng thái Enabled của nút;
/// - ngăn một module UI khác mở màn hình trái phép.
///
/// CheckoutService phía Application vẫn tiếp tục
/// kiểm tra quyền trước khi tạo đơn hàng.
/// </summary>
public sealed class SalesWindowService :
    ISalesWindowService
{
    private readonly IServiceProvider
        _serviceProvider;

    private readonly IPermissionService
        _permissionService;

    public SalesWindowService(
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

    public Task ShowAsync()
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.UseCheckout);

        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(
                authorization.Error.Message,
                "Không có quyền bán hàng",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Warning);

            return Task.CompletedTask;
        }

        var window =
            _serviceProvider
                .GetRequiredService<
                    SalesWindow>();

        window.Owner =
            global::System.Windows
                .Application
                .Current?
                .MainWindow;

        window.ShowDialog();

        return Task.CompletedTask;
    }
}