using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Abstractions.StoreSetup;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.StoreSetup;
using POS.Infrastructure.Support;
using POS.Wpf;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class IsolatedStartupTests
{
    [Fact]
    public void Isolated_production_composition_resolves_without_store_setup_file_or_printer()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "POS-R41-Hotfix-IsolatedStartup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "pos-enterprise-isolated.db");
        File.WriteAllBytes(databasePath, Array.Empty<byte>());

        var previousMode = Environment.GetEnvironmentVariable(
            DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);
        var previousDatabasePath = Environment.GetEnvironmentVariable(
            DatabaseRuntimeGuard.DatabasePathEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                DatabaseRuntimeGuard.IsolatedTestMode);
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.DatabasePathEnvironmentVariable,
                databasePath);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory
            });
            App.ConfigureApplicationConfiguration(builder);

            Assert.Equal(
                databasePath,
                builder.Configuration["Infrastructure:DatabasePath"]);
            Assert.True(App.ValidateDatabaseRuntime(builder).IsolatedTest);

            var services = App.CreatePreStartupInfrastructureServices(builder.Configuration);
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            var paths = provider.GetRequiredService<StoreSettingsPathProvider>();
            Assert.True(paths.RuntimeIsolated);
            Assert.Equal(root, paths.Root, StringComparer.OrdinalIgnoreCase);
            Assert.StartsWith(root, paths.LogoRoot, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(root, paths.DefaultBackupDirectory, StringComparison.OrdinalIgnoreCase);

            _ = provider.GetRequiredService<IStoreSettingsStore>();
            _ = provider.GetRequiredService<AutomaticBackupPathProvider>();
            _ = provider.GetRequiredService<AutomaticBackupRetentionService>();
            _ = provider.GetRequiredService<IAutomaticBackupService>();
            _ = provider.GetRequiredService<RestoreOperationStore>();
            _ = provider.GetRequiredService<RestoreWorkerService>();
            _ = provider.GetRequiredService<IReceiptService>();

            Assert.False(File.Exists(paths.SettingsPath));
            Assert.False(Directory.Exists(paths.LogoRoot));
            Assert.False(Directory.Exists(paths.DefaultBackupDirectory));
            Assert.NotEqual(
                Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "POS Enterprise")),
                paths.Root,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                previousMode);
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.DatabasePathEnvironmentVariable,
                previousDatabasePath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
