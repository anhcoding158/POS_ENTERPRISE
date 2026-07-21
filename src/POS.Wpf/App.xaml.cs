using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

namespace POS.Wpf;

/// <summary>
/// Composition root và quản lý vòng đời ứng dụng WPF.
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
         * Bắt buộc giữ ứng dụng sống trong lúc đóng
         * FirstRunSetupWindow hoặc LoginWindow.
         *
         * Sau khi ShellWindow được mở, chế độ shutdown
         * sẽ được chuyển sang OnMainWindowClose.
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
             * Product, Category và Inventory services.
             */
            builder.Services.AddScoped<
                IProductService,
                ProductService>();

            builder.Services.AddScoped<
                ICategoryService,
                CategoryService>();

            builder.Services.AddScoped<
                IInventoryService,
                InventoryService>();

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
             * Authentication ViewModels và Windows.
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

            var setupRequired =
                await IsInitialSetupRequiredAsync(
                    _host.Services);

            bool authenticationSucceeded;

            if (setupRequired)
            {
                authenticationSucceeded =
                    ShowInitialSetupWindow(
                        _host.Services);
            }
            else
            {
                authenticationSucceeded =
                    ShowLoginWindow(
                        _host.Services);
            }

            if (!authenticationSucceeded)
            {
                Shutdown(0);

                return;
            }

            EnsureAuthenticatedSession(
                _host.Services);

            ShowShellWindow(
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
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(
        global::System.Windows.ExitEventArgs e)
    {
        var host =
            _host;

        _host = null;

        if (host is not null)
        {
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

    private static async Task InitializeDatabaseAsync(
        IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var initializer =
            scope.ServiceProvider
                .GetRequiredService<
                    DatabaseInitializer>();

        await initializer.InitializeAsync();
    }

    private static async Task<bool>
        IsInitialSetupRequiredAsync(
            IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

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

        /*
         * Gán tạm để WPF quản lý focus và ownership,
         * nhưng OnExplicitShutdown ngăn việc đóng dialog
         * làm tắt toàn bộ ứng dụng.
         */
        MainWindow =
            setupWindow;

        var setupCompleted =
            setupWindow.ShowDialog() ==
            true;

        /*
         * Không giữ tham chiếu đến cửa sổ đã đóng.
         */
        if (ReferenceEquals(
                MainWindow,
                setupWindow))
        {
            MainWindow = null;
        }

        return setupCompleted;
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

        var loginSucceeded =
            loginWindow.ShowDialog() ==
            true;

        if (ReferenceEquals(
                MainWindow,
                loginWindow))
        {
            MainWindow = null;
        }

        return loginSucceeded;
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
    }

    private void ShowShellWindow(
        IServiceProvider serviceProvider)
    {
        var shellWindow =
            serviceProvider
                .GetRequiredService<
                    ShellWindow>();

        /*
         * Từ thời điểm này ShellWindow mới là cửa sổ chính
         * thật sự của ứng dụng.
         */
        MainWindow =
            shellWindow;

        /*
         * Sau khi đăng nhập thành công, đóng ShellWindow
         * sẽ kết thúc ứng dụng theo hành vi WPF thông thường.
         */
        ShutdownMode =
            global::System.Windows.ShutdownMode
                .OnMainWindowClose;

        shellWindow.Show();
        shellWindow.Activate();
    }
}