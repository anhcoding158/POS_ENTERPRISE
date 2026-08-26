using POS.Application.Authorization;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Employees;

public sealed record EmployeeListItemDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string? PhoneNumber,
    EmployeeStatus EmployeeStatus,
    int? UserId,
    string? Username,
    AccountStatus AccountStatus,
    Role? Role,
    DateTimeOffset? LastSuccessfulLoginUtc,
    int FailedLoginAttempts,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmployeeDetailsDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string? PhoneNumber,
    string? EmailAddress,
    EmployeeStatus EmployeeStatus,
    int? UserId,
    string? Username,
    AccountStatus AccountStatus,
    Role? Role,
    DateTimeOffset? LastSuccessfulLoginUtc,
    int FailedLoginAttempts,
    bool IsManuallyLocked,
    bool ForcePasswordChange,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SystemCapability> EffectivePermissions);
