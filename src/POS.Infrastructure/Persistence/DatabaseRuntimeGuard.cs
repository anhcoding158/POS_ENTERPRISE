using System.IO;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Enforces the boundary between normal/published runtime and an explicitly
/// isolated database test process.
/// </summary>
public static class DatabaseRuntimeGuard
{
    public const string RuntimeModeEnvironmentVariable =
        "POS_RUNTIME_MODE";

    public const string DatabasePathEnvironmentVariable =
        "Infrastructure__DatabasePath";

    public const string IsolatedTestMode =
        "IsolatedTest";

    public const string SafetyBlockMessage =
        "DATABASE SAFETY BLOCK\n\n" +
        "The application detected an external database override.\n\n" +
        "Remove Infrastructure__DatabasePath and restart the application.\n\n" +
        "No database was opened or modified.";

    public static DatabaseRuntimeState Validate(
        string databasePathProvider,
        string effectiveDatabasePath,
        string canonicalDatabasePath,
        bool isDevelopmentOutput,
        string? runtimeMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            databasePathProvider);

        var hasExternalOverride =
            !string.Equals(
                databasePathProvider,
                "Json",
                StringComparison.Ordinal);

        if (!hasExternalOverride)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                effectiveDatabasePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                canonicalDatabasePath);

            return new DatabaseRuntimeState(
                IsolatedTest: false,
                HasExternalOverride: false);
        }

        if (!isDevelopmentOutput ||
            string.IsNullOrWhiteSpace(effectiveDatabasePath) ||
            string.IsNullOrWhiteSpace(canonicalDatabasePath) ||
            !string.Equals(
                runtimeMode,
                IsolatedTestMode,
                StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(effectiveDatabasePath) ||
            PathsEqual(effectiveDatabasePath, canonicalDatabasePath))
        {
            throw new DatabaseSafetyBlockException();
        }

        return new DatabaseRuntimeState(
            IsolatedTest: true,
            HasExternalOverride: true);
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (
            Exception exception)
            when (exception is
                ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return true;
        }
    }
}

public sealed record DatabaseRuntimeState(
    bool IsolatedTest,
    bool HasExternalOverride);

public sealed class DatabaseSafetyBlockException : InvalidOperationException
{
    public DatabaseSafetyBlockException()
        : base(DatabaseRuntimeGuard.SafetyBlockMessage)
    {
    }
}
