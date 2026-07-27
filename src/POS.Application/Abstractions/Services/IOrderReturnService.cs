using POS.Application.Common;
using POS.Application.DTOs.Orders;

namespace POS.Application.Abstractions.Services;

public interface IOrderReturnService
{
    Task<Result<OrderReturnResultDto>> ProcessAsync(
        OrderReturnRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OrderReturnSummaryDto>>> GetReturnsByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnableOrderDto>> GetReturnableOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
