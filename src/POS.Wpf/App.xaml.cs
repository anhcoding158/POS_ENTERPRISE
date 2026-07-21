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
/// → Shell → Logout → Login.
/// </summary>
public partial class App :
    global::System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(
        global::System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

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
             * Product decorator.
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
             * Category decorator.
             */
            builder.Services.AddScoped<
                CategoryService>();

            builder.Services.AddScoped<
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
             * Inventory decorator.
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
 * Checkout core.
 *
 * CheckoutService là implementation thật.
 * Mọi nơi resolve ICheckoutService sẽ nhận
 * AuthorizedCheckoutService để enforce UseCheckout.
 */
            builder.Services.AddScoped<
                CheckoutService>();

            builder.Services.AddScoped<
                ICheckoutService>(
                    serviceProvider =>
                        new AuthorizedCheckoutService(
                            serviceProvider
                                .GetRequiredService<
                                    CheckoutService>(),

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
             * Chỉ xóa session RAM.
             *
             * Không xóa remembered credential khi người dùng
             * đóng ứng dụng bằng nút X.
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

    private async Task RunSessionLoopAsync(
        IServiceProvider serviceProvider)
    {
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
             * Chỉ thử tự đăng nhập một lần khi ứng dụng
             * vừa khởi động.
             *
             * Sau khi Logout, vòng while không tự khôi phục lại.
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

            await global::System.Windows.Threading
                .Dispatcher.Yield(
                    global::System.Windows.Threading
                        .DispatcherPriority.ApplicationIdle);

            if (!logoutRequested)
            {
                /*
                 * Đóng bằng X:
                 * chỉ xóa RAM, giữ credential 30 ngày.
                 */
                currentUserService.Clear();

                Shutdown(0);

                return;
            }

            /*
             * Đăng xuất:
             * AuthService đã xóa credential và session.
             * Vòng lặp mở LoginWindow.
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
         * Credential không tồn tại, hết hạn hoặc không hợp lệ
         * là trạng thái bình thường; LoginWindow sẽ được mở.
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