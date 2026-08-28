using POS.Domain.Enums;

namespace POS.Application.Authorization;

public enum PermissionRisk
{
    Standard,
    Elevated,
    Dangerous
}

public sealed record PermissionDefinition(
    SystemCapability Capability,
    string DisplayName,
    string Description,
    string BusinessArea,
    PermissionRisk Risk,
    bool CustomRoleAllowed,
    bool RequiresConfirmation);

/// <summary>
/// Danh mục quyền hiển thị tập trung. Mỗi mục ánh xạ đúng một capability
/// đang được hệ thống thực thi; không dùng chuỗi quyền riêng ở WPF.
/// </summary>
public static class PermissionCatalog
{
    private static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(SystemCapability.UseCheckout, "Mở quầy và bán hàng", "Thực hiện nghiệp vụ bán hàng tại quầy.", "Bán hàng", PermissionRisk.Standard, false, false),
        new(SystemCapability.ProcessReturns, "Xử lý trả hàng", "Xử lý trả hàng và hoàn tiền theo chính sách.", "Đơn hàng", PermissionRisk.Elevated, false, true),
        new(SystemCapability.ViewReports, "Xem báo cáo", "Xem các báo cáo vận hành đã được triển khai.", "Đơn hàng", PermissionRisk.Standard, false, false),
        new(SystemCapability.ViewProductCatalog, "Xem sản phẩm", "Tra cứu danh mục sản phẩm.", "Hàng hóa", PermissionRisk.Standard, false, false),
        new(SystemCapability.ManageProducts, "Quản lý sản phẩm", "Tạo, sửa và quản lý sản phẩm.", "Hàng hóa", PermissionRisk.Elevated, false, true),
        new(SystemCapability.ManageCategories, "Quản lý danh mục", "Tạo, sửa và quản lý danh mục sản phẩm.", "Hàng hóa", PermissionRisk.Elevated, false, true),
        new(SystemCapability.ViewInventoryHistory, "Xem tồn kho", "Xem lịch sử và số liệu tồn kho.", "Hàng hóa", PermissionRisk.Standard, false, false),
        new(SystemCapability.AdjustInventory, "Điều chỉnh tồn kho", "Ghi nhận điều chỉnh tồn kho có kiểm soát.", "Hàng hóa", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.ViewEmployees, "Xem nhân viên", "Xem hồ sơ nhân viên và tài khoản.", "Nhân viên và tài khoản", PermissionRisk.Standard, false, false),
        new(SystemCapability.ManageEmployees, "Quản lý nhân viên", "Tạo, sửa và thay đổi trạng thái nhân viên.", "Nhân viên và tài khoản", PermissionRisk.Elevated, false, true),
        new(SystemCapability.ManageUsers, "Quản lý tài khoản cũ", "Capability tương thích cho luồng quản lý tài khoản hiện hữu.", "Nhân viên và tài khoản", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.ManageAccounts, "Quản lý tài khoản", "Tạo và quản lý tài khoản đăng nhập.", "Nhân viên và tài khoản", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.ResetPasswords, "Đặt lại mật khẩu", "Đặt lại mật khẩu theo boundary bảo mật hiện hữu.", "Nhân viên và tài khoản", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.LockUnlockAccounts, "Khóa hoặc mở khóa tài khoản", "Thay đổi trạng thái khóa tài khoản.", "Nhân viên và tài khoản", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.AssignRolesPermissions, "Gán vai trò và quyền", "Gán một trong các vai trò hệ thống cho tài khoản.", "Nhân viên và tài khoản", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.ViewSecurityStatus, "Xem trạng thái bảo mật", "Xem trạng thái đăng nhập và bảo mật tài khoản.", "Nhân viên và tài khoản", PermissionRisk.Elevated, false, false),
        new(SystemCapability.ManageStoreSetup, "Quản lý cài đặt cửa hàng", "Quản lý thông tin và thiết bị cửa hàng.", "Cấu hình cửa hàng", PermissionRisk.Dangerous, false, true),
        new(SystemCapability.ViewAuditLog, "Xem nhật ký hoạt động", "Tra cứu các thay đổi quản trị đã được ghi audit.", "Dữ liệu & hỗ trợ", PermissionRisk.Elevated, false, false),
        new(SystemCapability.ApplySalesDiscount, "Áp dụng giảm giá", "Áp dụng giảm giá theo policy bán hàng.", "Bán hàng", PermissionRisk.Elevated, false, true)
    ];

    public static IReadOnlyList<PermissionDefinition> All => Definitions;

    public static PermissionDefinition Get(SystemCapability capability) =>
        Definitions.First(definition => definition.Capability == capability);
}

public sealed record RolePermissionSnapshot(
    Role Role,
    string DisplayName,
    bool IsBuiltIn,
    bool IsProtected,
    int AccountUsageCount,
    IReadOnlyList<PermissionDefinition> Permissions,
    IReadOnlyList<PermissionDefinition> DeniedPermissions);
