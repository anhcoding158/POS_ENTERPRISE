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
        var authorization = permissions.Authorize(SystemPermission.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<OrderReturnResultDto>(authorization.Error))
            : inner.ProcessAsync(request, cancellationToken);
    }

    public Task<Result<IReadOnlyList<OrderReturnSummaryDto>>> GetReturnsByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var authorization = permissions.Authorize(SystemPermission.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyList<OrderReturnSummaryDto>>(authorization.Error))
            : inner.GetReturnsByOrderIdAsync(orderId, cancellationToken);
    }

    public Task<Result<ReturnableOrderDto>> GetReturnableOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var authorization = permissions.Authorize(SystemPermission.ProcessReturns);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<ReturnableOrderDto>(authorization.Error))
            : inner.GetReturnableOrderAsync(orderId, cancellationToken);
    }
}
