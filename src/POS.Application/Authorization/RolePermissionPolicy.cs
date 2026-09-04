using POS.Domain.Enums;

namespace POS.Application.Authorization;

/// <summary>
/// Ma trận quyền tập trung theo vai trò.
///
/// Đây là nguồn sự thật duy nhất cho ánh xạ:
/// Role → SystemCapability.
/// </summary>
public static class RolePermissionPolicy
{
    public static bool HasPermission(
        Role role,
        SystemCapability permission)
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
                    SystemCapability.ViewProductCatalog or
                    SystemCapability.ManageProducts or
                    SystemCapability.ManageCategories or
                    SystemCapability.ViewInventoryHistory or
                    SystemCapability.AdjustInventory or
                    SystemCapability.UseCheckout or
                    SystemCapability.ViewReports or
                    SystemCapability.ProcessReturns or
                    SystemCapability.ApplySalesDiscount or
                    SystemCapability.ViewSuppliers or
                    SystemCapability.ManageSuppliers or
                    SystemCapability.ViewPurchaseOrders or
                    SystemCapability.ManagePurchaseOrders,

            Role.Cashier =>
                permission is
                    SystemCapability.ViewProductCatalog or
                    SystemCapability.UseCheckout,

            Role.InventoryStaff =>
                permission is
                    SystemCapability.ViewProductCatalog or
                    SystemCapability.ViewInventoryHistory or
                    SystemCapability.AdjustInventory or
                    SystemCapability.ViewSuppliers or
                    SystemCapability.ViewPurchaseOrders,

            _ =>
                false
        };
    }

    public static string GetDisplayName(
        SystemCapability permission)
    {
        ValidatePermission(
            permission);

        return permission switch
        {
            SystemCapability.ViewProductCatalog =>
                "xem danh mục sản phẩm",

            SystemCapability.ManageProducts =>
                "quản lý sản phẩm",

            SystemCapability.ManageCategories =>
                "quản lý danh mục",

            SystemCapability.ViewInventoryHistory =>
                "xem lịch sử tồn kho",

            SystemCapability.AdjustInventory =>
                "điều chỉnh tồn kho",

            SystemCapability.UseCheckout =>
                "thực hiện bán hàng",

            SystemCapability.ViewReports =>
                "xem báo cáo",

            SystemCapability.ManageUsers =>
                "quản lý tài khoản",

            SystemCapability.ProcessReturns =>
                "xử lý trả hàng và hoàn tiền",

            SystemCapability.ApplySalesDiscount =>
                "áp dụng giảm giá bán hàng",

            SystemCapability.ManageStoreSetup =>
                "cấu hình cửa hàng",

            SystemCapability.ViewEmployees =>
                "xem nhân viên",

            SystemCapability.ManageEmployees =>
                "quản lý nhân viên",

            SystemCapability.ManageAccounts =>
                "quản lý tài khoản",

            SystemCapability.ResetPasswords =>
                "đặt lại mật khẩu",

            SystemCapability.LockUnlockAccounts =>
                "khóa hoặc mở khóa tài khoản",

            SystemCapability.AssignRolesPermissions =>
                "gán vai trò và quyền",

            SystemCapability.ViewSecurityStatus =>
                "xem trạng thái bảo mật",

            SystemCapability.ViewAuditLog =>
                "xem nhật ký hoạt động",

            SystemCapability.ViewSuppliers =>
                "xem nhà cung cấp",

            SystemCapability.ManageSuppliers =>
                "quản lý nhà cung cấp",

            SystemCapability.ViewPurchaseOrders =>
                "xem Purchase Order",

            SystemCapability.ManagePurchaseOrders =>
                "quản lý Purchase Order",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(permission),
                    permission,
                    "Quyền hệ thống không hợp lệ.")
        };
    }

    public static IReadOnlyList<SystemCapability> GetEffectivePermissions(Role role)
    {
        ValidateRole(role);
        return Enum.GetValues<SystemCapability>()
            .Where(permission => HasPermission(role, permission))
            .ToArray();
    }

    public static string GetRoleDisplayName(Role role)
    {
        ValidateRole(role);
        return role switch
        {
            Role.Administrator => "Quản trị viên",
            Role.Manager => "Quản lý",
            Role.Cashier => "Thu ngân",
            Role.InventoryStaff => "Nhân viên kho",
            _ => "Không xác định"
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
        SystemCapability permission)
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
