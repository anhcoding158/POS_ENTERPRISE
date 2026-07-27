using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingApplicationTests
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
    public async Task
        Archive_must_record_current_authenticated_user()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed.UserId);

        var result =
            await service.ArchiveAsync(
                seed.ProductId);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.True(product.IsArchived);
        Assert.False(product.IsActive);

        Assert.Equal(
            seed.UserId,
            product.ArchivedByUserId);

        Assert.Equal(
            ArchivedAtUtc,
            product.ArchivedAtUtc);
    }

    [Fact]
    public async Task
        Archive_must_reject_when_current_user_is_missing()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                userId: null);

        var result =
            await service.ArchiveAsync(
                seed.ProductId);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.Authentication
                .CurrentUserNotFound,
            result.Error.Code);

        Assert.False(
            context.ChangeTracker
                .HasChanges());

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.False(product.IsArchived);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task
        Archive_must_reject_invalid_product_id()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                userId: 1);

        var result =
            await service.ArchiveAsync(
                productId: 0);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);

        Assert.Empty(
            context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task
        Archive_must_return_not_found_for_missing_product()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed.UserId);

        var result =
            await service.ArchiveAsync(
                productId: int.MaxValue);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.Products.NotFound,
            result.Error.Code);
    }

    [Fact]
    public async Task
        Archive_must_reject_product_already_archived_without_overwriting_metadata()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                archived: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed.UserId);

        var result =
            await service.ArchiveAsync(
                seed.ProductId);

        Assert.True(result.IsFailure);

        Assert.Equal(
            "PRODUCT.ALREADY_ARCHIVED",
            result.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            CreatedAtUtc.AddMinutes(30),
            product.ArchivedAtUtc);

        Assert.Equal(
            seed.UserId,
            product.ArchivedByUserId);
    }

    [Fact]
    public async Task
        Restore_must_clear_archive_metadata_and_keep_product_inactive()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                archived: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                userId: null);

        var result =
            await service.RestoreAsync(
                seed.ProductId);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.False(product.IsArchived);
        Assert.False(product.IsActive);
        Assert.Null(product.ArchivedAtUtc);
        Assert.Null(product.ArchivedByUserId);
    }

    [Fact]
    public async Task
        Restore_must_not_require_active_category()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                archived: true,
                categoryActive: false);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                userId: null);

        var result =
            await service.RestoreAsync(
                seed.ProductId);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.False(product.IsArchived);
        Assert.False(product.IsActive);
    }

    [Fact]
    public async Task
        Restore_must_reject_product_not_archived()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync();

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                userId: null);

        var result =
            await service.RestoreAsync(
                seed.ProductId);

        Assert.True(result.IsFailure);

        Assert.Equal(
            "PRODUCT.NOT_ARCHIVED",
            result.Error.Code);
    }

    [Fact]
    public async Task
        Set_active_must_reject_archived_product_and_preserve_metadata()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                archived: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed.UserId);

        var result =
            await service.SetActiveStateAsync(
                seed.ProductId,
                isActive: true);

        Assert.True(result.IsFailure);

        Assert.Equal(
            "PRODUCT.ARCHIVED_CANNOT_ACTIVATE",
            result.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.True(product.IsArchived);
        Assert.False(product.IsActive);

        Assert.Equal(
            CreatedAtUtc.AddMinutes(30),
            product.ArchivedAtUtc);

        Assert.Equal(
            seed.UserId,
            product.ArchivedByUserId);
    }

    [Fact]
    public async Task
        Archive_must_keep_stock_image_prices_and_inventory_history_unchanged()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                includeMovement: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed.UserId);

        var result =
            await service.ArchiveAsync(
                seed.ProductId);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        var movement =
            await verifyContext
                .InventoryMovements
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(12, product.StockQuantity);
        Assert.Equal("images/product.png", product.ImagePath);
        Assert.Equal(10_000, product.CostPrice);
        Assert.Equal(15_000, product.SalePrice);
        Assert.Equal(12, movement.QuantityAfter);
        Assert.Equal("Tồn đầu kỳ", movement.Reason);
    }

    [Fact]
    public async Task
        Archive_concurrency_conflict_must_be_mapped_without_retry()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync();

        await using var context =
            database.CreateContext();

        var unitOfWork =
            new ConcurrentUpdateUnitOfWork(
                new EfUnitOfWork(
                    context),
                async cancellationToken =>
                {
                    await using var concurrentContext =
                        database.CreateContext();

                    var product =
                        await concurrentContext.Products
                            .SingleAsync(
                                cancellationToken);

                    product.ChangePrices(
                        costPrice: 11_000,
                        salePrice: 16_000,
                        ArchivedAtUtc);

                    await concurrentContext
                        .SaveChangesAsync(
                            cancellationToken);
                });

        var service =
            CreateService(
                context,
                seed.UserId,
                unitOfWork);

        var result =
            await service.ArchiveAsync(
                seed.ProductId);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.Products
                .ConcurrencyConflict,
            result.Error.Code);

        Assert.Equal(
            1,
            unitOfWork.SaveCallCount);

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync();

        Assert.False(product.IsArchived);
        Assert.Equal(11_000, product.CostPrice);
        Assert.Equal(16_000, product.SalePrice);
    }

    private static ProductService CreateService(
        PosDbContext context,
        int? userId,
        IUnitOfWork? unitOfWork = null)
    {
        var currentUserService =
            new TestCurrentUserService(
                userId);

        return new ProductService(
            new ProductRepository(
                context),

            new CategoryRepository(
                context),

            new InventoryMovementRepository(
                context),

            unitOfWork ??
                new EfUnitOfWork(
                    context),

            new FixedClock(),

            currentUserService);
    }

    private sealed class ConcurrentUpdateUnitOfWork :
        IUnitOfWork
    {
        private readonly IUnitOfWork
            _inner;

        private readonly Func<
            CancellationToken,
            Task>
            _beforeSave;

        public ConcurrentUpdateUnitOfWork(
            IUnitOfWork inner,
            Func<CancellationToken, Task> beforeSave)
        {
            _inner =
                inner ??
                throw new ArgumentNullException(
                    nameof(inner));

            _beforeSave =
                beforeSave ??
                throw new ArgumentNullException(
                    nameof(beforeSave));
        }

        public int SaveCallCount { get; private set; }

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            await _beforeSave(
                cancellationToken);

            return await _inner.SaveChangesAsync(
                cancellationToken);
        }

        public Task<IApplicationTransaction>
            BeginTransactionAsync(
                CancellationToken cancellationToken = default)
        {
            return _inner.BeginTransactionAsync(
                cancellationToken);
        }
    }

    private sealed class FixedClock :
        IClock
    {
        public DateTimeOffset UtcNow =>
            ArchivedAtUtc;
    }

    private sealed class TestCurrentUserService :
        ICurrentUserService
    {
        public TestCurrentUserService(
            int? userId)
        {
            UserId = userId;
        }

        public AuthenticatedUserDto? CurrentUser =>
            null;

        public bool IsAuthenticated =>
            UserId.HasValue;

        public int? UserId { get; }

        public string? Username => null;

        public string? FullName => null;

        public Role? Role => null;

        public bool IsInRole(
            Role role)
        {
            return false;
        }

        public void SetCurrentUser(
            AuthenticatedUserDto user)
        {
            throw new InvalidOperationException(
                "Test current user is immutable.");
        }

        public void Clear()
        {
        }
    }

    private sealed record SeedResult(
        int ProductId,
        int UserId);

    private sealed class TestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private TestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<TestDatabase>
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
                new TestDatabase(
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

        public async Task<SeedResult> SeedAsync(
            bool archived = false,
            bool categoryActive = true,
            bool includeMovement = false)
        {
            await using var context =
                CreateContext();

            var category =
                new Category(
                    "Archive application",
                    displayOrder: 1,
                    CreatedAtUtc);

            if (!categoryActive)
            {
                category.Deactivate(
                    CreatedAtUtc.AddMinutes(10));
            }

            var user =
                new User(
                    "archive.application",
                    "archive-application-password-hash",
                    "Archive Application",
                    Role.Administrator,
                    CreatedAtUtc);

            context.AddRange(
                category,
                user);

            await context.SaveChangesAsync();

            var product =
                new Product(
                    category.Id,
                    "ARCHIVE-APPLICATION",
                    name: "Archive application product",
                    unitName: "Cái",
                    costPrice: 10_000,
                    salePrice: 15_000,
                    stockQuantity: 12,
                    minimumStock: 2,
                    trackInventory: true,
                    allowNegativeStock: false,
                    CreatedAtUtc,
                    barcode: "ARCHIVE-001",
                    description: "Archive test",
                    imagePath: "images/product.png");

            if (archived)
            {
                product.Archive(
                    user.Id,
                    CreatedAtUtc.AddMinutes(30));
            }

            context.Products.Add(
                product);

            await context.SaveChangesAsync();

            if (includeMovement)
            {
                context.InventoryMovements.Add(
                    new InventoryMovement(
                        product.Id,
                        InventoryMovementType
                            .OpeningBalance,
                        quantityDelta: 12,
                        quantityBefore: 0,
                        quantityAfter: 12,
                        reason: "Tồn đầu kỳ",
                        occurredAtUtc:
                            CreatedAtUtc));

                await context.SaveChangesAsync();
            }

            return new SeedResult(
                product.Id,
                user.Id);
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
                dbContext.ChangeTracker
                    .Entries<AuditableEntity>()
                    .Where(
                        entry =>
                            entry.State is
                                EntityState.Added or
                                EntityState.Modified);

            foreach (var entry in entries)
            {
                SetConcurrencyToken(
                    entry);
            }
        }

        private static void SetConcurrencyToken(
            EntityEntry<AuditableEntity> entry)
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