using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace POS.Infrastructure.Logging;

internal static partial class ManagedLogPolicy
{
    internal static bool IsManagedRegularFileCandidate(
        string logRoot,
        string candidatePath,
        FileAttributes attributes)
    {
        try
        {
            var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(logRoot));
            var canonicalCandidate = Path.GetFullPath(candidatePath);
            var parent = Path.GetDirectoryName(canonicalCandidate);
            return parent is not null &&
                string.Equals(parent, canonicalRoot, StringComparison.OrdinalIgnoreCase) &&
                TryParseName(Path.GetFileName(canonicalCandidate), out _, out _) &&
                (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryParseName(string name, out DateOnly date, out int sequence)
    {
        date = default;
        sequence = default;
        var match = ManagedFileName().Match(name);
        return match.Success &&
            DateOnly.TryParseExact(match.Groups[1].Value, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
            int.TryParse(match.Groups[2].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out sequence);
    }

    [GeneratedRegex(@"^pos-enterprise-(\d{8})-(\d{4})\.log$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ManagedFileName();
}
