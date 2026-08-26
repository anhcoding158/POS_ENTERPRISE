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
    ForcedPasswordChangeCompleted = 10
}
