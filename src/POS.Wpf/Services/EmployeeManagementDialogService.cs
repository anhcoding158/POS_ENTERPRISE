using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IEmployeeManagementDialogService
{
    void Show(global::System.Windows.Window owner);
}

public sealed class EmployeeManagementDialogService : IEmployeeManagementDialogService
{
    private readonly IServiceProvider _services;
    private readonly IPermissionService _permissions;
    private readonly ILogger<EmployeeManagementDialogService> _logger;
    private EmployeeManagementWindow? _openWindow;

    public EmployeeManagementDialogService(
        IServiceProvider services,
        IPermissionService permissions,
        ILogger<EmployeeManagementDialogService> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Show(global::System.Windows.Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var authorization = _permissions.Authorize(SystemCapability.ViewEmployees);
        if (authorization.IsFailure)
        {
            global::System.Windows.MessageBox.Show(
                owner,
                authorization.AppError.Message,
                "Kh\u00f4ng c\u00f3 quy\u1ec1n truy c\u1eadp",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (_openWindow is { IsVisible: true })
        {
            _openWindow.Activate();
            return;
        }

        try
        {
            var viewModel = _services.GetRequiredService<EmployeeManagementViewModel>();
            _openWindow = new EmployeeManagementWindow(viewModel);
            _openWindow.Owner = owner;
            _openWindow.Closed += (_, _) => _openWindow = null;
            _openWindow.DataContext = viewModel;
            _openWindow.ShowDialog();
        }
        catch (Exception exception)
        {
            _openWindow = null;
            global::POS.Application.Common.PosLog.Error(
                _logger,
                exception,
                "Kh\u00f4ng th\u1ec3 m\u1edf m\u00e0n h\u00ecnh Nh\u00e2n vi\u00ean v\u00e0 t\u00e0i kho\u1ea3n. ExceptionChain={ExceptionChain}",
                FormatExceptionChain(exception));
            global::System.Windows.MessageBox.Show(
                owner,
                "Kh\u00f4ng th\u1ec3 m\u1edf m\u00e0n h\u00ecnh Nh\u00e2n vi\u00ean v\u00e0 t\u00e0i kho\u1ea3n. Vui l\u00f2ng th\u1eed l\u1ea1i ho\u1eb7c li\u00ean h\u1ec7 qu\u1ea3n tr\u1ecb vi\u00ean.",
                "Kh\u00f4ng th\u1ec3 m\u1edf m\u00e0n h\u00ecnh",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Error);
        }
    }

    private static string FormatExceptionChain(Exception exception)
    {
        var parts = new List<string>();
        var current = exception;
        var depth = 0;
        while (current is not null && depth++ < 8)
        {
            parts.Add(
                $"{current.GetType().FullName}: " +
                global::POS.Application.Common.SafeDiagnosticPolicy.SanitizeText(current.Message));
            current = current.InnerException!;
        }

        return string.Join(" <- ", parts);
    }
}
