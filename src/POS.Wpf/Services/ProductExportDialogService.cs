using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Exports;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class ProductExportDialogService : IProductExportDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ProductExportDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<bool> ShowAsync(ProductSearchRequest? productFilters = null, InventorySearchRequest? historyFilters = null)
        => ShowCoreAsync(productFilters, historyFilters, null);

    public Task<bool> ShowTemplateAsync()
        => ShowCoreAsync(null, null, ProductExportReportType.ProductImportTemplate);

    private Task<bool> ShowCoreAsync(ProductSearchRequest? productFilters, InventorySearchRequest? historyFilters, ProductExportReportType? initialReport)
    {
        using var scope = _serviceProvider.CreateScope();
        var viewModel = new ProductExportViewModel(
            scope.ServiceProvider.GetRequiredService<POS.Application.Abstractions.Services.IProductExportService>(),
            scope.ServiceProvider.GetRequiredService<POS.Application.Abstractions.Exports.IProductExportWriter>(),
            productFilters,
            historyFilters,
            initialReport);
        using (viewModel)
        {
            var window = new ProductExportWindow(viewModel)
            {
                Owner = global::System.Windows.Application.Current?.MainWindow
            };
            return Task.FromResult(window.ShowDialog() == true);
        }
    }
}
