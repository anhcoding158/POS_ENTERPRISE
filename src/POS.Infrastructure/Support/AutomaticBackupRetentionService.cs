using System.Globalization;
using System.IO;
using POS.Application.Abstractions.Services;

namespace POS.Infrastructure.Support;

public sealed record AutomaticBackupRetentionResult(string? Warning)
{
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
}

public sealed class AutomaticBackupRetentionService(AutomaticBackupPathProvider paths, AutomaticBackupPolicy policy)
{
    public Task<AutomaticBackupRetentionResult> PruneAsync(string protectedArtifactIdentifier, CancellationToken cancellationToken = default)
    {
        if (!paths.IsOwnedArtifactIdentifier(protectedArtifactIdentifier))
            return Task.FromResult(new AutomaticBackupRetentionResult("Không thể xác nhận artifact mới nhất để áp dụng retention."));
        var warnings = new List<string>();
        try
        {
            if (!paths.IsManagedRootSafe()) return Warning("Thư mục automatic backup không an toàn để áp dụng retention.");
            var root = new DirectoryInfo(paths.Root);
            if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0) return Warning("Thư mục automatic backup không an toàn để áp dụng retention.");
            var files = root.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Where(IsEligible)
                .Select(file => (File: file, Timestamp: Parse(file.Name)!)).OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.File.Name, StringComparer.Ordinal).Select(x => x.File).ToList();
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files.Take(Math.Max(0, policy.RecentRetentionCount))) keep.Add(file.Name);
            foreach (var group in files.Where(x => !keep.Contains(x.Name)).GroupBy(x => Week(Parse(x.Name)!.Value))
                .OrderByDescending(x => x.Key.Year).ThenByDescending(x => x.Key.Week).Take(Math.Max(0, policy.WeeklyRetentionCount)))
                keep.Add(group.OrderByDescending(x => Parse(x.Name)).ThenByDescending(x => x.Name, StringComparer.Ordinal).First().Name);
            foreach (var group in files.Where(x => !keep.Contains(x.Name)).GroupBy(x => (Parse(x.Name)!.Value.Year, Parse(x.Name)!.Value.Month))
                .OrderByDescending(x => x.Key.Year).ThenByDescending(x => x.Key.Month).Take(Math.Max(0, policy.MonthlyRetentionCount)))
                keep.Add(group.OrderByDescending(x => Parse(x.Name)).ThenByDescending(x => x.Name, StringComparer.Ordinal).First().Name);
            keep.Add(protectedArtifactIdentifier);
            ulong total = files.Aggregate(0UL, (sum, file) => Add(sum, (ulong)Math.Max(0, file.Length)));
            var count = files.Count;
            foreach (var file in files.Where(x => !keep.Contains(x.Name))
                .OrderBy(x => Parse(x.Name))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { if (!IsEligible(file)) continue; var length = (ulong)Math.Max(0, file.Length); file.Delete(); count--; total = total >= length ? total - length : 0; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warnings.Add("Không thể xóa một automatic backup cũ."); }
            }
            if (count > keep.Count || total > (ulong)policy.MaximumTotalBytes) warnings.Add("Retention chưa thể đạt quota; các snapshot GFS bắt buộc vẫn được giữ.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warnings.Add("Không thể hoàn tất retention."); }
        return Task.FromResult(new AutomaticBackupRetentionResult(warnings.Count == 0 ? null : string.Join(" ", warnings.Distinct(StringComparer.Ordinal))));
    }
    private static Task<AutomaticBackupRetentionResult> Warning(string message) => Task.FromResult(new AutomaticBackupRetentionResult(message));
    private bool IsEligible(FileInfo file) { try { return paths.IsOwnedArtifactIdentifier(file.Name) && (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0 && string.Equals(Path.GetDirectoryName(Path.GetFullPath(file.FullName)), paths.Root, StringComparison.OrdinalIgnoreCase) && Parse(file.Name) is not null; } catch { return false; } }
    private static DateTimeOffset? Parse(string name) { const string p = "pos-enterprise-automatic-"; const string s = ".db"; if (!name.StartsWith(p, StringComparison.Ordinal) || !name.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return null; return DateTimeOffset.TryParseExact(name[p.Length..^s.Length], "yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value) ? value : null; }
    private static (int Year, int Week) Week(DateTimeOffset value) => (ISOWeek.GetYear(value.UtcDateTime), ISOWeek.GetWeekOfYear(value.UtcDateTime));
    private static ulong Add(ulong left, ulong right) => ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
}
