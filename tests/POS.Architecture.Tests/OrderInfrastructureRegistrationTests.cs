using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Persistence;
using POS.Infrastructure;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Khóa cấu hình DI bắt buộc cho Order persistence.
///
/// Test này ngăn việc vô tình xóa, đăng ký trùng
/// hoặc đổi sai lifetime của IOrderRepository.
/// </summary>
public sealed class
    OrderInfrastructureRegistrationTests
{
    [Fact]
    public void
        Order_repository_must_be_registered_once_as_scoped()
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                [
                    "Infrastructure:" +
                    "DatabasePath"
                ] =
                    "data/order-registration-test.db",

                [
                    "Infrastructure:" +
                    "DatabaseTimeoutSeconds"
                ] =
                    "30",

                [
                    "Infrastructure:" +
                    "ApplyMigrationsOnStartup"
                ] =
                    "false",

                [
                    "Infrastructure:" +
                    "SeedDefaultAdministrator"
                ] =
                    "false"
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var services =
            new ServiceCollection();

        services.AddInfrastructure(
            configuration);

        var registrations =
            services
                .Where(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(IOrderRepository))
                .ToArray();

        var registration =
            Assert.Single(
                registrations);

        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);

        Assert.Equal(
            typeof(OrderRepository),
            registration.ImplementationType);
    }
}