namespace POS.Domain.Enums;

/// <summary>
/// Phương thức thanh toán của đơn hàng.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,

    VietQr = 2,

    BankTransfer = 3,

    Card = 4
}