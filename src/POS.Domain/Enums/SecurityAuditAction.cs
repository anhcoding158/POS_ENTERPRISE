namespace POS.Domain.Enums;

public enum SecurityAuditAction
{
    EmployeeCreated = 1,
    EmployeeUpdated = 2,
    EmployeeDeactivated = 3,
    EmployeeReactivated = 4,
    AccountCreated = 5,
    PasswordReset = 6,
    AccountLocked = 7,
    AccountUnlocked = 8,
    RoleChanged = 9,
    ForcedPasswordChangeCompleted = 10,
    BulkProductPricesUpdated = 11,
    BulkProductCategoryChanged = 12,
    BulkProductActiveStateChanged = 13,
    BulkProductMinimumStockChanged = 14,
    BulkProductOperation = 15,
    LoginFailed = 16,
    SupplierCreated = 17,
    SupplierUpdated = 18,
    SupplierDeactivated = 19,
    SupplierReactivated = 20
}
