using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Logging;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SafeSupportBundleTests : IDisposable
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 8, 8, 5, 6, 7, TimeSpan.Zero);
    private static readonly Guid FixedId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly string[] FixedEntryNames =
    [
        "diagnostics/configuration.json", "diagnostics/integrity.json",
        "diagnostics/migrations.json", "diagnostics/runtime.json",
        "diagnostics/version.json", "logs/pos-enterprise-20260808-0001.log",
        "manifest.json"
    ];
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "POS-SupportBundleTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Export_creates_atomic_fixed_schema_bundle_with_safe_typed_diagnostics()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        var logs = Directory.CreateDirectory(Path.Combine(_root, "logs")).FullName;
        File.WriteAllText(ManagedPath(logs, 1), "2026 safe operation\n", new UTF8Encoding(false));
        await using var context = Context(Path.Combine(_root, "isolated.db"));
        var service = Service(context, logs);

        var result = await service.ExportAsync(new SupportBundleRequest(destination));

        Assert.Equal(SupportBundleStatus.Success, result.Status);
        var expected = Path.Combine(destination,
            "POS-Enterprise-Support-20260808-050607-11111111222233334444555555555555.zip");
        Assert.Equal(expected, result.ArchivePath);
        Assert.True(File.Exists(expected));
        Assert.Empty(Directory.GetFiles(destination, "*.tmp"));
        using var archive = ZipFile.OpenRead(expected);
        Assert.Equal(FixedEntryNames,
            archive.Entries.Select(entry => entry.FullName).Order().ToArray());

        using var manifest = JsonDocument.Parse(await ReadAsync(archive, "manifest.json"));
        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(manifest.RootElement.GetProperty("databaseIncluded").GetBoolean());
        var allText = await ReadAllTextAsync(archive);
        Assert.DoesNotContain(Environment.UserName, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_root, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Include_database_fails_closed_before_destination_or_artifact_work()
    {
        var missing = Path.Combine(_root, "must-not-exist");
        await using var context = Context(Path.Combine(_root, "must-not-open.db"));
        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(missing, IncludeDatabase: true));

        Assert.Equal(SupportBundleStatus.DatabaseInclusionNotSupported, result.Status);
        Assert.Null(result.ArchivePath);
        Assert.False(Directory.Exists(missing));
        Assert.False(File.Exists(Path.Combine(_root, "must-not-open.db")));
        Assert.Empty(Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative-folder")]
    public async Task Invalid_destination_returns_typed_failure_without_archive(string destination)
    {
        await using var context = Context(Path.Combine(_root, "unused.db"));
        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination));
        Assert.Equal(SupportBundleStatus.InvalidDestination, result.Status);
        Assert.Null(result.ArchivePath);
    }

    [Fact]
    public async Task Missing_destination_is_not_created()
    {
        var destination = Path.Combine(_root, "missing");
        await using var context = Context(Path.Combine(_root, "unused.db"));
        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination));
        Assert.Equal(SupportBundleStatus.InvalidDestination, result.Status);
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task Existing_final_name_is_never_overwritten_and_temp_is_removed()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        var final = Path.Combine(destination,
            "POS-Enterprise-Support-20260808-050607-11111111222233334444555555555555.zip");
        await File.WriteAllTextAsync(final, "foreign");
        await using var context = Context(Path.Combine(_root, "isolated.db"));

        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination));

        Assert.Equal(SupportBundleStatus.ArchiveAlreadyExists, result.Status);
        Assert.Null(result.ArchivePath);
        Assert.Equal("foreign", await File.ReadAllTextAsync(final));
        Assert.Empty(Directory.GetFiles(destination, "*.tmp"));
    }

    [Fact]
    public async Task Existing_foreign_temp_is_never_opened_overwritten_or_deleted()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        var foreignTemp = Path.Combine(destination,
            ".11111111222233334444555555555555.support-bundle.tmp");
        await File.WriteAllTextAsync(foreignTemp, "foreign-temp");
        await using var context = Context(Path.Combine(_root, "must-not-open.db"));

        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination));

        Assert.Equal(SupportBundleStatus.ArchiveCreationFailure, result.Status);
        Assert.Null(result.ArchivePath);
        Assert.Equal("foreign-temp", await File.ReadAllTextAsync(foreignTemp));
        Assert.False(File.Exists(Path.Combine(_root, "must-not-open.db")));
    }

    [Fact]
    public async Task Pre_cancelled_export_leaves_no_artifact_or_database()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        await using var context = Context(Path.Combine(_root, "must-not-open.db"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination), cancellation.Token);

        Assert.Equal(SupportBundleStatus.Cancelled, result.Status);
        Assert.Null(result.ArchivePath);
        Assert.Empty(Directory.GetFiles(destination));
        Assert.False(File.Exists(Path.Combine(_root, "must-not-open.db")));
    }

    [Fact]
    public async Task Logs_are_top_level_managed_newest_first_bounded_and_resanitized()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        var logs = Directory.CreateDirectory(Path.Combine(_root, "logs")).FullName;
        var old = ManagedPath(logs, 1);
        var newest = ManagedPath(logs, 2);
        await File.WriteAllTextAsync(old, "old safe line\n");
        var canary = "token=split-canary-123";
        await File.WriteAllTextAsync(newest,
            "safe newest\n" + canary + "\n" + new string('x', 600) + "\n" + "Việt Nam ✓\n",
            new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(old, FixedUtc.AddMinutes(-2).UtcDateTime);
        File.SetLastWriteTimeUtc(newest, FixedUtc.AddMinutes(-1).UtcDateTime);
        await File.WriteAllTextAsync(Path.Combine(logs, "foreign.log"), canary);
        var nested = Directory.CreateDirectory(Path.Combine(logs, "nested"));
        await File.WriteAllTextAsync(Path.Combine(nested.FullName,
            "pos-enterprise-20260808-0003.log"), canary);
        await using var heldOpen = new FileStream(newest, FileMode.Open, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        heldOpen.Seek(0, SeekOrigin.End);
        await using var context = Context(Path.Combine(_root, "isolated.db"));
        var service = Service(context, logs, maxLogBytes: 128, maxRecordChars: 256);

        var result = await service.ExportAsync(new SupportBundleRequest(destination));

        Assert.True(result.IsSuccess);
        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("foreign", StringComparison.Ordinal));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("0003", StringComparison.Ordinal));
        var exportedLogs = archive.Entries.Where(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(exportedLogs);
        Assert.True(exportedLogs.Sum(entry => entry.Length) <= 128);
        var allText = await ReadAllTextAsync(archive);
        Assert.DoesNotContain(canary, allText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", allText, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 300), allText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_failure_is_normalized_and_bundle_still_succeeds()
    {
        var destination = Directory.CreateDirectory(Path.Combine(_root, "export")).FullName;
        var context = Context(Path.Combine(_root, "isolated.db"));
        await context.DisposeAsync();

        var result = await Service(context, Path.Combine(_root, "logs"))
            .ExportAsync(new SupportBundleRequest(destination));

        Assert.True(result.IsSuccess);
        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var text = await ReadAllTextAsync(archive);
        Assert.Contains("unavailable", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ObjectDisposedException", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_are_bounded_before_allocation()
    {
        Assert.Throws<InvalidOperationException>(() => new SupportBundleOptions
            { MaxExportedLogBytes = SupportBundleOptions.MaximumExportedLogBytes + 1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SupportBundleOptions
            { MaxLogRecordChars = SupportBundleOptions.MaximumLogRecordChars + 1 }.Validate());
        new SupportBundleOptions().Validate();
    }

    [Fact]
    public void Contract_and_di_preserve_layering_and_register_service_once()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        services.AddInfrastructure(configuration);
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ISupportBundleService));

        var applicationProject = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "POS.Application", "POS.Application.csproj"));
        foreach (var forbidden in new[]
            { "POS.Infrastructure", "EntityFrameworkCore", "Sqlite", "ZipArchive", "POS.Wpf" })
            Assert.DoesNotContain(forbidden, applicationProject, StringComparison.OrdinalIgnoreCase);

        var request = new SupportBundleRequest(_root);
        Assert.False(request.IncludeDatabase);
        foreach (var status in Enum.GetValues<SupportBundleStatus>().Where(s => s != SupportBundleStatus.Success))
            Assert.Null(SupportBundleResult.Failure(status).ArchivePath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { }
    }

    private static SupportBundleService Service(
        PosDbContext context, string logDirectory,
        long maxLogBytes = 20 * 1024 * 1024, int maxRecordChars = 4096) =>
        new(context, new InfrastructureOptions { ApplyMigrationsOnStartup = false },
            new SafeFileLoggerOptions { LogDirectory = logDirectory },
            new SupportBundleOptions
            {
                MaxExportedLogBytes = maxLogBytes,
                MaxLogRecordChars = maxRecordChars
            }, () => FixedUtc, () => FixedId);

    private static PosDbContext Context(string databasePath)
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new PosDbContext(options);
    }

    private static string ManagedPath(string root, int sequence) =>
        Path.Combine(root, $"pos-enterprise-20260808-{sequence:D4}.log");

    private static async Task<string> ReadAsync(ZipArchive archive, string name)
    {
        var entry = Assert.Single(archive.Entries, value => value.FullName == name);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string> ReadAllTextAsync(ZipArchive archive)
    {
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            builder.Append(await reader.ReadToEndAsync());
        }
        return builder.ToString();
    }

    private static string SolutionRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
