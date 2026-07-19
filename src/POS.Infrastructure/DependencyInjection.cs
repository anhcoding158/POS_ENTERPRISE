using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Infrastructure.Common;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;

namespace POS.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ của tầng Infrastructure.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var infrastructureSection =
            configuration.GetSection(
                InfrastructureOptions.SectionName);

        services
            .AddOptions<InfrastructureOptions>()
            .Bind(infrastructureSection)
            .Validate(
                options =>
                {
                    try
                    {
                        options.Validate();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                "Cấu hình Infrastructure không hợp lệ.")
            .ValidateOnStart();

        services.AddSingleton<DatabasePathResolver>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<PosDbContext>(
            (serviceProvider, optionsBuilder) =>
            {
                var infrastructureOptions =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<InfrastructureOptions>>()
                        .Value;

                var pathResolver =
                    serviceProvider
                        .GetRequiredService<
                            DatabasePathResolver>();

                var connectionString =
                    pathResolver.CreateConnectionString(
                        infrastructureOptions);

                optionsBuilder.UseSqlite(
                    connectionString,
                    sqliteOptions =>
                    {
                        sqliteOptions.CommandTimeout(
                            infrastructureOptions
                                .DatabaseTimeoutSeconds);
                    });

                optionsBuilder.EnableDetailedErrors();
            });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<
            ICategoryRepository,
            CategoryRepository>();

        services.AddScoped<
            IProductRepository,
            ProductRepository>();

        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}