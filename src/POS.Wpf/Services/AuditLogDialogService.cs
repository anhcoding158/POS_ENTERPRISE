using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IAuditLogDialogService
{
    void Show(global::System.Windows.Window owner);
}

public sealed class AuditLogDialogService : IAuditLogDialogService
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissions;
    private AuditLogWindow? _openWindow;

    public AuditLogDialogService(IServiceProvider services, IPermissionService permissions)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var authorization = _permissions.Authorize(SystemCapability.ViewAuditLog);
        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(owner, authorization.AppError.Message, "Không có quyền truy cập", global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (_openWindow is { IsVisible: true }) { _openWindow.Activate(); return; }
        _openWindow = _services.GetRequiredService<AuditLogWindow>();
        _openWindow.Owner = owner;
        _openWindow.Closed += (_, _) => _openWindow = null;
        _openWindow.ShowDialog();
    }
}
