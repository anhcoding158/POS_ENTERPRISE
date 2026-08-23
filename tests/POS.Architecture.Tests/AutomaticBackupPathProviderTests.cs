using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupPathProviderTests
{
    [Fact]
    public void Normal_runtime_preserves_canonical_root_and_state_path_without_database()
    {
        var provider = AutomaticBackupPathProvider.CreateForRuntime(null, null, AppContext.BaseDirectory);

        Assert.Equal(AutomaticBackupPathProvider.GetCanonicalProductionRoot(), provider.Root);
        Assert.Equal(Path.Combine(provider.Root, AutomaticBackupPathProvider.StateFileName), provider.StatePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("isolatedtest")]
    [InlineData("IsolatedTest ")]
    public void Non_exact_runtime_mode_cannot_redirect_production_root(string runtimeMode)
    {
        var provider = AutomaticBackupPathProvider.CreateForRuntime(
            runtimeMode, Path.Combine(Path.GetTempPath(), "ignored.db"), AppContext.BaseDirectory);

        Assert.Equal(AutomaticBackupPathProvider.GetCanonicalProductionRoot(), provider.Root);
    }

    [Fact]
    public void Existing_custom_root_constructor_remains_compatible()
    {
        using var directory = new TempDirectory();
        var provider = new AutomaticBackupPathProvider(directory.Path + Path.DirectorySeparatorChar);

        Assert.Equal(directory.Path, provider.Root);
    }

    [Fact]
    public void Exact_isolated_mode_derives_direct_child_root_and_state_path()
    {
        using var directory = new TempDirectory();
        var databasePath = Path.Combine(directory.Path, "pos-enterprise-isolated.db");

        var provider = AutomaticBackupPathProvider.CreateForRuntime(
            DatabaseRuntimeGuard.IsolatedTestMode, databasePath, AppContext.BaseDirectory);

        Assert.Equal(Path.Combine(directory.Path, AutomaticBackupPathProvider.RootDirectoryName), provider.Root);
        Assert.Equal(Path.Combine(provider.Root, AutomaticBackupPathProvider.StateFileName), provider.StatePath);
        Assert.Equal(directory.Path, Path.GetDirectoryName(provider.Root));
        Assert.NotEqual(AutomaticBackupPathProvider.GetCanonicalProductionRoot(), provider.Root);
        Assert.False(Directory.Exists(provider.Root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\database.db")]
    public void Exact_isolated_mode_rejects_missing_or_non_absolute_database_without_fallback(string? databasePath)
    {
        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode, databasePath, AppContext.BaseDirectory));
    }

    [Fact]
    public void Isolated_database_at_drive_root_is_rejected()
    {
        var driveRoot = Path.GetPathRoot(Path.GetTempPath())!;

        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode,
                Path.Combine(driveRoot, "pos-enterprise-isolated.db"), AppContext.BaseDirectory));
    }

    [Fact]
    public void Traversal_segments_are_rejected_before_root_creation()
    {
        using var directory = new TempDirectory();
        var escapedRoot = Path.Combine(directory.Path, AutomaticBackupPathProvider.RootDirectoryName);
        var path = Path.Combine(directory.Path, "child", "..", "pos-enterprise-isolated.db");

        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode, path, AppContext.BaseDirectory));
        Assert.False(Directory.Exists(escapedRoot));
    }

    [Fact]
    public void Canonical_production_root_collision_is_rejected()
    {
        using var directory = new TempDirectory();
        var databasePath = Path.Combine(directory.Path, "pos-enterprise-isolated.db");
        var collidingRoot = Path.Combine(directory.Path, AutomaticBackupPathProvider.RootDirectoryName);

        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode, databasePath,
                AppContext.BaseDirectory, collidingRoot));
        Assert.False(Directory.Exists(collidingRoot));
    }

    [Fact]
    public void Application_or_repository_boundary_is_rejected_without_creating_root()
    {
        var repositoryRoot = FindRepositoryRoot();
        var databasePath = Path.Combine(repositoryRoot, "pos-enterprise-isolated-probe.db");
        var root = Path.Combine(repositoryRoot, AutomaticBackupPathProvider.RootDirectoryName);

        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode, databasePath, repositoryRoot));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Prefix_collision_is_not_treated_as_application_containment()
    {
        using var databaseDirectory = new TempDirectory();
        using var applicationDirectory = new TempDirectory(databaseDirectory.Path + "-application");

        var provider = AutomaticBackupPathProvider.CreateForRuntime(
            DatabaseRuntimeGuard.IsolatedTestMode,
            Path.Combine(databaseDirectory.Path, "pos-enterprise-isolated.db"),
            applicationDirectory.Path);

        Assert.Equal(Path.Combine(databaseDirectory.Path, AutomaticBackupPathProvider.RootDirectoryName), provider.Root);
    }

    [Fact]
    public void Existing_reparse_boundary_is_rejected_when_platform_allows_symbolic_links()
    {
        using var owner = new TempDirectory();
        var target = Directory.CreateDirectory(Path.Combine(owner.Path, "target"));
        var link = Path.Combine(owner.Path, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target.FullName);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or
            PlatformNotSupportedException)
        {
            return;
        }

        Assert.Throws<AutomaticBackupIsolationException>(() =>
            AutomaticBackupPathProvider.CreateForRuntime(
                DatabaseRuntimeGuard.IsolatedTestMode,
                Path.Combine(link, "pos-enterprise-isolated.db"), AppContext.BaseDirectory));
    }

    [Fact]
    public void Di_resolves_production_provider_as_singleton()
    {
        WithRuntimeMode(null, () =>
        {
            var services = CreateInfrastructureServices(null);
            using var serviceProvider = services.BuildServiceProvider();
            var first = serviceProvider.GetRequiredService<AutomaticBackupPathProvider>();
            var second = serviceProvider.GetRequiredService<AutomaticBackupPathProvider>();

            Assert.Same(first, second);
            Assert.Equal(AutomaticBackupPathProvider.GetCanonicalProductionRoot(), first.Root);
            Assert.Equal(ServiceLifetime.Singleton,
                Assert.Single(services, item => item.ServiceType == typeof(AutomaticBackupPathProvider)).Lifetime);
        });
    }

    [Fact]
    public void Di_resolves_one_isolated_provider_for_state_retention_and_service()
    {
        using var directory = new TempDirectory();
        var databasePath = Path.Combine(directory.Path, "pos-enterprise-isolated.db");
        WithRuntimeMode(DatabaseRuntimeGuard.IsolatedTestMode, () =>
        {
            var services = CreateInfrastructureServices(databasePath);
            using var serviceProvider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var paths = serviceProvider.GetRequiredService<AutomaticBackupPathProvider>();
            var state = serviceProvider.GetRequiredService<IAutomaticBackupStateStore>();
            var retention = serviceProvider.GetRequiredService<AutomaticBackupRetentionService>();
            var automatic = serviceProvider.GetRequiredService<IAutomaticBackupService>();

            Assert.Equal(Path.Combine(directory.Path, AutomaticBackupPathProvider.RootDirectoryName), paths.Root);
            Assert.Same(paths, CapturedProvider(state));
            Assert.Same(paths, CapturedProvider(retention));
            Assert.Same(paths, CapturedProvider(automatic));
        });
    }

    [Fact]
    public void Invalid_isolated_di_composition_fails_closed()
    {
        WithRuntimeMode(DatabaseRuntimeGuard.IsolatedTestMode, () =>
        {
            var services = CreateInfrastructureServices("relative\\database.db");
            using var serviceProvider = services.BuildServiceProvider();

            Assert.Throws<AutomaticBackupIsolationException>(() =>
                serviceProvider.GetRequiredService<AutomaticBackupPathProvider>());
        });
    }

    [Fact]
    public void Launcher_declares_and_reports_the_isolated_path_contract()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "scripts", "Start-POS-IsolatedTest.ps1"));

        Assert.Contains("$childEnvironment['POS_RUNTIME_MODE'] = 'IsolatedTest'", script, StringComparison.Ordinal);
        Assert.Contains("$childEnvironment['Infrastructure__DatabasePath']", script, StringComparison.Ordinal);
        Assert.Contains("$expectedAutomaticBackupRoot", script, StringComparison.Ordinal);
        Assert.Contains("$expectedAutomaticBackupStatePath", script, StringComparison.Ordinal);
        Assert.Contains("Expected automatic backup root:", script, StringComparison.Ordinal);
        Assert.Contains("Expected automatic backup state path:", script, StringComparison.Ordinal);
        Assert.Contains("Child process ID:", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$childEnvironment['LOCALAPPDATA']", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test-PathWithinBoundary $canonicalTestRoot $expectedAutomaticBackupRoot", script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
    }

    private static IServiceCollection CreateInfrastructureServices(string? databasePath)
    {
        var values = new Dictionary<string, string?>();
        if (databasePath is not null) values["Infrastructure:DatabasePath"] = databasePath;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration);
    }

    private static AutomaticBackupPathProvider CapturedProvider(object service)
    {
        var field = service.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(item => item.FieldType == typeof(AutomaticBackupPathProvider));
        return Assert.IsType<AutomaticBackupPathProvider>(field.GetValue(service));
    }

    private static void WithRuntimeMode(string? value, Action action)
    {
        var name = DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable;
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "POS.Enterprise.slnx"))) return current.FullName;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory(string? path = null)
        {
            Path = System.IO.Path.GetFullPath(path ?? System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "POS-AutoPath-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path) &&
                (File.GetAttributes(Path) & FileAttributes.ReparsePoint) == 0)
                Directory.Delete(Path, true);
        }
    }
}
