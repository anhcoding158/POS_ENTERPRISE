using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation mở cửa sổ quản lý danh mục.
/// </summary>
public sealed class CategoryManagementDialogService :
    ICategoryManagementDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    public CategoryManagementDialogService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));
    }

    public async Task ShowAsync()
    {
        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    CategoryManagementViewModel>();

        await viewModel.InitializeAsync();

        var window =
            new CategoryManagementWindow(
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