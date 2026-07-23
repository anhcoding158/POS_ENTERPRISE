using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf;

/// <summary>
/// Composition root và vòng đời chính của ứng dụng.
///
/// Luồng hoạt động:
/// First-run / Remembered Login / Login
/// → Shell
/// → Logout
/// → Login.
/// </summary>
public partial class App :
    global::System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(
        global::System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        /*
         * Ứng dụng chỉ tắt khi chính App gọi Shutdown.
         *
         * Điều này cho phép đóng LoginWindow, ShellWindow
         * hoặc SalesWindow mà không làm ứng dụng tự kết thúc
         * ngoài ý muốn.
         */
        ShutdownMode =
            global::System.Windows.ShutdownMode
                .OnExplicitShutdown;

        try
        {
            var builder =
                Host.CreateApplicationBuilder(
                    new HostApplicationBuilderSettings
                    {
                        ContentRootPath =
                            AppContext.BaseDirectory
                    });

            ConfigureApplicationConfiguration(
                builder);

            ConfigureApplicationServices(
                builder.Services,
                builder.Configuration);

            _host =
                builder.Build();

            await _host.StartAsync();

            await InitializeDatabaseAsync(
                _host.Services);

            await RunSessionLoopAsync(
                _host.Services);
        }
        catch (Exception exception)
        {
            var rootException =
                exception.GetBaseException();

            global::System.Windows.MessageBox.Show(
                $"Ứng dụng không thể khởi động.\n\n" +
                $"{rootException.Message}",
                "POS Enterprise",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(
        global::System.Windows.ExitEventArgs e)
    {
        var host =
            _host;

        _host =
            null;

        if (host is not null)
        {
            /*
             * Khi đóng ứng dụng bằng nút X:
             *
             * - chỉ xóa phiên đăng nhập trong RAM;
             * - không xóa remembered credential;
             * - lần mở ứng dụng sau vẫn có thể tự đăng nhập.
             */
            var currentUserService =
                host.Services
                    .GetService<
                        ICurrentUserService>();

            currentUserService?
                .Clear();

            try
            {
                await host.StopAsync(
                    TimeSpan.FromSeconds(5));
            }
            catch
            {
                /*
                 * Quá trình tắt host là best-effort.
                 *
                 * Không để lỗi dừng background service
                 * làm ứng dụng treo trong lúc thoát.
                 */
            }
            finally
            {
                host.Dispose();
            }
        }

        base.OnExit(e);
    }

    private static void
        ConfigureApplicationConfiguration(
            HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(
            builder);

        builder.Configuration
            .AddJsonFile(
                "appsettings.json",
                optional:
                    false,
                reloadOnChange:
                    true)
            .AddJsonFile(
                $"appsettings." +
                $"{builder.Environment.EnvironmentName}.json",
                optional:
                    true,
                reloadOnChange:
                    true);
    }

    private static void ConfigureApplicationServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        ArgumentNullException.ThrowIfNull(
            configuration);

        /*
         * Infrastructure:
         *
         * - DbContext;
         * - repositories;
         * - Unit of Work;
         * - authentication infrastructure;
         * - remembered login;
         * - permission service;
         * - clock;
         * - order-code generator;
         * - database initializer.
         */
        services.AddInfrastructure(
            configuration);

        ConfigureAuthenticationServices(
            services);

        ConfigureApplicationServiceDecorators(
            services);

        ConfigureDialogServices(
            services);

        ConfigureViewModelsAndWindows(
            services);
    }

    private static void
        ConfigureAuthenticationServices(
            IServiceCollection services)
    {
        /*
         * Initial setup và authentication sử dụng scoped
         * lifetime để dùng chung DbContext trong từng thao tác.
         */
        services.AddScoped<
            IInitialSetupService,
            InitialSetupService>();

        services.AddScoped<
            IAuthService,
            AuthService>();
    }

    private static void
        ConfigureApplicationServiceDecorators(
            IServiceCollection services)
    {
        /*
         * Product service.
         *
         * ProductService là implementation nghiệp vụ thật.
         * IProductService luôn được resolve qua decorator
         * để enforce quyền truy cập.
         */
        services.AddScoped<
            ProductService>();

        services.AddScoped<
            IProductService>(
                serviceProvider =>
                    new AuthorizedProductService(
                        serviceProvider
                            .GetRequiredService<
                                ProductService>(),

                        serviceProvider
                            .GetRequiredService<
                                IPermissionService>()));

        /*
         * Category service.
         */
        services.AddScoped<
            CategoryService>();

        services.AddScoped<
            ICategoryService>(
                serviceProvider =>
                    new AuthorizedCategoryService(
                        serviceProvider
                            .GetRequiredService<
                                CategoryService>(),

                        serviceProvider
                            .GetRequiredService<
                                IPermissionService>()));

        /*
         * Inventory service.
         */
        services.AddScoped<
            InventoryService>();

        services.AddScoped<
            IInventoryService>(
                serviceProvider =>
                    new AuthorizedInventoryService(
                        serviceProvider
                            .GetRequiredService<
                                InventoryService>(),

                        serviceProvider
                            .GetRequiredService<
                                IPermissionService>()));

        /*
         * Checkout service.
         *
         * CheckoutService chịu trách nhiệm:
         * - đọc lại giá từ database;
         * - kiểm tra tồn kho;
         * - tạo Order;
         * - trừ tồn;
         * - tạo InventoryMovement;
         * - commit transaction.
         *
         * Mọi nơi resolve ICheckoutService đều nhận
         * AuthorizedCheckoutService để enforce UseCheckout.
         */
        services.AddScoped<
            CheckoutService>();

        services.AddScoped<
            ICheckoutService>(
                serviceProvider =>
                    new AuthorizedCheckoutService(
                        serviceProvider
                            .GetRequiredService<
                                CheckoutService>(),

                        serviceProvider
                            .GetRequiredService<
                                IPermissionService>()));
    }

    private static void ConfigureDialogServices(
        IServiceCollection services)
    {
        /*
         * Các dialog service không trực tiếp giữ DbContext.
         *
         * Mỗi lần mở dialog, service sẽ tạo scope phù hợp
         * cho ViewModel và các application service bên trong.
         */
        services.AddSingleton<
            IProductDialogService,
            ProductDialogService>();

        services.AddSingleton<
            ICategoryDialogService,
            CategoryDialogService>();

        services.AddSingleton<
            ICategoryManagementDialogService,
            CategoryManagementDialogService>();

        services.AddSingleton<
            IInventoryDialogService,
            InventoryDialogService>();

        /*
         * ReceiptPreviewService chỉ thuộc Presentation:
         * - hiển thị snapshot hóa đơn đã commit;
         * - không giữ DbContext;
         * - lỗi preview/in không rollback giao dịch.
         */
        services.AddSingleton<
            IReceiptPreviewService,
            ReceiptPreviewService>();

        /*
         * SalesWindowService được resolve trong một scope
         * do ShellWindow tạo ra.
         *
         * Scope tồn tại trong toàn bộ thời gian
         * SalesWindow.ShowDialog() đang chạy.
         */
        services.AddTransient<
            ISalesWindowService,
            SalesWindowService>();
    }

    private static void ConfigureViewModelsAndWindows(
        IServiceCollection services)
    {
        /*
         * First-run setup UI.
         */
        services.AddTransient<
            FirstRunSetupViewModel>();

        services.AddTransient<
            FirstRunSetupWindow>();

        /*
         * Login UI.
         */
        services.AddTransient<
            LoginViewModel>();

        services.AddTransient<
            LoginWindow>();

        /*
         * Product, Category và Inventory UI.
         */
        services.AddTransient<
            ProductEditorViewModel>();

        services.AddTransient<
            CategoryEditorViewModel>();

        services.AddTransient<
            CategoryManagementViewModel>();

        services.AddTransient<
            InventoryAdjustmentViewModel>();

        services.AddTransient<
            InventoryHistoryViewModel>();

        /*
         * Premium Sales Terminal.
         */
        services.AddTransient<
            SalesViewModel>();

        services.AddTransient<
            SalesWindow>();

        /*
         * Main Shell.
         */
        services.AddTransient<
            ShellViewModel>();

        services.AddTransient<
            ShellWindow>();
    }

    private async Task RunSessionLoopAsync(
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(
            serviceProvider);

        var setupRequired =
            await IsInitialSetupRequiredAsync(
                serviceProvider);

        if (setupRequired)
        {
            var setupCompleted =
                ShowInitialSetupWindow(
                    serviceProvider);

            ClearMainWindowReference();

            if (!setupCompleted)
            {
                Shutdown(0);

                return;
            }

            EnsureAuthenticatedSession(
                serviceProvider);
        }
        else
        {
            /*
             * Chỉ thử khôi phục remembered login
             * đúng một lần khi ứng dụng khởi động.
             *
             * Sau khi người dùng chủ động Logout,
             * vòng lặp không tự đăng nhập lại.
             */
            await TryRestoreRememberedLoginAsync(
                serviceProvider);
        }

        while (true)
        {
            var currentUserService =
                serviceProvider
                    .GetRequiredService<
                        ICurrentUserService>();

            if (!currentUserService
                .IsAuthenticated)
            {
                var loginSucceeded =
                    ShowLoginWindow(
                        serviceProvider);

                ClearMainWindowReference();

                if (!loginSucceeded)
                {
                    Shutdown(0);

                    return;
                }
            }

            EnsureAuthenticatedSession(
                serviceProvider);

            var logoutRequested =
                ShowShellWindow(
                    serviceProvider);

            ClearMainWindowReference();

            /*
             * Cho Dispatcher xử lý xong các event đóng cửa sổ
             * trước khi tiếp tục mở LoginWindow hoặc Shutdown.
             */
            await global::System.Windows.Threading
                .Dispatcher.Yield(
                    global::System.Windows.Threading
                        .DispatcherPriority.ApplicationIdle);

            if (!logoutRequested)
            {
                /*
                 * Shell đóng bằng nút X:
                 *
                 * - xóa session RAM;
                 * - giữ remembered credential;
                 * - thoát ứng dụng.
                 */
                currentUserService.Clear();

                Shutdown(0);

                return;
            }

            /*
             * Khi người dùng bấm Logout:
             *
             * - AuthService đã xóa remembered credential;
             * - AuthService đã xóa current session;
             * - vòng while tiếp tục và mở LoginWindow.
             */
        }
    }

    private static async Task
        TryRestoreRememberedLoginAsync(
            IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider
                .CreateAsyncScope();

        var authService =
            scope.ServiceProvider
                .GetRequiredService<
                    IAuthService>();

        /*
         * Credential không tồn tại, hết hạn, hỏng,
         * tài khoản bị khóa hoặc mật khẩu đã thay đổi
         * đều là trạng thái bình thường.
         *
         * Khi restore không thành công,
         * RunSessionLoopAsync sẽ mở LoginWindow.
         */
        await authService
            .TryRestoreRememberedLoginAsync();
    }

    private static async Task InitializeDatabaseAsync(
        IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider
                .CreateAsyncScope();

        var initializer =
            scope.ServiceProvider
                .GetRequiredService<
                    DatabaseInitializer>();

        await initializer
            .InitializeAsync();
    }

    private static async Task<bool>
        IsInitialSetupRequiredAsync(
            IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider
                .CreateAsyncScope();

        var setupService =
            scope.ServiceProvider
                .GetRequiredService<
                    IInitialSetupService>();

        var result =
            await setupService
                .IsSetupRequiredAsync();

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                result.Error.Message);
        }

        return result.Value;
    }

    private bool ShowInitialSetupWindow(
        IServiceProvider serviceProvider)
    {
        /*
         * Scope tồn tại trong toàn bộ thời gian
         * FirstRunSetupWindow đang hiển thị.
         *
         * Khi cửa sổ đóng, DbContext và các scoped service
         * của bước setup được dispose ngay.
         */
        using var scope =
            serviceProvider
                .CreateScope();

        var setupWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    FirstRunSetupWindow>();

        MainWindow =
            setupWindow;

        return setupWindow.ShowDialog() ==
               true;
    }

    private bool ShowLoginWindow(
        IServiceProvider serviceProvider)
    {
        /*
         * Mỗi lần mở LoginWindow có một DI scope mới.
         *
         * Điều này tránh việc IAuthService và DbContext
         * bị resolve từ root provider rồi tồn tại suốt app.
         */
        using var scope =
            serviceProvider
                .CreateScope();

        var loginWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    LoginWindow>();

        MainWindow =
            loginWindow;

        return loginWindow.ShowDialog() ==
               true;
    }

    private bool ShowShellWindow(
        IServiceProvider serviceProvider)
    {
        /*
         * Shell có scope riêng trong toàn bộ thời gian
         * cửa sổ chính đang mở.
         *
         * Khi Shell đóng hoặc Logout, toàn bộ scoped service
         * của phiên Shell được dispose trước khi mở phiên mới.
         */
        using var scope =
            serviceProvider
                .CreateScope();

        var shellWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    ShellWindow>();

        MainWindow =
            shellWindow;

        shellWindow.ShowDialog();

        return shellWindow
            .LogoutRequested;
    }

    private static void EnsureAuthenticatedSession(
        IServiceProvider serviceProvider)
    {
        var currentUserService =
            serviceProvider
                .GetRequiredService<
                    ICurrentUserService>();

        if (!currentUserService
            .IsAuthenticated)
        {
            throw new InvalidOperationException(
                "Không tìm thấy phiên đăng nhập hợp lệ.");
        }

        if (currentUserService.UserId is null ||
            currentUserService.Role is null ||
            string.IsNullOrWhiteSpace(
                currentUserService.Username))
        {
            throw new InvalidOperationException(
                "Phiên đăng nhập không đầy đủ thông tin.");
        }
    }

    private void ClearMainWindowReference()
    {
        MainWindow =
            null;
    }
}