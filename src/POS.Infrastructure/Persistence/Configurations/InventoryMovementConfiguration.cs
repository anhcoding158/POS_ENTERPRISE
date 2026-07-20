using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping lịch sử biến động kho sang SQLite.
///
/// InventoryMovement là dữ liệu kiểm toán bất biến:
/// - chỉ thêm mới;
/// - không cập nhật;
/// - không xóa dây chuyền khi Product bị xóa.
/// </summary>
public sealed class InventoryMovementConfiguration :
    IEntityTypeConfiguration<InventoryMovement>
{
    private static readonly
        ValueConverter<DateTimeOffset, long>
        DateTimeOffsetToUnixMillisecondsConverter =
            new(
                value =>
                    value
                        .ToUniversalTime()
                        .ToUnixTimeMilliseconds(),

                value =>
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            value));

    public void Configure(
        EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable(
            "InventoryMovements",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryMovements_ProductId_Positive",
                    "\"ProductId\" > 0");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_MovementType_Range",
                    $"\"MovementType\" >= " +
                    $"{(int)InventoryMovementType.StockIn} " +
                    $"AND \"MovementType\" <= " +
                    $"{(int)InventoryMovementType.OpeningBalance}");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_QuantityBefore_Range",
                    $"\"QuantityBefore\" >= " +
                    $"-{BusinessRules.Products.MaximumStockQuantity} " +
                    $"AND \"QuantityBefore\" <= " +
                    $"{BusinessRules.Products.MaximumStockQuantity}");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_QuantityAfter_Range",
                    $"\"QuantityAfter\" >= " +
                    $"-{BusinessRules.Products.MaximumStockQuantity} " +
                    $"AND \"QuantityAfter\" <= " +
                    $"{BusinessRules.Products.MaximumStockQuantity}");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_QuantityDelta_Range",
                    $"\"QuantityDelta\" >= " +
                    $"-{BusinessRules.Inventory.MaximumQuantityDelta} " +
                    $"AND \"QuantityDelta\" <= " +
                    $"{BusinessRules.Inventory.MaximumQuantityDelta}");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_QuantityEquation",
                    "\"QuantityAfter\" = " +
                    "\"QuantityBefore\" + " +
                    "\"QuantityDelta\"");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_ZeroDelta_StocktakeOnly",
                    "\"QuantityDelta\" <> 0 OR " +
                    $"\"MovementType\" = " +
                    $"{(int)InventoryMovementType.Stocktake}");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_IncreaseDirection",
                    $"\"MovementType\" NOT IN (" +
                    $"{(int)InventoryMovementType.StockIn}, " +
                    $"{(int)InventoryMovementType.Refund}) " +
                    "OR \"QuantityDelta\" > 0");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_DecreaseDirection",
                    $"\"MovementType\" NOT IN (" +
                    $"{(int)InventoryMovementType.StockOut}, " +
                    $"{(int)InventoryMovementType.Sale}) " +
                    "OR \"QuantityDelta\" < 0");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_AdjustmentDirection",
                    $"\"MovementType\" <> " +
                    $"{(int)InventoryMovementType.Adjustment} " +
                    "OR \"QuantityDelta\" <> 0");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_OpeningBalance",
                    $"\"MovementType\" <> " +
                    $"{(int)InventoryMovementType.OpeningBalance} " +
                    "OR (" +
                    "\"QuantityBefore\" = 0 AND " +
                    "\"QuantityDelta\" <> 0" +
                    ")");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_ReferencePair",
                    "(" +
                    "\"ReferenceType\" IS NULL AND " +
                    "\"ReferenceId\" IS NULL" +
                    ") OR (" +
                    "\"ReferenceType\" IS NOT NULL AND " +
                    "\"ReferenceId\" IS NOT NULL" +
                    ")");

                table.HasCheckConstraint(
                    "CK_InventoryMovements_PerformedByUserId",
                    "\"PerformedByUserId\" IS NULL OR " +
                    "\"PerformedByUserId\" > 0");
            });

        builder.HasKey(
            movement =>
                movement.Id);

        builder.Property(
                movement =>
                    movement.Id)
            .ValueGeneratedOnAdd();

        builder.Property(
                movement =>
                    movement.ProductId)
            .IsRequired();

        builder.Property(
                movement =>
                    movement.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                movement =>
                    movement.QuantityDelta)
            .IsRequired();

        builder.Property(
                movement =>
                    movement.QuantityBefore)
            .IsRequired();

        builder.Property(
                movement =>
                    movement.QuantityAfter)
            .IsRequired();

        builder.Property(
                movement =>
                    movement.Reason)
            .HasMaxLength(
                BusinessRules.Inventory
                    .ReasonMaxLength)
            .IsRequired();

        builder.Property(
                movement =>
                    movement.ReferenceType)
            .HasMaxLength(
                BusinessRules.Inventory
                    .ReferenceTypeMaxLength)
            .UseCollation("NOCASE");

        builder.Property(
                movement =>
                    movement.ReferenceId)
            .HasMaxLength(
                BusinessRules.Inventory
                    .ReferenceIdMaxLength);

        builder.Property(
                movement =>
                    movement.PerformedByUserId);

        builder.Property(
                movement =>
                    movement.OccurredAtUtc)
            .HasConversion(
                DateTimeOffsetToUnixMillisecondsConverter)
            .HasColumnType("INTEGER")
            .IsRequired();

        builder.Ignore(
            movement =>
                movement.IsIncrease);

        builder.Ignore(
            movement =>
                movement.IsDecrease);

        /*
         * Không cascade delete lịch sử kho khi Product bị xóa.
         * Product hiện đang soft-deactivate, nhưng Restrict vẫn
         * là hàng rào an toàn ở database.
         */
        builder.HasOne(
                movement =>
                    movement.Product)
            .WithMany()
            .HasForeignKey(
                movement =>
                    movement.ProductId)
            .IsRequired()
            .OnDelete(
                DeleteBehavior.Restrict);

        /*
         * Lịch sử của một sản phẩm,
         * sắp xếp theo thời gian.
         */
        builder.HasIndex(
                movement =>
                    new
                    {
                        movement.ProductId,
                        movement.OccurredAtUtc
                    })
            .HasDatabaseName(
                "IX_InventoryMovements_Product_OccurredAt");

        /*
         * Tra cứu movement từ Order, Refund,
         * phiếu nhập hoặc chứng từ bên ngoài.
         */
        builder.HasIndex(
                movement =>
                    new
                    {
                        movement.ReferenceType,
                        movement.ReferenceId
                    })
            .HasDatabaseName(
                "IX_InventoryMovements_Reference");

        builder.HasIndex(
                movement =>
                    movement.OccurredAtUtc)
            .HasDatabaseName(
                "IX_InventoryMovements_OccurredAt");

        builder.HasIndex(
                movement =>
                    new
                    {
                        movement.MovementType,
                        movement.OccurredAtUtc
                    })
            .HasDatabaseName(
                "IX_InventoryMovements_Type_OccurredAt");
    }
}