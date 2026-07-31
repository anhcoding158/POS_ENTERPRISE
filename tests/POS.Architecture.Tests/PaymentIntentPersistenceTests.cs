using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentPersistenceTests
{
    [Fact]
    public async Task Payment_intent_schema_has_required_unique_indexes_and_foreign_keys()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var entity = context.Model.FindEntityType(typeof(PaymentIntent));
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["ClientRequestId"]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["DisplayCode"]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["CompletedOrderId"]));
        Assert.Equal(4, entity.GetForeignKeys().Count());
        Assert.True(entity.FindProperty("ConcurrencyToken")!.IsConcurrencyToken);
    }

    [Fact]
    public async Task Database_integrity_check_passes_after_payment_intent_persistence()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var result = await database.PaymentIntentService(context)
            .CreateAsync(new(Guid.NewGuid(), new(
                [new POS.Application.DTOs.Checkout.CheckoutLineRequest(database.ProductId, 1)],
                POS.Domain.Enums.PaymentMethod.VietQr,
                0,
                confirmedPaymentAmount: 1,
                clientRequestId: Guid.NewGuid())));
        Assert.True(result.IsSuccess);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", await command.ExecuteScalarAsync());
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }
}
