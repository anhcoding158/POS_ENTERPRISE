namespace POS.Application.Authorization;

/// <summary>
/// Các quyền nghiệp vụ ổn định của POS Enterprise.
///
/// Không kiểm tra quyền trực tiếp bằng tên Role trong UI.
/// Mỗi chức năng phải yêu cầu một SystemCapability cụ thể.
/// </summary>
public enum SystemCapability
{
    ViewProductCatalog = 1,

    ManageProducts = 2,

    ManageCategories = 3,

    ViewInventoryHistory = 4,

    AdjustInventory = 5,

    UseCheckout = 6,

    ViewReports = 7,

    ManageUsers = 8,

    ProcessReturns = 9,

    ApplySalesDiscount = 10,

    ManageStoreSetup = 11,

    ViewEmployees = 12,

    ManageEmployees = 13,

    ManageAccounts = 14,

    ResetPasswords = 15,

    LockUnlockAccounts = 16,

    AssignRolesPermissions = 17,

    ViewSecurityStatus = 18,

    ViewAuditLog = 19,

    ViewSuppliers = 20,

    ManageSuppliers = 21,

    ViewPurchaseOrders = 22,

    ManagePurchaseOrders = 23
}
