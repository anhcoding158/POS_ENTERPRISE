using System.Reflection;
using POS.Application.DTOs.Inventory;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Khóa các contract tồn kho quan trọng để những lần
/// refactor sau không làm thay đổi dữ liệu lịch sử.
/// </summary>
public sealed class InventoryContractTests
{
    [Theory]
    [InlineData(
        InventoryMovementType.Unknown,
        0)]
    [InlineData(
        InventoryMovementType.StockIn,
        1)]
    [InlineData(
        InventoryMovementType.StockOut,
        2)]
    [InlineData(
        InventoryMovementType.Adjustment,
        3)]
    [InlineData(
        InventoryMovementType.Stocktake,
        4)]
    [InlineData(
        InventoryMovementType.Sale,
        5)]
    [InlineData(
        InventoryMovementType.Refund,
        6)]
    [InlineData(
        InventoryMovementType.OpeningBalance,
        7)]
    public void Movement_type_numeric_values_must_remain_stable(
        InventoryMovementType movementType,
        int expectedValue)
    {
        Assert.Equal(
            expectedValue,
            (int)movementType);
    }

    [Theory]
    [InlineData(
        "ProductId",
        typeof(int))]
    [InlineData(
        "MovementType",
        typeof(InventoryMovementType))]
    [InlineData(
        "QuantityDelta",
        typeof(int))]
    [InlineData(
        "QuantityBefore",
        typeof(int))]
    [InlineData(
        "QuantityAfter",
        typeof(int))]
    [InlineData(
        "Reason",
        typeof(string))]
    [InlineData(
        "ReferenceType",
        typeof(string))]
    [InlineData(
        "ReferenceId",
        typeof(string))]
    [InlineData(
        "PerformedByUserId",
        typeof(int?))]
    [InlineData(
        "OccurredAtUtc",
        typeof(DateTimeOffset))]
    public void Movement_property_contract_must_remain_stable(
        string propertyName,
        Type expectedType)
    {
        var property =
            typeof(InventoryMovement)
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

        Assert.NotNull(property);

        Assert.Equal(
            expectedType,
            property.PropertyType);
    }

    [Theory]
    [InlineData("ProductId")]
    [InlineData("MovementType")]
    [InlineData("QuantityDelta")]
    [InlineData("QuantityBefore")]
    [InlineData("QuantityAfter")]
    [InlineData("Reason")]
    [InlineData("ReferenceType")]
    [InlineData("ReferenceId")]
    [InlineData("PerformedByUserId")]
    [InlineData("OccurredAtUtc")]
    public void Movement_state_must_not_have_public_setters(
        string propertyName)
    {
        var property =
            typeof(InventoryMovement)
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

        Assert.NotNull(property);

        var setter =
            property.SetMethod;

        Assert.True(
            setter is null ||
            !setter.IsPublic,
            $"InventoryMovement.{propertyName} " +
            "không được có public setter.");
    }

    [Fact]
    public void Movement_must_be_entity_but_not_auditable_entity()
    {
        Assert.True(
            typeof(Entity)
                .IsAssignableFrom(
                    typeof(InventoryMovement)));

        Assert.False(
            typeof(AuditableEntity)
                .IsAssignableFrom(
                    typeof(InventoryMovement)));
    }

    [Theory]
    [InlineData(InventoryMovementType.Sale)]
    [InlineData(InventoryMovementType.Refund)]
    [InlineData(InventoryMovementType.OpeningBalance)]
    [InlineData(InventoryMovementType.Unknown)]
    public void Manual_request_must_reject_system_movements(
        InventoryMovementType movementType)
    {
        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    new InventoryAdjustmentRequest(
                        productId: 1,
                        movementType,
                        quantity: 1,
                        reason: "Kiểm thử"));
    }
}