using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Suppliers;
using POS.Application.Services;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SupplierAuthorizationTests
{
    [Theory]
    [InlineData(Role.Administrator, true, true)]
    [InlineData(Role.Manager, true, true)]
    [InlineData(Role.InventoryStaff, true, false)]
    [InlineData(Role.Cashier, false, false)]
    public void Supplier_capability_matrix_is_explicit(Role role, bool canView, bool canManage)
    {
        Assert.Equal(canView, RolePermissionPolicy.HasPermission(role, SystemCapability.ViewSuppliers));
        Assert.Equal(canManage, RolePermissionPolicy.HasPermission(role, SystemCapability.ManageSuppliers));
    }

    [Fact]
    public void Supplier_capabilities_have_one_catalog_definition()
    {
        Assert.Equal(1, PermissionCatalog.All.Count(item => item.Capability == SystemCapability.ViewSuppliers));
        Assert.Equal(1, PermissionCatalog.All.Count(item => item.Capability == SystemCapability.ManageSuppliers));
        Assert.Equal("xem nhà cung cấp", RolePermissionPolicy.GetDisplayName(SystemCapability.ViewSuppliers));
        Assert.Equal("quản lý nhà cung cấp", RolePermissionPolicy.GetDisplayName(SystemCapability.ManageSuppliers));
    }

    [Fact]
    public async Task Unauthorized_write_does_not_call_inner_service()
    {
        var inner = new SpySupplierService();
        var authorized = new AuthorizedSupplierService(inner, new FakePermissionService(view: true, manage: false));
        var result = await authorized.CreateAsync(new CreateSupplierRequest("NCC01", "Nhà cung cấp"));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Forbidden, result.AppError.Code);
        Assert.Equal(0, inner.WriteCalls);
    }

    private sealed class FakePermissionService(bool view, bool manage) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) =>
            permission == SystemCapability.ViewSuppliers ? view :
            permission == SystemCapability.ManageSuppliers && manage;
        public Result Authorize(SystemCapability permission) =>
            HasPermission(permission)
                ? Result.Success()
                : Result.Failure(new AppError(ErrorCodes.General.Forbidden, "Forbidden"));
    }

    private sealed class SpySupplierService : ISupplierService
    {
        public int WriteCalls { get; private set; }
        public Task<Result<PagedResult<SupplierListItemDto>>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Result.Failure<PagedResult<SupplierListItemDto>>(new AppError("TEST", "not used")));
        public Task<Result<SupplierDetailsDto>> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Failure<SupplierDetailsDto>(new AppError("TEST", "not used")));
        public Task<Result<SupplierDetailsDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default) { WriteCalls++; return Task.FromResult(Result.Failure<SupplierDetailsDto>(new AppError("TEST", "not used"))); }
        public Task<Result<SupplierDetailsDto>> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default) { WriteCalls++; return Task.FromResult(Result.Failure<SupplierDetailsDto>(new AppError("TEST", "not used"))); }
        public Task<Result> SetActiveStateAsync(SetSupplierActiveStateRequest request, CancellationToken cancellationToken = default) { WriteCalls++; return Task.FromResult(Result.Failure(new AppError("TEST", "not used"))); }
    }
}
