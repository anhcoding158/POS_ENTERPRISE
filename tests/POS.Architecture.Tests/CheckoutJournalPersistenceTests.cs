using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;
using POS.Domain.Enums;

namespace POS.Architecture.Tests;

public sealed class CheckoutJournalPersistenceTests
{
    private const string Fingerprint = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Client_request_id_unique_index_rejects_duplicate()
    {
        await using var database = await Database.CreateAsync();
        var id = Guid.NewGuid();
        await using var first = database.Context();
        first.CheckoutRequestJournals.Add(Create(id, database.UserId));
        await first.SaveChangesAsync();
        await using var second = database.Context();
        second.CheckoutRequestJournals.Add(Create(id, database.UserId));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Multiple_prepared_rows_with_null_order_id_are_allowed()
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        context.CheckoutRequestJournals.AddRange(
            Create(Guid.NewGuid(), database.UserId),
            Create(Guid.NewGuid(), database.UserId));
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.CheckoutRequestJournals.CountAsync());
    }

    [Fact]
    public async Task Repository_read_methods_are_no_tracking_and_recovery_is_bounded()
    {
        await using var database = await Database.CreateAsync();
        await using (var seed = database.Context())
        {
            seed.CheckoutRequestJournals.AddRange(Enumerable.Range(0, 3)
                .Select(_ => Create(Guid.NewGuid(), database.UserId)));
            await seed.SaveChangesAsync();
        }
        await using var read = database.Context();
        var repository = new CheckoutRequestJournalRepository(read);
        var records = await repository.GetActiveRecoveryAsync(database.UserId, 2);
        Assert.Equal(2, records.Count);
        Assert.Empty(read.ChangeTracker.Entries<CheckoutRequestJournal>());
    }

    [Theory]
    [InlineData("RequestFingerprint", "bad")]
    [InlineData("PreparedQuoteFingerprint", "bad")]
    [InlineData("CanonicalRequestJson", " ")]
    [InlineData("PreparedQuoteJson", " ")]
    public async Task Database_constraints_reject_invalid_fingerprint_or_json(
        string column, string value)
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        var journal = Create(Guid.NewGuid(), database.UserId);
        context.Add(journal);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<SqliteException>(() =>
            ExecuteInvalidUpdateAsync(
                context,
                $"UPDATE CheckoutRequestJournals SET {column} = $value WHERE Id = $id",
                journal.Id,
                value));
    }

    [Theory]
    [InlineData("Status = 1, OrderId = 1, CompletedAtUtc = NULL")]
    [InlineData("Status = 2, OrderId = NULL, CompletedAtUtc = NULL")]
    [InlineData("Status = 3, OrderId = NULL, CompletedAtUtc = NULL, AbandonedAtUtc = NULL, AbandonedByUserId = NULL")]
    [InlineData("Status = 1, AcknowledgedAtUtc = 1")]
    public async Task Database_rejects_invalid_state_shape(string assignment)
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        var journal = Create(Guid.NewGuid(), database.UserId);
        context.Add(journal);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<SqliteException>(() =>
            ExecuteInvalidUpdateAsync(
                context,
                $"UPDATE CheckoutRequestJournals SET {assignment} WHERE Id = $id",
                journal.Id));
    }

    [Fact]
    public async Task Journal_foreign_keys_are_restrict_and_order_id_is_unique_nullable()
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Context();
        var entity = context.Model.FindEntityType(typeof(CheckoutRequestJournal))!;
        Assert.All(entity.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        var orderIndex = Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(CheckoutRequestJournal.OrderId));
        Assert.True(orderIndex.IsUnique);
        Assert.NotNull(orderIndex.GetFilter());
    }

    [Fact]
    public async Task Journal_concurrency_token_rejects_lost_update()
    {
        await using var database = await Database.CreateAsync();
        var id = Guid.NewGuid();
        await using (var seed = database.Context())
        {
            seed.Add(Create(id, database.UserId));
            await seed.SaveChangesAsync();
        }
        await using var first = database.Context();
        await using var second = database.Context();
        var firstJournal = await first.CheckoutRequestJournals.SingleAsync();
        var secondJournal = await second.CheckoutRequestJournals.SingleAsync();
        firstJournal.Abandon(database.UserId, Now.AddMinutes(1));
        secondJournal.Abandon(database.UserId, Now.AddMinutes(2));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Recovery_query_excludes_abandoned_and_is_user_scoped()
    {
        await using var database = await Database.CreateAsync();
        await using (var seed = database.Context())
        {
            var active = Create(Guid.NewGuid(), database.UserId);
            var abandoned = Create(Guid.NewGuid(), database.UserId);
            abandoned.Abandon(database.UserId, Now.AddMinutes(1));
            seed.AddRange(active, abandoned);
            await seed.SaveChangesAsync();
        }
        await using var read = database.Context();
        var rows = await new CheckoutRequestJournalRepository(read)
            .GetActiveRecoveryAsync(database.UserId, 25);
        Assert.Single(rows);
        Assert.Equal(CheckoutRequestStatus.Prepared, rows[0].Status);
        Assert.Empty(await new CheckoutRequestJournalRepository(read)
            .GetActiveRecoveryAsync(database.OtherUserId, 25));
    }

    private static CheckoutRequestJournal Create(Guid id, int userId) =>
        new(id, Fingerprint, "{\"version\":1}", Fingerprint, "{\"version\":1}", userId, Now);

    private static async Task ExecuteInvalidUpdateAsync(
        PosDbContext context,
        string sql,
        int id,
        string? value = null)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "$id";
        idParameter.Value = id;
        command.Parameters.Add(idParameter);
        if (value is not null)
        {
            var valueParameter = command.CreateParameter();
            valueParameter.ParameterName = "$value";
            valueParameter.Value = value;
            command.Parameters.Add(valueParameter);
        }
        await command.ExecuteNonQueryAsync();
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<PosDbContext> _options;
        private Database(SqliteConnection connection, DbContextOptions<PosDbContext> options) =>
            (_connection, _options) = (connection, options);
        public int UserId { get; private set; }
        public int OtherUserId { get; private set; }
        public PosDbContext Context() => new(_options);

        public static async Task<Database> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            var database = new Database(connection, options);
            await using var context = database.Context();
            await context.Database.EnsureCreatedAsync();
            var user = new User($"journal.{Guid.NewGuid():N}", "hash", "Journal User",
                POS.Domain.Enums.Role.Cashier, Now);
            var other = new User($"other.{Guid.NewGuid():N}", "hash", "Other User",
                POS.Domain.Enums.Role.Cashier, Now);
            context.Users.AddRange(user, other);
            await context.SaveChangesAsync();
            database.UserId = user.Id;
            database.OtherUserId = other.Id;
            return database;
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
