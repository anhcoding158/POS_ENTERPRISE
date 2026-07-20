using Microsoft.Extensions.DependencyInjection;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

/// <summary>
/// WPF implementation của hộp thoại quản lý danh mục.
///
/// CategoryEditorViewModel không giữ DbContext.
/// Mỗi thao tác tải hoặc lưu bên trong ViewModel
/// sẽ tạo DI scope riêng.
/// </summary>
public sealed class CategoryDialogService :
    ICategoryDialogService
{
    private readonly IServiceProvider
        _serviceProvider;

    public CategoryDialogService(
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
            categoryId: null);
    }

    public Task<bool> ShowEditAsync(
        int categoryId)
    {
        if (categoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(categoryId),
                categoryId,
                "Mã danh mục phải lớn hơn 0.");
        }

        return ShowEditorAsync(
            categoryId);
    }

    private async Task<bool> ShowEditorAsync(
        int? categoryId)
    {
        /*
         * ViewModel là transient.
         *
         * Mỗi lần mở editor nhận một instance mới,
         * tránh dữ liệu và validation của cửa sổ trước
         * bị giữ lại cho lần mở tiếp theo.
         */
        var viewModel =
            _serviceProvider
                .GetRequiredService<
                    CategoryEditorViewModel>();

        await viewModel.InitializeAsync(
            categoryId);

        var window =
            new CategoryEditorWindow(
                viewModel)
            {
                Owner =
                    global::System.Windows
                        .Application
                        .Current?
                        .MainWindow
            };

        return window.ShowDialog() ==
               true;
    }
}