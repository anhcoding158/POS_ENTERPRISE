using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Audit append-only cho các thay đổi quản trị trong các master và tài khoản.
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
        : this(actorUserId, targetEmployeeId, targetUserId, action, result, operationId, utcNow,
            actorDisplayNameSnapshot: null, targetDisplayNameSnapshot: null,
            businessArea: "Nhân viên và tài khoản", targetType: "Nhân viên/tài khoản",
            terminalId: "Không xác định", changes: null)
    {
    }

    public SecurityAuditEvent(
        int? actorUserId,
        int? targetEmployeeId,
        int? targetUserId,
        SecurityAuditAction action,
        string result,
        Guid operationId,
        DateTimeOffset utcNow,
        string? actorDisplayNameSnapshot,
        string? targetDisplayNameSnapshot,
        string businessArea,
        string targetType,
        string terminalId,
        IEnumerable<SecurityAuditChange>? changes)
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
        ActorDisplayNameSnapshot = NormalizeSnapshot(actorDisplayNameSnapshot);
        TargetDisplayNameSnapshot = NormalizeSnapshot(targetDisplayNameSnapshot);
        BusinessArea = NormalizeRequired(businessArea, 120, "SECURITY_AUDIT.INVALID_BUSINESS_AREA");
        TargetType = NormalizeRequired(targetType, 80, "SECURITY_AUDIT.INVALID_TARGET_TYPE");
        TerminalId = NormalizeRequired(terminalId, 80, "SECURITY_AUDIT.INVALID_TERMINAL");
        BeforeValuesJson = SecurityAuditChangeSet.Serialize(changes);
        AfterValuesJson = SecurityAuditChangeSet.Serialize(changes);
        MarkCreated(utcNow);
    }

    public int? ActorUserId { get; private set; }
    public int? TargetEmployeeId { get; private set; }
    public int? TargetUserId { get; private set; }
    public SecurityAuditAction Action { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public Guid OperationId { get; private set; }
    public string ActorDisplayNameSnapshot { get; private set; } = string.Empty;
    public string TargetDisplayNameSnapshot { get; private set; } = string.Empty;
    public string BusinessArea { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TerminalId { get; private set; } = string.Empty;
    public string BeforeValuesJson { get; private set; } = "[]";
    public string AfterValuesJson { get; private set; } = "[]";

    private static string NormalizeSnapshot(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 200 ? normalized[..200] : normalized;
    }

    private static string NormalizeRequired(string? value, int maxLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw new DomainException(code, "Thông tin audit không hợp lệ.");
        return normalized;
    }
}
