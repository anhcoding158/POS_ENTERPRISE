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
/// - InventoryMovement.
/// </summary>
public sealed class PosDbContext :
    DbContext
{
    public PosDbContext(
        DbContextOptions<PosDbContext> options)
        : base(options)
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

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder);

        /*
         * Tự động áp dụng mọi class triển khai:
         *
         * IEntityTypeConfiguration<TEntity>
         *
         * trong assembly POS.Infrastructure.
         */
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PosDbContext).Assembly);
    }
}