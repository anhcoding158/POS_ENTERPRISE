using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderReturnPersistenceTests
{
    [Fact]
    public async Task Client_request_id_unique_index_must_reject_duplicate()
    {
        await using var connection = await CreateSchemaAsync();
        var requestId = Guid.NewGuid().ToString();
        await InsertReturnAsync(connection, requestId, new string('A', 64), 1);

        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertReturnAsync(connection, requestId, new string('B', 64), 1));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 2, 1)]
    [InlineData(2, -1, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(2, 1, 0)]
    public async Task Order_return_item_constraints_must_reject_invalid_quantities(
        int returnQuantity,
        int restockQuantity,
        long refundAmount)
    {
        await using var connection = await CreateSchemaAsync();
        await InsertReturnAsync(connection, Guid.NewGuid().ToString(), new string('A', 64), 1);

        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            """
            INSERT INTO OrderReturnItems
                (OrderReturnId, OrderItemId, ProductId, ProductCode, ProductName,
                 UnitName, ReturnQuantity, RestockQuantity, RefundAmount)
            VALUES (1, 1, 1, 'P1', 'Snapshot', 'Cai', $return, $restock, $refund);
            """,
            ("$return", returnQuantity),
            ("$restock", restockQuantity),
            ("$refund", refundAmount)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task Return_balance_constraints_must_reject_negative_values(
        int returnedQuantity,
        long refundedAmount)
    {
        await using var connection = await CreateSchemaAsync();
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            """
            INSERT INTO OrderReturnBalances
                (OrderItemId, ReturnedQuantity, RefundedAmount, ConcurrencyToken)
            VALUES (1, $quantity, $amount, $token);
            """,
            ("$quantity", returnedQuantity),
            ("$amount", refundedAmount),
            ("$token", Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task Order_return_total_constraint_must_reject_invalid_total()
    {
        await using var connection = await CreateSchemaAsync();
        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertReturnAsync(connection, Guid.NewGuid().ToString(), new string('A', 64), 0));
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task Request_fingerprint_database_shape_must_reject_invalid_length_or_hex(
        string fingerprint)
    {
        await using var connection = await CreateSchemaAsync();
        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertReturnAsync(connection, Guid.NewGuid().ToString(), fingerprint, 1));
    }

    [Fact]
    public async Task Multiple_valid_returns_for_same_order_must_be_allowed()
    {
        await using var connection = await CreateSchemaAsync();
        await InsertReturnAsync(connection, Guid.NewGuid().ToString(), new string('A', 64), 1);
        await InsertReturnAsync(connection, Guid.NewGuid().ToString(), new string('B', 64), 1);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OrderReturns;";
        Assert.Equal(2L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public void OrderReturnPersistence_model_must_have_required_tables_and_constraints()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var model = context.Model;
        Assert.Equal("OrderReturns", model.FindEntityType("POS.Domain.Entities.OrderReturn")!.GetTableName());
        Assert.Equal("OrderReturnItems", model.FindEntityType("POS.Domain.Entities.OrderReturnItem")!.GetTableName());
        Assert.Equal("OrderReturnBalances", model.FindEntityType("POS.Domain.Entities.OrderReturnBalance")!.GetTableName());

        var token = model.FindEntityType("POS.Domain.Entities.OrderReturnBalance")!
            .FindProperty("ConcurrencyToken");
        Assert.True(token!.IsConcurrencyToken);
    }

    [Fact]
    public void OrderReturnPersistence_client_request_id_must_be_unique()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var entity = context.Model.FindEntityType("POS.Domain.Entities.OrderReturn")!;
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Single().Name == "ClientRequestId");
    }

    [Fact]
    public void OrderReturnPersistence_foreign_keys_must_restrict_original_data()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var returnEntity = context.Model.FindEntityType("POS.Domain.Entities.OrderReturn")!;
        Assert.All(
            returnEntity.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        var itemEntity = context.Model.FindEntityType("POS.Domain.Entities.OrderReturnItem")!;
        Assert.All(
            itemEntity.GetForeignKeys().Where(key => key.PrincipalEntityType.ClrType.Name != "OrderReturn"),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    private static PosDbContext CreateContext(SqliteConnection connection) =>
        new(
            new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite(connection)
                .Options);

    private static async Task<SqliteConnection> CreateSchemaAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        return connection;
    }

    private static Task<int> InsertReturnAsync(
        SqliteConnection connection,
        string requestId,
        string fingerprint,
        long total) =>
        ExecuteAsync(
            connection,
            """
            INSERT INTO OrderReturns
                (ClientRequestId, RequestFingerprint, OrderId, ProcessedByUserId,
                 CreatedAtUtc, Reason, RefundMethod, TotalRefundAmount)
            VALUES ($request, $fingerprint, 1, 1, 0, 'Reason', 1, $total);
            """,
            ("$request", requestId),
            ("$fingerprint", fingerprint),
            ("$total", total));

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteNonQueryAsync();
    }
}
