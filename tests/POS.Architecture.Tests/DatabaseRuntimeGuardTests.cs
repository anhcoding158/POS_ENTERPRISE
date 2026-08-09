using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class DatabaseRuntimeGuardTests
{
    private const string ProviderJson = "Json";
    private const string ProviderEnvironment = "EnvironmentVariables";
    private const string CanonicalDevelopmentDatabase =
        @"C:\pos\data\pos-enterprise.db";
    private const string CanonicalPublishedDatabase =
        @"C:\Users\Test\AppData\Local\POS Enterprise\data\pos-enterprise.db";

    [Fact]
    public void Normal_source_without_override_uses_normal_mode()
    {
        var state = DatabaseRuntimeGuard.Validate(
            ProviderJson,
            "data/pos-enterprise.db",
            CanonicalDevelopmentDatabase,
            isDevelopmentOutput: true,
            runtimeMode: null);

        Assert.False(state.IsolatedTest);
        Assert.False(state.HasExternalOverride);
    }

    [Fact]
    public void Stale_environment_override_is_blocked_without_isolated_mode()
    {
        var exception =
            Assert.Throws<DatabaseSafetyBlockException>(() =>
                DatabaseRuntimeGuard.Validate(
                    ProviderEnvironment,
                    @"C:\temp\stale-test.db",
                    CanonicalDevelopmentDatabase,
                    isDevelopmentOutput: true,
                    runtimeMode: null));

        Assert.DoesNotContain(
            @"C:\temp\stale-test.db",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "DATABASE SAFETY BLOCK",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Isolated_test_requires_explicit_mode_and_absolute_noncanonical_path()
    {
        var state = DatabaseRuntimeGuard.Validate(
            ProviderEnvironment,
            @"C:\temp\isolated-test.db",
            CanonicalDevelopmentDatabase,
            isDevelopmentOutput: true,
            runtimeMode: DatabaseRuntimeGuard.IsolatedTestMode);

        Assert.True(state.IsolatedTest);
        Assert.True(state.HasExternalOverride);

        Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                @"C:\temp\isolated-test.db",
                CanonicalDevelopmentDatabase,
                isDevelopmentOutput: true,
                runtimeMode: "isolatedtest"));

        Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                "relative-test.db",
                CanonicalDevelopmentDatabase,
                isDevelopmentOutput: true,
                runtimeMode: DatabaseRuntimeGuard.IsolatedTestMode));

        Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                string.Empty,
                CanonicalDevelopmentDatabase,
                isDevelopmentOutput: true,
                runtimeMode: null));
    }

    [Fact]
    public void Isolated_test_cannot_target_canonical_development_or_published_database()
    {
        Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                CanonicalDevelopmentDatabase,
                CanonicalDevelopmentDatabase,
                isDevelopmentOutput: true,
                runtimeMode: DatabaseRuntimeGuard.IsolatedTestMode));

        Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                CanonicalPublishedDatabase,
                CanonicalPublishedDatabase,
                isDevelopmentOutput: true,
                runtimeMode: DatabaseRuntimeGuard.IsolatedTestMode));
    }

    [Fact]
    public void Published_runtime_blocks_override_even_when_environment_is_development()
    {
        var exception = Assert.Throws<DatabaseSafetyBlockException>(() =>
            DatabaseRuntimeGuard.Validate(
                ProviderEnvironment,
                @"C:\temp\isolated-test.db",
                CanonicalPublishedDatabase,
                isDevelopmentOutput: false,
                runtimeMode: DatabaseRuntimeGuard.IsolatedTestMode));

        Assert.Equal(
            DatabaseRuntimeGuard.SafetyBlockMessage,
            exception.Message);
    }

    [Fact]
    public void Application_guard_precedes_host_identity_and_database_initialization()
    {
        var repositoryRoot =
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", ".."));
        var source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "POS.Wpf", "App.xaml.cs"));

        var guardIndex = source.IndexOf(
            "ValidateDatabaseRuntime(",
            StringComparison.Ordinal);
        var identityIndex = source.IndexOf(
            "ResolveDatabaseIdentity(",
            StringComparison.Ordinal);
        var hostStartIndex = source.IndexOf(
            "await _host.StartAsync()",
            StringComparison.Ordinal);
        var databaseInitializationIndex = source.IndexOf(
            "await InitializeDatabaseAsync(",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0);
        Assert.True(identityIndex > guardIndex);
        Assert.True(hostStartIndex > guardIndex);
        Assert.True(databaseInitializationIndex > guardIndex);
    }

    [Fact]
    public void Startup_diagnostics_are_metadata_only()
    {
        var repositoryRoot =
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", ".."));
        var source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "POS.Wpf", "App.xaml.cs"));
        var start = source.IndexOf(
            "private static void LogStartupDiagnostics",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "internal static DatabaseRuntimeState",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        var diagnostics = source[start..end];
        Assert.DoesNotContain(
            "Environment.ProcessPath",
            diagnostics,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Environment.CurrentDirectory",
            diagnostics,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AppContext.BaseDirectory",
            diagnostics,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "options.DatabasePath",
            diagnostics,
            StringComparison.Ordinal);
    }
}
