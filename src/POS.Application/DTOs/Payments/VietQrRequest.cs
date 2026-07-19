namespace POS.Application.DTOs.Payments;

/// <summary>
/// Dữ liệu yêu cầu tạo mã VietQR.
///
/// Thông tin tài khoản ngân hàng không nằm trong request.
/// Infrastructure sẽ đọc ngân hàng, số tài khoản và tên chủ
/// tài khoản từ cấu hình ứng dụng.
/// </summary>
public sealed class VietQrRequest
{
    public VietQrRequest(
        long amount,
        string? orderCode,
        string? transferContent = null)
    {
        Amount = amount;
        OrderCode = NormalizeRequiredText(orderCode);

        TransferContent =
            string.IsNullOrWhiteSpace(transferContent)
                ? OrderCode
                : transferContent.Trim();
    }

    /// <summary>
    /// Số tiền cần thanh toán theo đơn vị VND.
    /// </summary>
    public long Amount { get; }

    /// <summary>
    /// Mã đơn hàng dùng để đối soát.
    /// </summary>
    public string OrderCode { get; }

    /// <summary>
    /// Nội dung chuyển khoản.
    /// </summary>
    public string TransferContent { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}