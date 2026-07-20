using POS.Application.Common;
using POS.Application.DTOs.Inventory;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case quản lý tồn kho.
///
/// Mọi thay đổi tồn kho phải đi qua interface này,
/// không chỉnh trực tiếp StockQuantity từ WPF.
/// </summary>
public interface IInventoryService
{
    Task<Result<InventoryAdjustmentResultDto>> AdjustAsync(
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<InventoryMovementDto>>> SearchAsync(
        InventorySearchRequest request,
        CancellationToken cancellationToken = default);
}