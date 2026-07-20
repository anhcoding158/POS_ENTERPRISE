using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.Abstractions.DateTime;
using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Integration test cho Category CRUD.
///
/// Sử dụng SQLite thật trong bộ nhớ để kiểm tra đồng thời:
/// - Domain validation;
/// - CategoryService;
/// - CategoryRepository;
/// - EF Core mapping;
/// - unique index không phân biệt hoa thường;
/// - tìm kiếm có escape ký tự LIKE;
/// - phân trang;
/// - active state;
/// - optimistic concurrency.
/// </summary>
public sealed class CategoryIntegrationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                21,
                1,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Create_must_normalize_and_persist_category()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    CreatedAtUtc);

            var result =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "  Đồ uống lạnh  ",
                        displayOrder:
                            5,
                        description:
                            "  Nước giải khát và đồ uống lạnh.  "));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.True(
                result.Value.Id > 0);

            Assert.Equal(
                "Đồ uống lạnh",
                result.Value.Name);

            Assert.Equal(
                "Nước giải khát và đồ uống lạnh.",
                result.Value.Description);

            Assert.Equal(
                5,
                result.Value.DisplayOrder);

            Assert.True(
                result.Value.IsActive);

            Assert.Equal(
                CreatedAtUtc,
                result.Value.CreatedAtUtc);

            Assert.Equal(
                CreatedAtUtc,
                result.Value.UpdatedAtUtc);
        }

        await using var verifyContext =
            database.CreateContext();

        var category =
            await verifyContext
                .Categories
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            "Đồ uống lạnh",
            category.Name);

        Assert.Equal(
            "Nước giải khát và đồ uống lạnh.",
            category.Description);

        Assert.Equal(
            5,
            category.DisplayOrder);

        Assert.True(
            category.IsActive);
    }

    [Fact]
    public async Task
        Duplicate_name_must_be_rejected_case_insensitively()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    CreatedAtUtc);

            var firstResult =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "CA PHE",
                        displayOrder:
                            1));

            Assert.True(
                firstResult.IsSuccess,
                firstResult.Error.ToString());

            var duplicateResult =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "ca phe",
                        displayOrder:
                            2));

            Assert.True(
                duplicateResult.IsFailure);

            Assert.Equal(
                ErrorCodes.Categories
                    .NameAlreadyExists,
                duplicateResult.Error.Code);
        }

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            1,
            await verifyContext
                .Categories
                .CountAsync());
    }

    [Fact]
    public async Task
        Invalid_display_order_must_not_persist_category()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    CreatedAtUtc);

            var result =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Danh mục không hợp lệ",
                        displayOrder:
                            -1));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                "CATEGORY.INVALID_DISPLAY_ORDER",
                result.Error.Code);
        }

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext
                .Categories
                .CountAsync());
    }

    [Fact]
    public async Task
        Update_must_change_details_and_active_state()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        int categoryId;

        await using (
            var createContext =
                database.CreateContext())
        {
            var createService =
                CreateService(
                    createContext,
                    CreatedAtUtc);

            var createResult =
                await createService.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Tên danh mục cũ",
                        displayOrder:
                            10,
                        description:
                            "Mô tả cũ"));

            Assert.True(
                createResult.IsSuccess,
                createResult.Error.ToString());

            categoryId =
                createResult.Value.Id;
        }

        var updatedAtUtc =
            CreatedAtUtc.AddHours(2);

        await using (
            var updateContext =
                database.CreateContext())
        {
            var updateService =
                CreateService(
                    updateContext,
                    updatedAtUtc);

            var updateResult =
                await updateService.UpdateAsync(
                    new UpdateCategoryRequest(
                        categoryId:
                            categoryId,
                        name:
                            "Tên danh mục mới",
                        displayOrder:
                            3,
                        isActive:
                            false,
                        description:
                            "Mô tả mới"));

            Assert.True(
                updateResult.IsSuccess,
                updateResult.Error.ToString());

            Assert.Equal(
                categoryId,
                updateResult.Value.Id);

            Assert.Equal(
                "Tên danh mục mới",
                updateResult.Value.Name);

            Assert.Equal(
                "Mô tả mới",
                updateResult.Value.Description);

            Assert.Equal(
                3,
                updateResult.Value.DisplayOrder);

            Assert.False(
                updateResult.Value.IsActive);

            Assert.Equal(
                CreatedAtUtc,
                updateResult.Value.CreatedAtUtc);

            Assert.Equal(
                updatedAtUtc,
                updateResult.Value.UpdatedAtUtc);
        }

        await using var verifyContext =
            database.CreateContext();

        var category =
            await verifyContext
                .Categories
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            "Tên danh mục mới",
            category.Name);

        Assert.Equal(
            "Mô tả mới",
            category.Description);

        Assert.Equal(
            3,
            category.DisplayOrder);

        Assert.False(
            category.IsActive);
    }

    [Fact]
    public async Task
        Active_list_must_exclude_inactive_and_sort_stably()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        int inactiveCategoryId;

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    CreatedAtUtc);

            var categoryB =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Bánh ngọt",
                        displayOrder:
                            2));

            var categoryC =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Cà phê",
                        displayOrder:
                            1));

            var categoryA =
                await service.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Ăn sáng",
                        displayOrder:
                            1));

            Assert.True(
                categoryA.IsSuccess,
                categoryA.Error.ToString());

            Assert.True(
                categoryB.IsSuccess,
                categoryB.Error.ToString());

            Assert.True(
                categoryC.IsSuccess,
                categoryC.Error.ToString());

            inactiveCategoryId =
                categoryB.Value.Id;

            var deactivateResult =
                await service.SetActiveStateAsync(
                    inactiveCategoryId,
                    isActive:
                        false);

            Assert.True(
                deactivateResult.IsSuccess,
                deactivateResult.Error.ToString());
        }

        await using (
            var listContext =
                database.CreateContext())
        {
            var service =
                CreateService(
                    listContext,
                    CreatedAtUtc.AddHours(1));

            var listResult =
                await service.ListActiveAsync();

            Assert.True(
                listResult.IsSuccess,
                listResult.Error.ToString());

            Assert.Equal(
                2,
                listResult.Value.Count);

            Assert.Equal(
                "Ăn sáng",
                listResult.Value[0].Name);

            Assert.Equal(
                "Cà phê",
                listResult.Value[1].Name);

            Assert.DoesNotContain(
                listResult.Value,
                category =>
                    category.Id ==
                    inactiveCategoryId);
        }
    }

    [Fact]
    public async Task
        Search_must_escape_like_characters_and_paginate()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    CreatedAtUtc);

            var requests =
                new[]
                {
                    new CreateCategoryRequest(
                        "SALE 100%",
                        1),

                    new CreateCategoryRequest(
                        "MA_HANG",
                        2),

                    new CreateCategoryRequest(
                        @"DUONG\DAN",
                        3),

                    new CreateCategoryRequest(
                        "BINH THUONG",
                        4)
                };

            foreach (var request in requests)
            {
                var createResult =
                    await service.CreateAsync(
                        request);

                Assert.True(
                    createResult.IsSuccess,
                    createResult.Error.ToString());
            }

            var percentResult =
                await service.SearchAsync(
                    new CategorySearchRequest(
                        searchTerm:
                            "%",
                        pageNumber:
                            1,
                        pageSize:
                            20));

            Assert.True(
                percentResult.IsSuccess,
                percentResult.Error.ToString());

            var percentCategory =
                Assert.Single(
                    percentResult.Value.Items);

            Assert.Equal(
                "SALE 100%",
                percentCategory.Name);

            var underscoreResult =
                await service.SearchAsync(
                    new CategorySearchRequest(
                        searchTerm:
                            "_",
                        pageNumber:
                            1,
                        pageSize:
                            20));

            Assert.True(
                underscoreResult.IsSuccess,
                underscoreResult.Error.ToString());

            var underscoreCategory =
                Assert.Single(
                    underscoreResult.Value.Items);

            Assert.Equal(
                "MA_HANG",
                underscoreCategory.Name);

            var slashResult =
                await service.SearchAsync(
                    new CategorySearchRequest(
                        searchTerm:
                            "\\",
                        pageNumber:
                            1,
                        pageSize:
                            20));

            Assert.True(
                slashResult.IsSuccess,
                slashResult.Error.ToString());

            var slashCategory =
                Assert.Single(
                    slashResult.Value.Items);

            Assert.Equal(
                @"DUONG\DAN",
                slashCategory.Name);

            var firstPage =
                await service.SearchAsync(
                    new CategorySearchRequest(
                        pageNumber:
                            1,
                        pageSize:
                            2));

            var secondPage =
                await service.SearchAsync(
                    new CategorySearchRequest(
                        pageNumber:
                            2,
                        pageSize:
                            2));

            Assert.True(
                firstPage.IsSuccess,
                firstPage.Error.ToString());

            Assert.True(
                secondPage.IsSuccess,
                secondPage.Error.ToString());

            Assert.Equal(
                4,
                firstPage.Value.TotalCount);

            Assert.Equal(
                2,
                firstPage.Value.Items.Count);

            Assert.Equal(
                2,
                secondPage.Value.Items.Count);

            var firstPageIds =
                firstPage.Value.Items
                    .Select(
                        category =>
                            category.Id)
                    .ToHashSet();

            Assert.DoesNotContain(
                secondPage.Value.Items,
                category =>
                    firstPageIds.Contains(
                        category.Id));
        }
    }

    [Fact]
    public async Task
        Stale_update_must_return_concurrency_conflict()
    {
        await using var database =
            await CategoryTestDatabase
                .CreateAsync();

        int categoryId;

        await using (
            var seedContext =
                database.CreateContext())
        {
            var seedService =
                CreateService(
                    seedContext,
                    CreatedAtUtc);

            var createResult =
                await seedService.CreateAsync(
                    new CreateCategoryRequest(
                        name:
                            "Danh mục ban đầu",
                        displayOrder:
                            1));

            Assert.True(
                createResult.IsSuccess,
                createResult.Error.ToString());

            categoryId =
                createResult.Value.Id;
        }

        await using var firstContext =
            database.CreateContext();

        await using var staleContext =
            database.CreateContext();

        var firstService =
            CreateService(
                firstContext,
                CreatedAtUtc.AddHours(1));

        var staleService =
            CreateService(
                staleContext,
                CreatedAtUtc.AddHours(2));

        /*
         * Nạp Category vào staleContext trước khi thao tác
         * ở firstContext được commit.
         */
        var staleLoadResult =
            await staleService.GetByIdAsync(
                categoryId);

        Assert.True(
            staleLoadResult.IsSuccess,
            staleLoadResult.Error.ToString());

        var firstUpdateResult =
            await firstService.UpdateAsync(
                new UpdateCategoryRequest(
                    categoryId:
                        categoryId,
                    name:
                        "Danh mục từ cửa sổ thứ nhất",
                    displayOrder:
                        2,
                    isActive:
                        true,
                    description:
                        "Dữ liệu mới nhất"));

        Assert.True(
            firstUpdateResult.IsSuccess,
            firstUpdateResult.Error.ToString());

        var staleUpdateResult =
            await staleService.UpdateAsync(
                new UpdateCategoryRequest(
                    categoryId:
                        categoryId,
                    name:
                        "Danh mục từ cửa sổ cũ",
                    displayOrder:
                        3,
                    isActive:
                        true,
                    description:
                        "Không được phép ghi đè"));

        Assert.True(
            staleUpdateResult.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Conflict,
            staleUpdateResult.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        var category =
            await verifyContext
                .Categories
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            "Danh mục từ cửa sổ thứ nhất",
            category.Name);

        Assert.Equal(
            "Dữ liệu mới nhất",
            category.Description);

        Assert.Equal(
            2,
            category.DisplayOrder);
    }

    private static CategoryService
        CreateService(
            PosDbContext context,
            DateTimeOffset utcNow)
    {
        return new CategoryService(
            new CategoryRepository(
                context),

            new EfUnitOfWork(
                context),

            new FixedClock(
                utcNow));
    }

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow { get; }
    }

    /// <summary>
    /// SQLite trong bộ nhớ.
    ///
    /// Connection được giữ mở trong toàn bộ test để database
    /// không bị xóa khi đổi DbContext.
    /// </summary>
    private sealed class CategoryTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private CategoryTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            CategoryTestDatabase>
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
                new CategoryTestDatabase(
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

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }

    /// <summary>
    /// Mô phỏng AuditableEntityInterceptor ở production:
    /// mỗi Added hoặc Modified entity nhận GUID token mới.
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