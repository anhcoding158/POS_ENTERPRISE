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
    public static ShellPermissionState Create(
        IPermissionService permissionService)
    {
        ArgumentNullException.ThrowIfNull(
            permissionService);

        return new ShellPermissionState(
            CanViewProductCatalog:
                permissionService.HasPermission(
                    SystemPermission.ViewProductCatalog),

            CanManageProducts:
                permissionService.HasPermission(
                    SystemPermission.ManageProducts),

            CanManageCategories:
                permissionService.HasPermission(
                    SystemPermission.ManageCategories),

            CanAdjustInventory:
                permissionService.HasPermission(
                    SystemPermission.AdjustInventory),

            CanViewInventoryHistory:
                permissionService.HasPermission(
                    SystemPermission.ViewInventoryHistory),

            CanUseCheckout:
                permissionService.HasPermission(
                    SystemPermission.UseCheckout),

            CanViewReports:
                permissionService.HasPermission(
                    SystemPermission.ViewReports),

            CanManageUsers:
                permissionService.HasPermission(
                    SystemPermission.ManageUsers));
    }
}