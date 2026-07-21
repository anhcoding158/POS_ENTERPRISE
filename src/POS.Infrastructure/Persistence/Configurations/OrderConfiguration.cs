using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping Order aggregate root sang bảng Orders.
/// </summary>
public sealed class OrderConfiguration :
    IEntityTypeConfiguration<Order>
{
    private static readonly
        ValueConverter<
            DateTimeOffset?,
            long?>
        NullableDateTimeOffsetConverter =
            new(
                value =>
                    value.HasValue
                        ? value.Value
                            .ToUniversalTime()
                            .ToUnixTimeMilliseconds()
                        : null,

                value =>
                    value.HasValue
                        ? DateTimeOffset
                            .FromUnixTimeMilliseconds(
                                value.Value)
                        : null);

    public void Configure(
        EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(
            "Orders",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Orders_CashierUserId_Positive",
                    "\"CashierUserId\" > 0");

                table.HasCheckConstraint(
                    "CK_Orders_CustomerId_Positive",
                    "\"CustomerId\" IS NULL OR " +
                    "\"CustomerId\" > 0");

                table.HasCheckConstraint(
                    "CK_Orders_RestaurantTableId_Positive",
                    "\"RestaurantTableId\" IS NULL OR " +
                    "\"RestaurantTableId\" > 0");

                table.HasCheckConstraint(
                    "CK_Orders_DiscountId_Positive",
                    "\"DiscountId\" IS NULL OR " +
                    "\"DiscountId\" > 0");

                table.HasCheckConstraint(
                    "CK_Orders_Status_Valid",
                    $"\"Status\" IN (" +
                    $"{(int)OrderStatus.Draft}, " +
                    $"{(int)OrderStatus.PendingPayment}, " +
                    $"{(int)OrderStatus.Paid}, " +
                    $"{(int)OrderStatus.Completed}, " +
                    $"{(int)OrderStatus.Cancelled}, " +
                    $"{(int)OrderStatus.PartiallyRefunded}, " +
                    $"{(int)OrderStatus.Refunded})");

                table.HasCheckConstraint(
                    "CK_Orders_PaymentMethod_Valid",
                    "\"PaymentMethod\" IS NULL OR " +
                    $"\"PaymentMethod\" IN (" +
                    $"{(int)PaymentMethod.Cash}, " +
                    $"{(int)PaymentMethod.VietQr}, " +
                    $"{(int)PaymentMethod.BankTransfer}, " +
                    $"{(int)PaymentMethod.Card})");

                table.HasCheckConstraint(
                    "CK_Orders_Subtotal_Range",
                    $"\"Subtotal\" >= 0 AND " +
                    $"\"Subtotal\" <= " +
                    $"{BusinessRules.Orders.MaximumOrderAmount}");

                table.HasCheckConstraint(
                    "CK_Orders_DiscountAmount_Range",
                    "\"DiscountAmount\" >= 0 AND " +
                    "\"DiscountAmount\" <= \"Subtotal\"");

                table.HasCheckConstraint(
                    "CK_Orders_TotalAmount_Equation",
                    "\"TotalAmount\" = " +
                    "\"Subtotal\" - \"DiscountAmount\"");

                table.HasCheckConstraint(
                    "CK_Orders_TotalAmount_Range",
                    $"\"TotalAmount\" >= 0 AND " +
                    $"\"TotalAmount\" <= " +
                    $"{BusinessRules.Orders.MaximumOrderAmount}");

                table.HasCheckConstraint(
                    "CK_Orders_CashReceived_NonNegative",
                    "\"CashReceived\" >= 0");

                table.HasCheckConstraint(
                    "CK_Orders_ChangeAmount_NonNegative",
                    "\"ChangeAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_Orders_ChangeNotAboveCash",
                    "\"ChangeAmount\" <= \"CashReceived\"");

                table.HasCheckConstraint(
                    "CK_Orders_RefundedAmount_Range",
                    "\"RefundedAmount\" >= 0 AND " +
                    "\"RefundedAmount\" <= \"TotalAmount\"");

                table.HasCheckConstraint(
                    "CK_Orders_CashPayment_Rule",
                    "\"PaymentMethod\" IS NULL OR " +
                    $"\"PaymentMethod\" <> " +
                    $"{(int)PaymentMethod.Cash} OR " +
                    "\"CashReceived\" >= \"TotalAmount\"");

                table.HasCheckConstraint(
                    "CK_Orders_NonCashPayment_Rule",
                    "\"PaymentMethod\" IS NULL OR " +
                    $"\"PaymentMethod\" = " +
                    $"{(int)PaymentMethod.Cash} OR " +
                    "(\"CashReceived\" = 0 AND " +
                    "\"ChangeAmount\" = 0)");

                table.HasCheckConstraint(
                    "CK_Orders_PaidState_Payment",
                    $"(\"Status\" IN (" +
                    $"{(int)OrderStatus.Draft}, " +
                    $"{(int)OrderStatus.PendingPayment}, " +
                    $"{(int)OrderStatus.Cancelled}) " +
                    "AND \"PaymentMethod\" IS NULL) OR " +
                    $"(\"Status\" IN (" +
                    $"{(int)OrderStatus.Paid}, " +
                    $"{(int)OrderStatus.Completed}, " +
                    $"{(int)OrderStatus.PartiallyRefunded}, " +
                    $"{(int)OrderStatus.Refunded}) " +
                    "AND \"PaymentMethod\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "CK_Orders_PaidAtUtc_State",
                    $"(\"Status\" IN (" +
                    $"{(int)OrderStatus.Draft}, " +
                    $"{(int)OrderStatus.PendingPayment}, " +
                    $"{(int)OrderStatus.Cancelled}) " +
                    "AND \"PaidAtUtc\" IS NULL) OR " +
                    $"(\"Status\" IN (" +
                    $"{(int)OrderStatus.Paid}, " +
                    $"{(int)OrderStatus.Completed}, " +
                    $"{(int)OrderStatus.PartiallyRefunded}, " +
                    $"{(int)OrderStatus.Refunded}) " +
                    "AND \"PaidAtUtc\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "CK_Orders_CancelledAtUtc_State",
                    $"(\"Status\" = " +
                    $"{(int)OrderStatus.Cancelled} " +
                    "AND \"CancelledAtUtc\" IS NOT NULL) OR " +
                    $"(\"Status\" <> " +
                    $"{(int)OrderStatus.Cancelled} " +
                    "AND \"CancelledAtUtc\" IS NULL)");
            });

        builder.ConfigureAuditableEntity();

        builder.Property(
                order =>
                    order.OrderCode)
            .HasMaxLength(
                BusinessRules.Orders
                    .CodeMaxLength)
            .UseCollation(
                "NOCASE")
            .IsRequired();

        builder.Property(
                order =>
                    order.CashierUserId)
            .IsRequired();

        builder.Property(
                order =>
                    order.CustomerId);

        builder.Property(
                order =>
                    order.RestaurantTableId);

        builder.Property(
                order =>
                    order.DiscountId);

        builder.Property(
                order =>
                    order.DiscountCode)
            .HasMaxLength(
                BusinessRules.Discounts
                    .CodeMaxLength)
            .UseCollation(
                "NOCASE");

        builder.Property(
                order =>
                    order.Notes)
            .HasMaxLength(
                BusinessRules.Orders
                    .NotesMaxLength);

        builder.Property(
                order =>
                    order.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                order =>
                    order.Subtotal)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.DiscountAmount)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.TotalAmount)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.PaymentMethod)
            .HasConversion<int>()
            .HasColumnType(
                "INTEGER");

        builder.Property(
                order =>
                    order.CashReceived)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.ChangeAmount)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.RefundedAmount)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                order =>
                    order.PaidAtUtc)
            .HasConversion(
                NullableDateTimeOffsetConverter)
            .HasColumnType(
                "INTEGER");

        builder.Property(
                order =>
                    order.CompletedAtUtc)
            .HasConversion(
                NullableDateTimeOffsetConverter)
            .HasColumnType(
                "INTEGER");

        builder.Property(
                order =>
                    order.CancelledAtUtc)
            .HasConversion(
                NullableDateTimeOffsetConverter)
            .HasColumnType(
                "INTEGER");

        builder.Property(
                order =>
                    order.CancelReason)
            .HasMaxLength(
                BusinessRules.Orders
                    .CancelReasonMaxLength);

        /*
         * Các giá trị được tính từ dữ liệu đã lưu,
         * không phải cột vật lý.
         */
        builder.Ignore(
            order =>
                order.ActiveItemCount);

        builder.Ignore(
            order =>
                order.TotalCostAmount);

        builder.Ignore(
            order =>
                order.GrossProfit);

        builder.Ignore(
            order =>
                order.RemainingRefundableAmount);

        /*
         * Customer, Table và Discount chưa được đưa vào
         * persistence model trong 8A.
         *
         * Các scalar ID vẫn được lưu để contract Order ổn định.
         */
        builder.Ignore(
            order =>
                order.Customer);

        builder.Ignore(
            order =>
                order.RestaurantTable);

        builder.Ignore(
            order =>
                order.Discount);

        builder.HasOne(
                order =>
                    order.CashierUser)
            .WithMany()
            .HasForeignKey(
                order =>
                    order.CashierUserId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasMany(
                order =>
                    order.Items)
            .WithOne(
                item =>
                    item.Order)
            .HasForeignKey(
                item =>
                    item.OrderId)
            .OnDelete(
                DeleteBehavior.Cascade)
            .IsRequired();

        builder.Navigation(
                order =>
                    order.Items)
            .HasField(
                "_items")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);

        builder.HasIndex(
                order =>
                    order.OrderCode)
            .IsUnique()
            .HasDatabaseName(
                "UX_Orders_OrderCode");

        builder.HasIndex(
                order =>
                    new
                    {
                        order.Status,
                        order.CreatedAtUtc
                    })
            .HasDatabaseName(
                "IX_Orders_Status_CreatedAtUtc");

        builder.HasIndex(
                order =>
                    new
                    {
                        order.CashierUserId,
                        order.CreatedAtUtc
                    })
            .HasDatabaseName(
                "IX_Orders_Cashier_CreatedAtUtc");

        builder.HasIndex(
                order =>
                    new
                    {
                        order.CustomerId,
                        order.CreatedAtUtc
                    })
            .HasDatabaseName(
                "IX_Orders_Customer_CreatedAtUtc");

        builder.HasIndex(
                order =>
                    new
                    {
                        order.RestaurantTableId,
                        order.Status
                    })
            .HasDatabaseName(
                "IX_Orders_Table_Status");
    }
}