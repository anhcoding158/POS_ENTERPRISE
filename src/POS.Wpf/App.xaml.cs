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
/// First-run/Login → Shell → Logout → Login.
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
         * App tự quyết định khi nào tiến trình kết thúc.
         *
         * Đóng SetupWindow, LoginWindow hoặc ShellWindow
         * không được tự động shutdown ứng dụng.
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

            builder.Configuration
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: true)
                .AddJsonFile(
                    $"appsettings." +
                    $"{builder.Environment.EnvironmentName}.json",
                    optional: true,
                    reloadOnChange: true);

            builder.Services.AddInfrastructure(
                builder.Configuration);

            /*
             * Authentication services.
             */
            builder.Services.AddScoped<
                IInitialSetupService,
                InitialSetupService>();

            builder.Services.AddScoped<
                IAuthService,
                AuthService>();

            /*
             * =================================================
             * PRODUCT SERVICES
             * =================================================
             *
             * ProductService thật được đăng ký bằng
             * concrete type.
             *
             * Mọi nơi yêu cầu IProductService sẽ nhận
             * AuthorizedProductService.
             */
            builder.Services.AddScoped<
                ProductService>();

            builder.Services.AddScoped<
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
             * =================================================
             * CATEGORY SERVICES
             * =================================================
             */
            builder.Services.AddScoped<
                ICategoryService,
                CategoryService>();

            /*
             * =================================================
             * INVENTORY SERVICES
             * =================================================
             *
             * InventoryService thật không được resolve
             * thông qua IInventoryService trực tiếp.
             *
             * Mọi nơi yêu cầu IInventoryService sẽ nhận
             * AuthorizedInventoryService.
             */
            builder.Services.AddScoped<
                InventoryService>();

            builder.Services.AddScoped<
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
             * Dialog services.
             */
            builder.Services.AddSingleton<
                IProductDialogService,
                ProductDialogService>();

            builder.Services.AddSingleton<
                ICategoryDialogService,
                CategoryDialogService>();

            builder.Services.AddSingleton<
                ICategoryManagementDialogService,
                CategoryManagementDialogService>();

            builder.Services.AddSingleton<
                IInventoryDialogService,
                InventoryDialogService>();

            /*
             * Authentication UI.
             */
            builder.Services.AddTransient<
                FirstRunSetupViewModel>();

            builder.Services.AddTransient<
                FirstRunSetupWindow>();

            builder.Services.AddTransient<
                LoginViewModel>();

            builder.Services.AddTransient<
                LoginWindow>();

            /*
             * Product, Category và Inventory UI.
             */
            builder.Services.AddTransient<
                ProductEditorViewModel>();

            builder.Services.AddTransient<
                CategoryEditorViewModel>();

            builder.Services.AddTransient<
                CategoryManagementViewModel>();

            builder.Services.AddTransient<
                InventoryAdjustmentViewModel>();

            builder.Services.AddTransient<
                InventoryHistoryViewModel>();

            /*
             * Main Shell.
             */
            builder.Services.AddTransient<
                ShellViewModel>();

            builder.Services.AddTransient<
                ShellWindow>();

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
             * Xóa phiên khỏi bộ nhớ khi ứng dụng kết thúc.
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
            finally
            {
                host.Dispose();
            }
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Vòng lặp phiên làm việc.
    ///
    /// Đăng xuất không tắt ứng dụng mà quay lại LoginWindow.
    /// Đóng Shell bằng nút X mới kết thúc ứng dụng.
    /// </summary>
    private async Task RunSessionLoopAsync(
        IServiceProvider serviceProvider)
    {
        var setupRequired =
            await IsInitialSetupRequiredAsync(
                serviceProvider);

        /*
         * Chỉ chạy khi database chưa có bất kỳ User nào.
         */
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

            /*
             * InitialSetupService tự tạo session
             * cho Administrator vừa được tạo.
             */
            EnsureAuthenticatedSession(
                serviceProvider);
        }

        while (true)
        {
            var currentUserService =
                serviceProvider
                    .GetRequiredService<
                        ICurrentUserService>();

            /*
             * Lần mở ứng dụng mới hoặc sau khi đăng xuất.
             */
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

            /*
             * ShowDialog giữ luồng ở đây cho đến khi
             * ShellWindow thực sự đóng.
             */
            var logoutRequested =
                ShowShellWindow(
                    serviceProvider);

            ClearMainWindowReference();

            /*
             * Cho WPF hoàn tất tháo Window cũ khỏi
             * visual tree trước khi mở LoginWindow mới.
             */
            await global::System.Windows.Threading
                .Dispatcher.Yield(
                    global::System.Windows.Threading
                        .DispatcherPriority.ApplicationIdle);

            if (!logoutRequested)
            {
                /*
                 * Người dùng đóng Shell bằng nút X.
                 */
                currentUserService.Clear();

                Shutdown(0);

                return;
            }

            /*
             * Khi LogoutRequested = true:
             * IAuthService.Logout đã xóa session.
             *
             * Vòng while chạy lại và mở LoginWindow.
             */
        }
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
        var setupWindow =
            serviceProvider
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
        var loginWindow =
            serviceProvider
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
        var shellWindow =
            serviceProvider
                .GetRequiredService<
                    ShellWindow>();

        MainWindow =
            shellWindow;

        /*
         * Không dùng shellWindow.Show().
         *
         * ShowDialog giúp App chờ Shell đóng rồi mới
         * kiểm tra người dùng muốn logout hay thoát.
         */
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