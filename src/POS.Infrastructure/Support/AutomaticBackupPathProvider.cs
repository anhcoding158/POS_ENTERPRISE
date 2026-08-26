using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using POS.Infrastructure.Persistence;
using POS.Application.Abstractions.StoreSetup;
using POS.Infrastructure.StoreSetup;

namespace POS.Infrastructure.Support;

public sealed partial class AutomaticBackupPathProvider
{
    public const string StateFileName = "automatic-backup-state.json";
    public const string ArtifactPrefix = "pos-enterprise-automatic-";
    public const string RootDirectoryName = "automatic-backups";

    private readonly string _root;
    private readonly IStoreSettingsStore? _settingsStore;
    private readonly bool _isolated;

    public AutomaticBackupPathProvider() : this(GetCanonicalProductionRoot()) { }

    public AutomaticBackupPathProvider(string root, IStoreSettingsStore? settingsStore = null, bool isolated = false)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("Automatic backup root must be absolute.", nameof(root));
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _settingsStore = settingsStore;
        _isolated = isolated;
    }

    public static AutomaticBackupPathProvider CreateForRuntime(
        string? runtimeMode,
        string? effectiveDatabasePath,
        string applicationBaseDirectory) =>
        CreateForRuntime(runtimeMode, effectiveDatabasePath, applicationBaseDirectory,
            GetCanonicalProductionRoot());

    internal static AutomaticBackupPathProvider CreateForRuntime(
        string? runtimeMode,
        string? effectiveDatabasePath,
        string applicationBaseDirectory,
        string canonicalProductionRoot)
    {
        if (!string.Equals(runtimeMode, DatabaseRuntimeGuard.IsolatedTestMode, StringComparison.Ordinal))
            return new AutomaticBackupPathProvider();

        if (string.IsNullOrWhiteSpace(effectiveDatabasePath) ||
            !Path.IsPathFullyQualified(effectiveDatabasePath))
            throw new AutomaticBackupIsolationException(
                "IsolatedTest requires an absolute Infrastructure:DatabasePath.");

        if (string.IsNullOrWhiteSpace(applicationBaseDirectory) ||
            !Path.IsPathFullyQualified(applicationBaseDirectory))
            throw new AutomaticBackupIsolationException(
                "IsolatedTest requires an absolute application boundary.");

        try
        {
            RejectTraversalSegments(effectiveDatabasePath);
            var databasePath = Path.GetFullPath(effectiveDatabasePath);
            var databaseDirectory = Path.GetDirectoryName(databasePath);
            if (string.IsNullOrWhiteSpace(databaseDirectory) || IsDriveRoot(databaseDirectory) ||
                !Directory.Exists(databaseDirectory))
                throw new AutomaticBackupIsolationException(
                    "The isolated database directory is not a usable existing boundary.");

            databaseDirectory = NormalizeDirectory(databaseDirectory);
            var root = NormalizeDirectory(Path.Combine(databaseDirectory, RootDirectoryName));
            if (!IsDirectChild(databaseDirectory, root))
                throw new AutomaticBackupIsolationException(
                    "The isolated automatic backup root must be a direct child of the database directory.");

            if (PathsEqual(root, canonicalProductionRoot))
                throw new AutomaticBackupIsolationException(
                    "The isolated automatic backup root must not be the production root.");

            var applicationBoundary = NormalizeDirectory(applicationBaseDirectory);
            RejectApplicationOrRepositoryBoundary(databasePath, root, applicationBoundary);

            if (HasReparsePointInExistingChain(databaseDirectory) ||
                HasReparsePointInExistingChain(root) ||
                File.Exists(databasePath) &&
                (File.GetAttributes(databasePath) & FileAttributes.ReparsePoint) != 0)
                throw new AutomaticBackupIsolationException(
                    "The isolated automatic backup boundary must not contain a reparse point.");

            return new AutomaticBackupPathProvider(root);
        }
        catch (AutomaticBackupIsolationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
            NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            throw new AutomaticBackupIsolationException(
                "The isolated automatic backup configuration is invalid.", exception);
        }
    }

    public static string GetCanonicalProductionRoot()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
            throw new InvalidOperationException("LocalApplicationData is unavailable.");
        return NormalizeDirectory(Path.Combine(
            localApplicationData, "POS Enterprise", RootDirectoryName));
    }

    public string Root => ResolveEffectiveRoot();
    public string StatePath => Path.Combine(Root, StateFileName);

    public bool IsManagedRootSafe()
    {
        try
        {
            for (var current = new DirectoryInfo(Root); current is not null; current = current.Parent)
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            }
            return true;
        }
        catch { return false; }
    }

    private static void RejectApplicationOrRepositoryBoundary(
        string databasePath,
        string root,
        string applicationBoundary)
    {
        if (IsEqualOrDescendant(applicationBoundary, databasePath) ||
            IsEqualOrDescendant(applicationBoundary, root))
            throw new AutomaticBackupIsolationException(
                "The isolated automatic backup boundary must not be inside application output.");

        var repositoryBoundary = DatabasePathResolver.FindSolutionRoot(applicationBoundary);
        if (repositoryBoundary is not null &&
            (IsEqualOrDescendant(repositoryBoundary, databasePath) ||
             IsEqualOrDescendant(repositoryBoundary, root)))
            throw new AutomaticBackupIsolationException(
                "The isolated automatic backup boundary must not be inside the repository.");
    }

    private static bool IsDirectChild(string parent, string candidate) =>
        string.Equals(Path.GetDirectoryName(candidate), parent, StringComparison.OrdinalIgnoreCase);

    private static bool IsEqualOrDescendant(string parent, string candidate)
    {
        var relative = Path.GetRelativePath(NormalizeDirectory(parent), Path.GetFullPath(candidate));
        return string.Equals(relative, ".", StringComparison.Ordinal) ||
            !(string.Equals(relative, "..", StringComparison.Ordinal) ||
              relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
              relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
              Path.IsPathRooted(relative));
    }

    private static bool HasReparsePointInExistingChain(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static void RejectTraversalSegments(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var remainder = path[root.Length..];
        if (remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new AutomaticBackupIsolationException(
                "The isolated database path must not contain traversal segments.");
    }

    private static bool IsDriveRoot(string path) =>
        PathsEqual(path, Path.GetPathRoot(Path.GetFullPath(path)) ?? string.Empty);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizeDirectory(left), NormalizeDirectory(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public string CreateArtifactIdentifier(DateTimeOffset utcNow)
    {
        var stem = ArtifactPrefix + utcNow.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        for (var suffix = 0; ; suffix++)
        {
            var name = suffix == 0 ? stem + ".db" : $"{stem}-{suffix:D3}.db";
            if (!File.Exists(Path.Combine(Root, name))) return name;
        }
    }

    public bool IsOwnedArtifactIdentifier(string? identifier)
    {
        if (Root.Length == 0) return false;
        if (string.IsNullOrWhiteSpace(identifier) || Path.IsPathRooted(identifier) ||
            identifier.Contains(Path.DirectorySeparatorChar) ||
            identifier.Contains(Path.AltDirectorySeparatorChar) || identifier.Contains("..", StringComparison.Ordinal))
            return false;
        if (!OwnedArtifactRegex().IsMatch(identifier)) return false;
        var timestamp = identifier.Substring(ArtifactPrefix.Length, 18);
        return DateTime.TryParseExact(timestamp, "yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
    }

    public bool TryGetOwnedArtifactPath(string? identifier, out string? path)
    {
        path = null;
        if (!IsOwnedArtifactIdentifier(identifier)) return false;
        var candidate = Path.GetFullPath(Path.Combine(Root, identifier!));
        if (!string.Equals(Path.GetDirectoryName(candidate), Root, StringComparison.OrdinalIgnoreCase)) return false;
        path = candidate;
        return true;
    }

    private string ResolveEffectiveRoot()
    {
        if (_settingsStore is null || _isolated) return _root;
        var configured = _settingsStore.Current.BackupDirectory;
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured) || configured.StartsWith("\\\\", StringComparison.Ordinal)) return _root;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured)); }
        catch (ArgumentException) { return _root; }
    }

    [GeneratedRegex("^pos-enterprise-automatic-[0-9]{8}-[0-9]{9}(?:-[0-9]{3})?\\.db$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedArtifactRegex();
}

public sealed class AutomaticBackupIsolationException : InvalidOperationException
{
    public AutomaticBackupIsolationException(string message) : base(message) { }
    public AutomaticBackupIsolationException(string message, Exception innerException)
        : base(message, innerException) { }
}
