using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using POS.Application.Common;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SqliteBusyLockedUxTests
{
    private readonly SqliteFailureClassifier _classifier = new();

    [Theory]
    [InlineData(5, 5, DatabaseFailureKind.Busy)]
    [InlineData(5, 517, DatabaseFailureKind.Busy)]
    [InlineData(6, 262, DatabaseFailureKind.Locked)]
    [InlineData(13, 13, DatabaseFailureKind.DiskFull)]
    [InlineData(11, 11, DatabaseFailureKind.Corruption)]
    [InlineData(26, 26, DatabaseFailureKind.Corruption)]
    [InlineData(10, 10, DatabaseFailureKind.Unknown)]
    public void Classifier_uses_numeric_base_code(
        int code, int extendedCode, DatabaseFailureKind expected)
    {
        var exception = new SqliteException("sensitive provider detail", code, extendedCode);
        Assert.Equal(expected, _classifier.Classify(exception));
    }

    [Fact]
    public void Classifier_unwraps_DbUpdateException_and_multiple_wrappers()
    {
        var sqlite = new SqliteException("detail", 5, 517);
        var wrapped = new InvalidOperationException("wrapper", new DbUpdateException("ef", sqlite));
        Assert.Equal(DatabaseFailureKind.Busy, _classifier.Classify(wrapped));
    }

    [Fact]
    public void Non_sqlite_and_cancellation_are_not_misclassified()
    {
        Assert.Null(_classifier.Classify(new InvalidOperationException()));
        Assert.Null(_classifier.Classify(new OperationCanceledException()));
    }

    [Fact]
    public async Task Safe_busy_operation_retries_bounded_and_stops_after_success()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var retry = NewRetry(delays);

        var result = await retry.ExecuteAsync<int>(_ =>
        {
            attempts++;
            return attempts < 3
                ? Task.FromException<int>(Busy())
                : Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
        Assert.Equal(2, delays.Count);
        Assert.All(delays, value => Assert.Equal(SqliteSafeOperationRetry.Delay, value));
    }

    [Fact]
    public async Task Persistent_busy_exhausts_exact_maximum_and_preserves_cause()
    {
        var attempts = 0;
        var retry = NewRetry([]);
        var error = await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            retry.ExecuteAsync<int>(_ =>
            {
                attempts++;
                return Task.FromException<int>(Busy());
            }));

        Assert.Equal(SqliteSafeOperationRetry.MaximumAttempts, attempts);
        Assert.Equal(DatabaseFailureKind.Busy, error.Kind);
        Assert.IsType<SqliteException>(error.InnerException);
    }

    [Theory]
    [InlineData(6, DatabaseFailureKind.Locked)]
    [InlineData(13, DatabaseFailureKind.DiskFull)]
    [InlineData(11, DatabaseFailureKind.Corruption)]
    [InlineData(10, DatabaseFailureKind.Unknown)]
    public async Task Non_busy_failures_are_never_retried(int code, DatabaseFailureKind kind)
    {
        var attempts = 0;
        var retry = NewRetry([]);
        var error = await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            retry.ExecuteAsync<int>(_ =>
            {
                attempts++;
                return Task.FromException<int>(new SqliteException("detail", code));
            }));

        Assert.Equal(1, attempts);
        Assert.Equal(kind, error.Kind);
    }

    [Fact]
    public async Task Cancellation_stops_immediately_without_translation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var retry = NewRetry([]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            retry.ExecuteAsync(_ => Task.FromResult(1), source.Token));
    }

    [Theory]
    [InlineData(DatabaseFailureKind.Busy, true)]
    [InlineData(DatabaseFailureKind.Locked, true)]
    [InlineData(DatabaseFailureKind.DiskFull, false)]
    [InlineData(DatabaseFailureKind.Corruption, false)]
    [InlineData(DatabaseFailureKind.Unknown, false)]
    public void Presentation_is_actionable_and_sanitized(DatabaseFailureKind kind, bool canRetry)
    {
        var value = DatabaseFailurePresenter.Present(kind);
        Assert.Equal(canRetry, value.CanRetry);
        Assert.DoesNotContain("SQLite", value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".db", value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Real_sqlite_write_contention_retries_then_succeeds_without_partial_row()
    {
        var directory = Path.Combine(Path.GetTempPath(), "POS-R22-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "contention.db");
        var cs = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 1 }.ToString();

        SqliteConnection? owner = null;
        SqliteConnection? contender = null;
        try
        {
            owner = new SqliteConnection(cs);
            contender = new SqliteConnection(cs);
            await owner.OpenAsync();
            await contender.OpenAsync();
            await ExecuteAsync(contender, "PRAGMA busy_timeout=1;");
            await ExecuteAsync(owner, "CREATE TABLE Items(Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);");
            await using var held = await owner.BeginTransactionAsync();
            await ExecuteAsync(owner, "INSERT INTO Items(Value) VALUES ('owner');", held);

            var delays = 0;
            var retry = new SqliteSafeOperationRetry(_classifier, async (_, token) =>
            {
                delays++;
                if (delays == 1)
                {
                    await held.CommitAsync(token);
                }
            });

            await retry.ExecuteAsync(async token =>
            {
                await ExecuteAsync(contender, "INSERT INTO Items(Value) VALUES ('contender');", null, token);
                return true;
            });

            await using var count = contender.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM Items;";
            Assert.Equal(2L, (long)(await count.ExecuteScalarAsync())!);
            Assert.Equal(1, delays);
        }
        finally
        {
            if (contender is not null) await contender.DisposeAsync();
            if (owner is not null) await owner.DisposeAsync();
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Persistent_lock_checkout_is_atomic_and_retry_has_no_duplicates()
    {
        var directory = NewTemporaryDirectory();
        var path = Path.Combine(directory, "acceptance.db");
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            DefaultTimeout = 1,
            Pooling = false
        }.ToString();

        try
        {
            await using var setup = new SqliteConnection(cs);
            await setup.OpenAsync();
            await ExecuteAsync(setup, "CREATE TABLE Orders(Id INTEGER PRIMARY KEY);");
            await ExecuteAsync(setup, "CREATE TABLE OrderItems(Id INTEGER PRIMARY KEY, OrderId INTEGER NOT NULL UNIQUE);");
            await ExecuteAsync(setup, "CREATE TABLE InventoryMovements(Id INTEGER PRIMARY KEY, OrderId INTEGER NOT NULL UNIQUE);");
            await ExecuteAsync(setup, "CREATE TABLE ReceiptSnapshots(Id INTEGER PRIMARY KEY, OrderId INTEGER NOT NULL UNIQUE);");

            await using var owner = new SqliteConnection(cs);
            await using var contender = new SqliteConnection(cs);
            await owner.OpenAsync();
            await contender.OpenAsync();
            await ExecuteAsync(owner, "PRAGMA busy_timeout=1;");
            await ExecuteAsync(contender, "PRAGMA busy_timeout=1;");
            await ExecuteAsync(owner, "BEGIN IMMEDIATE;");

            var failure = await Assert.ThrowsAsync<SqliteException>(() =>
                PersistCheckoutAsync(contender, 1));
            Assert.Equal(DatabaseFailureKind.Busy, _classifier.Classify(failure));
            var afterFailure = await CountsAsync(setup);
            Assert.Equal(new long[] { 0, 0, 0, 0 }, afterFailure);

            await ExecuteAsync(owner, "ROLLBACK;");
            await PersistCheckoutAsync(contender, 1);
            var afterRetry = await CountsAsync(setup);
            Assert.Equal(new long[] { 1, 1, 1, 1 }, afterRetry);

            var duplicate = await Assert.ThrowsAsync<SqliteException>(() =>
                PersistCheckoutAsync(contender, 1));
            Assert.Equal(19, duplicate.SqliteErrorCode);
            var afterDuplicate = await CountsAsync(setup);
            Assert.Equal(new long[] { 1, 1, 1, 1 }, afterDuplicate);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Simulated_sqlite_full_is_safely_classified_and_presented()
    {
        var providerFailure = new SqliteException(
            "database or disk is full at C:\\sensitive\\store.db", 13, 13);
        var translated = _classifier.Translate(providerFailure);
        var presentation = DatabaseFailurePresenter.Present(translated.Kind);

        Assert.Equal(DatabaseFailureKind.DiskFull, translated.Kind);
        Assert.False(presentation.CanRetry);
        Assert.DoesNotContain("sensitive", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("store.db", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(providerFailure.Message, presentation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Not_a_database_blocks_startup_without_changing_or_recreating_file()
    {
        var directory = NewTemporaryDirectory();
        var path = Path.Combine(directory, "not-a-database.db");
        await File.WriteAllBytesAsync(path, "R2.2 deterministic invalid SQLite file"u8.ToArray());
        var beforeHash = await HashAsync(path);
        var beforeLength = new FileInfo(path).Length;

        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Infrastructure:DatabasePath"] = path,
                    ["Infrastructure:ApplyMigrationsOnStartup"] = bool.TrueString,
                    ["Infrastructure:SeedDemoProductCatalog"] = bool.FalseString,
                    ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(configuration);
            await using (var provider = services.BuildServiceProvider())
            await using (var scope = provider.CreateAsyncScope())
            {
                var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                    scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync());
                Assert.Equal(DatabaseFailureKind.Corruption, _classifier.Classify(exception));
            }
            SqliteConnection.ClearAllPools();
            Assert.Equal(beforeHash, await HashAsync(path));
            Assert.Equal(beforeLength, new FileInfo(path).Length);
            Assert.DoesNotContain(Directory.GetFiles(directory), file => file != path);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Startup_must_finish_database_initialization_before_opening_any_session_window()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "POS.Wpf", "App.xaml.cs"));
        var initialize = source.IndexOf("await InitializeDatabaseAsync(", StringComparison.Ordinal);
        var session = source.IndexOf("await RunSessionLoopAsync(", StringComparison.Ordinal);

        Assert.True(initialize >= 0);
        Assert.True(session > initialize);
        var startupPrefix = source[..session];
        Assert.DoesNotContain("new ShellWindow", startupPrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("new SalesWindow", startupPrefix, StringComparison.Ordinal);
    }

    private SqliteSafeOperationRetry NewRetry(List<TimeSpan> delays) =>
        new(_classifier, (delay, _) => { delays.Add(delay); return Task.CompletedTask; });

    private static SqliteException Busy() => new("busy", 5, 5);

    private static string NewTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "POS-R22-Acceptance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task PersistCheckoutAsync(SqliteConnection connection, long orderId)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var table in new[] { "Orders", "OrderItems", "InventoryMovements", "ReceiptSnapshots" })
            {
                await ExecuteAsync(connection, $"INSERT INTO {table}(Id{(table == "Orders" ? "" : ", OrderId")}) VALUES ({orderId}{(table == "Orders" ? "" : $", {orderId}")});", transaction);
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<long[]> CountsAsync(SqliteConnection connection)
    {
        var counts = new List<long>();
        foreach (var table in new[] { "Orders", "OrderItems", "InventoryMovements", "ReceiptSnapshots" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            counts.Add((long)(await command.ExecuteScalarAsync())!);
        }
        return counts.ToArray();
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        System.Data.Common.DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = (SqliteTransaction?)transaction;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
