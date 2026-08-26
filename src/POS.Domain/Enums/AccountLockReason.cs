namespace POS.Domain.Enums;

public enum AccountLockReason
{
    None = 0,
    TemporaryFailedLogin = 1,
    ManualAdministratorAction = 2
}
