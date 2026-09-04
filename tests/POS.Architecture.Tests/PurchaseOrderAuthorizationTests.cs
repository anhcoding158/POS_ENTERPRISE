using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Purchasing;
using POS.Application.Services;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderAuthorizationTests
{
    [Theory]
    [InlineData(Role.Administrator, true, true)]
    [InlineData(Role.Manager, true, true)]
    [InlineData(Role.InventoryStaff, true, false)]
    [InlineData(Role.Cashier, false, false)]
    public void Purchase_order_capability_matrix_is_explicit(Role role, bool canView, bool canManage)
    {
        Assert.Equal(canView, RolePermissionPolicy.HasPermission(role, SystemCapability.ViewPurchaseOrders));
        Assert.Equal(canManage, RolePermissionPolicy.HasPermission(role, SystemCapability.ManagePurchaseOrders));
    }

    [Fact]
    public void Purchase_order_capabilities_have_one_catalog_definition()
    {
        Assert.Equal(1, PermissionCatalog.All.Count(item => item.Capability == SystemCapability.ViewPurchaseOrders));
        Assert.Equal(1, PermissionCatalog.All.Count(item => item.Capability == SystemCapability.ManagePurchaseOrders));
        Assert.Equal("xem Purchase Order", RolePermissionPolicy.GetDisplayName(SystemCapability.ViewPurchaseOrders));
        Assert.Equal("quản lý Purchase Order", RolePermissionPolicy.GetDisplayName(SystemCapability.ManagePurchaseOrders));
    }

    [Fact]
    public async Task Inventory_staff_can_read_but_cannot_mutate_through_decorator()
    {
        var inner = new SpyPurchaseOrderService();
        var authorized = new AuthorizedPurchaseOrderService(
            inner,
            new FakePermissionService(view: true, manage: false));

        var read = await authorized.GetByIdAsync(1);
        var write = await authorized.CancelAsync(new CancelPurchaseOrderRequest(1, "x", DateTimeOffset.UtcNow));

        Assert.Equal("INNER_READ", read.AppError.Code);
        Assert.Equal(ErrorCodes.General.Forbidden, write.AppError.Code);
        Assert.Equal(1, inner.ReadCalls);
        Assert.Equal(0, inner.WriteCalls);
    }

    [Fact]
    public async Task Cashier_cannot_read_or_mutate_through_decorator()
    {
        var inner = new SpyPurchaseOrderService();
        var authorized = new AuthorizedPurchaseOrderService(
            inner,
            new FakePermissionService(view: false, manage: false));

        var result = await authorized.SearchAsync(new PurchaseOrderSearchRequest());

        Assert.Equal(ErrorCodes.General.Forbidden, result.AppError.Code);
        Assert.Equal(0, inner.ReadCalls + inner.WriteCalls);
    }

    private sealed class FakePermissionService(bool view, bool manage) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) =>
            permission == SystemCapability.ViewPurchaseOrders ? view :
            permission == SystemCapability.ManagePurchaseOrders && manage;

        public Result Authorize(SystemCapability permission) =>
            HasPermission(permission)
                ? Result.Success()
                : Result.Failure(new AppError(ErrorCodes.General.Forbidden, "Forbidden"));
    }

    private sealed class SpyPurchaseOrderService : IPurchaseOrderService
    {
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }

        public Task<Result<PagedResult<PurchaseOrderListItemDto>>> SearchAsync(PurchaseOrderSearchRequest request, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(Result.Failure<PagedResult<PurchaseOrderListItemDto>>(new AppError("INNER_READ", "read")));
        }

        public Task<Result<PurchaseOrderDetailsDto>> GetByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(Result.Failure<PurchaseOrderDetailsDto>(new AppError("INNER_READ", "read")));
        }

        public Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default) => Write();
        public Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(UpdateDraftPurchaseOrderRequest request, CancellationToken cancellationToken = default) => Write();
        public Task<Result<PurchaseOrderDetailsDto>> MarkOrderedAsync(MarkPurchaseOrderOrderedRequest request, CancellationToken cancellationToken = default) => Write();
        public Task<Result<PurchaseOrderDetailsDto>> AmendOrderedAsync(AmendOrderedPurchaseOrderRequest request, CancellationToken cancellationToken = default) => Write();
        public Task<Result<PurchaseOrderDetailsDto>> CancelAsync(CancelPurchaseOrderRequest request, CancellationToken cancellationToken = default) => Write();

        private Task<Result<PurchaseOrderDetailsDto>> Write()
        {
            WriteCalls++;
            return Task.FromResult(Result.Failure<PurchaseOrderDetailsDto>(new AppError("INNER_WRITE", "write")));
        }
    }
}
