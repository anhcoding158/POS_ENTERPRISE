using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Services;
using POS.Infrastructure.Authentication;
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
        ArgumentNullException.ThrowIfNull(
            services);

        ArgumentNullException.ThrowIfNull(
            configuration);

        var infrastructureSection =
            configuration.GetSection(
                InfrastructureOptions.SectionName);

        services
            .AddOptions<InfrastructureOptions>()
            .Bind(
                infrastructureSection)
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

        /*
         * Dịch vụ dùng chung toàn ứng dụng.
         */
        services.AddSingleton<
            DatabasePathResolver>();

        services.AddSingleton<
            AuditableEntityInterceptor>();

        services.AddSingleton<
            IClock,
            SystemClock>();

        services.AddSingleton<
            IPasswordHasher,
            BCryptPasswordHasher>();

        services.AddSingleton<
            ICurrentUserService,
            CurrentUserService>();

        /*
         * Credential được mã hóa bằng Windows DPAPI
         * và lưu dưới LocalApplicationData.
         */
        services.AddSingleton<
            IRememberedLoginStore,
            WindowsRememberedLoginStore>();

        services.AddSingleton<
            IPermissionService,
            PermissionService>();

        /*
         * DbContext ngắn hạn theo từng DI scope.
         */
        services.AddDbContext<PosDbContext>(
            (serviceProvider, optionsBuilder) =>
            {
                var infrastructureOptions =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<
                                InfrastructureOptions>>()
                        .Value;

                var pathResolver =
                    serviceProvider
                        .GetRequiredService<
                            DatabasePathResolver>();

                var connectionString =
                    pathResolver
                        .CreateConnectionString(
                            infrastructureOptions);

                var auditableEntityInterceptor =
                    serviceProvider
                        .GetRequiredService<
                            AuditableEntityInterceptor>();

                optionsBuilder.UseSqlite(
                    connectionString,
                    sqliteOptions =>
                    {
                        sqliteOptions.CommandTimeout(
                            infrastructureOptions
                                .DatabaseTimeoutSeconds);
                    });

                optionsBuilder.AddInterceptors(
                    auditableEntityInterceptor);

                optionsBuilder.EnableDetailedErrors();
            });

        services.AddScoped<
            IUnitOfWork,
            EfUnitOfWork>();

        services.AddScoped<
            ICategoryRepository,
            CategoryRepository>();

        services.AddScoped<
            IProductRepository,
            ProductRepository>();

        services.AddScoped<
            IInventoryMovementRepository,
            InventoryMovementRepository>();

        services.AddScoped<
             IUserRepository,
            UserRepository>();

        /*
         * Order repository dùng cùng scoped DbContext
         * với IUnitOfWork để toàn bộ aggregate Order
         * được lưu trong một transaction thống nhất.
         */
        services.AddScoped<
            IOrderRepository,
            OrderRepository>();

        services.AddScoped<
            DatabaseInitializer>();



        return services;
    }
}