using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.ProductImports;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Payments;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Abstractions.StoreSetup;
using POS.Application.Services;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Common;
using POS.Infrastructure.Logging;
using POS.Infrastructure.ProductImports;
using POS.Infrastructure.Orders;
using POS.Infrastructure.Payments;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Support;
using POS.Infrastructure.Storage;
using POS.Infrastructure.StoreSetup;

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

        services
            .AddOptions<DatabaseStorageOptions>()
            .Bind(configuration.GetSection(DatabaseStorageOptions.SectionName))
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
                "Cấu hình theo dõi dung lượng database không hợp lệ.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IStorageMetadataProvider,
            SystemStorageMetadataProvider>();
        services.TryAddSingleton<IDatabaseStorageMonitor,
            DatabaseStorageMonitor>();

        services
            .AddOptions<SupportBundleOptions>()
            .Bind(configuration.GetSection(SupportBundleOptions.SectionName))
            .Validate(options =>
            {
                try { options.Validate(); return true; }
                catch { return false; }
            }, "Cấu hình Support Bundle không hợp lệ.")
            .ValidateOnStart();

        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(SafeFileLoggerOptions)))
        {
            var safeFileOptions = new SafeFileLoggerOptions();
            configuration.GetSection(SafeFileLoggerOptions.SectionName).Bind(safeFileOptions);
            safeFileOptions.Validate();
            services.AddSingleton(safeFileOptions);
        }

        services.TryAddScoped<ISupportBundleService, SupportBundleService>();
        services.TryAddSingleton<IProductImportPreviewService, ProductImportPreviewService>();
        services.TryAddScoped<IManualBackupService, ManualBackupService>();
        services.AddSingleton(_ => new StoreSettingsPathProvider(
            Environment.GetEnvironmentVariable(DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable),
            configuration[$"{InfrastructureOptions.SectionName}:DatabasePath"],
            AppContext.BaseDirectory));
        services.AddSingleton<IStoreSettingsValidator, POS.Application.Validation.StoreSettingsValidator>();
        services.AddSingleton<IStoreSettingsStore, JsonStoreSettingsStore>();
        services.AddSingleton<IStoreSettingsReadinessEvaluator, StoreSettingsReadinessEvaluator>();
        services.AddSingleton<ManagedLogoService>();
        services.AddSingleton<IStoreSettingsLogoService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ManagedLogoService>());
        services.AddSingleton<IStoreSettingsLogoContentProvider>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ManagedLogoService>());
        services.AddSingleton<IPrinterTestService, PrinterTestService>();
        services.AddSingleton<IStoreSettingsQrPreviewService, StoreSettingsQrPreviewService>();
        services.TryAddSingleton<IBackupCoordinator, BackupCoordinator>();
        services.TryAddSingleton(AutomaticBackupPolicy.Production);
        services.TryAddSingleton(serviceProvider =>
        {
            var paths = serviceProvider.GetRequiredService<StoreSettingsPathProvider>();
            var settingsStore = serviceProvider.GetRequiredService<IStoreSettingsStore>();
            var settings = settingsStore.Current;
            var runtimeMode = Environment.GetEnvironmentVariable(DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);
            var root = paths.RuntimeIsolated
                ? paths.DefaultBackupDirectory
                : string.IsNullOrWhiteSpace(settings.BackupDirectory)
                    ? AutomaticBackupPathProvider.GetCanonicalProductionRoot()
                    : settings.BackupDirectory.Trim();
            return new AutomaticBackupPathProvider(root, settingsStore, paths.RuntimeIsolated);
        });
        services.TryAddSingleton<IAutomaticBackupStateStore, AutomaticBackupStateStore>();
        services.TryAddSingleton<AutomaticBackupRetentionService>();
        services.TryAddSingleton<IAutomaticBackupStatusSource, AutomaticBackupStatusSource>();
        services.TryAddSingleton<IAutomaticBackupService, AutomaticBackupService>();
        services.TryAddSingleton<RestoreArtifactInspector>();
        services.TryAddSingleton<IRestoreArtifactInspector>(serviceProvider =>
            serviceProvider.GetRequiredService<RestoreArtifactInspector>());
        services.TryAddSingleton<RestoreOperationStore>();
        services.TryAddSingleton<IRestorePreparationService, RestorePreparationService>();
        services.TryAddSingleton<RestoreWorkerService>();

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

        services.AddSingleton<SqliteFailureClassifier>();
        services.AddSingleton<IDatabaseFailureClassifier>(serviceProvider =>
            serviceProvider.GetRequiredService<SqliteFailureClassifier>());
        services.AddSingleton<SqliteSafeOperationRetry>();

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
            IReceiptStoreSnapshotProvider>(serviceProvider =>
                new ReceiptStoreSnapshotProvider(
                    serviceProvider.GetRequiredService<IStoreSettingsStore>(),
                    serviceProvider.GetRequiredService<IStoreSettingsLogoContentProvider>(),
                    serviceProvider.GetRequiredService<IOptions<ReceiptStoreOptions>>(),
                    serviceProvider.GetRequiredService<ILogger<ReceiptStoreSnapshotProvider>>()));

        services.AddSingleton<
            ReceiptDocumentBuilder>();

        services.AddSingleton<IReceiptService, WpfReceiptService>();
        services.AddSingleton<ILabelPrinterCatalog, WpfLabelPrinterCatalog>();
        services.AddSingleton<ILabelPrintSettingsStore, JsonLabelPrintSettingsStore>();
        services.AddSingleton<ILabelPrintDispatcher, WpfLabelPrintDispatcher>();
        services.AddSingleton<ILabelPrintingService, WpfLabelPrintingService>();

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

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISecurityAuditRepository, SecurityAuditRepository>();
        services.AddScoped<ISecurityAuditQueryRepository, SecurityAuditQueryRepository>();
        services.AddSingleton<POS.Application.Abstractions.Security.ITerminalIdentityProvider, POS.Infrastructure.Security.TerminalIdentityProvider>();

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
