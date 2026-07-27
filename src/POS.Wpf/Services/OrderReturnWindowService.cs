using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public interface IOrderReturnWindowService
{
    Task<bool> ShowAsync(int orderId);
}

public interface IOrderReturnConfirmationService
{
    bool Confirm(string message);
}

public sealed class OrderReturnConfirmationService :
    IOrderReturnConfirmationService
{
    public bool Confirm(string message) =>
        global::System.Windows.MessageBox.Show(
            message,
            "Xác nhận trả hàng",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;
}

public sealed class OrderReturnWindowService(IServiceScopeFactory scopeFactory) :
    IOrderReturnWindowService
{
    public async Task<bool> ShowAsync(int orderId)
    {
        using var scope = scopeFactory.CreateScope();
        using var viewModel = new OrderReturnViewModel(
            scope.ServiceProvider.GetRequiredService<IOrderReturnService>(),
            scope.ServiceProvider.GetRequiredService<IOrderReturnConfirmationService>(),
            orderId);
        var window = new OrderReturnWindow(viewModel)
        {
            Owner = global::System.Windows.Application.Current?.MainWindow
        };
        await viewModel.LoadAsync();
        window.ShowDialog();
        return viewModel.IsSuccessful;
    }
}
