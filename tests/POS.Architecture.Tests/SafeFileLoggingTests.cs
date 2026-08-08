using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using POS.Infrastructure.Logging;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SafeFileLoggingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "pos-safe-log-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Size_rotation_and_dispose_flush_create_valid_bounded_segments()
    {
        using (var provider = CreateProvider(maxFileBytes: 300))
        {
            var logger = provider.CreateLogger("Rotation");
            for (var i = 0; i < 20; i++)
                PosLog.Information(logger,
                    "Operation {Operation} Stage {Stage} Attempt {Attempt}",
                    "Rotation", "Write", i);
        }

        var files = ManagedFiles();
        Assert.True(files.Length > 1);
        Assert.All(files, file => Assert.InRange(file.Length, 1, 300));
        Assert.Contains("Operation Rotation", File.ReadAllText(files[^1].FullName));
    }

    [Fact]
    public void Utc_date_change_rotates_to_new_named_segment()
    {
        var now = new DateTimeOffset(2026, 8, 8, 23, 59, 0, TimeSpan.Zero);
        using var provider = CreateProvider(utcNow: () => now);
        var logger = provider.CreateLogger("DateRotation");
        PosLog.Information(logger, "Operation {Operation}", "DayOne");
        now = now.AddMinutes(2);
        PosLog.Information(logger, "Operation {Operation}", "DayTwo");

        Assert.Contains(ManagedFiles(), file => file.Name.Contains("20260808", StringComparison.Ordinal));
        Assert.Contains(ManagedFiles(), file => file.Name.Contains("20260809", StringComparison.Ordinal));
    }

    [Fact]
    public void Cleanup_removes_oldest_managed_files_for_count_age_and_total_size_only()
    {
        Directory.CreateDirectory(_root);
        var old = ManagedPath("20260101", 0);
        var middle = ManagedPath("20260807", 0);
        File.WriteAllBytes(old, new byte[300]);
        File.WriteAllBytes(middle, new byte[300]);
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(middle, new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc));
        var foreign = Path.Combine(_root, "keep-me.txt");
        File.WriteAllText(foreign, "foreign");

        using (var provider = CreateProvider(maxFileBytes: 300,
                   maxSegments: 2, maxDirectoryBytes: 500,
                   maxAgeDays: 14,
                   utcNow: () => new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero)))
            PosLog.Information(provider.CreateLogger("Cleanup"),
                "Operation {Operation}", "Cleanup");

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(foreign));
        var managed = ManagedFiles();
        Assert.InRange(managed.Length, 1, 2);
        Assert.True(managed.Sum(file => file.Length) <= 500);
        Assert.DoesNotContain(managed, file => file.FullName.StartsWith(
            Path.GetFullPath(Path.Combine(_root, "..")), StringComparison.Ordinal) &&
            !file.FullName.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal));
    }

    [Fact]
    public void Cleanup_ignores_managed_named_reparse_points_and_preserves_external_target()
    {
        var parent = Path.GetDirectoryName(_root)!;
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(_root);
        var externalTarget = Path.Combine(parent, $"target-{Guid.NewGuid():N}.txt");
        const string canary = "external-target-must-survive";
        File.WriteAllText(externalTarget, canary);
        var link = ManagedPath("20260101", 0);
        var regularOld = ManagedPath("20260102", 0);
        File.WriteAllBytes(regularOld, new byte[300]);
        File.SetLastWriteTimeUtc(
            regularOld,
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var realLinkCreated = false;
        try
        {
            try
            {
                File.CreateSymbolicLink(link, externalTarget);
                realLinkCreated = true;
            }
            catch (Exception exception) when (exception is
                UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                Assert.False(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
                    _root,
                    link,
                    FileAttributes.Archive | FileAttributes.ReparsePoint));
            }

            using (var provider = CreateProvider(
                       maxFileBytes: 300,
                       maxSegments: 1,
                       maxDirectoryBytes: 300,
                       maxAgeDays: 14,
                       utcNow: () => new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero)))
            {
                PosLog.Information(
                    provider.CreateLogger("ReparseGuard"),
                    "Operation {Operation}",
                    "Cleanup");
            }

            if (realLinkCreated)
            {
                Assert.True(File.Exists(link));
                Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            }
            Assert.True(File.Exists(externalTarget));
            Assert.Equal(canary, File.ReadAllText(externalTarget));
            Assert.False(File.Exists(regularOld));
            Assert.Single(ManagedFiles(), file =>
                (file.Attributes & FileAttributes.ReparsePoint) == 0);
        }
        finally
        {
            try { if (File.Exists(link)) File.Delete(link); } catch { }
            try { if (File.Exists(externalTarget)) File.Delete(externalTarget); } catch { }
        }
    }

    [Fact]
    public void Managed_candidate_guard_rejects_directory_nested_and_prefix_sibling_paths()
    {
        var managedName = "pos-enterprise-20260808-0001.log";
        var direct = Path.Combine(_root, managedName);
        var nested = Path.Combine(_root, "nested", managedName);
        var prefixSibling = Path.Combine($"{_root}-other", managedName);

        Assert.True(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
            _root, direct, FileAttributes.Archive));
        Assert.False(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
            _root, direct, FileAttributes.Directory));
        Assert.False(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
            _root, direct, FileAttributes.ReparsePoint));
        Assert.False(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
            _root, nested, FileAttributes.Archive));
        Assert.False(SafeFileLoggerProvider.IsManagedRegularFileCandidate(
            _root, prefixSibling, FileAttributes.Archive));
    }

    [Fact]
    public void PosLog_redacts_secrets_sql_customer_payment_and_sqlite_detail()
    {
        using (var provider = CreateProvider())
        {
            var logger = provider.CreateLogger("Privacy");
            PosLog.Error(logger,
                new SqliteException(
                    "SELECT * FROM Customers at C:\\Users\\private\\store.db", 5, 517),
                "Operation {Operation}; Password {Password}; Token {Token}; " +
                "ConnectionString {ConnectionString}; Sql {Sql}; Customer {Customer}; " +
                "Phone {Phone}; PaymentDetail {PaymentDetail}",
                "Checkout", "open-sesame", "token-value",
                "Data Source=C:\\Users\\private\\store.db", "SELECT * FROM Customers",
                "Nguyen Van A", "0901234567", "4111111111111111");
        }

        var text = File.ReadAllText(Assert.Single(ManagedFiles()).FullName);
        Assert.Contains("Operation Checkout", text);
        Assert.Contains("ExceptionType=Microsoft.Data.Sqlite.SqliteException", text);
        Assert.Contains("SqliteErrorCode=5", text);
        Assert.Contains("SqliteExtendedErrorCode=517", text);
        Assert.DoesNotContain("open-sesame", text, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Data Source", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Customers at", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nguyen Van A", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0901234567", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Concurrent_logging_produces_complete_lines()
    {
        using (var provider = CreateProvider(
                   maxFileBytes: 1024 * 1024,
                   maxDirectoryBytes: 2 * 1024 * 1024))
        {
            var logger = provider.CreateLogger("Concurrent");
            await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() =>
                PosLog.Information(logger, "Operation {Operation} Attempt {Attempt}", "Concurrent", i))));
        }

        var lines = File.ReadAllLines(Assert.Single(ManagedFiles()).FullName);
        Assert.Equal(200, lines.Length);
        Assert.All(lines, line => Assert.Contains("Operation Concurrent Attempt", line));
    }

    [Fact]
    public void Unwritable_log_location_does_not_escape_to_business_caller()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_root)!);
        File.WriteAllText(_root, "this path is a file");
        using var provider = CreateProvider();
        var logger = provider.CreateLogger("Failure");
        var exception = Record.Exception(() =>
            PosLog.Error(logger, "Operation {Operation}", "Business"));
        Assert.Null(exception);
    }

    [Fact]
    public void Invalid_options_are_rejected()
    {
        var options = Options();
        options.MaxFileSizeBytes = 0;
        Assert.Throws<InvalidOperationException>(() => new SafeFileLoggerProvider(options));
        options = Options();
        options.MaxFileSizeBytes = options.MaxDirectorySizeBytes + 1;
        Assert.Throws<InvalidOperationException>(() => new SafeFileLoggerProvider(options));
    }

    [Fact]
    public void Di_registration_is_idempotent_and_preserves_architecture_direction()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddLogging(builder => builder.AddPosSafeFile(configuration));
        services.AddLogging(builder => builder.AddPosSafeFile(configuration));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ILoggerProvider) &&
            descriptor.ImplementationType == typeof(SafeFileLoggerProvider));

        var applicationProject = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "POS.Application", "POS.Application.csproj"));
        Assert.DoesNotContain("POS.Infrastructure", applicationProject, StringComparison.Ordinal);
        Assert.DoesNotContain("POS.Wpf", applicationProject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trace_fallbacks_emit_exception_type_without_sensitive_detail()
    {
        var secret = "SELECT secret FROM Customers at C:\\Users\\private\\store.db";
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var command = new POS.Wpf.Commands.AsyncRelayCommand(
                () => Task.FromException(new InvalidOperationException(secret)));
            command.Execute(null);
            await WaitUntilAsync(() => !command.IsExecuting);

            var identity = POS.Infrastructure.Persistence.DatabaseIdentity.FromResolvedPath(
                Path.Combine(_root, "identity-only.db"));
            using var coordinator = new POS.Infrastructure.Platform.WindowsSingleInstanceCoordinator(identity);
            var handler = (Action<Exception>)typeof(POS.Infrastructure.Platform.WindowsSingleInstanceCoordinator)
                .GetField("_listenerErrorHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(coordinator)!;
            handler(new SqliteException(secret, 5, 517));
            Trace.Flush();

            var trace = output.ToString();
            Assert.Contains("ExceptionType=System.InvalidOperationException", trace);
            Assert.Contains("ExceptionType=Microsoft.Data.Sqlite.SqliteException", trace);
            Assert.DoesNotContain(secret, trace, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\Users", trace, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            else if (File.Exists(_root)) File.Delete(_root);
        }
        catch { }
    }

    private SafeFileLoggerProvider CreateProvider(
        long maxFileBytes = 4096, int maxSegments = 10,
        long maxDirectoryBytes = 50 * 1024, int maxAgeDays = 14,
        Func<DateTimeOffset>? utcNow = null)
    {
        var options = Options();
        options.MaxFileSizeBytes = maxFileBytes;
        options.MaxSegmentCount = maxSegments;
        options.MaxDirectorySizeBytes = maxDirectoryBytes;
        options.MaxAgeDays = maxAgeDays;
        return new SafeFileLoggerProvider(options, utcNow ?? (() => DateTimeOffset.UtcNow));
    }

    private SafeFileLoggerOptions Options() => new() { LogDirectory = _root };
    private FileInfo[] ManagedFiles() => Directory.Exists(_root)
        ? new DirectoryInfo(_root).GetFiles("pos-enterprise-*.log").OrderBy(f => f.Name).ToArray()
        : [];
    private string ManagedPath(string date, int sequence) =>
        Path.Combine(_root, $"pos-enterprise-{date}-{sequence:D4}.log");
    private static string SolutionRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }
}
