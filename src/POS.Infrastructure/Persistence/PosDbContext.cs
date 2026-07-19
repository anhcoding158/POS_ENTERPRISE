using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của POS Enterprise.
///
/// Giai đoạn Product vertical slice hiện đăng ký:
/// - Category;
/// - Product.
///
/// Các aggregate còn lại sẽ được bổ sung theo từng chặng
/// để migration luôn được kiểm soát.
/// </summary>
public sealed class PosDbContext : DbContext
{
    public PosDbContext(
        DbContextOptions<PosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories =>
        Set<Category>();

    public DbSet<Product> Products =>
        Set<Product>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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