using POS.Domain.Enums;

namespace POS.Application.DTOs.Customers;

/// <summary>
/// Dữ liệu cập nhật thông tin khách hàng.
/// </summary>
public sealed class UpdateCustomerRequest
{
    public UpdateCustomerRequest(
        int customerId,
        string? code,
        string? fullName,
        string? phoneNumber,
        string? address,
        string? notes,
        CustomerTier tier,
        bool isActive)
    {
        CustomerId = customerId;

        Code = NormalizeRequiredText(code);
        FullName = NormalizeRequiredText(fullName);
        PhoneNumber = NormalizeRequiredText(phoneNumber);
        Address = NormalizeOptionalText(address);
        Notes = NormalizeOptionalText(notes);

        Tier = tier;
        IsActive = isActive;
    }

    public int CustomerId { get; }

    public string Code { get; }

    public string FullName { get; }

    public string PhoneNumber { get; }

    public string? Address { get; }

    public string? Notes { get; }

    public CustomerTier Tier { get; }

    public bool IsActive { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}