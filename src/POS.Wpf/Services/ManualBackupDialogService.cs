using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class ManualBackupDialogService : IManualBackupDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ManualBackupDialogService(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = _serviceProvider.GetRequiredService<ManualBackupWindow>();
        window.Owner = owner;
        window.ShowDialog();
    }
}
