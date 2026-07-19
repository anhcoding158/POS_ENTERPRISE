using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Wpf.Views;

namespace POS.Wpf;

/// <summary>
/// Composition root của ứng dụng WPF.
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

            builder.Configuration.AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: true);

            builder.Services.AddInfrastructure(
                builder.Configuration);

            builder.Services.AddScoped<
                IProductService,
                ProductService>();

            builder.Services.AddTransient<LoginWindow>();

            _host = builder.Build();

            await _host.StartAsync();

            await InitializeDatabaseAsync(
                _host.Services);

            var loginWindow =
                _host.Services.GetRequiredService<
                    LoginWindow>();

            MainWindow = loginWindow;

            loginWindow.Show();
        }
        catch (Exception exception)
        {
            global::System.Windows.MessageBox.Show(
                $"Ứng dụng không thể khởi động.\n\n" +
                $"{exception.Message}",
                "POS Enterprise",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(
        global::System.Windows.ExitEventArgs e)
    {
        var host = _host;

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