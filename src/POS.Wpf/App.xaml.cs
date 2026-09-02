using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using POS.Application.Common;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Exports;
using POS.Application.Abstractions.Payments;
using POS.Application.Abstractions.ProductImports;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Services;
using POS.Infrastructure;
using POS.Infrastructure.Payments;
using POS.Infrastructure.Platform;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Logging;
using POS.Infrastructure.Support;
using POS.Infrastructure.Exports;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(
    "POS.Architecture.Tests")]

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
    private WindowsSingleInstanceCoordinator?
        _singleInstanceCoordinator;
    private readonly WindowActivationCoordinator
        _windowActivationCoordinator =
            new(new WindowActivationService());
    private global::System.Windows.Window?
        _activationWindow;
    private RestoreOperationPlan? _pendingRestoreOutcome;

    protected override async void OnStartup(
        global::System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var workerParse = ParseRestoreWorkerArguments(e.Args);
        if (workerParse.IsWorkerMode)
        {
            var exitCode = workerParse.Request is null
                ? RestoreWorkerExitCodes.InvalidArguments
                : await RunRestoreWorkerModeAsync(workerParse.Request);
            Shutdown(exitCode);
            return;
        }

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

            var databaseRuntimeState =
                ValidateDatabaseRuntime(
                    builder);

            builder.Logging.AddPosSafeFile(
                builder.Configuration);

            var databaseIdentity =
                ResolveDatabaseIdentity(
                    builder.Configuration);

            var singleInstanceCoordinator =
                new WindowsSingleInstanceCoordinator(
                    databaseIdentity);

            if (!singleInstanceCoordinator.TryAcquire())
            {
                var activationRequested =
                    await WindowsSingleInstanceCoordinator
                        .RequestActivationAsync(
                            databaseIdentity);

                var message =
                    activationRequested
                        ? "Ứng dụng đang chạy với dữ liệu cửa hàng này. " +
                          "Cửa sổ đang mở đã được yêu cầu đưa lên phía trước."
                        : "Ứng dụng đang chạy với dữ liệu cửa hàng này. " +
                          "Vui lòng chuyển sang cửa sổ ứng dụng đang mở.";

                singleInstanceCoordinator.Dispose();

                global::System.Windows.MessageBox.Show(
                    message,
                    "POS Enterprise",
                    global::System.Windows
                        .MessageBoxButton.OK,
                    global::System.Windows
                        .MessageBoxImage.Information);

                Shutdown(0);

                return;
            }

            _singleInstanceCoordinator =
                singleInstanceCoordinator;

            singleInstanceCoordinator
                .StartActivationListener(
                    HandleActivationRequestAsync);

            _pendingRestoreOutcome = await RecoverRestoreBeforeDatabaseStartupAsync(
                builder.Configuration);

            ConfigureApplicationServices(
                builder.Services,
                builder.Configuration);

            _host =
                builder.Build();

            await _host.StartAsync();
            LogIsolatedStartupMilestone("HostStarted");

            LogStartupDiagnostics(
                _host.Services.GetRequiredService<
                    ILogger<App>>(),
                builder,
                databaseRuntimeState);

            await InitializeDatabaseAsync(
                _host.Services);
            LogIsolatedStartupMilestone("DatabaseInitialized");

            await PresentAndAcknowledgeRestoreOutcomeAsync(
                builder.Configuration,
                _pendingRestoreOutcome);

            _host.Services.GetRequiredService<AutomaticBackupHostedService>()
                .MarkDatabaseInitialized();

            await RunSessionLoopAsync(
                _host.Services);
        }
        catch (DatabaseSafetyBlockException exception)
        {
            global::System.Windows.MessageBox.Show(
                exception.Message,
                "POS Enterprise",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);

            Shutdown(-1);
        }
        catch (RestoreStartupBlockException exception)
        {
            global::System.Windows.MessageBox.Show(
                exception.SafeMessage,
                "POS Enterprise",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
        catch (Exception exception)
        {
            LogStartupFailureSafely(exception);
            var classifier = new POS.Infrastructure.Persistence.SqliteFailureClassifier();
            var kind = classifier.Classify(exception);
            var presentation = kind is null
                ? null
                : POS.Wpf.Services.DatabaseFailurePresenter.Present(kind.Value);

            global::System.Windows.MessageBox.Show(
                presentation?.Message ??
                    "Ứng dụng không thể khởi động an toàn. Vui lòng liên hệ quản trị viên.",
                presentation?.Title ?? "POS Enterprise",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    private static void LogStartupFailureSafely(Exception exception)
    {
        try
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(
                        DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable),
                    DatabaseRuntimeGuard.IsolatedTestMode,
                    StringComparison.Ordinal))
            {
                return;
            }

            var configuredDatabasePath = Environment.GetEnvironmentVariable(
                DatabaseRuntimeGuard.DatabasePathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredDatabasePath) ||
                !Path.IsPathFullyQualified(configuredDatabasePath))
            {
                return;
            }

            var databasePath = Path.GetFullPath(configuredDatabasePath);
            var directory = Path.GetDirectoryName(databasePath);
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                return;
            }

            var directoryInfo = new DirectoryInfo(directory);
            if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            var diagnosticPath = Path.Combine(directory, "startup-failure.log");
            if (File.Exists(diagnosticPath) &&
                (File.GetAttributes(diagnosticPath) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            var lines = new List<string>
            {
                $"Utc={DateTime.UtcNow:O}",
                "RuntimeMode=IsolatedTest",
                "ExceptionChain=" + FormatStartupExceptionChain(exception, databasePath)
            };
            File.AppendAllLines(diagnosticPath, lines, new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Startup diagnostics must never change the fail-closed outcome.
        }
    }

    private static string FormatStartupExceptionChain(Exception exception, string databasePath)
    {
        var parts = new List<string>();
        var current = exception;
        var depth = 0;
        while (current is not null && depth++ < 8)
        {
            var message = current.Message
                .Replace(databasePath, "<ISOLATED_DB>", StringComparison.OrdinalIgnoreCase)
                .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            parts.Add($"{current.GetType().FullName}: {message}");
            current = current.InnerException!;
        }

        return string.Join(" <- ", parts);
    }

    private static void LogIsolatedStartupMilestone(string milestone)
    {
        try
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(
                        DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable),
                    DatabaseRuntimeGuard.IsolatedTestMode,
                    StringComparison.Ordinal))
            {
                return;
            }

            var configuredDatabasePath = Environment.GetEnvironmentVariable(
                DatabaseRuntimeGuard.DatabasePathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredDatabasePath) ||
                !Path.IsPathFullyQualified(configuredDatabasePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(configuredDatabasePath));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return;

            var path = Path.Combine(directory, "startup-diagnostics.log");
            if (File.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            var safeMilestone = new string((milestone ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-')
                .ToArray());
            if (string.IsNullOrWhiteSpace(safeMilestone)) return;
            File.AppendAllText(
                path,
                $"Utc={DateTime.UtcNow:O}; Milestone={safeMilestone}{Environment.NewLine}",
                new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Diagnostics must never affect startup or shutdown.
        }
    }

    protected override async void OnExit(
        global::System.Windows.ExitEventArgs e)
    {
        var host =
            _host;

        _host =
            null;

        var singleInstanceCoordinator =
            _singleInstanceCoordinator;

        _singleInstanceCoordinator =
            null;

        try
        {
            if (singleInstanceCoordinator is not null)
            {
                await singleInstanceCoordinator
                    .StopActivationListenerAsync();
            }
        }
        catch
        {
            // Listener shutdown is best-effort during application exit.
        }

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

                singleInstanceCoordinator?
                    .Dispose();
            }
        }
        else
        {
            singleInstanceCoordinator?
                .Dispose();
        }

        base.OnExit(e);
    }

    internal static RestoreWorkerArgumentParseResult ParseRestoreWorkerArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!arguments.Any(value => string.Equals(value, "--restore-worker", StringComparison.Ordinal)))
            return new(false, null);

        if (arguments.Count != 7 ||
            !string.Equals(arguments[0], "--restore-worker", StringComparison.Ordinal))
            return new(true, null);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < arguments.Count; index += 2)
        {
            var name = arguments[index];
            if (name is not ("--plan" or "--operation" or "--token") ||
                index + 1 >= arguments.Count || values.ContainsKey(name) ||
                string.IsNullOrWhiteSpace(arguments[index + 1]))
                return new(true, null);
            values.Add(name, arguments[index + 1]);
        }

        if (values.Count != 3) return new(true, null);
        if (!values.TryGetValue("--plan", out var plan) ||
            !values.TryGetValue("--operation", out var operationText) ||
            !values.TryGetValue("--token", out var token))
            return new(true, null);
        if (!Path.IsPathFullyQualified(plan) || !Guid.TryParseExact(operationText, "D", out var operationId) ||
            operationId == Guid.Empty || !IsValidRestoreToken(token))
            return new(true, null);

        return new(true, new(plan, operationId, token));
    }

    private static bool IsValidRestoreToken(string token)
    {
        if (token.Length != 44) return false;
        try { return Convert.FromBase64String(token).Length == 32; }
        catch (FormatException) { return false; }
    }

    private static async Task<int> RunRestoreWorkerModeAsync(RestoreWorkerRequest request)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory
            });
            ConfigureApplicationConfiguration(builder);
            _ = ValidateDatabaseRuntime(builder);

            var services = CreatePreStartupInfrastructureServices(builder.Configuration);
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var worker = provider.GetRequiredService<RestoreWorkerService>();
            var result = await worker.ExecuteAsync(request.PlanPath, request.OperationId,
                request.OneTimeToken, CancellationToken.None);

            if (result.Status is RestoreExecutionStatus.Success or
                RestoreExecutionStatus.RollbackSucceeded or RestoreExecutionStatus.RollbackFailed)
            {
                return TryRestartTrustedExecutable()
                    ? RestoreWorkerExitCodes.RestartStarted
                    : RestoreWorkerExitCodes.RestartFailed;
            }

            return result.Status == RestoreExecutionStatus.ParentExitTimeout
                ? RestoreWorkerExitCodes.ParentExitTimeout
                : RestoreWorkerExitCodes.ExecutionFailed;
        }
        catch
        {
            return RestoreWorkerExitCodes.ExecutionFailed;
        }
    }

    internal static bool TryRestartTrustedExecutable()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
                !File.Exists(executable)) return false;
            var attributes = File.GetAttributes(executable);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return false;
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable)
            {
                UseShellExecute = false
            });
            return process is not null && process.Id > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<RestoreOperationPlan?> RecoverRestoreBeforeDatabaseStartupAsync(
        IConfiguration configuration)
    {
        var services = CreatePreStartupInfrastructureServices(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var store = provider.GetRequiredService<RestoreOperationStore>();
        var discovery = await store.DiscoverStartupOperationAsync(CancellationToken.None);
        if (discovery.IsBlocked)
            throw new RestoreStartupBlockException(
                "Phát hiện nhiều thao tác khôi phục hoặc dữ liệu phục hồi không an toàn. Database chưa được mở. Hãy giữ nguyên các tệp phục hồi và liên hệ hỗ trợ.");
        if (discovery.Operation is null) return null;

        var plan = discovery.Operation.Plan;
        if (plan.State == RestoreOperationState.RollbackFailed)
            throw new RestoreStartupBlockException(
                "Không thể khôi phục dữ liệu ban đầu. Không tiếp tục sử dụng phần mềm. Hãy giữ nguyên các tệp phục hồi và liên hệ hỗ trợ.");
        if (plan.State is RestoreOperationState.Verified or RestoreOperationState.RolledBack)
            return plan;

        var token = await store.AuthorizeTrustedStartupRecoveryAsync(plan, CancellationToken.None);
        var worker = provider.GetRequiredService<RestoreWorkerService>();
        var result = await worker.ExecuteAsync(plan.OperationMarkerPath, plan.OperationId,
            token, CancellationToken.None);
        var finalDiscovery = await store.DiscoverStartupOperationAsync(CancellationToken.None);
        var finalPlan = finalDiscovery.Operation?.Plan;
        if (finalPlan?.State == RestoreOperationState.RollbackFailed ||
            result.Status == RestoreExecutionStatus.RollbackFailed)
            throw new RestoreStartupBlockException(
                "Không thể khôi phục dữ liệu ban đầu. Không tiếp tục sử dụng phần mềm. Hãy giữ nguyên các tệp phục hồi và liên hệ hỗ trợ.");
        if (finalPlan?.State is RestoreOperationState.Verified or RestoreOperationState.RolledBack)
            return finalPlan;

        throw new RestoreStartupBlockException(
            "Không thể hoàn tất phục hồi thao tác khôi phục an toàn. Database chưa được mở. Hãy giữ nguyên các tệp phục hồi và liên hệ hỗ trợ.");
    }

    private static async Task PresentAndAcknowledgeRestoreOutcomeAsync(
        IConfiguration configuration,
        RestoreOperationPlan? outcome)
    {
        if (outcome is null) return;
        var message = outcome.State switch
        {
            RestoreOperationState.Verified => "Khôi phục dữ liệu thành công.",
            RestoreOperationState.RolledBack =>
                "Khôi phục không thành công. Dữ liệu ban đầu đã được phục hồi an toàn.",
            _ => null
        };
        if (message is null) return;

        global::System.Windows.MessageBox.Show(
            message,
            "POS Enterprise",
            global::System.Windows.MessageBoxButton.OK,
            outcome.State == RestoreOperationState.Verified
                ? global::System.Windows.MessageBoxImage.Information
                : global::System.Windows.MessageBoxImage.Warning);

        var services = CreatePreStartupInfrastructureServices(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await RestoreOperationStore.AcknowledgeTerminalResultAsync(outcome, CancellationToken.None);
    }

    internal static IServiceCollection CreatePreStartupInfrastructureServices(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services;
    }

    private static DatabaseIdentity
        ResolveDatabaseIdentity(
            ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        var options =
            new InfrastructureOptions();

        configuration
            .GetSection(
                InfrastructureOptions.SectionName)
            .Bind(options);

        options.Validate();

        return DatabasePathResolver
            .ResolveDatabaseIdentity(
                options.DatabasePath);
    }

    internal static string
        GetDatabasePathConfigurationProvider(
            IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        if (configuration is not IConfigurationRoot root)
        {
            return "Unknown";
        }

        foreach (var provider in root.Providers.Reverse())
        {
            if (provider.TryGet(
                    "Infrastructure:DatabasePath",
                    out _))
            {
                return provider.GetType().Name switch
                {
                    "EnvironmentVariablesConfigurationProvider" =>
                        "EnvironmentVariables",
                    "CommandLineConfigurationProvider" =>
                        "CommandLine",
                    "JsonConfigurationProvider" =>
                        "Json",
                    "MemoryConfigurationProvider" =>
                        "Memory",
                    _ => "Other"
                };
            }
        }

        return "NotFound";
    }

    private static void LogStartupDiagnostics(
        ILogger logger,
        HostApplicationBuilder builder,
        DatabaseRuntimeState databaseRuntimeState)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(databaseRuntimeState);

        PosLog.Information(
            logger,
            "EnvironmentName={EnvironmentName}; " +
            "DatabasePathProvider={DatabasePathProvider}; " +
            "RuntimeMode={RuntimeMode}; " +
            "EnvironmentDatabasePathOverridePresent=" +
            "{EnvironmentDatabasePathOverridePresent}",
            GetSafeEnvironmentName(
                builder.Environment.EnvironmentName),
            GetDatabasePathConfigurationProvider(
                builder.Configuration),
            databaseRuntimeState.IsolatedTest
                ? "IsolatedTest"
                : "Normal",
            !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    DatabaseRuntimeGuard
                        .DatabasePathEnvironmentVariable)));
    }

    internal static DatabaseRuntimeState
        ValidateDatabaseRuntime(
            HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var provider =
            GetDatabasePathConfigurationProvider(
                builder.Configuration);

        var canonicalDatabasePath =
            GetCanonicalDatabasePath(
                builder.Configuration,
                provider);

        var state =
            DatabaseRuntimeGuard.Validate(
                provider,
                builder.Configuration[
                    "Infrastructure:DatabasePath"] ??
                string.Empty,
                DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(
                    canonicalDatabasePath),
                DatabasePathResolver.IsDevelopmentOutput(
                    AppContext.BaseDirectory),
                Environment.GetEnvironmentVariable(
                    DatabaseRuntimeGuard
                        .RuntimeModeEnvironmentVariable));

        var options =
            new InfrastructureOptions();

        builder.Configuration
            .GetSection(
                InfrastructureOptions.SectionName)
            .Bind(options);

        options.Validate();

        return state;
    }

    private static string GetCanonicalDatabasePath(
        ConfigurationManager configuration,
        string effectiveProvider)
    {
        if (string.Equals(
                effectiveProvider,
                "Json",
                StringComparison.Ordinal))
        {
            var value = configuration[
                "Infrastructure:DatabasePath"];

            return string.IsNullOrWhiteSpace(value)
                ? new InfrastructureOptions().DatabasePath
                : value;
        }

        var root =
            (IConfigurationRoot)configuration;
        var providers =
            root.Providers.ToArray();

        for (var index = providers.Length - 1;
             index >= 0;
             index--)
        {
            var provider = providers[index];
            var providerName = provider.GetType().Name;

            if (providerName is
                "EnvironmentVariablesConfigurationProvider" or
                "CommandLineConfigurationProvider")
            {
                continue;
            }

            if (provider.TryGet(
                    "Infrastructure:DatabasePath",
                    out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return new InfrastructureOptions().DatabasePath;
    }

    internal static string GetSafeEnvironmentName(
        string? environmentName) =>
        environmentName switch
        {
            "Development" => "Development",
            "Staging" => "Staging",
            "Production" => "Production",
            _ => "Other"
        };

    private Task HandleActivationRequestAsync()
    {
        void RequestActivation()
        {
            _windowActivationCoordinator
                .RequestActivation();
        }

        if (Dispatcher.CheckAccess())
        {
            RequestActivation();

            return Task.CompletedTask;
        }

        try
        {
            Dispatcher.BeginInvoke(
                RequestActivation,
                global::System.Windows.Threading
                    .DispatcherPriority.Normal);
        }
        catch (InvalidOperationException)
        {
            // The dispatcher is already shutting down.
        }

        return Task.CompletedTask;
    }

    internal static void
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
                    true)
            .AddEnvironmentVariables();
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
         * - database initializer;
         * - VietQR payload/PNG core.
         */
        services.AddInfrastructure(
            configuration);

        services.AddSingleton<AutomaticBackupHostedService>();
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<AutomaticBackupHostedService>());

        ConfigureAuthenticationServices(
            services);

        services.AddScoped<EmployeeAccountService>();
        services.AddScoped<IEmployeeAccountService>(serviceProvider =>
            serviceProvider.GetRequiredService<EmployeeAccountService>());

        services.AddScoped<IRolePermissionManagementService, RolePermissionManagementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

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

        services.AddScoped<ProductImportService>();
        services.AddScoped<IProductImportService>(serviceProvider =>
            new AuthorizedProductImportService(
                serviceProvider.GetRequiredService<ProductImportService>(),
                serviceProvider.GetRequiredService<IPermissionService>()));

        services.AddScoped<ProductExportService>();
        services.AddScoped<IProductExportService>(serviceProvider =>
            serviceProvider.GetRequiredService<ProductExportService>());
        services.AddSingleton<IProductExportWriter, ProductExportFileWriter>();
        services.AddScoped<IBulkProductOperationService, BulkProductOperationService>();

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
         * - tạo receipt snapshot;
         * - commit transaction.
         *
         * Mọi nơi resolve ICheckoutService đều nhận
         * AuthorizedCheckoutService để enforce UseCheckout.
         */
        services.AddScoped<CheckoutService>(serviceProvider =>
            global::Microsoft.Extensions.DependencyInjection
                .ActivatorUtilities.CreateInstance<CheckoutService>(
                    serviceProvider,
                    serviceProvider.GetRequiredService<
                        IReceiptStoreSnapshotProvider>()));

        services.AddScoped<
            PaymentIntentService>();

        services.AddScoped<IPaymentIntentService>(
            serviceProvider =>
                new AuthorizedPaymentIntentService(
                    serviceProvider.GetRequiredService<PaymentIntentService>(),
                    serviceProvider.GetRequiredService<IPermissionService>()));

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

        services.AddScoped<HeldSaleService>();

        services.AddScoped<IHeldSaleService>(
            serviceProvider =>
                new AuthorizedHeldSaleService(
                    serviceProvider.GetRequiredService<HeldSaleService>(),
                    serviceProvider.GetRequiredService<IPermissionService>()));
    }

    private static void ConfigureDialogServices(
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        /*
         * Các dialog service không trực tiếp giữ DbContext.
         *
         * Mỗi dialog hoặc service điều phối chỉ phụ thuộc
         * vào service có lifetime phù hợp với toàn ứng dụng.
         */
        services.AddSingleton<
            IProductDialogService,
            ProductDialogService>();

        services.AddSingleton<
            IProductImportDialogService,
            ProductImportDialogService>();

        services.AddSingleton<
            IProductExportDialogService,
            ProductExportDialogService>();

        services.AddSingleton<
            IBulkProductDialogService,
            BulkProductDialogService>();

        services.AddSingleton<
            ILabelPrintDialogService,
            LabelPrintDialogService>();

        services.AddSingleton<
            ICategoryDialogService,
            CategoryDialogService>();

        services.AddSingleton<
            ICategoryManagementDialogService,
            CategoryManagementDialogService>();

        services.AddSingleton<
            IInventoryDialogService,
            InventoryDialogService>();

        services.AddSingleton<
            IHeldSaleDialogService,
            HeldSaleDialogService>();

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
         * VietQrPaymentDialogService hiện có hai constructor:
         *
         * - constructor production dùng payload QR tải từ ảnh;
         * - constructor compatibility dành cho test cũ.
         *
         * Đăng ký bằng factory để Microsoft DI luôn chọn
         * constructor production, tránh lỗi ambiguous constructor.
         */
        services.AddSingleton<
            IVietQrPaymentDialogService>(
                serviceProvider =>
                    new VietQrPaymentDialogService(
                        serviceProvider
                            .GetRequiredService<
                                StoredVietQrService>(),

                        serviceProvider
                            .GetRequiredService<
                                IVietQrPayloadStore>(),

                        serviceProvider
                            .GetRequiredService<
                                IVietQrRecipientMetadataStore>(),

                        serviceProvider
                            .GetRequiredService<
                                ILogger<
                                    VietQrPaymentDialogService>>()));

        /*
         * SalesPaymentFlowService điều phối bước xác thực
         * phương thức thanh toán trước Checkout.
         *
         * Nó chịu trách nhiệm:
         * - kiểm tra tiền mặt có đủ hay không;
         * - mở dialog VietQR;
         * - phân biệt hủy và xác nhận;
         * - tạo authorization VietQR;
         * - tái sử dụng authorization khi Checkout cần thử lại;
         * - không mở QR lần hai sau khi đã nhận tiền.
         *
         * Service không giữ DbContext và không giữ giỏ hàng.
         * Sequence mã tham chiếu được bảo vệ bằng Interlocked,
         * vì vậy singleton là lifetime phù hợp.
         */
        services.AddSingleton<
            ISalesPaymentFlowService,
            SalesPaymentFlowService>();

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

        services.AddSingleton<
            IOrderHistoryWindowService,
            OrderHistoryWindowService>();

        services.AddSingleton<
            IOrderReturnWindowService,
            OrderReturnWindowService>();
        services.AddSingleton<
            IOrderReturnConfirmationService,
            OrderReturnConfirmationService>();

        services.AddSingleton<
            ICheckoutRecoveryConfirmationService,
            CheckoutRecoveryConfirmationService>();

        services.AddSingleton<
            ISupportBundleFolderPicker,
            SupportBundleFolderPicker>();

        services.AddSingleton<
            IManualBackupFolderPicker,
            ManualBackupFolderPicker>();

        services.AddSingleton<
            IRestoreArtifactFilePicker,
            RestoreArtifactFilePicker>();

        services.AddSingleton<IStoreSettingsFilePicker, StoreSettingsFilePicker>();
        services.AddScoped<IStoreSettingsDialogService, StoreSettingsDialogService>();
        services.AddSingleton<IEmployeeManagementDialogService, EmployeeManagementDialogService>();
        services.AddSingleton<IRolePermissionManagementDialogService, RolePermissionManagementDialogService>();
        services.AddSingleton<IAuditLogDialogService, AuditLogDialogService>();

        services.AddScoped<
            ISupportBundleDialogService,
            SupportBundleDialogService>();

        services.AddScoped<
            IManualBackupDialogService,
            ManualBackupDialogService>();

        services.AddScoped<
            IStorageStatusDialogService,
            StorageStatusDialogService>();
    }

    private static void ConfigureViewModelsAndWindows(
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

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

        services.AddTransient<
            ForcedPasswordChangeViewModel>();

        services.AddTransient<
            ForcedPasswordChangeWindow>();

        /*
         * Product, Category và Inventory UI.
         */
        services.AddTransient<
            ProductEditorViewModel>();

        services.AddTransient<ProductImportWizardViewModel>();
        services.AddTransient<ProductImportWizardWindow>();

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

        services.AddTransient<
            OrderHistoryViewModel>();

        services.AddTransient<
            OrderHistoryWindow>();

        services.AddTransient<
            SupportBundleViewModel>();

        services.AddTransient<
            SupportBundleWindow>();

        services.AddTransient<
            ManualBackupViewModel>();

        services.AddTransient<
            ManualBackupWindow>();

        services.AddTransient<
            RestoreWizardViewModel>();

        services.AddTransient<
            RestoreWizardWindow>();

        services.AddTransient<StoreSettingsViewModel>();
        services.AddTransient<StoreSettingsWindow>();
        services.AddTransient<EmployeeManagementViewModel>();
        services.AddTransient<RolePermissionManagementViewModel>();
        services.AddTransient<RolePermissionManagementWindow>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<AuditLogWindow>();

        services.AddScoped<
            StorageStatusViewModel>();

        services.AddScoped<AutomaticBackupStatusViewModel>();

        services.AddTransient<
            StorageStatusWindow>();

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

        LogIsolatedStartupMilestone("SessionLoopEntered");

        var setupRequired =
            await IsInitialSetupRequiredAsync(
                serviceProvider);

        if (setupRequired)
        {
            LogIsolatedStartupMilestone("InitialSetupWindowOpening");
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

            if (!await EnsurePasswordChangeCompletedAsync(serviceProvider))
            {
                Shutdown(0);
                return;
            }
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
                LogIsolatedStartupMilestone("LoginWindowOpening");
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

            if (!await EnsurePasswordChangeCompletedAsync(serviceProvider))
            {
                Shutdown(0);
                return;
            }

            EnsureAuthenticatedSession(
                serviceProvider);

            LogIsolatedStartupMilestone("ShellWindowOpening");
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
                result.AppError.Message);
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

        LogIsolatedStartupMilestone("InitialSetupWindowResolving");
        var setupWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    FirstRunSetupWindow>();
        LogIsolatedStartupMilestone("InitialSetupWindowConstructed");

        SetMainWindowReference(
            setupWindow);
        LogIsolatedStartupMilestone("InitialSetupWindowReady");

        var result = setupWindow.ShowDialog() ==
               true;
        LogIsolatedStartupMilestone("InitialSetupDialogReturned");
        return result;
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

        LogIsolatedStartupMilestone("LoginWindowResolving");
        var loginWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    LoginWindow>();
        LogIsolatedStartupMilestone("LoginWindowConstructed");

        SetMainWindowReference(
            loginWindow);
        LogIsolatedStartupMilestone("LoginWindowReady");

        var result = loginWindow.ShowDialog() ==
               true;
        LogIsolatedStartupMilestone("LoginDialogReturned");
        return result;
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

        LogIsolatedStartupMilestone("ShellWindowResolving");
        var shellWindow =
            scope.ServiceProvider
                .GetRequiredService<
                    ShellWindow>();
        LogIsolatedStartupMilestone("ShellWindowConstructed");

        SetMainWindowReference(
            shellWindow);
        LogIsolatedStartupMilestone("ShellWindowReady");

        shellWindow.ShowDialog();
        LogIsolatedStartupMilestone("ShellDialogReturned");

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

    private void SetMainWindowReference(
        global::System.Windows.Window window)
    {
        ArgumentNullException.ThrowIfNull(
            window);

        ClearMainWindowReference();

        MainWindow =
            window;

        _activationWindow =
            window;

        window.Loaded +=
            OnActivationWindowLoaded;

        window.Closed +=
            OnActivationWindowClosed;

        _windowActivationCoordinator
            .SetTarget(
                new WpfWindowActivationTarget(
                    window));
    }

    private async Task<bool> EnsurePasswordChangeCompletedAsync(IServiceProvider serviceProvider)
    {
        var currentUser = serviceProvider.GetRequiredService<ICurrentUserService>().CurrentUser;
        if (currentUser is null || !currentUser.ForcePasswordChange)
        {
            return true;
        }

        await Dispatcher.InvokeAsync(() => { }, global::System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        using var scope = serviceProvider.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<ForcedPasswordChangeWindow>();
        SetMainWindowReference(window);
        var completed = window.ShowDialog() == true;
        ClearMainWindowReference();
        return completed && serviceProvider.GetRequiredService<ICurrentUserService>().CurrentUser?.ForcePasswordChange == false;
    }

    private void OnActivationWindowLoaded(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        _windowActivationCoordinator
            .NotifyTargetReady();
    }

    private void OnActivationWindowClosed(
        object? sender,
        EventArgs e)
    {
        if (!ReferenceEquals(
                sender,
                _activationWindow))
        {
            return;
        }

        _windowActivationCoordinator
            .ClearTarget();

        _activationWindow =
            null;
    }

    private void ClearMainWindowReference()
    {
        if (_activationWindow is not null)
        {
            _activationWindow.Loaded -=
                OnActivationWindowLoaded;

            _activationWindow.Closed -=
                OnActivationWindowClosed;

            _activationWindow =
                null;
        }

        _windowActivationCoordinator
            .ClearTarget();

        MainWindow =
            null;
    }
}

internal sealed record RestoreWorkerRequest(
    string PlanPath,
    Guid OperationId,
    string OneTimeToken);

internal sealed record RestoreWorkerArgumentParseResult(
    bool IsWorkerMode,
    RestoreWorkerRequest? Request);

internal static class RestoreWorkerExitCodes
{
    internal const int RestartStarted = 0;
    internal const int InvalidArguments = 21;
    internal const int ExecutionFailed = 22;
    internal const int ParentExitTimeout = 23;
    internal const int RestartFailed = 24;
}

internal sealed class RestoreStartupBlockException(string safeMessage) : Exception
{
    internal string SafeMessage { get; } = safeMessage;
}
