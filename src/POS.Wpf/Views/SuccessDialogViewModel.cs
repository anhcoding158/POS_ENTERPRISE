using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public sealed class SuccessDialogViewModel
{
    public SuccessDialogViewModel(
        string title,
        string message,
        IReadOnlyList<SuccessDialogLine> details,
        string acknowledgeAutomationId)
    {
        Title = title;
        Message = message;
        Details = details;
        AcknowledgeAutomationId = acknowledgeAutomationId;
    }

    public string Title { get; }
    public string Message { get; }
    public IReadOnlyList<SuccessDialogLine> Details { get; }
    public string AcknowledgeAutomationId { get; }

    public static SuccessDialogViewModel From(SuccessDialogRequest request) => new(
        request.Title,
        request.Message,
        request.Details,
        request.AcknowledgeAutomationId);

    public static SuccessDialogViewModel FromEmployee(EmployeeAccountSuccessEventArgs details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (details.IsEmployeeCreate)
        {
            return new(
                details.Title,
                $"Hồ sơ {details.EmployeeName} đã được tạo.",
                [
                    new($"Mã nhân viên: {details.EmployeeCode}"),
                    new($"Trạng thái: {details.EmployeeStatus}")
                ],
                "EmployeeAccountSuccessAcknowledgeButton");
        }

        return new(
            details.Title,
            details.IsCreate
                ? $"Tài khoản {details.Username} đã được tạo cho {details.EmployeeName}."
                : $"Đã đặt lại mật khẩu cho tài khoản {details.Username}.",
            [
                new("Hãy bàn giao mật khẩu tạm thời cho nhân viên."),
                new(details.IsCreate
                    ? "Nhân viên phải đổi mật khẩu khi đăng nhập lần đầu."
                    : "Nhân viên phải đổi mật khẩu khi đăng nhập tiếp theo."),
                new("Trạng thái: Chờ nhân viên đổi mật khẩu lần đầu")
            ],
            "EmployeeAccountSuccessAcknowledgeButton");
    }
}
