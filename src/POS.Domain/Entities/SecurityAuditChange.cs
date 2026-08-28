using System.Text.Json;
using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>
/// Thay đổi audit được allow-list ở write boundary. Không nhận entity tùy ý.
/// </summary>
public sealed record SecurityAuditChange(
    string FieldKey,
    string? BeforeValue,
    string? AfterValue);

public static class SecurityAuditChangeSet
{
    private static readonly string[] ForbiddenFragments =
    ["password", "pass", "hash", "token", "secret", "connection", "apikey", "privatekey"];

    public static string Serialize(IEnumerable<SecurityAuditChange>? changes)
    {
        var values = (changes ?? Array.Empty<SecurityAuditChange>()).ToArray();
        if (values.Length > 32)
            throw new DomainException("SECURITY_AUDIT.CHANGESET_TOO_LARGE", "Số thay đổi audit vượt giới hạn.");

        foreach (var change in values)
        {
            var key = change.FieldKey?.Trim() ?? string.Empty;
            if (key.Length is 0 or > 80 || ForbiddenFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException("SECURITY_AUDIT.FORBIDDEN_FIELD", "Trường audit không được phép.");
            if (ContainsForbiddenValue(change.BeforeValue) || ContainsForbiddenValue(change.AfterValue))
                throw new DomainException("SECURITY_AUDIT.FORBIDDEN_VALUE", "Giá trị audit không được phép.");
        }

        return JsonSerializer.Serialize(values);
    }

    public static IReadOnlyList<SecurityAuditChange> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SecurityAuditChange>();
        try
        {
            var values = JsonSerializer.Deserialize<SecurityAuditChange[]>(json) ?? [];
            return values.Length > 32 ? Array.Empty<SecurityAuditChange>() : values;
        }
        catch (JsonException)
        {
            return Array.Empty<SecurityAuditChange>();
        }
    }

    private static bool ContainsForbiddenValue(string? value) =>
        value is not null && (value.Length > 2000 || ForbiddenFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
}
