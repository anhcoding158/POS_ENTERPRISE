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
    private readonly IStoreSettingsDialogService? _settingsDialogService;

    public SalesWindowService(
        IServiceProvider serviceProvider,
        IPermissionService permissionService,
        IStoreSettingsReadinessEvaluator? readinessEvaluator = null,
        IStoreSettingsStore? settingsStore = null,
        IStoreSettingsDialogService? settingsDialogService = null)
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
        _settingsDialogService = settingsDialogService;
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
            await _settingsStore.LoadAsync();
            var readiness = await _readinessEvaluator.EvaluateAsync(_settingsStore.Current);
            if (!readiness.IsReady)
            {
                var dialog = new StoreReadinessDialogWindow(readiness.Errors);
                var owner = global::System.Windows.Application.Current?.MainWindow;
                if (owner is not null)
                    dialog.Owner = owner;
                dialog.ShowDialog();
                if (dialog.OpenSettingsRequested && _currentUserIsAdministrator() && _settingsDialogService is not null && owner is not null)
                    _settingsDialogService.Show(owner);
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
