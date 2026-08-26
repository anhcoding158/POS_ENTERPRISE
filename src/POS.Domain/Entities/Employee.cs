using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Hồ sơ nhân viên. Tài khoản đăng nhập là quan hệ tùy chọn.
/// Không xóa hồ sơ để giữ lịch sử nghiệp vụ và audit.
/// </summary>
public sealed class Employee : AuditableEntity
{
    private Employee()
    {
    }

    public Employee(
        string employeeCode,
        string fullName,
        string? phoneNumber,
        string? emailAddress,
        DateTimeOffset utcNow)
    {
        SetEmployeeCode(employeeCode);
        SetFullName(fullName);
        SetPhoneNumber(phoneNumber);
        SetEmailAddress(emailAddress);
        IsActive = true;
        MarkCreated(utcNow);
    }

    public string EmployeeCode { get; private set; } = string.Empty;

    public string NormalizedEmployeeCode { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public string? EmailAddress { get; private set; }

    public bool IsActive { get; private set; }

    public int? UserId { get; private set; }

    public User? LoginAccount { get; private set; }

    public void UpdateProfile(
        string employeeCode,
        string fullName,
        string? phoneNumber,
        string? emailAddress,
        DateTimeOffset utcNow)
    {
        SetEmployeeCode(employeeCode);
        SetFullName(fullName);
        SetPhoneNumber(phoneNumber);
        SetEmailAddress(emailAddress);
        MarkUpdated(utcNow);
    }

    public void AttachAccount(
        User account,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (LoginAccount is not null || UserId.HasValue)
        {
            throw new DomainException(
                "EMPLOYEE.ACCOUNT_ALREADY_LINKED",
                "Nhân viên đã được liên kết tài khoản đăng nhập.");
        }

        LoginAccount = account;
        UserId = account.Id > 0 ? account.Id : null;
        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated(utcNow);
    }

    private void SetEmployeeCode(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < BusinessRules.Employees.EmployeeCodeMinLength ||
            trimmed.Length > BusinessRules.Employees.EmployeeCodeMaxLength ||
            !trimmed.All(IsAllowedCodeCharacter))
        {
            throw new DomainException(
                "EMPLOYEE.INVALID_CODE",
                "Mã nhân viên không hợp lệ.");
        }

        EmployeeCode = trimmed;
        NormalizedEmployeeCode = trimmed.ToUpperInvariant();
    }

    private void SetFullName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 ||
            trimmed.Length > BusinessRules.Employees.FullNameMaxLength ||
            trimmed.Any(char.IsControl))
        {
            throw new DomainException(
                "EMPLOYEE.INVALID_NAME",
                "Họ tên nhân viên không hợp lệ.");
        }

        FullName = trimmed;
    }

    private void SetPhoneNumber(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (trimmed is not null &&
            (trimmed.Length > BusinessRules.Employees.PhoneNumberMaxLength ||
             trimmed.Any(char.IsControl)))
        {
            throw new DomainException(
                "EMPLOYEE.INVALID_PHONE",
                "Số điện thoại nhân viên không hợp lệ.");
        }

        PhoneNumber = trimmed;
    }

    private void SetEmailAddress(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (trimmed is not null &&
            (trimmed.Length > BusinessRules.Employees.EmailAddressMaxLength ||
             trimmed.Any(char.IsControl)))
        {
            throw new DomainException(
                "EMPLOYEE.INVALID_EMAIL",
                "Email nhân viên không hợp lệ.");
        }

        EmailAddress = trimmed;
    }

    private static bool IsAllowedCodeCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
    }
}
