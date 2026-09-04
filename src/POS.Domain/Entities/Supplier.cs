using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Hồ sơ nhà cung cấp. Supplier không bị hard-delete để giữ lịch sử
/// chứng từ mua hàng trong các checkpoint sau.
/// </summary>
public sealed class Supplier : AuditableEntity
{
    private Supplier()
    {
    }

    public Supplier(
        string code,
        string name,
        string? taxCode,
        string? contactName,
        string? phoneNumber,
        string? emailAddress,
        string? address,
        string? notes,
        DateTimeOffset utcNow)
    {
        SetCode(code);
        SetName(name);
        TaxCode = NormalizeOptional(taxCode, BusinessRules.Suppliers.TaxCodeMaxLength, "SUPPLIER.INVALID_TAX_CODE");
        ContactName = NormalizeOptional(contactName, BusinessRules.Suppliers.ContactNameMaxLength, "SUPPLIER.INVALID_CONTACT_NAME");
        PhoneNumber = NormalizeOptional(phoneNumber, BusinessRules.Suppliers.PhoneNumberMaxLength, "SUPPLIER.INVALID_PHONE");
        EmailAddress = NormalizeOptional(emailAddress, BusinessRules.Suppliers.EmailAddressMaxLength, "SUPPLIER.INVALID_EMAIL");
        Address = NormalizeOptional(address, BusinessRules.Suppliers.AddressMaxLength, "SUPPLIER.INVALID_ADDRESS");
        Notes = NormalizeOptional(notes, BusinessRules.Suppliers.NotesMaxLength, "SUPPLIER.INVALID_NOTES");
        IsActive = true;
        MarkCreated(utcNow);
    }

    public string Code { get; private set; } = string.Empty;
    public string NormalizedCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? TaxCode { get; private set; }
    public string? ContactName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? EmailAddress { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdateProfile(
        string code,
        string name,
        string? taxCode,
        string? contactName,
        string? phoneNumber,
        string? emailAddress,
        string? address,
        string? notes,
        DateTimeOffset utcNow)
    {
        SetCode(code);
        SetName(name);
        TaxCode = NormalizeOptional(taxCode, BusinessRules.Suppliers.TaxCodeMaxLength, "SUPPLIER.INVALID_TAX_CODE");
        ContactName = NormalizeOptional(contactName, BusinessRules.Suppliers.ContactNameMaxLength, "SUPPLIER.INVALID_CONTACT_NAME");
        PhoneNumber = NormalizeOptional(phoneNumber, BusinessRules.Suppliers.PhoneNumberMaxLength, "SUPPLIER.INVALID_PHONE");
        EmailAddress = NormalizeOptional(emailAddress, BusinessRules.Suppliers.EmailAddressMaxLength, "SUPPLIER.INVALID_EMAIL");
        Address = NormalizeOptional(address, BusinessRules.Suppliers.AddressMaxLength, "SUPPLIER.INVALID_ADDRESS");
        Notes = NormalizeOptional(notes, BusinessRules.Suppliers.NotesMaxLength, "SUPPLIER.INVALID_NOTES");
        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive) return;
        IsActive = true;
        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive) return;
        IsActive = false;
        MarkUpdated(utcNow);
    }

    private void SetCode(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < BusinessRules.Suppliers.CodeMinLength ||
            trimmed.Length > BusinessRules.Suppliers.CodeMaxLength ||
            !trimmed.All(IsAllowedCodeCharacter))
        {
            throw new DomainException("SUPPLIER.INVALID_CODE", "Mã nhà cung cấp không hợp lệ.");
        }

        Code = trimmed;
        NormalizedCode = trimmed.ToUpperInvariant();
    }

    private static void ValidateText(string? value, int maxLength, string code, string message, bool required = false)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if ((required && trimmed.Length == 0) || trimmed.Length > maxLength || trimmed.Any(char.IsControl))
            throw new DomainException(code, message);
    }

    private void SetName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        ValidateText(trimmed, BusinessRules.Suppliers.NameMaxLength, "SUPPLIER.INVALID_NAME", "Tên nhà cung cấp không hợp lệ.", required: true);
        Name = trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        ValidateText(trimmed, maxLength, code, "Thông tin nhà cung cấp không hợp lệ.");
        return trimmed;
    }

    private static bool IsAllowedCodeCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
}
