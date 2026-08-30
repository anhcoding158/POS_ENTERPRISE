using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class BulkProductOperationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Preview_then_commit_updates_prices_once_and_writes_summary_audit()
    {
        var product = NewProduct(1, "SP-001", 100, 200);
        var products = new FakeProductRepository(product);
        var audit = new FakeAuditRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(products, audit, unitOfWork);

        var previewResult = await service.PreviewAsync(new(
            BulkProductOperationType.SetPrices,
            [new(1, product.UpdatedAtUtc)],
            CostPrice: 150,
            SalePrice: 300));

        Assert.True(previewResult.IsSuccess);
        Assert.True(previewResult.Value.CanConfirm);
        Assert.Equal(100, product.CostPrice);
        Assert.Equal(200, product.SalePrice);

        var commitResult = await service.CommitAsync(previewResult.Value);

        Assert.True(commitResult.IsSuccess);
        Assert.True(commitResult.Value.IsCommitted);
        Assert.Equal(1, commitResult.Value.ChangedCount);
        Assert.Equal(150, product.CostPrice);
        Assert.Equal(300, product.SalePrice);
        Assert.Single(audit.Events);
        Assert.True(unitOfWork.Transaction.WasCommitted);
    }

    [Fact]
    public async Task Bulk_minimum_stock_never_mutates_current_stock()
    {
        var product = NewProduct(2, "SP-002", 100, 200, stock: 12);
        var products = new FakeProductRepository(product);
        var service = CreateService(products, new FakeAuditRepository(), new FakeUnitOfWork());
        var preview = (await service.PreviewAsync(new(
            BulkProductOperationType.SetMinimumStock,
            [new(1, product.UpdatedAtUtc)],
            MinimumStock: 8))).Value;

        var result = await service.CommitAsync(preview);

        Assert.True(result.Value.IsCommitted);
        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(8, product.MinimumStock);
    }

    [Fact]
    public async Task Stale_preview_is_rejected_without_mutation()
    {
        var product = NewProduct(3, "SP-003", 100, 200);
        var products = new FakeProductRepository(product);
        var service = CreateService(products, new FakeAuditRepository(), new FakeUnitOfWork());
        var stale = new BulkProductOperationRequest(
            BulkProductOperationType.SetPrices,
            [new(1, product.UpdatedAtUtc.AddMinutes(-1))],
            CostPrice: 150,
            SalePrice: 300);

        var preview = (await service.PreviewAsync(stale)).Value;

        Assert.False(preview.CanConfirm);
        Assert.Equal(100, product.CostPrice);
        Assert.Equal(200, product.SalePrice);
    }

    [Fact]
    public async Task Permission_denial_happens_before_product_read_or_mutation()
    {
        var product = NewProduct(4, "SP-004", 100, 200);
        var products = new FakeProductRepository(product);
        var service = new BulkProductOperationService(
            products,
            new FakeCategoryRepository(),
            new FakeAuditRepository(),
            new FakeUnitOfWork(),
            new FixedClock(),
            new FakeCurrentUserService(),
            new FakePermissionService(false));

        var result = await service.PreviewAsync(new(
            BulkProductOperationType.SetPrices,
            [new(1, product.UpdatedAtUtc)],
            CostPrice: 150,
            SalePrice: 300));

        Assert.True(result.IsFailure);
        Assert.Equal(0, products.ReadCount);
        Assert.Equal(100, product.CostPrice);
    }

    private static BulkProductOperationService CreateService(
        FakeProductRepository products,
        FakeAuditRepository audit,
        FakeUnitOfWork unitOfWork) => new(
            products,
            new FakeCategoryRepository(),
            audit,
            unitOfWork,
            new FixedClock(),
            new FakeCurrentUserService(),
            new FakePermissionService(true));

    private static Product NewProduct(int id, string code, long cost, long sale, int stock = 0)
    {
        _ = id;
        return new Product(1, code, "Sản phẩm test", "Cái", cost, sale, stock, 3, true, false, Now);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now.AddMinutes(1);
    }

    private sealed class FakePermissionService(bool allowed) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => allowed;
        public Result Authorize(SystemCapability permission) => allowed
            ? Result.Success()
            : Result.Failure(new AppError("FORBIDDEN", "Không có quyền."));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly AuthenticatedUserDto _user = new(10, "admin", "Quản trị viên", global::POS.Domain.Enums.Role.Administrator, Now);
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

    private sealed class FakeAuditRepository : ISecurityAuditRepository
    {
        public List<SecurityAuditEvent> Events { get; } = [];
        public Task AddAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public FakeTransaction Transaction { get; } = new();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult<IApplicationTransaction>(Transaction);
    }

    private sealed class FakeTransaction : IApplicationTransaction
    {
        public bool WasCommitted { get; private set; }
        public bool IsCompleted => WasCommitted;
        public Task CommitAsync(CancellationToken cancellationToken = default) { WasCommitted = true; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProductRepository(Product product) : IProductRepository
    {
        public int ReadCount { get; private set; }
        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default) { ReadCount++; return Task.FromResult<Product?>(productId == 1 ? product : null); }
        public Task<Product?> GetByIdReadOnlyAsync(int productId, CancellationToken cancellationToken = default) { ReadCount++; return Task.FromResult<Product?>(productId == 1 ? product : null); }
        public Task ReloadTrackedAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<PagedResult<Product>> SearchAsync(string? searchTerm, int? categoryId, bool? isActive, bool? isLowStock, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<Product>> SearchAsync(string? searchTerm, int? categoryId, bool? isActive, bool? isLowStock, int pageNumber, int pageSize, bool? isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, int? excludeProductId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> BarcodeExistsAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public Task<Category?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default) => Task.FromResult<Category?>(null);
        public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<Category?>(null);
        public Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>([]);
        public Task<PagedResult<Category>> SearchAsync(string? searchTerm, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
