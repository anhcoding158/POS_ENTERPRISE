using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Products;

namespace POS.Application.Services;

/// <summary>
/// Lớp bảo vệ phân quyền cho toàn bộ use case sản phẩm.
///
/// Mục đích:
/// - UI không phải hàng rào bảo mật duy nhất;
/// - mọi lời gọi IProductService đều phải qua kiểm tra quyền;
/// - ngăn ViewModel, dialog hoặc module khác gọi vòng qua giao diện.
/// </summary>
public sealed class AuthorizedProductService :
    IProductService
{
    private readonly IProductService
        _innerService;

    private readonly IPermissionService
        _permissionService;

    public AuthorizedProductService(
        IProductService innerService,
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

    public Task<
        Result<PagedResult<ProductListItemDto>>>
        SearchAsync(
            ProductSearchRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ViewProductCatalog);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<
                    PagedResult<ProductListItemDto>>(
                    authorization.Error));
        }

        return _innerService.SearchAsync(
            request,
            cancellationToken);
    }

    public Task<Result<ProductDetailsDto>>
        GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ViewProductCatalog);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    authorization.Error));
        }

        return _innerService.GetByIdAsync(
            productId,
            cancellationToken);
    }

    public Task<Result<ProductDetailsDto>>
        CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageProducts);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    authorization.Error));
        }

        return _innerService.CreateAsync(
            request,
            cancellationToken);
    }

    public Task<Result<ProductDetailsDto>>
        UpdateAsync(
            UpdateProductRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageProducts);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    authorization.Error));
        }

        return _innerService.UpdateAsync(
            request,
            cancellationToken);
    }

    public Task<Result> SetActiveStateAsync(
        int productId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ManageProducts);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure(
                    authorization.Error));
        }

        return _innerService.SetActiveStateAsync(
            productId,
            isActive,
            cancellationToken);
    }
}