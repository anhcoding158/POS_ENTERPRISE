using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation của hộp thoại quản lý sản phẩm.
/// </summary>
public sealed class ProductDialogService :
    IProductDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    public ProductDialogService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));
    }

    public Task<bool> ShowCreateAsync()
    {
        return ShowEditorAsync(
            productId: null);
    }

    public Task<bool> ShowEditAsync(
        int productId)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        return ShowEditorAsync(productId);
    }

    private async Task<bool> ShowEditorAsync(
        int? productId)
    {
        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    ProductEditorViewModel>();

        await viewModel.InitializeAsync(
            productId);

        var window =
            new ProductEditorWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        return window.ShowDialog() == true;
    }
}