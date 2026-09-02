using System.Globalization;
using POS.Domain.Enums;
using POS.Application.Services;

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
    Guid OperationId)
{
    public string ActionText => AuditPresentationResolver.ActionText(Action);
    public string ResultText => AuditPresentationResolver.ResultText(Result);
    public string TechnicalTarget { get; init; } = string.Empty;
}

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
    public string TechnicalTarget { get; init; } = string.Empty;
    public string LocalTimeText => OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    public string ActionText => AuditPresentationResolver.ActionText(Action);
    public string ResultText => AuditPresentationResolver.ResultText(Result);
    public string OperationText => ChangeValue("operation") ?? "—";
    public string RequestedCountText => ChangeValue("requested_count") ?? "—";
    public string ChangedCountText => ChangeValue("changed_count") ?? "—";
    public string NoOpCountText => ChangeValue("no_op_count") ?? "—";
    public bool HasTechnicalTarget => !string.IsNullOrWhiteSpace(TechnicalTarget);

    private string? ChangeValue(string fieldKey) =>
        Changes.FirstOrDefault(change => change.FieldKey == fieldKey)?.AfterValue;
}
