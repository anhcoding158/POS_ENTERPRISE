namespace POS.Wpf.Services;

public interface ISupplierDialogService
{
    Task<int?> ShowCreateAsync(global::System.Windows.Window owner);
    Task<bool> ShowEditAsync(global::System.Windows.Window owner, int supplierId);
}

public interface ISupplierManagementDialogService
{
    void Show(global::System.Windows.Window owner);
}
