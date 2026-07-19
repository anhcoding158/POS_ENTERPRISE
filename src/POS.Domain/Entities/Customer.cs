using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Khách hàng, điểm thưởng, hạng thành viên
/// và tổng giá trị mua hàng.
/// </summary>
public sealed class Customer : AuditableEntity
{
    private Customer()
    {
    }

    public Customer(
        string code,
        string fullName,
        string phoneNumber,
        DateTimeOffset utcNow,
        string? address = null,
        string? notes = null)
    {
        SetCode(code);
        SetFullName(fullName);
        SetPhoneNumber(phoneNumber);
        SetAddress(address);
        SetNotes(notes);

        Tier = CustomerTier.Standard;
        IsActive = true;

        MarkCreated(utcNow);
    }

    public string Code { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string NormalizedPhoneNumber { get; private set; } =
        string.Empty;

    public string? Address { get; private set; }

    public string? Notes { get; private set; }

    public CustomerTier Tier { get; private set; }

    public long LoyaltyPoints { get; private set; }

    public long TotalSpent { get; private set; }

    public int OrderCount { get; private set; }

    public DateTimeOffset? LastPurchaseAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeCode(
        string code,
        DateTimeOffset utcNow)
    {
        SetCode(code);
        MarkUpdated(utcNow);
    }

    public void UpdateProfile(
        string fullName,
        string phoneNumber,
        string? address,
        string? notes,
        DateTimeOffset utcNow)
    {
        SetFullName(fullName);
        SetPhoneNumber(phoneNumber);
        SetAddress(address);
        SetNotes(notes);

        MarkUpdated(utcNow);
    }

    public void RegisterPurchase(
        long amount,
        long earnedPoints,
        DateTimeOffset utcNow)
    {
        if (amount <= 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_PURCHASE_AMOUNT",
                "Giá trị mua hàng phải lớn hơn 0.");
        }

        if (earnedPoints < 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_EARNED_POINTS",
                "Điểm được cộng không được nhỏ hơn 0.");
        }

        EnsureCanAdd(
            TotalSpent,
            amount,
            "CUSTOMER.TOTAL_SPENT_OVERFLOW");

        EnsureCanAdd(
            LoyaltyPoints,
            earnedPoints,
            "CUSTOMER.POINTS_OVERFLOW");

        if (OrderCount == int.MaxValue)
        {
            throw new DomainException(
                "CUSTOMER.ORDER_COUNT_OVERFLOW",
                "Số lượng đơn hàng của khách đã vượt giới hạn.");
        }

        TotalSpent += amount;
        LoyaltyPoints += earnedPoints;
        OrderCount++;
        LastPurchaseAtUtc = utcNow.ToUniversalTime();

