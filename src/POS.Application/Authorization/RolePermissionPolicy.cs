using POS.Domain.Enums;

namespace POS.Application.Authorization;

/// <summary>
/// Ma trận quyền tập trung theo vai trò.
///
/// Đây là nguồn sự thật duy nhất cho ánh xạ:
/// Role → SystemPermission.
/// </summary>
public static class RolePermissionPolicy
{
    public static bool HasPermission(
        Role role,
        SystemPermission permission)
    {
        ValidateRole(
            role);

        ValidatePermission(
            permission);

        return role switch
        {
            Role.Administrator =>
                true,

            Role.Manager =>
                permission is
                    SystemPermission.ViewProductCatalog or
                    SystemPermission.ManageProducts or
                    SystemPermission.ManageCategories or
                    SystemPermission.ViewInventoryHistory or
                    SystemPermission.AdjustInventory or
                    SystemPermission.UseCheckout or
                    SystemPermission.ViewReports,

            Role.Cashier =>
                permission is
                    SystemPermission.ViewProductCatalog or
                    SystemPermission.UseCheckout,

            Role.InventoryStaff =>
                permission is
                    SystemPermission.ViewProductCatalog or
                    SystemPermission.ViewInventoryHistory or
                    SystemPermission.AdjustInventory,

            _ =>
                false
        };
    }

    public static string GetDisplayName(
        SystemPermission permission)
    {
        ValidatePermission(
            permission);

        return permission switch
        {
            SystemPermission.ViewProductCatalog =>
                "xem danh mục sản phẩm",

            SystemPermission.ManageProducts =>
                "quản lý sản phẩm",

            SystemPermission.ManageCategories =>
                "quản lý danh mục",

            SystemPermission.ViewInventoryHistory =>
                "xem lịch sử tồn kho",

            SystemPermission.AdjustInventory =>
                "điều chỉnh tồn kho",

            SystemPermission.UseCheckout =>
                "thực hiện bán hàng",

            SystemPermission.ViewReports =>
                "xem báo cáo",

            SystemPermission.ManageUsers =>
                "quản lý tài khoản",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(permission),
                    permission,
                    "Quyền hệ thống không hợp lệ.")
        };
    }

    private static void ValidateRole(
        Role role)
    {
        if (!Enum.IsDefined(
                role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Vai trò người dùng không hợp lệ.");
        }
    }

    private static void ValidatePermission(
        SystemPermission permission)
    {
        if (!Enum.IsDefined(
                permission))
        {
            throw new ArgumentOutOfRangeException(
                nameof(permission),
                permission,
                "Quyền hệ thống không hợp lệ.");
        }
    }
}