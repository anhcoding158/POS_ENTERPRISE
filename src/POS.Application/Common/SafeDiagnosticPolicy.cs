using System.Globalization;
using System.Text.RegularExpressions;

namespace POS.Application.Common;

public static class SafeDiagnosticPolicy
{
    public const string Redacted = "[REDACTED]";
    public const string OverlongRecord = "[OVERLONG-RECORD-REDACTED]";

    private static readonly Regex SensitiveName = new(
        "password|passwd|pwd|pin|token|secret|apikey|api_key|cookie|session|" +
        "connectionstring|datasource|wifipassword|payload|sql|customer|phone|" +
        "email|address|accountnumber|card|receipt|cart|paymentdetail|backup(path)?|" +
        "printername|errormessage|fullname|username",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SensitiveValue = new(
        @"(?i)(data\s+source\s*=|password\s*=|token\s*=|secret\s*=|" +
        @"select\s+.+\s+from|insert\s+into|update\s+.+\s+set|delete\s+from|" +
        @"pragma\s+|bearer\s+|[A-Z]:\\Users\\|/home/|" +
        @"[\w.+-]+@[\w.-]+\.[A-Z]{2,}|(?:\+?\d[\s.-]?){9,})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Sanitize(string propertyName, object? value)
    {
        if (SensitiveName.IsMatch(propertyName ?? string.Empty)) return Redacted;
        if (value is null) return "null";

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return SanitizeText(text);
    }

    public static string SanitizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length > 256 || SensitiveValue.IsMatch(text)) return Redacted;
        return text.Replace('\r', ' ').Replace('\n', ' ');
    }
}
