using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.ProductImports;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.ProductImports;
using POS.Application.ProductImports;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Common;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductImportIntegrationTests
{
    [Fact]
    public async Task Confirmed_custom_mapping_is_reparsed_and_imported_by_the_real_service()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var headers = Enumerable.Range(1, 11).Select(index => "Nguồn " + index).ToArray();
        var file = fixture.WriteCsvWithHeaders(
            "custom-mapping",
            headers,
            Row("IMP-MAP-001", "000321", "Mapped", "35", "25", "2", "1", "Đang bán", ""));
        var mapping = ProductImportSchemaCatalog.Fields
            .Select((field, index) => new ProductImportColumnMapping(index, field.CanonicalKey))
            .ToArray();
        var preview = await fixture.PreviewAsync(
            file,
            new ProductImportPreviewOptions(ColumnMappings: mapping));

        var result = await fixture.CreateService().ImportAsync(
            new ProductImportRequest(file, preview, ProductImportDuplicatePolicy.Error));

        Assert.True(result.IsCommitted);
        Assert.Equal(1, result.CreatedCount);
        await using var verify = fixture.CreateContext();
        var product = await verify.Products.SingleAsync();
        Assert.Equal("IMP-MAP-001", product.Code);
        Assert.Equal("000321", product.Barcode);
    }

    [Fact]
    public async Task Full_schema_create_commits_product_opening_balance_and_audit_summary()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.WriteCsv("create", Row("IMP-001", "089000000001", "Cà phê rang xay", "35", "25", "12", "3", "Đang bán", "Gói 500 g"));
        var preview = await fixture.PreviewAsync(file);

        var result = await fixture.CreateService().ImportAsync(new ProductImportRequest(file, preview, ProductImportDuplicatePolicy.Error));

        Assert.True(result.IsCommitted);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.TotalValidRowsRequested);

        await using var verify = fixture.CreateContext();
        var product = await verify.Products.SingleAsync();
        Assert.Equal("IMP-001", product.Code);
        Assert.Equal("089000000001", product.Barcode);
        Assert.Equal("Cái", product.UnitName);
        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(3, product.MinimumStock);
        Assert.False(product.IsArchived);
        var movement = await verify.InventoryMovements.SingleAsync();
        Assert.Equal(InventoryMovementType.OpeningBalance, movement.MovementType);
        Assert.Equal(12, movement.QuantityAfter);
        var audit = await verify.SecurityAuditEvents.SingleAsync(a => a.OperationId == result.BatchId);
        Assert.Equal(SecurityAuditAction.BulkProductOperation, audit.Action);
        Assert.Equal("Sản phẩm và nhập dữ liệu", audit.BusinessArea);
        Assert.Contains("created_count", audit.AfterValuesJson, StringComparison.Ordinal);
        Assert.DoesNotContain(file, audit.AfterValuesJson, StringComparison.OrdinalIgnoreCase);

        var details = await new SecurityAuditQueryRepository(verify).GetDetailsAsync(audit.Id);
        Assert.NotNull(details);
        Assert.Equal("Nhập dữ liệu sản phẩm", details.ActionText);
        Assert.Equal("Lô nhập sản phẩm", details.TargetType);
        Assert.Equal("Batch " + result.BatchId.ToString("N"), details.TechnicalTarget);
    }

    [Fact]
    public async Task Skip_duplicate_database_keeps_original_product()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await fixture.ImportAsync("first", Row("IMP-002", "089000000002", "Cũ", "10", "8"), ProductImportDuplicatePolicy.Error);
        var file = fixture.WriteCsv("skip", Row("IMP-002", "089000000002", "Không ghi đè", "99", "88"));

        var result = await fixture.ImportFileAsync(file, ProductImportDuplicatePolicy.Skip);

        Assert.True(result.IsCommitted);
        Assert.Equal(1, result.SkippedCount);
        await using var verify = fixture.CreateContext();
        Assert.Equal("Cũ", await verify.Products.Select(product => product.Name).SingleAsync());
    }

    [Fact]
    public async Task Update_keeps_product_id_stock_and_inventory_history()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await fixture.ImportAsync("initial", Row("IMP-003", "089000000003", "Tên cũ", "20", "15", "9"), ProductImportDuplicatePolicy.Error);
        await using (var beforeContext = fixture.CreateContext())
        {
            var before = await beforeContext.Products.SingleAsync();
            fixture.RememberProduct(before.Id, before.StockQuantity);
        }

        var file = fixture.WriteCsv("update", Row("IMP-003", "089000000003", "Tên mới", "30", "18", "0", "5", "Ngừng bán", "Đã cập nhật"));
        var result = await fixture.ImportFileAsync(file, ProductImportDuplicatePolicy.Update);

        Assert.True(result.IsCommitted);
        Assert.Equal(1, result.UpdatedCount);
        await using var verify = fixture.CreateContext();
        var product = await verify.Products.SingleAsync();
        Assert.Equal(fixture.RememberedProductId, product.Id);
        Assert.Equal(fixture.RememberedStock, product.StockQuantity);
        Assert.Equal("Tên mới", product.Name);
        Assert.False(product.IsActive);
        Assert.Single(await verify.InventoryMovements.ToArrayAsync());
    }

    [Fact]
    public async Task Error_duplicate_rolls_back_entire_batch()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await fixture.ImportAsync("existing", Row("IMP-004", "089000000004", "Đã có", "10", "8"), ProductImportDuplicatePolicy.Error);
        var file = fixture.WriteCsv(
            "error",
            Row("IMP-005", "089000000005", "Không được lưu", "10", "8"),
            Row("IMP-004", "089000000004", "Trùng", "11", "9"));

        var result = await fixture.ImportFileAsync(file, ProductImportDuplicatePolicy.Error);

        Assert.False(result.IsCommitted);
        Assert.Equal(ProductImportBatchStatus.RolledBack, result.Status);
        Assert.Equal(2, result.FailedCount);
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Products.CountAsync());
        Assert.DoesNotContain(await verify.Products.Select(product => product.Code).ToArrayAsync(), code => code == "IMP-005");
    }

    [Fact]
    public async Task Same_file_duplicates_follow_skip_update_and_error_policies()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var skip = await fixture.ImportFileAsync(
            fixture.WriteCsv("same-skip", Row("IMP-006", "089000000006", "Một", "10", "8"), Row("IMP-006", "089000000006", "Hai", "11", "9")),
            ProductImportDuplicatePolicy.Skip);
        Assert.Equal(1, skip.CreatedCount);
        Assert.Equal(1, skip.SkippedCount);

        var update = await fixture.ImportFileAsync(
            fixture.WriteCsv("same-update", Row("IMP-007", "089000000007", "Một", "10", "8"), Row("IMP-007", "089000000007", "Hai", "11", "9")),
            ProductImportDuplicatePolicy.Update);
        Assert.Equal(1, update.CreatedCount);
        Assert.Equal(1, update.UpdatedCount);

        var error = await fixture.ImportFileAsync(
            fixture.WriteCsv("same-error", Row("IMP-008", "089000000008", "Một", "10", "8"), Row("IMP-008", "089000000008", "Hai", "11", "9")),
            ProductImportDuplicatePolicy.Error);
        Assert.Equal(ProductImportBatchStatus.RolledBack, error.Status);
        await using var verify = fixture.CreateContext();
        Assert.DoesNotContain(await verify.Products.Select(product => product.Code).ToArrayAsync(), code => code == "IMP-008");
    }

    [Fact]
    public async Task Code_barcode_conflict_is_rejected_without_mutation()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await fixture.ImportAsync("a", Row("IMP-009", "089000000009", "A", "10", "8"), ProductImportDuplicatePolicy.Error);
        await fixture.ImportAsync("b", Row("IMP-010", "089000000010", "B", "10", "8"), ProductImportDuplicatePolicy.Error);
        var file = fixture.WriteCsv("conflict", Row("IMP-009", "089000000010", "Xung đột", "20", "15"));

        var result = await fixture.ImportFileAsync(file, ProductImportDuplicatePolicy.Update);

        Assert.Equal(ProductImportBatchStatus.RolledBack, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "IDENTITY_CONFLICT");
        await using var verify = fixture.CreateContext();
        Assert.Equal(2, await verify.Products.CountAsync());
    }

    [Fact]
    public async Task Inactive_category_and_missing_permission_are_rejected_before_mutation()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await using (var context = fixture.CreateContext())
        {
            var category = await context.Categories.SingleAsync();
            category.Deactivate(fixture.Now);
            await context.SaveChangesAsync();
        }
        var inactiveFile = fixture.WriteCsv("inactive", Row("IMP-011", "089000000011", "Không có danh mục", "10", "8"));
        var inactive = await fixture.ImportFileAsync(inactiveFile, ProductImportDuplicatePolicy.Error);
        Assert.Contains(inactive.Issues, issue => issue.Code == "CATEGORY_INACTIVE");

        fixture.SetRole(Role.Cashier);
        var deniedFile = fixture.WriteCsv("denied", Row("IMP-012", "089000000012", "Không có quyền", "10", "8"));
        var denied = await fixture.CreateAuthorizedService().ImportAsync(new ProductImportRequest(deniedFile, await fixture.PreviewAsync(deniedFile), ProductImportDuplicatePolicy.Error));
        Assert.Contains(denied.Issues, issue => issue.Code == "GENERAL.FORBIDDEN");
        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.Products.ToArrayAsync());
    }

    [Fact]
    public async Task Update_cannot_use_initial_stock_and_stale_preview_is_rejected()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        await fixture.ImportAsync("stock", Row("IMP-013", "089000000013", "Có tồn", "10", "8", "4"), ProductImportDuplicatePolicy.Error);
        var updateFile = fixture.WriteCsv("stock-update", Row("IMP-013", "089000000013", "Không ghi đè tồn", "11", "9", "2"));
        var update = await fixture.ImportFileAsync(updateFile, ProductImportDuplicatePolicy.Update);
        Assert.Contains(update.Issues, issue => issue.Code == "UPDATE_OPENING_STOCK_NOT_ALLOWED");

        var staleFile = fixture.WriteCsv("stale", Row("IMP-014", "089000000014", "Bản xem trước", "10", "8"));
        var preview = await fixture.PreviewAsync(staleFile);
        File.AppendAllText(staleFile, Environment.NewLine + "");
        var stale = await fixture.CreateService().ImportAsync(new ProductImportRequest(staleFile, preview, ProductImportDuplicatePolicy.Error));
        Assert.Contains(stale.Issues, issue => issue.Code == "PREVIEW_STALE");
        await using var verify = fixture.CreateContext();
        Assert.DoesNotContain(await verify.Products.Select(product => product.Code).ToArrayAsync(), code => code == "IMP-014");
    }

    [Fact]
    public async Task Cancellation_before_start_does_not_mutate_database()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var file = fixture.WriteCsv("cancel", Row("IMP-015", "089000000015", "Bị hủy", "10", "8"));
        var preview = await fixture.PreviewAsync(file);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.CreateService().ImportAsync(
                new ProductImportRequest(file, preview, ProductImportDuplicatePolicy.Error),
                cancellation.Token));
        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.Products.ToArrayAsync());
    }

    private static string Row(string code, string barcode, string name, string sale, string cost, string stock = "0", string minimum = "0", string status = "Đang bán", string notes = "") =>
        string.Join(',', code, barcode, name, "Đồ uống", "Cái", sale, cost, stock, minimum, status, notes);

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly string _root;
        private readonly string _databasePath;
        private PosDbContext? _serviceContext;
        private readonly CurrentUserService _currentUser = new();
        public readonly DateTimeOffset Now = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
        public int CategoryId { get; private set; }
        public int RememberedProductId { get; private set; }
        public int RememberedStock { get; private set; }

        private ImportFixture(string root, string databasePath)
        {
            _root = root;
            _databasePath = databasePath;
        }

        public static async Task<ImportFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"POS-Enterprise-ProductImport-{Guid.NewGuid():N}");
            var databasePath = Path.Combine(root, "import.db");
            await PortableDevelopmentDatabase.CreateMigratedAsync(databasePath, seedEmployee: true);
            var fixture = new ImportFixture(root, databasePath);
            await using var context = fixture.CreateContext();
            var category = new Category("Đồ uống", 1, fixture.Now);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            fixture.CategoryId = category.Id;
            var user = await context.Users.SingleAsync(user => user.Username == "portable-admin");
            fixture._currentUser.SetCurrentUser(new AuthenticatedUserDto(user.Id, user.Username, user.FullName, user.Role, fixture.Now));
            return fixture;
        }

        public PosDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite($"Data Source={_databasePath};Foreign Keys=True;Pooling=False")
                .Options;
            return new PosDbContext(options);
        }

        public string WriteCsv(string name, params string[] rows) =>
            WriteCsvWithHeaders(name, ["ProductCode", "Barcode", "Tên", "Danh mục", "Đơn vị tính", "Giá bán", "Giá vốn", "Tồn đầu", "Tồn tối thiểu", "Trạng thái", "Ghi chú"], rows);

        public string WriteCsvWithHeaders(string name, string[] headers, params string[] rows)
        {
            var path = Path.Combine(_root, name + ".csv");
            File.WriteAllText(path, string.Join(',', headers) + Environment.NewLine + string.Join(Environment.NewLine, rows));
            return path;
        }

        public async Task<ProductImportPreviewResult> PreviewAsync(string file, ProductImportPreviewOptions? options = null)
        {
            var parser = new POS.Infrastructure.ProductImports.ProductImportPreviewService();
            var references = new ProductImportReferenceData(
                new Dictionary<string, int> { ["Đồ uống"] = CategoryId },
                new HashSet<string>(["Cái"], StringComparer.OrdinalIgnoreCase));
            return await parser.PreviewAsync(file, options is null
                ? new ProductImportPreviewOptions(References: references)
                : options with { References = references });
        }

        public ProductImportService CreateService()
        {
            _serviceContext?.Dispose();
            _serviceContext = CreateContext();
            return new ProductImportService(
                new ProductRepository(_serviceContext),
                new CategoryRepository(_serviceContext),
                new InventoryMovementRepository(_serviceContext),
                new SecurityAuditRepository(_serviceContext),
                new EfUnitOfWork(_serviceContext),
                new POS.Infrastructure.ProductImports.ProductImportPreviewService(),
                new FixedClock(Now),
                _currentUser,
                new PermissionService(_currentUser));
        }

        public AuthorizedProductImportService CreateAuthorizedService() =>
            new AuthorizedProductImportService(CreateService(), new PermissionService(_currentUser));

        public async Task<ProductImportResult> ImportAsync(string name, string row, ProductImportDuplicatePolicy policy)
        {
            var file = WriteCsv(name, row);
            return await ImportFileAsync(file, policy);
        }

        public async Task<ProductImportResult> ImportFileAsync(string file, ProductImportDuplicatePolicy policy)
        {
            var preview = await PreviewAsync(file);
            return await CreateService().ImportAsync(new ProductImportRequest(file, preview, policy));
        }

        public void RememberProduct(int id, int stock)
        {
            RememberedProductId = id;
            RememberedStock = stock;
        }

        public void SetRole(Role role)
        {
            var user = _currentUser.CurrentUser!;
            _currentUser.SetCurrentUser(new AuthenticatedUserDto(user.Id, user.Username, user.FullName, role, Now));
        }

        public ValueTask DisposeAsync()
        {
            _serviceContext?.Dispose();
            PortableDevelopmentDatabase.DeleteOwnedScenario(_root);
            return ValueTask.CompletedTask;
        }

        private sealed class FixedClock(DateTimeOffset value) : IClock
        {
            public DateTimeOffset UtcNow => value;
        }
    }
}
