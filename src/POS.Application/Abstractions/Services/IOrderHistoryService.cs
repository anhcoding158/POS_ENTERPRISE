using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;

namespace POS.Application.Abstractions.Services;

public interface IOrderHistoryService
{
    Task<Result<PagedResult<OrderHistoryListItemDto>>> SearchAsync(
        OrderHistorySearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderHistoryDetailsDto>> GetDetailsAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ReceiptRequest>> GetReprintReceiptAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
