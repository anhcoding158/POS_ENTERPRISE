using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;

namespace POS.Application.Services;

/// <summary>
/// Hàng rào phân quyền cho nghiệp vụ bán hàng.
///
/// Giao diện ẩn hoặc khóa nút không phải là biện pháp bảo mật.
/// Mọi Checkout đều phải đi qua decorator này.
/// </summary>
public sealed class AuthorizedCheckoutService :
    ICheckoutService
{
    private readonly ICheckoutService
        _innerService;

    private readonly IPermissionService
        _permissionService;

    public AuthorizedCheckoutService(
        ICheckoutService innerService,
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

    public Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.UseCheckout);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<CheckoutResultDto>(
                    authorization.Error));
        }

        return _innerService.CheckoutAsync(
            request,
            cancellationToken);
    }
}