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

        /*
         * =====================================================
         * INFRASTRUCTURE OPTIONS
         * =====================================================
         */

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
         * =====================================================
         * RECEIPT STORE OPTIONS
         * =====================================================
         */

        var receiptStoreSection =
            configuration.GetSection(
                ReceiptStoreOptions.SectionName);

        /*
         * Thông tin cửa hàng được kiểm tra ngay khi Host
         * khởi động.
         *
         * WifiPassword hoặc các secret khác không thuộc
         * ReceiptStoreOptions và không được đưa vào hóa đơn.
         */
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

        /*
         * =====================================================
         * RECEIPT PRINTER OPTIONS
         * =====================================================
         */

        var receiptPrinterSection =
            configuration.GetSection(
                ReceiptPrinterOptions.SectionName);

        /*
         * Hardware:PrinterName và Hardware:PaperSize
         * được bind thành typed options.
         *
         * Checkpoint hiện tại chỉ hỗ trợ giấy K80.
         */
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

        /*
         * =====================================================
         * VIETQR OPTIONS
         * =====================================================
         */

        var vietQrSection =
            configuration.GetSection(
                VietQrOptions.SectionName);

        /*
         * VietQR được phép tắt khi cửa hàng chưa cấu hình
         * tài khoản ngân hàng.
         *
         * Khi Payment:EnableVietQr = true, Host kiểm tra:
         * - BIN ngân hàng;
         * - số tài khoản;
         * - tên chủ tài khoản;
         * - tiền tố nội dung chuyển khoản;
         * - kích thước ảnh QR.
         *
         * ValidateOnStart giúp ứng dụng không chạy với
         * cấu hình VietQR đã bật nhưng không hợp lệ.
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

        /*
         * =====================================================
         * COMMON SINGLETON SERVICES
         * =====================================================
         */

        services.AddSingleton<
            DatabasePathResolver>();

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
         * =====================================================
         * RECEIPT SERVICES
         * =====================================================
         */

        /*
         * Receipt snapshot serializer là stateless.
         *
         * Store provider tạo một snapshot cấu hình bất biến
         * cho toàn bộ phiên chạy của ứng dụng.
         */
        services.AddSingleton<
            IReceiptSnapshotSerializer,
            ReceiptSnapshotJsonSerializer>();

        services.AddSingleton<
            IReceiptStoreSnapshotProvider,
            ReceiptStoreSnapshotProvider>();

        /*
         * Renderer không giữ state.
         *
         * WpfReceiptService là singleton và tự khóa để
         * không gửi hai print job đồng thời.
         */
        services.AddSingleton<
            ReceiptDocumentBuilder>();

        services.AddSingleton<
            IReceiptService,
            WpfReceiptService>();

        /*
         * =====================================================
         * VIETQR SERVICES
         * =====================================================
         */

        /*
         * VietQrService:
         * - không giữ DbContext;
         * - không lưu trạng thái giao dịch;
         * - không gọi API ngân hàng;
         * - không giữ dữ liệu thay đổi theo từng request;
         * - chỉ đọc typed options và tạo payload/PNG.
         *
         * Vì vậy Singleton là lifetime phù hợp.
         *
         * Việc tạo QR không đồng nghĩa tiền đã được nhận.
         * Service này không được tự MarkPaid Order.
         */
        services.AddSingleton<
            IVietQrService,
            VietQrService>();

        /*
         * =====================================================
         * ENTITY FRAMEWORK CORE
         * =====================================================
         */

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

        /*
         * =====================================================
         * PERSISTENCE SERVICES
         * =====================================================
         */

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