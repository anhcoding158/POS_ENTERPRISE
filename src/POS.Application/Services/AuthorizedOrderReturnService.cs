using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Orders;

namespace POS.Application.Services;

public sealed class AuthorizedOrderReturnService(
    OrderReturnService inner,
    IPermissionService permissions) : IOrderReturnService
{
    public Task<Result<OrderReturnResultDto>> ProcessAsync(
        OrderReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = permissions.Authorize(SystemCapability.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<OrderReturnResultDto>(authorization.AppError))
            : inner.ProcessAsync(request, cancellationToken);
    }

    public Task<Result<IReadOnlyList<OrderReturnSummaryDto>>> GetReturnsByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var authorization = permissions.Authorize(SystemCapability.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyList<OrderReturnSummaryDto>>(authorization.AppError))
            : inner.GetReturnsByOrderIdAsync(orderId, cancellationToken);
    }

    public Task<Result<ReturnableOrderDto>> GetReturnableOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var authorization = permissions.Authorize(SystemCapability.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<ReturnableOrderDto>(authorization.AppError))
            : inner.GetReturnableOrderAsync(orderId, cancellationToken);
    }
}
