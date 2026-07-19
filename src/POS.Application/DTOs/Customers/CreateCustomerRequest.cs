namespace POS.Application.DTOs.Customers;

/// <summary>
/// Dữ liệu tạo khách hàng mới.
/// </summary>
public sealed class CreateCustomerRequest
{
    public CreateCustomerRequest(
        string? code,
        string? fullName,
        string? phoneNumber,
        string? address,
        string? notes)
    {
        Code = NormalizeRequiredText(code);
        FullName = NormalizeRequiredText(fullName);
        PhoneNumber = NormalizeRequiredText(phoneNumber);
        Address = NormalizeOptionalText(address);
        Notes = NormalizeOptionalText(notes);
    }

    public string Code { get; }

    public string FullName { get; }

    public string PhoneNumber { get; }

    public string? Address { get; }

    public string? Notes { get; }

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