using System.IO;
using POS.Application.Abstractions.StoreSetup;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.StoreSetup;

public sealed class StoreSettingsReadinessEvaluator : IStoreSettingsReadinessEvaluator
{
    private readonly IStoreSettingsValidator _validator;
    private readonly StoreSettingsPathProvider _paths;

    public StoreSettingsReadinessEvaluator(IStoreSettingsValidator validator, StoreSettingsPathProvider paths)
    { _validator = validator ?? throw new ArgumentNullException(nameof(validator)); _paths = paths ?? throw new ArgumentNullException(nameof(paths)); }

    public Task<StoreSettingsReadiness> EvaluateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = _validator.Validate(settings).Issues.ToList();
        ValidateDirectory(settings.DatabaseDirectory, "DatabaseDirectory", "Database", issues, allowCurrent: true);
        ValidateDirectory(settings.BackupDirectory, "BackupDirectory", "Backup", issues, allowCurrent: false);
        if (!string.IsNullOrWhiteSpace(settings.DefaultPrinter) &&
            !OperatingSystem.IsWindows()) issues.Add(new("Printer.Platform", "DefaultPrinter", "Không thể kiểm tra máy in trên nền tảng này.", StoreSettingsIssueSeverity.Warning));
        if (!string.IsNullOrWhiteSpace(settings.DatabaseDirectory) && !PathsEqual(settings.DatabaseDirectory, _paths.EffectiveDatabaseDirectory))
            issues.Add(new("Database.RestartRequired", "DatabaseDirectory", "Vị trí database mới sẽ được áp dụng sau khi khởi động lại.", StoreSettingsIssueSeverity.Warning));
        var ready = issues.All(x => x.Severity != StoreSettingsIssueSeverity.Error);
        return Task.FromResult(new StoreSettingsReadiness(issues, ready));
    }

    private static void ValidateDirectory(string? value, string field, string label, List<StoreSettingsIssue> issues, bool allowCurrent)
    {
        if (string.IsNullOrWhiteSpace(value)) { issues.Add(new($"{field}.Required", field, $"{label} chưa được cấu hình.")); return; }
        try
        {
            var full = Path.GetFullPath(value.Trim());
            if (!Path.IsPathFullyQualified(full) || full.StartsWith("\\\\", StringComparison.Ordinal) || IsDriveRoot(full)) { issues.Add(new($"{field}.Unsafe", field, $"{label} phải là thư mục cục bộ không phải thư mục gốc.")); return; }
            if (!allowCurrent && IsRepositoryPath(full)) { issues.Add(new($"{field}.Repository", field, $"{label} không được nằm trong repository.")); return; }
            if (HasReparse(full)) { issues.Add(new($"{field}.Reparse", field, $"{label} chứa điểm nối/reparse không an toàn.")); return; }
            if (!Directory.Exists(full)) { issues.Add(new($"{field}.Missing", field, $"{label} chưa tồn tại; hãy tạo hoặc chọn thư mục có thể ghi.")); return; }
            if (!CanWrite(full)) issues.Add(new($"{field}.NotWritable", field, $"Không thể ghi vào {label}."));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        { issues.Add(new($"{field}.Unsafe", field, $"{label} không an toàn hoặc không hợp lệ.")); }
    }

    private static bool IsRepositoryPath(string path)
    {
        var root = DatabasePathResolver.FindSolutionRoot(path);
        return root is not null && IsDescendant(root, path);
    }
    private static bool CanWrite(string dir)
    {
        var probe = Path.Combine(dir, $".pos-store-setup-{Guid.NewGuid():N}.tmp");
        try { using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { } File.Delete(probe); return true; } catch { try { if (File.Exists(probe)) File.Delete(probe); } catch { } return false; }
    }
    private static bool HasReparse(string path)
    { for (var current = new DirectoryInfo(path); current is not null; current = current.Parent) if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) return true; return false; }
    private static bool IsDescendant(string root, string path) { var rel = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path)); return rel == "." || !(rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(rel)); }
    private static bool IsDriveRoot(string path) => string.Equals(Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path) ?? string.Empty), StringComparison.OrdinalIgnoreCase);
    private static bool PathsEqual(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
