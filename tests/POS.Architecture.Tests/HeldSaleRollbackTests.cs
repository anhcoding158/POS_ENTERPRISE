using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleRollbackTests
{
    [Fact]
    public async Task Receipt_serialization_failure_rolls_back_all_business_side_effects()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using var context = database.Context();
        var result = await database.CheckoutService(
                context,
                serializer: new HeldSaleThrowingSerializer())
            .CheckoutAsync(database.CheckoutRequest(Guid.NewGuid(), heldSaleId));

        Assert.True(result.IsFailure);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Active, 0, 20, 0, 0,
            CheckoutRequestStatus.Prepared);
    }

    [Fact]
    public async Task Receipt_repository_failure_rolls_back_all_business_side_effects()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using var context = database.Context();
        var result = await database.CheckoutService(
                context,
                snapshots: new HeldSaleThrowingSnapshotRepository())
            .CheckoutAsync(database.CheckoutRequest(Guid.NewGuid(), heldSaleId));

        Assert.True(result.IsFailure);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Active, 0, 20, 0, 0,
            CheckoutRequestStatus.Prepared);
    }
}
