using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation của các cửa sổ tồn kho.
///
/// Service không giữ DbContext.
/// ViewModel tạo scope ngắn cho từng thao tác dữ liệu.
/// </summary>
public sealed class InventoryDialogService :
    IInventoryDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    public InventoryDialogService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));
    }

    public async Task<bool> ShowAdjustmentAsync(
        int productId)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    InventoryAdjustmentViewModel>();

        var initialized =
            await viewModel.InitializeAsync(
                productId);

        if (!initialized)
        {
            global::System.Windows.MessageBox.Show(
                viewModel.ErrorMessage,
                "Không thể mở điều chỉnh kho",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);

            return false;
        }

        var window =
            new InventoryAdjustmentWindow(
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

    public async Task ShowHistoryAsync(
        int? productId = null)
    {
        if (productId.HasValue &&
            productId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    InventoryHistoryViewModel>();

        var initialized =
            await viewModel.InitializeAsync(
                productId);

        if (!initialized)
        {
            global::System.Windows.MessageBox.Show(
                viewModel.ErrorMessage,
                "Không thể mở lịch sử kho",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);

            return;
        }

        var window =
            new InventoryHistoryWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        window.ShowDialog();
    }
}