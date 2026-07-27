using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IOrderHistoryWindowService
{
    Task ShowAsync();
}

public sealed class OrderHistoryWindowService : IOrderHistoryWindowService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPermissionService _permissions;
    private OrderHistoryWindow? _openWindow;

    public OrderHistoryWindowService(
        IServiceScopeFactory scopeFactory,
        IPermissionService permissions)
    {
        _scopeFactory = scopeFactory ??
            throw new ArgumentNullException(nameof(scopeFactory));
        _permissions = permissions ??
            throw new ArgumentNullException(nameof(permissions));
    }

    public Task ShowAsync()
    {
        var authorization = _permissions.Authorize(
            SystemPermission.ViewReports);
        if (authorization.IsFailure)
        {
            return Task.CompletedTask;
        }

        if (_openWindow is not null)
        {
            _openWindow.Activate();
            _openWindow.Focus();
            return Task.CompletedTask;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var window = scope.ServiceProvider
                .GetRequiredService<OrderHistoryWindow>();
            _openWindow = window;
            window.Owner = global::System.Windows.Application.Current?.MainWindow;
            window.ShowDialog();
        }
        finally
        {
            _openWindow = null;
        }

        return Task.CompletedTask;
    }
}
