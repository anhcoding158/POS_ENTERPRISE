using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderCompositionTests
{
    [Fact]
    public void Production_composition_resolves_authorized_purchase_order_service()
    {
        var repositoryRoot = RepositoryLocator.GetPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(repositoryRoot, "src", "POS.Wpf"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] = Path.Combine(Path.GetTempPath(), "r62a-composition-unused.db"),
                ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        typeof(POS.Wpf.App).GetMethod(
                "ConfigureApplicationServices",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [services, configuration]);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<AuthorizedPurchaseOrderService>(
            scope.ServiceProvider.GetRequiredService<IPurchaseOrderService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPurchaseOrderService>());
    }
}
