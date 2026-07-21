using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Các hàng rào cuối trước khi tạo migration Order.
///
/// Kiểm tra:
/// - phân trang không làm mất TotalCount;
/// - chống overflow vị trí trang;
/// - cascade trong aggregate;
/// - restrict dữ liệu tham chiếu;
/// - optimistic concurrency;
/// - ánh xạ unique OrderCode;
/// - whitelist entity của model 8A.
/// </summary>
public sealed class
    OrderPersistenceGuardrailTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                21,
                10,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Search_beyond_last_page_must_preserve_total_count()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        await using (
            var context =
                database.CreateContext())
        {
            context.Orders.AddRange(
                CreateOrder(
                    seed,
                    "HD-PAGE-0001",
                    CreatedAtUtc),

                CreateOrder(
                    seed,
                    "HD-PAGE-0002",
                    CreatedAtUtc.AddMinutes(1)),

                CreateOrder(
                    seed,
                    "HD-PAGE-0003",
                    CreatedAtUtc.AddMinutes(2)));

            await context.SaveChangesAsync();
        }

        await using var searchContext =
            database.CreateContext();

        var repository =
            new OrderRepository(
                searchContext);

        var result =
            await repository.SearchAsync(
                searchTerm:
                    null,

                status:
                    OrderStatus.Draft,

                customerId:
                    null,

                cashierUserId:
                    seed.UserId,

                fromUtc:
                    null,

                toUtc:
                    null,

                pageNumber:
                    99,

                pageSize:
                    2);

        Assert.Empty(
            result.Items);

        Assert.Equal(
            3,
            result.TotalCount);

        Assert.Equal(
            2,
            result.TotalPages);

        Assert.Equal(
            99,
            result.PageNumber);

        Assert.False(
            result.HasNextPage);
    }

    [Fact]
    public async Task
        Search_must_reject_pagination_overflow()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var repository =
            new OrderRepository(
                context);

        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                () =>
                    repository.SearchAsync(
                        searchTerm:
                            null,

                        status:
                            null,

                        customerId:
                            null,

                        cashierUserId:
                            null,

                        fromUtc:
                            null,

                        toUtc:
                            null,

                        pageNumber:
                            int.MaxValue,

                        pageSize:
                            200));
    }

    [Fact]
    public async Task
        Deleting_order_must_cascade_items_and_modifiers()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        var orderId =
            await database.SeedOrderAsync(
                seed,
                "HD-CASCADE-0001",
                includeModifier:
                    true);

        await using (
            var deleteContext =
                database.CreateContext())
        {
            var repository =
                new OrderRepository(
                    deleteContext);

            var order =
                await repository.GetByIdAsync(
                    orderId);

            Assert.NotNull(
                order);

            deleteContext.Orders.Remove(
                order);

            await deleteContext
                .SaveChangesAsync();
        }

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext.Orders
                .CountAsync());

        Assert.Equal(
            0,
            await verifyContext.OrderItems
                .CountAsync());

        Assert.Equal(
            0,
            await verifyContext
                .OrderItemModifiers
                .CountAsync());

        /*
         * Cascade chỉ được xóa thành phần của aggregate.
         * User và Product tham chiếu phải còn nguyên.
         */
        Assert.Equal(
            1,
            await verifyContext.Users
                .CountAsync());

        Assert.Equal(
            1,
            await verifyContext.Products
                .CountAsync());
    }

    [Fact]
    public async Task
        Deleting_referenced_cashier_must_be_restricted()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        await database.SeedOrderAsync(
            seed,
            "HD-USER-RESTRICT-0001",
            includeModifier:
                false);

        await using var context =
            database.CreateContext();

        var cashier =
            await context.Users
                .SingleAsync(
                    user =>
                        user.Id ==
                        seed.UserId);

        context.Users.Remove(
            cashier);

        await Assert.ThrowsAsync<
            DbUpdateException>(
                () =>
                    context.SaveChangesAsync());
    }

    [Fact]
    public async Task
        Deleting_referenced_product_must_be_restricted()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        await database.SeedOrderAsync(
            seed,
            "HD-PRODUCT-RESTRICT-0001",
            includeModifier:
                false);

        await using var context =
            database.CreateContext();

        var product =
            await context.Products
                .SingleAsync(
                    item =>
                        item.Id ==
                        seed.ProductId);

        context.Products.Remove(
            product);

        await Assert.ThrowsAsync<
            DbUpdateException>(
                () =>
                    context.SaveChangesAsync());
    }

    [Fact]
    public async Task
        Stale_order_update_must_report_concurrency_conflict()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        var orderId =
            await database.SeedOrderAsync(
                seed,
                "HD-CONCURRENCY-0001",
                includeModifier:
                    false);

        await using var firstContext =
            database.CreateContext();

        await using var staleContext =
            database.CreateContext();

        var firstRepository =
            new OrderRepository(
                firstContext);

        var staleRepository =
            new OrderRepository(
                staleContext);

        /*
         * Cả hai context tải cùng concurrency token cũ.
         */
        var firstOrder =
            await firstRepository.GetByIdAsync(
                orderId);

        var staleOrder =
            await staleRepository.GetByIdAsync(
                orderId);

        Assert.NotNull(
            firstOrder);

        Assert.NotNull(
            staleOrder);

        firstOrder.ChangeNotes(
            "Cập nhật từ thao tác thứ nhất.",
            CreatedAtUtc.AddHours(1));

        staleOrder.ChangeNotes(
            "Cập nhật từ dữ liệu đã cũ.",
            CreatedAtUtc.AddHours(2));

        var firstUnitOfWork =
            new EfUnitOfWork(
                firstContext);

        await firstUnitOfWork
            .SaveChangesAsync();

        var staleUnitOfWork =
            new EfUnitOfWork(
                staleContext);

        var exception =
            await Assert.ThrowsAsync<
                PersistenceConflictException>(
                    () =>
                        staleUnitOfWork
                            .SaveChangesAsync());

        Assert.Equal(
            PersistenceConflictKind.Concurrency,
            exception.Kind);
    }

    [Fact]
    public async Task
        Duplicate_order_code_must_map_to_order_code_target()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync();

        await database.SeedOrderAsync(
            seed,
            "HD-UNIQUE-0001",
            includeModifier:
                false);

        await using var context =
            database.CreateContext();

        /*
         * Index OrderCode dùng NOCASE nên mã chữ thường
         * vẫn phải xung đột với mã chữ hoa đã lưu.
         */
        context.Orders.Add(
            CreateOrder(
                seed,
                "hd-unique-0001",
                CreatedAtUtc.AddHours(1)));

        var unitOfWork =
            new EfUnitOfWork(
                context);

        var exception =
            await Assert.ThrowsAsync<
                PersistenceConflictException>(
                    () =>
                        unitOfWork
                            .SaveChangesAsync());

        Assert.Equal(
            PersistenceConflictKind.UniqueConstraint,
            exception.Kind);

        Assert.Equal(
            PersistenceConflictTargets.OrderCode,
            exception.Target);
    }

    [Fact]
    public async Task
        Current_8A_model_must_only_contain_approved_entities()
    {
        await using var database =
            await OrderTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var expectedEntityTypes =
            new[]
            {
                typeof(Category),
                typeof(InventoryMovement),
                typeof(Order),
                typeof(OrderItem),
                typeof(OrderItemModifier),
                typeof(Product),
                typeof(User)
            }
            .OrderBy(
                type =>
                    type.FullName,
                StringComparer.Ordinal)
            .ToArray();

        var actualEntityTypes =
            context.Model
                .GetEntityTypes()
                .Select(
                    entityType =>
                        entityType.ClrType)
                .Distinct()
                .OrderBy(
                    type =>
                        type.FullName,
                    StringComparer.Ordinal)
                .ToArray();

        Assert.Equal(
            expectedEntityTypes,
            actualEntityTypes);

        var orderEntityType =
            context.Model
                .FindEntityType(
                    typeof(Order));

        Assert.NotNull(
            orderEntityType);

        /*
         * Ba ID này được lưu tạm dưới dạng scalar trong 8A.
         * Customer/Table/Discount chưa được phép tạo FK hoặc
         * kéo entity placeholder vào migration hiện tại.
         */
        Assert.NotNull(
            orderEntityType.FindProperty(
                nameof(Order.CustomerId)));

        Assert.NotNull(
            orderEntityType.FindProperty(
                nameof(Order.RestaurantTableId)));

        Assert.NotNull(
            orderEntityType.FindProperty(
                nameof(Order.DiscountId)));

        Assert.Null(
            orderEntityType.FindNavigation(
                nameof(Order.Customer)));

        Assert.Null(
            orderEntityType.FindNavigation(
                nameof(Order.RestaurantTable)));

        Assert.Null(
            orderEntityType.FindNavigation(
                nameof(Order.Discount)));

        var temporaryScalarIds =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                nameof(Order.CustomerId),
                nameof(Order.RestaurantTableId),
                nameof(Order.DiscountId)
            };

        Assert.DoesNotContain(
            orderEntityType.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Any(
                    property =>
                        temporaryScalarIds.Contains(
                            property.Name)));
    }

    private static Order CreateOrder(
        SeedData seed,
        string orderCode,
        DateTimeOffset utcNow,
        bool includeModifier = false)
    {
        var order =
            new Order(
                orderCode:
                    orderCode,

                cashierUserId:
                    seed.UserId,

                utcNow:
                    utcNow);

        var item =
            order.AddItem(
                productId:
                    seed.ProductId,

                productCode:
                    "SP-ORDER-TEST",

                productName:
                    "Sản phẩm kiểm thử đơn hàng",

                unitName:
                    "Phần",

                quantity:
                    2,

                unitCostPrice:
                    10_000,

                unitSalePrice:
                    25_000,

                utcNow:
                    utcNow);

        if (includeModifier)
        {
            order.AddItemModifier(
                item:
                    item,

                modifierId:
                    501,

                modifierGroupId:
                    50,

                modifierGroupName:
                    "Tùy chọn kiểm thử",

                modifierName:
                    "Phần thêm kiểm thử",

                quantity:
                    1,

                unitAdditionalPrice:
                    5_000,

                utcNow:
                    utcNow.AddMinutes(1));
        }

        return order;
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
                        new AuditableEntityInterceptor())
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
                        "Danh mục kiểm thử Order",

                    displayOrder:
                        1,

                    utcNow:
                        CreatedAtUtc);

            var user =
                new User(
                    username:
                        "order.guard",

                    passwordHash:
                        "order-guard-password-hash",

                    fullName:
                        "Thu ngân kiểm thử Order",

                    role:
                        Role.Cashier,

                    utcNow:
                        CreatedAtUtc);

            context.Categories.Add(
                category);

            context.Users.Add(
                user);

            await context
                .SaveChangesAsync();

            var product =
                new Product(
                    categoryId:
                        category.Id,

                    code:
                        "SP-ORDER-TEST",

                    name:
                        "Sản phẩm kiểm thử đơn hàng",

                    unitName:
                        "Phần",

                    costPrice:
                        10_000,

                    salePrice:
                        25_000,

                    stockQuantity:
                        100,

                    minimumStock:
                        5,

                    trackInventory:
                        true,

                    allowNegativeStock:
                        false,

                    utcNow:
                        CreatedAtUtc);

            context.Products.Add(
                product);

            await context
                .SaveChangesAsync();

            return new SeedData(
                user.Id,
                product.Id);
        }

        public async Task<int>
            SeedOrderAsync(
                SeedData seed,
                string orderCode,
                bool includeModifier)
        {
            await using var context =
                CreateContext();

            var order =
                CreateOrder(
                    seed,
                    orderCode,
                    CreatedAtUtc.AddMinutes(10),
                    includeModifier);

            context.Orders.Add(
                order);

            await context
                .SaveChangesAsync();

            return order.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection
                .DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}