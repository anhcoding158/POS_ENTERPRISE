using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.DTOs.Authentication;
using POS.Infrastructure.Persistence;
using POS.Wpf;
using POS.Wpf.ViewModels;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AuditRuntimeProbeTests
{
    [Fact]
    public async Task Probe_real_audit_initial_load_on_isolated_database()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scenario = Path.Combine(Path.GetTempPath(), "POS-Enterprise-Audit-Probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scenario);
        var database = Path.Combine(scenario, "pos-enterprise-isolated.db");
        File.Copy(Path.Combine(root, "data", "pos-enterprise.db"), database);

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(root, "src", "POS.Wpf"))
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Infrastructure:DatabasePath"] = database,
                    ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                })
                .Build();

            typeof(App).GetMethod("ConfigureApplicationServices", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, [services, configuration]);
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            provider.GetRequiredService<ICurrentUserService>().SetCurrentUser(new AuthenticatedUserDto(
                1, "isolated-admin", "Isolated Administrator", Role.Administrator, DateTimeOffset.UtcNow, false));
            var viewModel = scope.ServiceProvider.GetRequiredService<AuditLogViewModel>();

            await viewModel.InitializeAsync();
        }
        finally
        {
            try { Directory.Delete(scenario, recursive: true); } catch { }
        }
    }
}
