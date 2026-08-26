using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.StoreSetup;
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
    private readonly IStoreSettingsReadinessEvaluator? _readinessEvaluator;
    private readonly IStoreSettingsStore? _settingsStore;

    public SalesWindowService(
        IServiceProvider serviceProvider,
        IPermissionService permissionService,
        IStoreSettingsReadinessEvaluator? readinessEvaluator = null,
        IStoreSettingsStore? settingsStore = null)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));

        _permissionService =
            permissionService ??
            throw new ArgumentNullException(
                nameof(permissionService));
        _readinessEvaluator = readinessEvaluator;
        _settingsStore = settingsStore;
    }

    public async Task ShowAsync()
    {
        var authorization =
            _permissionService.Authorize(
                SystemCapability.UseCheckout);

        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(
                authorization.AppError.Message,
                "Không có quyền bán hàng",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Warning);

            return;
        }

        if (_readinessEvaluator is not null && _settingsStore is not null)
        {
            var readiness = await _readinessEvaluator.EvaluateAsync(_settingsStore.Current);
            if (!readiness.IsReady)
            {
                var hint = _currentUserIsAdministrator()
                    ? "Vui lòng mở Cài đặt cửa hàng để xử lý."
                    : "Vui lòng liên hệ Quản trị viên để xử lý.";
                global::System.Windows.MessageBox.Show(
                    "Chưa thể mở quầy bán hàng vì cấu hình cửa hàng chưa sẵn sàng.\n\n" + hint,
                    "Cấu hình cửa hàng chưa sẵn sàng",
                    global::System.Windows.MessageBoxButton.OK,
                    global::System.Windows.MessageBoxImage.Warning);
                return;
            }
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

        return;
    }

    private bool _currentUserIsAdministrator() =>
        _permissionService.HasPermission(SystemCapability.ManageStoreSetup);
}
