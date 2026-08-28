using System.Globalization;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Audit;

public sealed record AuditSearchRequest(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Actor = null,
    string? BusinessArea = null,
    SecurityAuditAction? Action = null,
    string? Result = null,
    string? Target = null,
    int PageNumber = 1,
    int PageSize = 25);

public sealed record AuditChangeDto(string FieldKey, string? BeforeValue, string? AfterValue);

public sealed record AuditListItemDto(
    int Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    SecurityAuditAction Action,
    string BusinessArea,
    string Target,
    string Result,
    string TerminalId,
    Guid OperationId);

public sealed record AuditDetailsDto(
    int Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    SecurityAuditAction Action,
    string BusinessArea,
    string Target,
    string Result,
    string TerminalId,
    Guid OperationId,
    IReadOnlyList<AuditChangeDto> Changes)
{
    public string TargetType { get; init; } = string.Empty;
    public string LocalTimeText => OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    public string ActionText => Action switch
    {
        SecurityAuditAction.EmployeeCreated => "Tạo nhân viên",
        SecurityAuditAction.EmployeeUpdated => "Cập nhật nhân viên",
        SecurityAuditAction.EmployeeDeactivated => "Ngừng hoạt động nhân viên",
        SecurityAuditAction.EmployeeReactivated => "Kích hoạt lại nhân viên",
        SecurityAuditAction.AccountCreated => "Tạo tài khoản",
        SecurityAuditAction.RoleChanged => "Thay đổi vai trò",
        SecurityAuditAction.AccountLocked => "Khóa tài khoản",
        SecurityAuditAction.AccountUnlocked => "Mở khóa tài khoản",
        SecurityAuditAction.PasswordReset => "Đặt lại mật khẩu",
        SecurityAuditAction.ForcedPasswordChangeCompleted => "Hoàn tất đổi mật khẩu bắt buộc",
        _ => "Hoạt động không xác định"
    };
}
