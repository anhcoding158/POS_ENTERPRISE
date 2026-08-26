using System.IO;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;

namespace POS.Infrastructure.StoreSetup;

public sealed class StoreSettingsPathProvider
{
    public const string SettingsFileName = "store-settings.json";
    public const string LogoDirectoryName = "store-logos";

    public StoreSettingsPathProvider(string? runtimeMode, string? configuredDatabasePath, string applicationBaseDirectory)
    {
        var isolated = string.Equals(runtimeMode, DatabaseRuntimeGuard.IsolatedTestMode, StringComparison.Ordinal);
        var databaseDirectory = ResolveDatabaseDirectory(configuredDatabasePath, isolated);
        var root = isolated ? databaseDirectory : ResolveProductionRoot();
        Root = Normalize(root);
        SettingsPath = Path.Combine(Root, SettingsFileName);
        LogoRoot = Path.Combine(Root, LogoDirectoryName);
        EffectiveDatabaseDirectory = Normalize(databaseDirectory);
        DefaultBackupDirectory = isolated
            ? Normalize(Path.Combine(databaseDirectory, AutomaticBackupDirectoryName))
            : Normalize(Path.Combine(ResolveProductionRoot(), AutomaticBackupDirectoryName));
        RuntimeIsolated = isolated;
        ApplicationBoundary = Normalize(applicationBaseDirectory);
    }

    public const string AutomaticBackupDirectoryName = "automatic-backups";
    public string Root { get; }
    public string SettingsPath { get; }
    public string LogoRoot { get; }
    public string EffectiveDatabaseDirectory { get; }
    public string DefaultBackupDirectory { get; }
    public string ApplicationBoundary { get; }
    public bool RuntimeIsolated { get; }

    private static string ResolveDatabaseDirectory(string? configured, bool isolated)
    {
        if (isolated)
        {
            if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
                throw new AutomaticBackupIsolationException("IsolatedTest cần đường dẫn database tuyệt đối.");
            var path = Path.GetFullPath(configured);
            return Path.GetDirectoryName(path) ?? throw new AutomaticBackupIsolationException("Không xác định được thư mục database.");
        }

        try
        {
            return Path.GetDirectoryName(DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(configured ?? "data/pos-enterprise.db"))
                ?? throw new InvalidOperationException("Không xác định được thư mục database.");
        }
        catch
        {
            return Path.Combine(Environment.CurrentDirectory, "data");
        }
    }

    private static string ResolveProductionRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new InvalidOperationException("LocalApplicationData không khả dụng.");
        return Path.Combine(local, "POS Enterprise");
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
