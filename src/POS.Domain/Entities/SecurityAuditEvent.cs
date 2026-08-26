using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Audit append-only cho các thay đổi nhân viên/tài khoản.
/// Không chứa mật khẩu, hash hoặc reset secret.
/// </summary>
public sealed class SecurityAuditEvent : AuditableEntity
{
    private SecurityAuditEvent()
    {
    }

    public SecurityAuditEvent(
        int? actorUserId,
        int? targetEmployeeId,
        int? targetUserId,
        SecurityAuditAction action,
        string result,
        Guid operationId,
        DateTimeOffset utcNow)
    {
        if (actorUserId is <= 0 || targetEmployeeId is <= 0 || targetUserId is <= 0)
        {
            throw new DomainException(
                "SECURITY_AUDIT.INVALID_TARGET",
                "Định danh audit không hợp lệ.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new DomainException(
                "SECURITY_AUDIT.INVALID_ACTION",
                "Hành động audit không hợp lệ.");
        }

        var normalizedResult = result?.Trim() ?? string.Empty;
        if (normalizedResult.Length == 0 || normalizedResult.Length > 100)
        {
            throw new DomainException(
                "SECURITY_AUDIT.INVALID_RESULT",
                "Kết quả audit không hợp lệ.");
        }

        ActorUserId = actorUserId;
        TargetEmployeeId = targetEmployeeId;
        TargetUserId = targetUserId;
        Action = action;
        Result = normalizedResult;
        OperationId = operationId == Guid.Empty ? Guid.NewGuid() : operationId;
        MarkCreated(utcNow);
    }

    public int? ActorUserId { get; private set; }
    public int? TargetEmployeeId { get; private set; }
    public int? TargetUserId { get; private set; }
    public SecurityAuditAction Action { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public Guid OperationId { get; private set; }
}
