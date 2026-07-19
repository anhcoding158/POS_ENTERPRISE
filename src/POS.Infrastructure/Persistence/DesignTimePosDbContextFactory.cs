using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using POS.Infrastructure;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Tạo PosDbContext cho các lệnh EF Core design-time:
///
/// dotnet ef migrations add
/// dotnet ef migrations remove
/// dotnet ef database update
/// </summary>
public sealed class DesignTimePosDbContextFactory :
    IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(
        string[] args)
    {
        var infrastructureOptions =
            LoadInfrastructureOptions();

        var pathResolver =
            new DatabasePathResolver();

        var connectionString =
            pathResolver.CreateConnectionString(
                infrastructureOptions);

        var optionsBuilder =
            new DbContextOptionsBuilder<PosDbContext>();

        optionsBuilder.UseSqlite(connectionString);

        /*
         * Hiển thị thêm thông tin model/query khi EF tooling lỗi.
         * Không bật SensitiveDataLogging để tránh lộ dữ liệu.
         */
        optionsBuilder.EnableDetailedErrors();

        return new PosDbContext(
            optionsBuilder.Options);
    }

    private static InfrastructureOptions
        LoadInfrastructureOptions()
    {
        var appSettingsPath =
            FindAppSettingsPath();

        if (!File.Exists(appSettingsPath))
        {
            var fallbackOptions =
                new InfrastructureOptions();

            fallbackOptions.Validate();

            return fallbackOptions;
        }

        var directory =
            Path.GetDirectoryName(appSettingsPath)
            ?? throw new InvalidOperationException(
                "Không xác định được thư mục appsettings.json.");

        var fileName =
            Path.GetFileName(appSettingsPath);

        var configuration =
            new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile(
                    fileName,
                    optional: false,
                    reloadOnChange: false)
                .Build();

        var section =
            configuration.GetSection(
                InfrastructureOptions.SectionName);

        var options =
            new InfrastructureOptions
            {
                DatabasePath =
                    section[
                        nameof(
                            InfrastructureOptions
                                .DatabasePath)]
                    ?? "data/pos-enterprise.db",

                DatabaseTimeoutSeconds =
                    ReadInteger(
                        section,
                        nameof(
                            InfrastructureOptions
                                .DatabaseTimeoutSeconds),
                        30),

                ApplyMigrationsOnStartup =
                    ReadBoolean(
                        section,
                        nameof(
                            InfrastructureOptions
                                .ApplyMigrationsOnStartup),
                        true),

                SeedDefaultAdministrator =
                    ReadBoolean(
                        section,
                        nameof(
                            InfrastructureOptions
                                .SeedDefaultAdministrator),
                        false),

                DefaultAdminUsername =
                    section[
                        nameof(
                            InfrastructureOptions
                                .DefaultAdminUsername)]
                    ?? "admin",

                DefaultAdminPassword =
                    section[
                        nameof(
                            InfrastructureOptions
                                .DefaultAdminPassword)]
                    ?? string.Empty,

                DefaultAdminFullName =
                    section[
                        nameof(
                            InfrastructureOptions
                                .DefaultAdminFullName)]
                    ?? "Quản trị viên hệ thống"
            };

        options.Validate();

        return options;
    }

    private static string FindAppSettingsPath()
    {
        var solutionRoot =
            DatabasePathResolver.FindSolutionRoot(
                Environment.CurrentDirectory)

            ?? DatabasePathResolver.FindSolutionRoot(
                AppContext.BaseDirectory);

        if (solutionRoot is not null)
        {
            return Path.Combine(
                solutionRoot,
                "src",
                "POS.Wpf",
                "appsettings.json");
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "appsettings.json");
    }

    private static int ReadInteger(
        IConfigurationSection section,
        string key,
        int fallbackValue)
    {
        var rawValue = section[key];

        return int.TryParse(
            rawValue,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedValue)
                ? parsedValue
                : fallbackValue;
    }

    private static bool ReadBoolean(
        IConfigurationSection section,
        string key,
        bool fallbackValue)
    {
        var rawValue = section[key];

        return bool.TryParse(
            rawValue,
            out var parsedValue)
                ? parsedValue
                : fallbackValue;
    }
}