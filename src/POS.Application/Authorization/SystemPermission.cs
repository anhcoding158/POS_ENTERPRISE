namespace POS.Application.Authorization;

/// <summary>
/// Các quyền nghiệp vụ ổn định của POS Enterprise.
///
/// Không kiểm tra quyền trực tiếp bằng tên Role trong UI.
/// Mỗi chức năng phải yêu cầu một SystemPermission cụ thể.
/// </summary>
public enum SystemPermission
{
    ViewProductCatalog = 1,

    ManageProducts = 2,

    ManageCategories = 3,

    ViewInventoryHistory = 4,

    AdjustInventory = 5,

    UseCheckout = 6,

    ViewReports = 7,

    ManageUsers = 8
}