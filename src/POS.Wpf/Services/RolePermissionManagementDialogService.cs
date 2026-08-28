using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IRolePermissionManagementDialogService
{
    void Show(global::System.Windows.Window owner);
}

public sealed class RolePermissionManagementDialogService : IRolePermissionManagementDialogService
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissions;
    private RolePermissionManagementWindow? _openWindow;

    public RolePermissionManagementDialogService(IServiceProvider services, IPermissionService permissions)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var authorization = _permissions.Authorize(SystemCapability.AssignRolesPermissions);
        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(owner, authorization.AppError.Message, "Không có quyền truy cập", global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (_openWindow is { IsVisible: true }) { _openWindow.Activate(); return; }
        var window = _services.GetRequiredService<RolePermissionManagementWindow>();
        _openWindow = window;
        window.Owner = owner;
        window.Closed += (_, _) => _openWindow = null;
        window.ShowDialog();
    }
}
