using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class SupportBundleDialogService : ISupportBundleDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public SupportBundleDialogService(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = _serviceProvider.GetRequiredService<SupportBundleWindow>();
        window.Owner = owner;
        window.ShowDialog();
    }
}
