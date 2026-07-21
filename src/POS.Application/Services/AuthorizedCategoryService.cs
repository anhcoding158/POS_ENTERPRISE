using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Categories;

namespace POS.Application.Services;

/// <summary>
/// Lớp bảo vệ phân quyền cho toàn bộ use case danh mục.
///
/// Mọi lời gọi ICategoryService từ ViewModel, dialog
/// hoặc module khác đều phải đi qua lớp này.
/// </summary>
public sealed class AuthorizedCategoryService :
    ICategoryService
{
    private readonly ICategoryService
        _innerService;

    private readonly IPermissionService
        _permissionService;

    public AuthorizedCategoryService(
        ICategoryService innerService,
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
    /// Danh sách danh mục hoạt động được dùng cho:
    /// - ComboBox sản phẩm;
    /// - màn bán hàng;
    /// - các danh sách lựa chọn nghiệp vụ.
    ///
    /// Người có quyền xem danh mục sản phẩm đều được đọc.
    /// </summary>
    public Task<
        Result<IReadOnlyList<CategoryOptionDto>>>
        ListActiveAsync(
            CancellationToken cancellationToken = default)
    {
        var authorization =
            _permissionService.Authorize(
                SystemPermission.ViewProductCatalog);

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<
                    IReadOnlyList<CategoryOptionDto>>(
                        authorization.Error));
        }

        return _innerService.ListActiveAsync(
            cancellationToken);
    }

    /// <summary>
    /// Tìm kiếm toàn bộ danh mục là chức năng quản trị.
    /// </summary>
    public Task<
        Result<PagedResult<CategoryListItemDto>>>
        SearchAsync(
            CategorySearchRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            AuthorizeCategoryManagement();

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<
                    PagedResult<CategoryListItemDto>>(
                        authorization.Error));
        }

        return _innerService.SearchAsync(
            request,
            cancellationToken);
    }

    /// <summary>
    /// Lấy chi tiết danh mục phục vụ màn chỉnh sửa,
    /// vì vậy cần quyền quản lý danh mục.
    /// </summary>
    public Task<Result<CategoryDetailsDto>>
        GetByIdAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            AuthorizeCategoryManagement();

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    authorization.Error));
        }

        return _innerService.GetByIdAsync(
            categoryId,
            cancellationToken);
    }

    public Task<Result<CategoryDetailsDto>>
        CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            AuthorizeCategoryManagement();

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    authorization.Error));
        }

        return _innerService.CreateAsync(
            request,
            cancellationToken);
    }

    public Task<Result<CategoryDetailsDto>>
        UpdateAsync(
            UpdateCategoryRequest request,
            CancellationToken cancellationToken = default)
    {
        var authorization =
            AuthorizeCategoryManagement();

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    authorization.Error));
        }

        return _innerService.UpdateAsync(
            request,
            cancellationToken);
    }

    public Task<Result> SetActiveStateAsync(
        int categoryId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var authorization =
            AuthorizeCategoryManagement();

        if (authorization.IsFailure)
        {
            return Task.FromResult(
                Result.Failure(
                    authorization.Error));
        }

        return _innerService.SetActiveStateAsync(
            categoryId,
            isActive,
            cancellationToken);
    }

    private Result AuthorizeCategoryManagement()
    {
        return _permissionService.Authorize(
            SystemPermission.ManageCategories);
    }
}