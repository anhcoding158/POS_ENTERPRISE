using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Typed configuration ánh xạ từ section:
///
/// "Store"
///
/// Không khai báo WifiPassword hoặc bất kỳ secret nào vì
/// các giá trị đó không thuộc dữ liệu hóa đơn.
/// </summary>
public sealed class ReceiptStoreOptions
{
    public const string SectionName =
        "Store";

    public string Name { get; set; } =
        string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? TaxCode { get; set; }

    public string? FooterMessage { get; set; }

    /// <summary>
    /// Kiểm tra cấu hình và tạo snapshot bất biến.
    ///
    /// ReceiptStoreSnapshotDto là nguồn sự thật cho:
    /// - chuẩn hóa khoảng trắng;
    /// - giới hạn độ dài;
    /// - tên cửa hàng bắt buộc;
    /// - loại bỏ giá trị tùy chọn trống.
    /// </summary>
    public ReceiptStoreSnapshotDto CreateSnapshot()
    {
        return new ReceiptStoreSnapshotDto(
            name:
                Name,

            address:
                Address,

            phone:
                Phone,

            taxCode:
                TaxCode,

            footerMessage:
                FooterMessage);
    }

    public void Validate()
    {
        _ =
            CreateSnapshot();
    }
}

/// <summary>
/// Infrastructure implementation cung cấp snapshot cửa hàng
/// đã được kiểm tra từ typed configuration.
///
/// Snapshot được tạo một lần khi provider được khởi tạo.
/// Mọi receipt trong cùng phiên ứng dụng dùng cùng thông tin
/// cửa hàng ổn định, không bị thay đổi giữa lúc checkout.
/// </summary>
public sealed class ReceiptStoreSnapshotProvider :
    IReceiptStoreSnapshotProvider
{
    private readonly ReceiptStoreSnapshotDto
        _storeSnapshot;

    public ReceiptStoreSnapshotProvider(
        IOptions<ReceiptStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        var value =
            options.Value ??
            throw new InvalidOperationException(
                "Không tìm thấy cấu hình Store.");

        try
        {
            value.Validate();

            _storeSnapshot =
                value.CreateSnapshot();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Cấu hình Store không hợp lệ.",
                exception);
        }
    }

    public ReceiptStoreSnapshotDto
        GetCurrentSnapshot()
    {
        /*
         * ReceiptStoreSnapshotDto là immutable,
         * nên có thể chia sẻ an toàn trong toàn bộ ứng dụng.
         */
        return _storeSnapshot;
    }
}