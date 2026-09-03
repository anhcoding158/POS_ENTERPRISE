using POS.Domain.Enums;

namespace POS.Application.DTOs.Employees;

public sealed class EmployeeSearchRequest
{
    public string? SearchTerm { get; init; }
    public EmployeeStatus? EmployeeStatus { get; init; }
    public AccountStatus? AccountStatus { get; init; }
    public Role? Role { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class CreateEmployeeRequest
{
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public bool CreateAccount { get; init; }
    public string? Username { get; init; }
    public string? TemporaryPassword { get; init; }
    public Role Role { get; init; } = Role.Cashier;
}

public sealed class UpdateEmployeeRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
}

public sealed class CreateEmployeeAccountRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public string Username { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
    public Role Role { get; init; } = Role.Cashier;
}

public sealed class ResetEmployeePasswordRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
}

public sealed class SetAccountLockRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public bool Locked { get; init; }
}

public sealed class SetEmployeeActiveRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public bool Active { get; init; }
}

public sealed class SetAccountActiveRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public bool Active { get; init; }
}

public sealed class ChangeEmployeeRoleRequest
{
    public int EmployeeId { get; init; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; init; }
    public Role Role { get; init; }
}

public sealed class CompletePasswordChangeRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
