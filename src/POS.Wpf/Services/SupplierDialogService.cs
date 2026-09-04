using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class SupplierDialogService : ISupplierDialogService
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissions;

    public SupplierDialogService(IServiceProvider services, IPermissionService permissions)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public async Task<int?> ShowCreateAsync(global::System.Windows.Window owner)
    {
        if (!Authorize(owner)) return null;
        var viewModel = _services.GetRequiredService<SupplierEditorViewModel>();
        await viewModel.InitializeAsync(null);
        var window = new SupplierEditorWindow(viewModel) { Owner = owner };
        return window.ShowDialog() == true ? viewModel.SavedSupplierId : null;
    }

    public async Task<bool> ShowEditAsync(global::System.Windows.Window owner, int supplierId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(supplierId);
        if (!Authorize(owner)) return false;
        var viewModel = _services.GetRequiredService<SupplierEditorViewModel>();
        await viewModel.InitializeAsync(supplierId);
        var window = new SupplierEditorWindow(viewModel) { Owner = owner };
        return window.ShowDialog() == true;
    }

    private bool Authorize(global::System.Windows.Window owner)
    {
        var result = _permissions.Authorize(SystemCapability.ManageSuppliers);
        if (result.IsSuccess) return true;
        global::System.Windows.MessageBox.Show(owner, result.AppError.Message, "Không có quyền truy cập",
            global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
        return false;
    }
}

public sealed class SupplierManagementDialogService : ISupplierManagementDialogService
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissions;
    private SupplierManagementWindow? _openWindow;

    public SupplierManagementDialogService(IServiceProvider services, IPermissionService permissions)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public void Show(global::System.Windows.Window owner)
    {
        var result = _permissions.Authorize(SystemCapability.ViewSuppliers);
        if (result.IsFailure)
        {
            global::System.Windows.MessageBox.Show(owner, result.AppError.Message, "Không có quyền truy cập",
                global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (_openWindow is { IsVisible: true }) { _openWindow.Activate(); return; }
        var viewModel = _services.GetRequiredService<SupplierManagementViewModel>();
        _openWindow = new SupplierManagementWindow(viewModel) { Owner = owner };
        _openWindow.Closed += (_, _) => _openWindow = null;
        _openWindow.ShowDialog();
    }
}
