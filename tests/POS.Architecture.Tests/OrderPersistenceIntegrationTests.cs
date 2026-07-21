using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Integration test cho nền tảng persistence của Order.
///
/// Sử dụng SQLite thật trong bộ nhớ để kiểm tra:
/// - mapping Order aggregate;
/// - cascade Order → Item → Modifier;
/// - snapshot giá và tên;
/// - quan hệ thu ngân;
/// - repository search;
/// - unique order code.
/// </summary>
public sealed class
    OrderPersistenceIntegrationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                21,
                8,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Save_and_reload_must_preserve_complete_order_aggregate()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database
                .SeedAsync();

        int orderId;

        await using (
            var context =
                database.CreateContext())
        {
            var repository =
                new OrderRepository(
                    context);

            var order =
                new Order(
                    orderCode:
                        "HD-20260721-0001",

                    cashierUserId:
                        seed.UserId,

                    utcNow:
                        CreatedAtUtc,

                    notes:
                        "Khách ngồi tại quầy.");

            var item =
                order.AddItem(
                    productId:
                        seed.ProductId,

                    productCode:
                        "CF-SUA",

                    productName:
                        "Cà phê sữa",

                    unitName:
                        "Ly",

                    quantity:
                        2,

                    unitCostPrice:
                        12_000,

                    unitSalePrice:
                        30_000,

                    utcNow:
                        CreatedAtUtc);

            order.AddItemModifier(
                item:
                    item,

                modifierId:
                    101,

                modifierGroupId:
                    10,

                modifierGroupName:
                    "Tùy chọn thêm",

                modifierName:
                    "Thêm shot cà phê",

                quantity:
                    1,

                unitAdditionalPrice:
                    5_000,

                utcNow:
                    CreatedAtUtc.AddMinutes(1));

            order.ApplyItemDiscount(
                item:
                    item,

                amount:
                    5_000,

                utcNow:
                    CreatedAtUtc.AddMinutes(2));

            order.PrepareForPayment(
                CreatedAtUtc.AddMinutes(3));

            order.MarkPaid(
                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    100_000,

                utcNow:
                    CreatedAtUtc.AddMinutes(4));

            order.Complete(
                CreatedAtUtc.AddMinutes(5));

            await repository.AddAsync(
                order);

            await context.SaveChangesAsync();

            orderId =
                order.Id;

            Assert.True(
                orderId > 0);

            Assert.True(
                item.Id > 0);

            Assert.True(
                item.Modifiers.Single().Id > 0);
        }

        await using (
            var verifyContext =
                database.CreateContext())
        {
            var repository =
                new OrderRepository(
                    verifyContext);

            var loaded =
                await repository.GetByIdAsync(
                    orderId);

            Assert.NotNull(
                loaded);

            Assert.Equal(
                "HD-20260721-0001",
                loaded.OrderCode);

            Assert.Equal(
                OrderStatus.Completed,
                loaded.Status);

            Assert.Equal(
                PaymentMethod.Cash,
                loaded.PaymentMethod);

            Assert.Equal(
                65_000,
                loaded.Subtotal);

            Assert.Equal(
                0,
                loaded.DiscountAmount);

            Assert.Equal(
                65_000,
                loaded.TotalAmount);

            Assert.Equal(
                100_000,
                loaded.CashReceived);

            Assert.Equal(
                35_000,
                loaded.ChangeAmount);

            Assert.NotNull(
                loaded.CashierUser);

            Assert.Equal(
                "Thu ngân kiểm thử",
                loaded.CashierUser.FullName);

            var loadedItem =
                Assert.Single(
                    loaded.Items);

            Assert.Equal(
                "CF-SUA",
                loadedItem.ProductCode);

            Assert.Equal(
                "Cà phê sữa",
                loadedItem.ProductName);

            Assert.Equal(
                2,
                loadedItem.Quantity);

            Assert.Equal(
                5_000,
                loadedItem.ModifierAmountPerUnit);

            Assert.Equal(
                35_000,
                loadedItem.FinalUnitPrice);

            Assert.Equal(
                70_000,
                loadedItem.GrossAmount);

            Assert.Equal(
                5_000,
                loadedItem.LineDiscountAmount);

            Assert.Equal(
                65_000,
                loadedItem.NetAmount);

            var modifier =
                Assert.Single(
                    loadedItem.Modifiers);

            Assert.Equal(
                101,
                modifier.ModifierId);

            Assert.Equal(
                10,
                modifier.ModifierGroupId);

            Assert.Equal(
                "Tùy chọn thêm",
                modifier.ModifierGroupName);

            Assert.Equal(
                "Thêm shot cà phê",
                modifier.ModifierName);

            Assert.Equal(
                5_000,
                modifier.AmountPerProductUnit);
        }
    }

    [Fact]
    public async Task
        Search_and_code_exists_must_use_persisted_orders()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database
                .SeedAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var repository =
                new OrderRepository(
                    context);

            await repository.AddAsync(
                new Order(
                    "HD-20260721-0001",
                    seed.UserId,
                    CreatedAtUtc));

            await repository.AddAsync(
                new Order(
                    "HD-20260721-0002",
                    seed.UserId,
                    CreatedAtUtc.AddHours(1)));

            await repository.AddAsync(
                new Order(
                    "HD-20260721-0003",
                    seed.UserId,
                    CreatedAtUtc.AddHours(2)));

            await context.SaveChangesAsync();
        }

        await using (
            var searchContext =
                database.CreateContext())
        {
            var repository =
                new OrderRepository(
                    searchContext);

            Assert.True(
                await repository.CodeExistsAsync(
                    "  hd-20260721-0002  "));

            Assert.False(
                await repository.CodeExistsAsync(
                    "HD-KHONG-TON-TAI"));

            var result =
                await repository.SearchAsync(
                    searchTerm:
                        "0002",

                    status:
                        OrderStatus.Draft,

                    customerId:
                        null,

                    cashierUserId:
                        seed.UserId,

                    fromUtc:
                        CreatedAtUtc.AddMinutes(30),

                    toUtc:
                        CreatedAtUtc.AddHours(1)
                            .AddMinutes(30),

                    pageNumber:
                        1,

                    pageSize:
                        20);

            Assert.Equal(
                1,
                result.TotalCount);

            var order =
                Assert.Single(
                    result.Items);

            Assert.Equal(
                "HD-20260721-0002",
                order.OrderCode);

            Assert.NotNull(
                order.CashierUser);
        }
    }

    [Fact]
    public async Task
        Duplicate_order_code_must_be_rejected_by_sqlite()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database
                .SeedAsync();

        await using var context =
            database.CreateContext();

        context.Orders.Add(
            new Order(
                "HD-TRUNG-MA",
                seed.UserId,
                CreatedAtUtc));

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        context.Orders.Add(
            new Order(
                "hd-trung-ma",
                seed.UserId,
                CreatedAtUtc.AddMinutes(1)));

        await Assert.ThrowsAsync<
            DbUpdateException>(
                () =>
                    context.SaveChangesAsync());
    }

    private sealed record SeedData(
        int UserId,
        int ProductId);

    private sealed class OrderTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private OrderTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            OrderTestDatabase>
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
                new OrderTestDatabase(
                    connection,
                    options);

            await using (
                var context =
                    database.CreateContext())
            {
                await context.Database
                    .EnsureCreatedAsync();
            }

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        public async Task<SeedData>
            SeedAsync()
        {
            await using var context =
                CreateContext();

            var category =
                new Category(
                    name:
                        "Đồ uống",

                    displayOrder:
                        1,

                    utcNow:
                        CreatedAtUtc);

            var user =
                new User(
                    username:
                        "cashier.test",

                    passwordHash:
                        "test-password-hash",

                    fullName:
                        "Thu ngân kiểm thử",

                    role:
                        Role.Cashier,

                    utcNow:
                        CreatedAtUtc);

            context.Categories.Add(
                category);

            context.Users.Add(
                user);

            await context.SaveChangesAsync();

            var product =
                new Product(
                    categoryId:
                        category.Id,

                    code:
                        "CF-SUA",

                    name:
                        "Cà phê sữa",

                    unitName:
                        "Ly",

                    costPrice:
                        12_000,

                    salePrice:
                        30_000,

                    stockQuantity:
                        100,

                    minimumStock:
                        10,

                    trackInventory:
                        true,

                    allowNegativeStock:
                        false,

                    utcNow:
                        CreatedAtUtc);

            context.Products.Add(
                product);

            await context.SaveChangesAsync();

            return new SeedData(
                user.Id,
                product.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection
                .DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }

    /// <summary>
    /// Mô phỏng AuditableEntityInterceptor ở production:
    /// mọi Added/Modified AuditableEntity nhận GUID mới.
    /// </summary>
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

        private static void
            ApplyConcurrencyTokens(
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

        private static void
            SetConcurrencyToken(
                EntityEntry<AuditableEntity>
                    entry)
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