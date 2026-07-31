using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Payments;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Common;
using POS.Infrastructure.Orders;
using POS.Infrastructure.Payments;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Printing;

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

        var receiptStoreSection =
            configuration.GetSection(
                ReceiptStoreOptions.SectionName);

        services
            .AddOptions<ReceiptStoreOptions>()
            .Bind(
                receiptStoreSection)
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
                "Cấu hình Store dùng cho hóa đơn không hợp lệ.")
            .ValidateOnStart();

        var receiptPrinterSection =
            configuration.GetSection(
                ReceiptPrinterOptions.SectionName);

        services
            .AddOptions<ReceiptPrinterOptions>()
            .Bind(
                receiptPrinterSection)
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
                "Cấu hình máy in hóa đơn không hợp lệ.")
            .ValidateOnStart();

        var vietQrSection =
            configuration.GetSection(
                VietQrOptions.SectionName);

        /*
         * Options cũ vẫn được giữ để tương thích:
         * - kích thước PNG;
         * - tiền tố nội dung chuyển khoản;
         * - các bài test và service cũ.
         *
         * Luồng production mới không yêu cầu bật
         * EnableVietQr hoặc nhập thông tin ngân hàng.
         */
        services
            .AddOptions<VietQrOptions>()
            .Bind(
                vietQrSection)
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
                "Cấu hình Payment/VietQR không hợp lệ.")
            .ValidateOnStart();

        services.AddSingleton<
            DatabasePathResolver>();

        services.AddSingleton<
            SqliteDatabaseSafetyService>();

        services.AddSingleton<
            AuditableEntityInterceptor>();

        services.AddSingleton<
            IClock,
            SystemClock>();

        services.AddSingleton<
            IOrderCodeGenerator,
            OrderCodeGenerator>();

        services.AddSingleton<
            IPasswordHasher,
            BCryptPasswordHasher>();

        services.AddSingleton<
            ICurrentUserService,
            CurrentUserService>();

        services.AddSingleton<
            IRememberedLoginStore,
            WindowsRememberedLoginStore>();

        services.AddSingleton<
            IPermissionService,
            PermissionService>();

        services.AddSingleton<
            IReceiptSnapshotSerializer,
            ReceiptSnapshotJsonSerializer>();

        services.AddSingleton<
            IReceiptStoreSnapshotProvider,
            ReceiptStoreSnapshotProvider>();

        services.AddSingleton<
            ReceiptDocumentBuilder>();

        services.AddSingleton<
            IReceiptService,
            WpfReceiptService>();

        /*
         * Service VietQR cũ được giữ để không phá
         * compatibility và bộ test hiện tại.
         */
        services.AddSingleton<
            IVietQrService,
            VietQrService>();

        services.AddSingleton<
            IVietQrPaymentGateway,
            VietQrPaymentGateway>();

        services.AddSingleton<
            IVietQrImageDecoder,
            VietQrImageDecoder>();

        /*
         * Pipeline mới bám đúng chương trình Python:
         *
         * ảnh QR → payload nền → DPAPI →
         * thêm tiền/nội dung → CRC mới → PNG.
         */
        services.AddSingleton<
            IVietQrPayloadStore,
            WindowsVietQrPayloadStore>();

        services.AddSingleton<
            IVietQrRecipientMetadataStore,
            WindowsVietQrRecipientMetadataStore>();

        services.AddSingleton<
            StoredVietQrService>();

        services.AddDbContext<PosDbContext>(
            (serviceProvider, optionsBuilder) =>
            {
                var infrastructureOptions =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<
                                InfrastructureOptions>>()
                        .Value;

                var connectionString =
                    DatabasePathResolver.CreateConnectionString(
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

        services.AddScoped<
            IOrderRepository,
            OrderRepository>();

        services.AddScoped<
            IOrderReturnRepository,
            OrderReturnRepository>();

        services.AddScoped<
            IOrderReceiptSnapshotRepository,
            OrderReceiptSnapshotRepository>();

        services.AddScoped<
            ICheckoutRequestJournalRepository,
            CheckoutRequestJournalRepository>();

        services.AddScoped<
            IHeldSaleRepository,
            HeldSaleRepository>();

        services.AddScoped<
            IPaymentIntentRepository,
            PaymentIntentRepository>();

        services.AddSingleton<
            ICheckoutRequestCanonicalizer,
            CheckoutRequestCanonicalizer>();

        services.AddSingleton<
            IHeldSaleRequestCanonicalizer,
            HeldSaleRequestCanonicalizer>();

        services.AddScoped<OrderHistoryService>();

        services.AddScoped<IOrderHistoryService>(
            serviceProvider =>
                new AuthorizedOrderHistoryService(
                    serviceProvider.GetRequiredService<
                        OrderHistoryService>(),
                    serviceProvider.GetRequiredService<
                        IPermissionService>()));

        services.AddScoped<OrderReturnService>();

        services.AddScoped<IOrderReturnService>(
            serviceProvider =>
                new AuthorizedOrderReturnService(
                    serviceProvider.GetRequiredService<OrderReturnService>(),
                    serviceProvider.GetRequiredService<IPermissionService>()));

        services.AddScoped<
            DatabaseInitializer>();

        return services;
    }
}
