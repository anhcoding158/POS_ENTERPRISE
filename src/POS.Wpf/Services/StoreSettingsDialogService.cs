using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IStoreSettingsDialogService
{
    void Show(global::System.Windows.Window owner);
}

public sealed class StoreSettingsDialogService(IServiceProvider services, IPermissionService permissions) : IStoreSettingsDialogService
{
    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var authorization = permissions.Authorize(SystemCapability.ManageStoreSetup);
        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(authorization.AppError.Message, "Không có quyền", global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            return;
        }
        var window = services.GetRequiredService<StoreSettingsWindow>();
        window.Owner = owner;
        window.ShowDialog();
    }
}
