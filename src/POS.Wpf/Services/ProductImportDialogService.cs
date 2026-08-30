using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class ProductImportDialogService : IProductImportDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPermissionService _permissionService;

    public ProductImportDialogService(IServiceProvider serviceProvider, IPermissionService permissionService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public Task<bool> ShowAsync()
    {
        var authorization = _permissionService.Authorize(SystemCapability.ManageProducts);
        if (authorization.IsFailure)
        {
            var owner = global::System.Windows.Application.Current?.MainWindow;
            if (owner is null)
                global::System.Windows.MessageBox.Show(authorization.AppError.Message, "Không có quyền truy cập", global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            else
                global::System.Windows.MessageBox.Show(owner, authorization.AppError.Message, "Không có quyền truy cập", global::System.Windows.MessageBoxButton.OK, global::System.Windows.MessageBoxImage.Warning);
            return Task.FromResult(false);
        }

        var viewModel = _serviceProvider.GetRequiredService<ProductImportWizardViewModel>();
        var window = new ProductImportWizardWindow(viewModel)
        {
            Owner = global::System.Windows.Application.Current?.MainWindow
        };
        return Task.FromResult(window.ShowDialog() == true);
    }
}
