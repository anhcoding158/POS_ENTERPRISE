using POS.Wpf.ViewModels;

namespace POS.Wpf.Services;

public interface IBulkProductDialogService
{
    Task<bool> ShowAsync(
        IReadOnlyList<ProductRowViewModel> selectedProducts);
}
