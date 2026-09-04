using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;

namespace POS.Wpf.Authorization;

/// <summary>
/// Trạng thái quyền dùng để trình bày ShellWindow.
///
/// Đây chỉ là lớp hỗ trợ UI.
/// Lớp bảo vệ thật vẫn nằm tại AuthorizedProductService,
/// AuthorizedCategoryService và AuthorizedInventoryService.
/// </summary>
public sealed record ShellPermissionState(
    bool CanViewProductCatalog,
    bool CanManageProducts,
    bool CanManageCategories,
    bool CanAdjustInventory,
    bool CanViewInventoryHistory,
    bool CanUseCheckout,
    bool CanViewReports,
    bool CanManageUsers)
{
    public bool CanViewSuppliers { get; init; }
    public bool CanManageSuppliers { get; init; }
    public static ShellPermissionState Create(
        IPermissionService permissionService)
    {
        ArgumentNullException.ThrowIfNull(
            permissionService);

        return new ShellPermissionState(
            CanViewProductCatalog:
                permissionService.HasPermission(
                    SystemCapability.ViewProductCatalog),

            CanManageProducts:
                permissionService.HasPermission(
                    SystemCapability.ManageProducts),

            CanManageCategories:
                permissionService.HasPermission(
                    SystemCapability.ManageCategories),

            CanAdjustInventory:
                permissionService.HasPermission(
                    SystemCapability.AdjustInventory),

            CanViewInventoryHistory:
                permissionService.HasPermission(
                    SystemCapability.ViewInventoryHistory),

            CanUseCheckout:
                permissionService.HasPermission(
                    SystemCapability.UseCheckout),

            CanViewReports:
                permissionService.HasPermission(
                    SystemCapability.ViewReports),

            CanManageUsers:
                permissionService.HasPermission(
                    SystemCapability.ManageUsers))
        {
            CanViewSuppliers =
                permissionService.HasPermission(
                    SystemCapability.ViewSuppliers),
            CanManageSuppliers =
                permissionService.HasPermission(
                    SystemCapability.ManageSuppliers)
        };
    }
}
