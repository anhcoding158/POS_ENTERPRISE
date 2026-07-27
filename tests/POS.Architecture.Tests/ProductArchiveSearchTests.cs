using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.Abstractions.DateTime;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchiveSearchTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                27,
                8,
                0,
                0,
                TimeSpan.Zero);

    private static readonly DateTimeOffset
        ArchivedAtUtc =
            CreatedAtUtc.AddHours(1);

    [Fact]
    public void Product_search_request_must_default_to_non_archived()
    {
        var request =
            new ProductSearchRequest(
                searchTerm: "SP",
                categoryId: 1,
                isActive: null,
                isLowStock: null,
                pageNumber: 1,
                pageSize: 20);

        Assert.False(
            request.IsArchived);
    }

    [Fact]
    public async Task Repository_search_must_exclude_archived_products_by_default()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedProductsAsync();

        await using var context =
            database.CreateContext();

        var repository =
            new ProductRepository(
                context);

        var result =
            await repository.SearchAsync(
                searchTerm: null,
                categoryId: null,
                isActive: null,
                isLowStock: null,
                pageNumber: 1,
                pageSize: 20);

        Assert.Equal(
            2,
            result.TotalCount);

        Assert.Contains(
            result.Items,
            product =>
                product.Id ==
                seed.ActiveProductId);

        Assert.Contains(
            result.Items,
            product =>
                product.Id ==
                seed.InactiveProductId);

        Assert.DoesNotContain(
            result.Items,
            product =>
                product.Id ==
                seed.ArchivedProductId);
    }

    [Fact]
    public async Task Repository_search_must_return_only_archived_products_when_requested()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedProductsAsync();

        await using var context =
            database.CreateContext();

        var result =
            await new ProductRepository(
                    context)
                .SearchAsync(
                    searchTerm: null,
                    categoryId: null,
                    isActive: null,
                    isLowStock: null,
                    pageNumber: 1,
                    pageSize: 20,
                    isArchived: true);

        var product =
            Assert.Single(
                result.Items);

        Assert.Equal(
            seed.ArchivedProductId,
            product.Id);
    }

    [Fact]
    public async Task Repository_search_must_return_all_archive_states_when_filter_is_null()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        await database.SeedProductsAsync();

        await using var context =
            database.CreateContext();

        var result =
            await new ProductRepository(
                    context)
                .SearchAsync(
                    searchTerm: null,
                    categoryId: null,
                    isActive: null,
                    isLowStock: null,
                    pageNumber: 1,
                    pageSize: 20,
                    isArchived: null);

        Assert.Equal(
            3,
            result.TotalCount);

        Assert.Contains(
            result.Items,
            product =>
                product.IsArchived);

        Assert.Contains(
            result.Items,
            product =>
                !product.IsArchived);
    }

    [Fact]
    public async Task Archive_filter_must_apply_before_paging_and_total_count()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        await database.SeedPagingProductsAsync();

        await using var context =
            database.CreateContext();

        var result =
            await new ProductRepository(
                    context)
                .SearchAsync(
                    searchTerm: null,
                    categoryId: null,
                    isActive: null,
                    isLowStock: null,
                    pageNumber: 1,
                    pageSize: 2,
                    isArchived: true);

        Assert.Equal(
            3,
            result.TotalCount);

        Assert.Equal(
            2,
            result.Items.Count);

        Assert.All(
            result.Items,
            product =>
                Assert.True(
                    product.IsArchived));
    }

    [Fact]
    public async Task Product_list_mapping_must_include_archive_state()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        await database.SeedProductsAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context);

        var result =
            await service.SearchAsync(
                new ProductSearchRequest(
                    isArchived: true));

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        var product =
            Assert.Single(
                result.Value.Items);

        Assert.True(
            product.IsArchived);
    }

    [Fact]
    public async Task Product_details_mapping_must_include_archive_metadata()
    {
        await using var database =
            await ProductArchiveTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedProductsAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context);

        var result =
            await service.GetByIdAsync(
                seed.ArchivedProductId);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.True(
            result.Value.IsArchived);

        Assert.Equal(
            ArchivedAtUtc,
            result.Value.ArchivedAtUtc);

        Assert.Equal(
            seed.ArchivedByUserId,
            result.Value.ArchivedByUserId);
    }

    private static ProductService CreateService(
        PosDbContext context)
    {
        return new ProductService(
            new ProductRepository(
                context),

            new CategoryRepository(
                context),

            new InventoryMovementRepository(
                context),

            new EfUnitOfWork(
                context),

            new FixedClock());
    }

    private sealed class FixedClock :
        IClock
    {
        public DateTimeOffset UtcNow =>
            CreatedAtUtc;
    }

    private sealed record SeedResult(
        int ActiveProductId,
        int InactiveProductId,
        int ArchivedProductId,
        int ArchivedByUserId);

    private sealed class ProductArchiveTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private ProductArchiveTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            ProductArchiveTestDatabase>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:;" +
                    "Foreign Keys=True");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                        PosDbContext>()
                    .UseSqlite(
                        connection)
                    .AddInterceptors(
                        new TestConcurrencyTokenInterceptor())
                    .EnableDetailedErrors()
                    .Options;

            var database =
                new ProductArchiveTestDatabase(
                    connection,
                    options);

            await using var context =
                database.CreateContext();

            await context.Database
                .EnsureCreatedAsync();

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        public async Task<SeedResult>
            SeedProductsAsync()
        {
            await using var context =
                CreateContext();

            var category =
                CreateCategory();

            var user =
                CreateUser();

            context.AddRange(
                category,
                user);

            await context.SaveChangesAsync();

            var activeProduct =
                CreateProduct(
                    category.Id,
                    "ARCHIVE-ACTIVE");

            var inactiveProduct =
                CreateProduct(
                    category.Id,
                    "ARCHIVE-INACTIVE");

            inactiveProduct.Deactivate(
                CreatedAtUtc.AddMinutes(30));

            var archivedProduct =
                CreateProduct(
                    category.Id,
                    "ARCHIVE-ARCHIVED");

            archivedProduct.Archive(
                user.Id,
                ArchivedAtUtc);

            context.Products.AddRange(
                activeProduct,
                inactiveProduct,
                archivedProduct);

            await context.SaveChangesAsync();

            return new SeedResult(
                activeProduct.Id,
                inactiveProduct.Id,
                archivedProduct.Id,
                user.Id);
        }

        public async Task SeedPagingProductsAsync()
        {
            await using var context =
                CreateContext();

            var category =
                CreateCategory();

            var user =
                CreateUser();

            context.AddRange(
                category,
                user);

            await context.SaveChangesAsync();

            for (var index = 1;
                 index <= 6;
                 index++)
            {
                var product =
                    CreateProduct(
                        category.Id,
                        $"ARCHIVE-PAGE-{index}");

                if (index <= 3)
                {
                    product.Archive(
                        user.Id,
                        ArchivedAtUtc);
                }

                context.Products.Add(
                    product);
            }

            await context.SaveChangesAsync();
        }

        private static Category CreateCategory()
        {
            return new Category(
                "Archive search",
                displayOrder: 1,
                CreatedAtUtc);
        }

        private static User CreateUser()
        {
            return new User(
                "archive.search",
                "archive-search-password-hash",
                "Archive Search",
                Role.Administrator,
                CreatedAtUtc);
        }

        private static Product CreateProduct(
            int categoryId,
            string code)
        {
            return new Product(
                categoryId,
                code,
                name: code,
                unitName: "Cái",
                costPrice: 10_000,
                salePrice: 15_000,
                stockQuantity: 10,
                minimumStock: 2,
                trackInventory: true,
                allowNegativeStock: false,
                CreatedAtUtc);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }

    private sealed class
        TestConcurrencyTokenInterceptor :
            SaveChangesInterceptor
    {
        public override
            InterceptionResult<int>
            SavingChanges(
                DbContextEventData eventData,
                InterceptionResult<int> result)
        {
            ApplyConcurrencyTokens(
                eventData.Context);

            return result;
        }

        public override
            ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            ApplyConcurrencyTokens(
                eventData.Context);

            return ValueTask.FromResult(
                result);
        }

        private static void ApplyConcurrencyTokens(
            DbContext? dbContext)
        {
            if (dbContext is null)
            {
                return;
            }

            var entries =
                dbContext
                    .ChangeTracker
                    .Entries<AuditableEntity>()
                    .Where(
                        entry =>
                            entry.State is
                                EntityState.Added or
                                EntityState.Modified);

            foreach (var entry in entries)
            {
                var tokenProperty =
                    entry.Properties
                        .Single(
                            property =>
                                property.Metadata
                                    .IsConcurrencyToken &&
                                property.Metadata.ClrType ==
                                    typeof(Guid));

                tokenProperty.CurrentValue =
                    Guid.NewGuid();
            }
        }
    }
}