        MarkUpdated(utcNow);
    }

    public void RegisterRefund(
        long refundedAmount,
        long pointsToRemove,
        DateTimeOffset utcNow)
    {
        if (refundedAmount <= 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_REFUND_AMOUNT",
                "Giá trị hoàn trả phải lớn hơn 0.");
        }

        if (refundedAmount > TotalSpent)
        {
            throw new DomainException(
                "CUSTOMER.REFUND_EXCEEDS_TOTAL_SPENT",
                "Giá trị hoàn trả vượt quá tổng chi tiêu của khách.");
        }

        if (pointsToRemove < 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_POINTS_TO_REMOVE",
                "Số điểm cần trừ không được nhỏ hơn 0.");
        }

        TotalSpent -= refundedAmount;

        LoyaltyPoints = Math.Max(
            0,
            LoyaltyPoints - pointsToRemove);

        MarkUpdated(utcNow);
    }

    public void AddLoyaltyPoints(
        long points,
        DateTimeOffset utcNow)
    {
        if (points <= 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_POINTS",
                "Số điểm cộng phải lớn hơn 0.");
        }

        EnsureCanAdd(
            LoyaltyPoints,
            points,
            "CUSTOMER.POINTS_OVERFLOW");

        LoyaltyPoints += points;

        MarkUpdated(utcNow);
    }

    public void RedeemLoyaltyPoints(
        long points,
        DateTimeOffset utcNow)
    {
        if (points <= 0)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_REDEEM_POINTS",
                "Số điểm sử dụng phải lớn hơn 0.");
        }

        if (points > LoyaltyPoints)
        {
            throw new DomainException(
                "CUSTOMER.INSUFFICIENT_POINTS",
                "Khách hàng không đủ điểm để sử dụng.");
        }

        LoyaltyPoints -= points;

        MarkUpdated(utcNow);
    }

    public void ChangeTier(
        CustomerTier tier,
        DateTimeOffset utcNow)
    {
        if (!Enum.IsDefined(tier))
        {
            throw new DomainException(
                "CUSTOMER.INVALID_TIER",
                "Hạng khách hàng không hợp lệ.");
        }

        Tier = tier;

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

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(utcNow);
    }

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(
                "CUSTOMER.CODE_REQUIRED",
                "Mã khách hàng không được để trống.");
        }

        var normalized = code
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Customers.CodeMaxLength)
        {
            throw new DomainException(
                "CUSTOMER.CODE_TOO_LONG",
                "Mã khách hàng vượt quá giới hạn cho phép.");
        }

        Code = normalized;
    }

    private void SetFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException(
                "CUSTOMER.FULL_NAME_REQUIRED",
                "Tên khách hàng không được để trống.");
        }

        var trimmed = fullName.Trim();

        if (trimmed.Length >
            BusinessRules.Customers.FullNameMaxLength)
        {
            throw new DomainException(
                "CUSTOMER.FULL_NAME_TOO_LONG",
                "Tên khách hàng vượt quá giới hạn cho phép.");
        }

        FullName = trimmed;
    }

    private void SetPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException(
                "CUSTOMER.PHONE_REQUIRED",
                "Số điện thoại không được để trống.");
        }

        var normalized = NormalizePhoneNumber(phoneNumber);

        if (normalized.Length <
                BusinessRules.Customers.PhoneNumberMinLength ||
            normalized.Length >
                BusinessRules.Customers.PhoneNumberMaxLength)
        {
            throw new DomainException(
                "CUSTOMER.INVALID_PHONE_LENGTH",
                "Độ dài số điện thoại không hợp lệ.");
        }

        PhoneNumber = phoneNumber.Trim();
        NormalizedPhoneNumber = normalized;
    }

    private void SetAddress(string? address)
    {
        var normalized = NormalizeOptionalText(address);

        if (normalized?.Length >
            BusinessRules.Customers.AddressMaxLength)
        {
            throw new DomainException(
                "CUSTOMER.ADDRESS_TOO_LONG",
                "Địa chỉ khách hàng vượt quá giới hạn.");
        }

        Address = normalized;
    }

    private void SetNotes(string? notes)
    {
        var normalized = NormalizeOptionalText(notes);

        if (normalized?.Length >
            BusinessRules.Customers.NotesMaxLength)
        {
            throw new DomainException(
                "CUSTOMER.NOTES_TOO_LONG",
                "Ghi chú khách hàng vượt quá giới hạn.");
        }

        Notes = normalized;
    }

    private static string NormalizePhoneNumber(string value)
    {
        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        if (digits.StartsWith(
                "0084",
                StringComparison.Ordinal))
        {
            digits = "0" + digits[4..];
        }
        else if (digits.StartsWith(
                     "84",
                     StringComparison.Ordinal) &&
                 digits.Length >= 11)
        {
            digits = "0" + digits[2..];
        }

        return digits;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void EnsureCanAdd(
        long current,
        long addition,
        string errorCode)
    {
        if (addition > long.MaxValue - current)
        {
            throw new DomainException(
                errorCode,
                "Giá trị vượt quá giới hạn hệ thống.");
        }
    }
}