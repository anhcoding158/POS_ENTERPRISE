using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class LabelPrintDialogService : ILabelPrintDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public LabelPrintDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<bool> ShowAsync(IReadOnlyList<ProductRowViewModel> selectedProducts)
    {
        ArgumentNullException.ThrowIfNull(selectedProducts);
        using var scope = _serviceProvider.CreateScope();
        var viewModel = new LabelPrintViewModel(
            selectedProducts,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            scope.ServiceProvider.GetRequiredService<ILabelPrinterCatalog>(),
            scope.ServiceProvider.GetRequiredService<ILabelPrintingService>(),
            scope.ServiceProvider.GetRequiredService<IPermissionService>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LabelPrintViewModel>>(),
            scope.ServiceProvider.GetRequiredService<ILabelPrintSettingsStore>());
        using (viewModel)
        {
            var window = new LabelPrintWindow(viewModel)
            {
                Owner = global::System.Windows.Application.Current?.MainWindow
            };
            return Task.FromResult(window.ShowDialog() == true);
        }
    }
}
