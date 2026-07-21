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
/// Composition root và vòng đời của ứng dụng WPF.
/// </summary>
public partial class App :
    global::System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(
        global::System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

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
             * Application services là Scoped.
             */
            builder.Services.AddScoped<
                IInitialSetupService,
                InitialSetupService>();

            builder.Services.AddScoped<
                IAuthService,
                AuthService>();

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
             * ViewModels và Windows.
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

            builder.Services.AddTransient<
                ShellViewModel>();

            builder.Services.AddTransient<
                ShellWindow>();

            _host =
                builder.Build();

            await _host.StartAsync();

            await InitializeDatabaseAsync(
                _host.Services);

            /*
             * Chặng 7B-2 sẽ thay đoạn này bằng:
             *
             * FirstRunSetupWindow hoặc LoginWindow.
             */
            var shellWindow =
                _host.Services
                    .GetRequiredService<
                        ShellWindow>();

            MainWindow =
                shellWindow;

            shellWindow.Show();
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
}