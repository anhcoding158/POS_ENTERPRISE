using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class BulkProductDialogService : IBulkProductDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public BulkProductDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<bool> ShowAsync(IReadOnlyList<ProductRowViewModel> selectedProducts)
    {
        ArgumentNullException.ThrowIfNull(selectedProducts);
        using var scope = _serviceProvider.CreateScope();
        var categoriesResult = await scope.ServiceProvider
            .GetRequiredService<ICategoryService>()
            .ListActiveAsync();
        if (categoriesResult.IsFailure)
        {
            global::System.Windows.MessageBox.Show(
                categoriesResult.AppError.Message,
                "Không thể mở thao tác hàng loạt",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);
            return false;
        }

        var viewModel = new BulkProductViewModel(
            selectedProducts,
            scope.ServiceProvider.GetRequiredService<IBulkProductOperationService>(),
            categoriesResult.Value);
        using (viewModel)
        {
            var window = new BulkProductWindow(viewModel)
            {
                Owner = global::System.Windows.Application.Current?.MainWindow
            };
            return window.ShowDialog() == true;
        }
    }
}
