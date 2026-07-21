using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;

namespace POS.Application.Services;

/// <summary>
/// Lớp bảo vệ các nghiệp vụ tồn kho bằng quyền của
/// người dùng đang đăng nhập.
///
/// InventoryService thật chỉ được gọi sau khi
/// việc phân quyền đã thành công.
/// </summary>
public sealed class AuthorizedInventoryService :
    IInventoryService
{
    private readonly IInventoryService
        _innerService;

    private readonly IPermissionService
        _permissionService;

    public AuthorizedInventoryService(
        IInventoryService innerService,
        IPermissionService permissionService)
    {
        _innerService =
            innerService ??
            throw new ArgumentNullException(
                nameof(innerService));

        _permissionService =
            permissionService ??
            throw new ArgumentNullException(
                nameof(permissionService));
    }

    /// <summary>
    /// Điều chỉnh tồn kho là thao tác làm thay đổi dữ liệu,
    /// vì vậy bắt buộc phải có quyền AdjustInventory.
    /// </summary>
    public Task<Result<InventoryAdjustmentResultDto>>
        AdjustAsync(
            InventoryAdjustmentRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorizationResult =
            _permissionService.Authorize(
                SystemPermission.AdjustInventory);

        if (authorizationResult.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<
                    InventoryAdjustmentResultDto>(
                        authorizationResult.Error));
        }

        return _innerService.AdjustAsync(
            request,
            cancellationToken);
    }

    /// <summary>
    /// Tra cứu lịch sử tồn kho bắt buộc phải có
    /// quyền ViewInventoryHistory.
    /// </summary>
    public Task<
        Result<PagedResult<InventoryMovementDto>>>
        SearchAsync(
            InventorySearchRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorizationResult =
            _permissionService.Authorize(
                SystemPermission
                    .ViewInventoryHistory);

        if (authorizationResult.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<
                    PagedResult<
                        InventoryMovementDto>>(
                            authorizationResult.Error));
        }

        return _innerService.SearchAsync(
            request,
            cancellationToken);
    }
}