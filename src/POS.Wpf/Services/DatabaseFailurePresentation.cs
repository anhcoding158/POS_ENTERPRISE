using POS.Application.Common;

namespace POS.Wpf.Services;

public sealed record DatabaseFailurePresentation(
    string Title,
    string Message,
    bool CanRetry);

public static class DatabaseFailurePresenter
{
    public static DatabaseFailurePresentation Present(DatabaseFailureKind kind) => kind switch
    {
        DatabaseFailureKind.Busy => new(
            "Dữ liệu đang bận",
            "Dữ liệu đang được xử lý tạm thời. Đơn hiện tại vẫn được giữ nguyên; hãy chờ một chút rồi thử lại.",
            true),
        DatabaseFailureKind.Locked => new(
            "Dữ liệu đang bị khóa",
            "Một thao tác khác đang khóa dữ liệu. Hãy đóng thao tác đó hoặc chờ hoàn tất rồi chủ động thử lại. Đơn hiện tại vẫn được giữ nguyên.",
            true),
        DatabaseFailureKind.DiskFull => new(
            "Không đủ dung lượng",
            "Chưa thể lưu dữ liệu. Hãy giải phóng dung lượng ổ đĩa an toàn rồi thử lại; ứng dụng không tự xóa dữ liệu bán hàng.",
            false),
        DatabaseFailureKind.Corruption => new(
            "Dữ liệu cần được kiểm tra",
            "Đã dừng thao tác để bảo vệ dữ liệu. Không tiếp tục ghi hoặc tạo lại tệp dữ liệu; hãy liên hệ quản trị viên để kiểm tra và phục hồi từ bản sao lưu phù hợp.",
            false),
        _ => new(
            "Không thể truy cập dữ liệu",
            "Thao tác chưa được xác nhận lưu. Dữ liệu đang nhập vẫn được giữ; vui lòng thử lại hoặc liên hệ quản trị viên.",
            false)
    };
}
