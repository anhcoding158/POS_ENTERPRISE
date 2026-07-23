using POS.Application.DTOs.Printing;

namespace POS.Application.Abstractions.Printing;

/// <summary>
/// Chuyển snapshot hóa đơn bất biến sang định dạng lưu trữ
/// và đọc lại snapshot đã lưu.
///
/// Implementation phải:
/// - giữ nguyên Unicode tiếng Việt;
/// - giữ nguyên tiền VND kiểu long;
/// - kiểm tra phiên bản contract;
/// - từ chối JSON hỏng hoặc dữ liệu không nhất quán;
/// - không tự đọc lại Product, Order hoặc cấu hình cửa hàng live.
/// </summary>
public interface IReceiptSnapshotSerializer
{
    /// <summary>
    /// Chuyển snapshot hợp lệ thành JSON ổn định để lưu trữ.
    /// </summary>
    string Serialize(
        ReceiptRequest snapshot);

    /// <summary>
    /// Đọc và kiểm tra toàn bộ snapshot từ JSON.
    ///
    /// Ném InvalidDataException nếu JSON không đúng contract,
    /// sai phiên bản hoặc vi phạm invariant hóa đơn.
    /// </summary>
    ReceiptRequest Deserialize(
        string json);
}