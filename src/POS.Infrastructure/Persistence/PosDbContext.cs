using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của POS Enterprise.
///
/// Các vertical slice đã đưa vào database:
/// - User;
/// - Category;
/// - Product;
/// - InventoryMovement;
/// - Order;
/// - OrderItem;
/// - OrderItemModifier.
/// </summary>
public sealed class PosDbContext :
    DbContext
{
    public PosDbContext(
        DbContextOptions<PosDbContext> options)
        : base(
            options)
    {
    }

    public DbSet<User> Users =>
        Set<User>();

    public DbSet<Category> Categories =>
        Set<Category>();

    public DbSet<Product> Products =>
        Set<Product>();

    public DbSet<InventoryMovement>
        InventoryMovements =>
            Set<InventoryMovement>();

    public DbSet<Order> Orders =>
        Set<Order>();

    public DbSet<OrderItem> OrderItems =>
        Set<OrderItem>();

    public DbSet<OrderItemModifier>
        OrderItemModifiers =>
            Set<OrderItemModifier>();

    public DbSet<OrderReceiptSnapshot>
        OrderReceiptSnapshots =>
            Set<OrderReceiptSnapshot>();

    public DbSet<OrderReturn> OrderReturns => Set<OrderReturn>();
    public DbSet<OrderReturnItem> OrderReturnItems => Set<OrderReturnItem>();
    public DbSet<OrderReturnBalance> OrderReturnBalances => Set<OrderReturnBalance>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PosDbContext).Assembly);
    }
}
