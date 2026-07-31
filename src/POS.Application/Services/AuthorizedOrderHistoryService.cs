using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;

namespace POS.Application.Services;

public sealed class AuthorizedOrderHistoryService : IOrderHistoryService
{
    private readonly IOrderHistoryService _inner;
    private readonly IPermissionService _permissions;

    public AuthorizedOrderHistoryService(
        IOrderHistoryService inner,
        IPermissionService permissions)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public Task<Result<PagedResult<OrderHistoryListItemDto>>> SearchAsync(
        OrderHistorySearchRequest request,
        CancellationToken cancellationToken = default) =>
        Authorize(
            () => _inner.SearchAsync(request, cancellationToken));

    public Task<Result<OrderHistoryDetailsDto>> GetDetailsAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        Authorize(
            () => _inner.GetDetailsAsync(orderId, cancellationToken));

    public Task<Result<ReceiptRequest>> GetReprintReceiptAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        Authorize(
            () => _inner.GetReprintReceiptAsync(orderId, cancellationToken));

    private Task<Result<T>> Authorize<T>(
        Func<Task<Result<T>>> operation)
    {
        var authorization = _permissions.Authorize(
            SystemCapability.ViewReports);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : operation();
    }
}
