using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping snapshot modifier của một OrderItem.
/// </summary>
public sealed class
    OrderItemModifierConfiguration :
        IEntityTypeConfiguration<
            OrderItemModifier>
{
    public void Configure(
        EntityTypeBuilder<
            OrderItemModifier> builder)
    {
        builder.ToTable(
            "OrderItemModifiers",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderItemModifiers_OrderItemId_Positive",
                    "\"OrderItemId\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderItemModifiers_ModifierId_Positive",
                    "\"ModifierId\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderItemModifiers_GroupId_Positive",
                    "\"ModifierGroupId\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderItemModifiers_Quantity_Range",
                    "\"Quantity\" > 0 AND " +
                    $"\"Quantity\" <= " +
                    $"{BusinessRules.Orders.MaximumLineQuantity}");

                table.HasCheckConstraint(
                    "CK_OrderItemModifiers_Price_Range",
                    "\"UnitAdditionalPrice\" >= 0 AND " +
                    $"\"UnitAdditionalPrice\" <= " +
                    $"{BusinessRules.Modifiers.MaximumAdditionalPrice}");
            });

        builder.HasKey(
            modifier =>
                modifier.Id);

        builder.Property(
                modifier =>
                    modifier.Id)
            .ValueGeneratedOnAdd();

        builder.Property(
                modifier =>
                    modifier.OrderItemId)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.ModifierId)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.ModifierGroupId)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.ModifierGroupName)
            .HasMaxLength(
                BusinessRules.ModifierGroups
                    .NameMaxLength)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.ModifierName)
            .HasMaxLength(
                BusinessRules.Modifiers
                    .NameMaxLength)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.Quantity)
            .IsRequired();

        builder.Property(
                modifier =>
                    modifier.UnitAdditionalPrice)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Ignore(
            modifier =>
                modifier.AmountPerProductUnit);

        /*
         * Domain tự gộp modifier trùng ID trong cùng dòng.
         * Unique index bảo vệ thêm tại database.
         */
        builder.HasIndex(
                modifier =>
                    new
                    {
                        modifier.OrderItemId,
                        modifier.ModifierId
                    })
            .IsUnique()
            .HasDatabaseName(
                "UX_OrderItemModifiers_OrderItem_Modifier");

        builder.HasIndex(
                modifier =>
                    new
                    {
                        modifier.OrderItemId,
                        modifier.ModifierGroupId
                    })
            .HasDatabaseName(
                "IX_OrderItemModifiers_Item_Group");
    }
}