using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authentication;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class StorageStatusDialogService : IStorageStatusDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserService _currentUserService;
    private bool _isOpen;

    public StorageStatusDialogService(
        IServiceProvider serviceProvider,
        ICurrentUserService currentUserService)
    {
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
        _currentUserService = currentUserService ??
            throw new ArgumentNullException(nameof(currentUserService));
    }

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!_currentUserService.IsAuthenticated || _isOpen) return;
        var window = _serviceProvider.GetRequiredService<StorageStatusWindow>();
        window.Owner = owner;
        _isOpen = true;
        try { window.ShowDialog(); }
        finally { _isOpen = false; }
    }
}
