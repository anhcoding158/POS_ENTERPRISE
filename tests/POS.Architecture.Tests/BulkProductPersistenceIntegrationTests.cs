using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Audit;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Chứng minh đường Bulk thật qua SQLite file cô lập:
/// preview/commit dùng đúng B/C, đọc lại bằng DbContext mới, A không bị tác động.
/// </summary>
public sealed class BulkProductPersistenceIntegrationTests
{
    private static readonly string[] SelectedCodes = ["BULK-B", "BULK-C"];

    private static readonly DateTimeOffset SeedTime =
        new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset OperationTime =
        new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Selected_B_and_C_preview_commit_and_readback_preserve_A_and_inventory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"POS-Enterprise-BulkPersistence-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "bulk-persistence.db");

        try
        {
            await PortableDevelopmentDatabase.CreateMigratedAsync(
                databasePath,
                seedEmployee: true);

            int adminUserId;
            int sourceCategoryId;
            int targetCategoryId;
            await using (var seedContext = CreateContext(databasePath))
            {
                adminUserId = await seedContext.Users
                    .Select(user => user.Id)
                    .SingleAsync();

                var sourceCategory = new Category(
                    "Bulk nguồn",
                    1,
                    SeedTime);
                var targetCategory = new Category(
                    "Bulk đích",
                    2,
                    SeedTime);
                seedContext.Categories.AddRange(
                    sourceCategory,
                    targetCategory);
                await seedContext.SaveChangesAsync();
                sourceCategoryId = sourceCategory.Id;
                targetCategoryId = targetCategory.Id;

                seedContext.Products.AddRange(
                    NewProduct(sourceCategoryId, "BULK-A", "Sản phẩm A", 10, 2),
                    NewProduct(sourceCategoryId, "BULK-B", "Sản phẩm B", 15, 3),
                    NewProduct(sourceCategoryId, "BULK-C", "Sản phẩm C", 11, 4));
                await seedContext.SaveChangesAsync();
            }

            var baseline = await ReadSnapshotAsync(databasePath);
            Assert.Equal(3, baseline.Products.Length);
            Assert.Equal(0, baseline.InventoryMovementCount);
            Assert.Equal(0, baseline.AuditCount);

            var prices = await PreviewAndCommitAsync(
                databasePath,
                adminUserId,
                BulkProductOperationType.SetPrices,
                (request, selection) => request with
                {
                    Selection = selection,
                    CostPrice = 31_000,
                    SalePrice = 61_000
                });
            Assert.Equal(2, prices.ChangedCount);
            Assert.Equal(0, prices.NoOpCount);
            var afterPrices = await ReadSnapshotAsync(databasePath);
            Assert.Equal(31_000, afterPrices.ByCode["BULK-B"].CostPrice);
            Assert.Equal(61_000, afterPrices.ByCode["BULK-B"].SalePrice);
            Assert.Equal(31_000, afterPrices.ByCode["BULK-C"].CostPrice);
            Assert.Equal(61_000, afterPrices.ByCode["BULK-C"].SalePrice);
            Assert.Equal(baseline.ByCode["BULK-A"], afterPrices.ByCode["BULK-A"]);

            var category = await PreviewAndCommitAsync(
                databasePath,
                adminUserId,
                BulkProductOperationType.SetCategory,
                (request, selection) => request with
                {
                    Selection = selection,
                    CategoryId = targetCategoryId
                });
            Assert.Equal(2, category.ChangedCount);
            var afterCategory = await ReadSnapshotAsync(databasePath);
            Assert.Equal(targetCategoryId, afterCategory.ByCode["BULK-B"].CategoryId);
            Assert.Equal(targetCategoryId, afterCategory.ByCode["BULK-C"].CategoryId);
            Assert.Equal(sourceCategoryId, afterCategory.ByCode["BULK-A"].CategoryId);

            var status = await PreviewAndCommitAsync(
                databasePath,
                adminUserId,
                BulkProductOperationType.SetActiveState,
                (request, selection) => request with
                {
                    Selection = selection,
                    IsActive = false
                });
            Assert.Equal(2, status.ChangedCount);
            var afterStatus = await ReadSnapshotAsync(databasePath);
            Assert.False(afterStatus.ByCode["BULK-B"].IsActive);
            Assert.False(afterStatus.ByCode["BULK-C"].IsActive);
            Assert.True(afterStatus.ByCode["BULK-A"].IsActive);

            var minimumStock = await PreviewAndCommitAsync(
                databasePath,
                adminUserId,
                BulkProductOperationType.SetMinimumStock,
                (request, selection) => request with
                {
                    Selection = selection,
                    MinimumStock = 9
                });
            Assert.Equal(2, minimumStock.ChangedCount);
            var final = await ReadSnapshotAsync(databasePath);

            Assert.Equal(9, final.ByCode["BULK-B"].MinimumStock);
            Assert.Equal(9, final.ByCode["BULK-C"].MinimumStock);
            Assert.Equal(baseline.ByCode["BULK-A"].MinimumStock, final.ByCode["BULK-A"].MinimumStock);
            Assert.Equal(baseline.ByCode["BULK-A"].StockQuantity, final.ByCode["BULK-A"].StockQuantity);
            Assert.Equal(baseline.ByCode["BULK-B"].StockQuantity, final.ByCode["BULK-B"].StockQuantity);
            Assert.Equal(baseline.ByCode["BULK-C"].StockQuantity, final.ByCode["BULK-C"].StockQuantity);
            Assert.Equal(baseline.InventoryMovementCount, final.InventoryMovementCount);

            Assert.Equal(4, final.AuditCount);
            Assert.All(final.AuditSummaries, summary =>
            {
                Assert.Equal("2", summary.RequestedCount);
                Assert.Equal("2", summary.ChangedCount);
                Assert.DoesNotContain("BULK-A", summary.SerializedChanges, StringComparison.Ordinal);
            });
            Assert.Equal(
                new[]
                {
                    SecurityAuditAction.BulkProductPricesUpdated,
                    SecurityAuditAction.BulkProductCategoryChanged,
                    SecurityAuditAction.BulkProductActiveStateChanged,
                    SecurityAuditAction.BulkProductMinimumStockChanged
                },
                final.AuditSummaries.Select(summary => summary.Action));
            Assert.All(final.AuditSummaries, summary =>
            {
                Assert.Equal("Sản phẩm", summary.BusinessArea);
                Assert.StartsWith("Batch ", summary.TargetDisplayName, StringComparison.Ordinal);
                Assert.NotEqual(SecurityAuditAction.EmployeeUpdated, summary.Action);
            });

            await using (var queryContext = CreateContext(databasePath))
            {
                var query = await new SecurityAuditQueryRepository(queryContext).SearchAsync(
                    new AuditSearchRequest(BusinessArea: "Sản phẩm"));
                Assert.Equal(4, query.TotalCount);
                Assert.All(query.Items, item =>
                {
                    Assert.NotEqual(SecurityAuditAction.EmployeeUpdated, item.Action);
                    Assert.Equal("Thành công", item.ResultText);
                    Assert.Equal("2 sản phẩm", item.Target);
                });
            }

            var legacyOperationId = Guid.NewGuid();
            await using (var legacyContext = CreateContext(databasePath))
            {
                legacyContext.SecurityAuditEvents.Add(new SecurityAuditEvent(
                    adminUserId, null, null, SecurityAuditAction.EmployeeUpdated, "Success", legacyOperationId, OperationTime,
                    "Portable Administrator", $"Batch {legacyOperationId:N}",
                    AuditPresentationResolver.LegacyBulkBusinessArea,
                    AuditPresentationResolver.LegacyBulkTargetType,
                    "Không xác định",
                    [new SecurityAuditChange("operation", null, nameof(BulkProductOperationType.SetPrices)), new SecurityAuditChange("requested_count", null, "2")]));
                await legacyContext.SaveChangesAsync();
            }

            await using (var compatibilityContext = CreateContext(databasePath))
            {
                var repository = new SecurityAuditQueryRepository(compatibilityContext);
                var legacyPrices = await repository.SearchAsync(new AuditSearchRequest(Action: SecurityAuditAction.BulkProductPricesUpdated));
                Assert.Equal(2, legacyPrices.TotalCount);
                Assert.All(legacyPrices.Items, item => Assert.Equal(SecurityAuditAction.BulkProductPricesUpdated, item.Action));
                var employeeUpdates = await repository.SearchAsync(new AuditSearchRequest(Action: SecurityAuditAction.EmployeeUpdated));
                Assert.Empty(employeeUpdates.Items);
                var details = await repository.GetDetailsAsync(
                    (await compatibilityContext.SecurityAuditEvents.SingleAsync(audit => audit.OperationId == legacyOperationId)).Id);
                Assert.NotNull(details);
                Assert.Equal("2 sản phẩm", details.Target);
                Assert.Equal(legacyOperationId, details.OperationId);
                Assert.Equal($"Batch {legacyOperationId:N}", details.TechnicalTarget);
            }
        }
        finally
        {
            PortableDevelopmentDatabase.DeleteOwnedScenario(root);
        }
    }

    private static async Task<BulkProductOperationResult> PreviewAndCommitAsync(
        string databasePath,
        int adminUserId,
        BulkProductOperationType operation,
        Func<BulkProductOperationRequest, IReadOnlyList<BulkProductSelection>, BulkProductOperationRequest> configure)
    {
        await using var readContext = CreateContext(databasePath);
        var selected = await readContext.Products
            .AsNoTracking()
            .Where(product => product.Code == "BULK-B" || product.Code == "BULK-C")
            .OrderBy(product => product.Code)
            .Select(product => new BulkProductSelection(product.Id, product.UpdatedAtUtc))
            .ToArrayAsync();
        Assert.Equal(2, selected.Length);

        var request = configure(
            new BulkProductOperationRequest(operation, selected),
            selected);

        await using var serviceContext = CreateContext(databasePath);
        var service = CreateService(serviceContext, adminUserId);
        var previewResult = await service.PreviewAsync(request);
        Assert.True(previewResult.IsSuccess);
        Assert.True(previewResult.Value.CanConfirm, string.Join("; ", previewResult.Value.Errors.Select(error => error.Message)));
        Assert.Equal(2, previewResult.Value.Rows.Count);
        Assert.All(previewResult.Value.Rows, row => Assert.Contains(row.ProductCode, SelectedCodes));

        var commitResult = await service.CommitAsync(previewResult.Value);
        Assert.True(commitResult.IsSuccess);
        Assert.True(commitResult.Value.IsCommitted);
        return commitResult.Value;
    }

    private static BulkProductOperationService CreateService(
        PosDbContext context,
        int adminUserId) => new(
        new ProductRepository(context),
        new CategoryRepository(context),
        new SecurityAuditRepository(context),
        new EfUnitOfWork(context),
        new FixedClock(),
        new DatabaseCurrentUserService(adminUserId),
        new AllowProductManagement());

    private static Product NewProduct(
        int categoryId,
        string code,
        string name,
        int stock,
        int minimumStock) => new(
        categoryId,
        code,
        name,
        "Cái",
        20_000,
        40_000,
        stock,
        minimumStock,
        trackInventory: true,
        allowNegativeStock: false,
        SeedTime);

    private static PosDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Pooling=False")
            .Options;
        return new PosDbContext(options);
    }

    private static async Task<DatabaseSnapshot> ReadSnapshotAsync(string databasePath)
    {
        await using var context = CreateContext(databasePath);
        var products = await context.Products
            .AsNoTracking()
            .OrderBy(product => product.Code)
            .ToDictionaryAsync(
                product => product.Code,
                product => new ProductSnapshot(
                    product.CategoryId,
                    product.CostPrice,
                    product.SalePrice,
                    product.StockQuantity,
                    product.MinimumStock,
                    product.IsActive));
        var audits = await context.SecurityAuditEvents
            .AsNoTracking()
            .OrderBy(audit => audit.CreatedAtUtc)
                .Select(audit => new AuditSummary(
                    audit.Action,
                    audit.BusinessArea,
                    audit.TargetDisplayNameSnapshot,
                    audit.BeforeValuesJson,
                    audit.AfterValuesJson))
            .ToArrayAsync();
        return new DatabaseSnapshot(
            products,
            await context.InventoryMovements.CountAsync(),
            audits.Length,
            audits);
    }

    private sealed record DatabaseSnapshot(
        IReadOnlyDictionary<string, ProductSnapshot> ByCode,
        int InventoryMovementCount,
        int AuditCount,
        IReadOnlyList<AuditSummary> AuditSummaries)
    {
        public ProductSnapshot[] Products => ByCode.Values.ToArray();
    }

    private sealed record ProductSnapshot(
        int CategoryId,
        long CostPrice,
        long SalePrice,
        int StockQuantity,
        int MinimumStock,
        bool IsActive);

    private sealed record AuditSummary(
        SecurityAuditAction Action,
        string BusinessArea,
        string TargetDisplayName,
        string SerializedChanges,
        string AfterValuesJson)
    {
        public string RequestedCount =>
            SecurityAuditChangeSet.Deserialize(SerializedChanges)
                .Single(change => change.FieldKey == "requested_count")
                .AfterValue!;

        public string ChangedCount =>
            SecurityAuditChangeSet.Deserialize(SerializedChanges)
                .Single(change => change.FieldKey == "changed_count")
                .AfterValue!;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => OperationTime;
    }

    private sealed class DatabaseCurrentUserService : ICurrentUserService
    {
        private readonly AuthenticatedUserDto _user;

        public DatabaseCurrentUserService(int userId)
        {
            _user = new AuthenticatedUserDto(
                userId,
                "portable-admin",
                "Portable Administrator",
                global::POS.Domain.Enums.Role.Administrator,
                SeedTime);
        }

        public AuthenticatedUserDto? CurrentUser => _user;
        public bool IsAuthenticated => true;
        public int? UserId => _user.Id;
        public string? Username => _user.Username;
        public string? FullName => _user.FullName;
        public Role? Role => _user.Role;
        public bool IsInRole(Role role) => _user.Role == role;
        public void SetCurrentUser(AuthenticatedUserDto user) { }
        public void Clear() { }
    }

    private sealed class AllowProductManagement : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) =>
            permission == SystemCapability.ManageProducts;

        public Result Authorize(SystemCapability permission) =>
            HasPermission(permission)
                ? Result.Success()
                : Result.Failure(new AppError("FORBIDDEN", "Không có quyền."));
    }
}
