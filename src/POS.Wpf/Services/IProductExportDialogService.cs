using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;

namespace POS.Wpf.Services;

public interface IProductExportDialogService
{
    Task<bool> ShowAsync(
        ProductSearchRequest? productFilters = null,
        InventorySearchRequest? historyFilters = null);

    Task<bool> ShowTemplateAsync();
}
