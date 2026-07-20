using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Domain.Common;
using POS.Domain.Entities;
using System.Globalization;

namespace POS.Application.Services;

/// <summary>
/// Triển khai các use case quản lý danh mục.
///
/// Service chịu trách nhiệm:
/// - tìm kiếm và phân trang;
/// - kiểm tra danh mục tồn tại;
/// - kiểm tra trùng tên;
/// - gọi Domain để áp dụng business rules;
/// - lưu qua IUnitOfWork;
/// - ánh xạ Entity sang DTO;
/// - chuyển persistence conflict thành lỗi Application.
/// </summary>
public sealed class CategoryService :
    ICategoryService
{
    private static readonly StringComparer
    VietnameseNameComparer =
        StringComparer.Create(
            CultureInfo.GetCultureInfo(
                "vi-VN"),
            ignoreCase: true);

    private readonly ICategoryRepository
        _categoryRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IClock
        _clock;

    public CategoryService(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _categoryRepository =
            categoryRepository ??
            throw new ArgumentNullException(
                nameof(categoryRepository));

        _unitOfWork =
            unitOfWork ??
            throw new ArgumentNullException(
                nameof(unitOfWork));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<
        Result<IReadOnlyList<CategoryOptionDto>>>
        ListActiveAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var categories =
            await _categoryRepository
                .ListActiveAsync(
                    cancellationToken);

        var items =
            categories
                .OrderBy(
                    category =>
                        category.DisplayOrder)
                .ThenBy(
                    category =>
                     category.Name,
                    VietnameseNameComparer)
                .ThenBy(
                    category =>
                        category.Id)
                .Select(
                    MapToOption)
                .ToArray();

        return Result.Success<
            IReadOnlyList<CategoryOptionDto>>(
                items);
    }

    public async Task<
        Result<PagedResult<CategoryListItemDto>>>
        SearchAsync(
            CategorySearchRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var categoryPage =
            await _categoryRepository
                .SearchAsync(
                    request.SearchTerm,
                    request.IsActive,
                    request.PageNumber,
                    request.PageSize,
                    cancellationToken);

        var items =
            categoryPage.Items
                .Select(
                    MapToListItem)
                .ToArray();

        var resultPage =
            new PagedResult<CategoryListItemDto>(
                items,
                categoryPage.PageNumber,
                categoryPage.PageSize,
                categoryPage.TotalCount);

        return Result.Success(
            resultPage);
    }

    public async Task<Result<CategoryDetailsDto>>
        GetByIdAsync(
            int categoryId,
            CancellationToken cancellationToken = default)
    {
        if (categoryId <= 0)
        {
            return ValidationFailure<
                CategoryDetailsDto>(
                    "Mã danh mục phải lớn hơn 0.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var category =
            await _categoryRepository
                .GetByIdAsync(
                    categoryId,
                    cancellationToken);

        if (category is null)
        {
            return CategoryNotFound<
                CategoryDetailsDto>();
        }

        return Result.Success(
            MapToDetails(
                category));
    }

    public async Task<Result<CategoryDetailsDto>>
        CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        Category category;

        try
        {
            category =
                new Category(
                    request.Name,
                    request.DisplayOrder,
                    _clock.UtcNow,
                    request.Description);
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                CategoryDetailsDto>(
                    exception);
        }

        /*
         * Pre-check giúp giao diện nhận phản hồi thân thiện.
         *
         * Unique index tại database vẫn là nguồn sự thật
         * khi hai cửa sổ tạo đồng thời.
         */
        if (await _categoryRepository
                .NameExistsAsync(
                    category.Name,
                    cancellationToken:
                        cancellationToken))
        {
            return NameAlreadyExists<
                CategoryDetailsDto>(
                    category.Name);
        }

        await _categoryRepository
            .AddAsync(
                category,
                cancellationToken);

        var saveResult =
            await SaveChangesSafelyAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<
                CategoryDetailsDto>(
                    saveResult.Error);
        }

        return Result.Success(
            MapToDetails(
                category));
    }

    public async Task<Result<CategoryDetailsDto>>
        UpdateAsync(
            UpdateCategoryRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.CategoryId <= 0)
        {
            return ValidationFailure<
                CategoryDetailsDto>(
                    "Mã danh mục phải lớn hơn 0.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var category =
            await _categoryRepository
                .GetByIdAsync(
                    request.CategoryId,
                    cancellationToken);

        if (category is null)
        {
            return CategoryNotFound<
                CategoryDetailsDto>();
        }

        var utcNow =
            _clock.UtcNow;

        Category validatedSnapshot;

        /*
         * Entity tạm chỉ dùng để Domain kiểm tra toàn bộ
         * dữ liệu trước khi entity thật bị thay đổi.
         */
        try
        {
            validatedSnapshot =
                new Category(
                    request.Name,
                    request.DisplayOrder,
                    utcNow,
                    request.Description);
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                CategoryDetailsDto>(
                    exception);
        }

        if (await _categoryRepository
                .NameExistsAsync(
                    validatedSnapshot.Name,
                    category.Id,
                    cancellationToken))
        {
            return NameAlreadyExists<
                CategoryDetailsDto>(
                    validatedSnapshot.Name);
        }

        try
        {
            category.Update(
                validatedSnapshot.Name,
                validatedSnapshot.Description,
                validatedSnapshot.DisplayOrder,
                utcNow);

            if (request.IsActive)
            {
                category.Activate(
                    utcNow);
            }
            else
            {
                category.Deactivate(
                    utcNow);
            }
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                CategoryDetailsDto>(
                    exception);
        }

        var saveResult =
            await SaveChangesSafelyAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<
                CategoryDetailsDto>(
                    saveResult.Error);
        }

        return Result.Success(
            MapToDetails(
                category));
    }

    public async Task<Result>
        SetActiveStateAsync(
            int categoryId,
            bool isActive,
            CancellationToken cancellationToken = default)
    {
        if (categoryId <= 0)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Validation,
                    "Mã danh mục phải lớn hơn 0."));
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var category =
            await _categoryRepository
                .GetByIdAsync(
                    categoryId,
                    cancellationToken);

        if (category is null)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Categories.NotFound,
                    "Không tìm thấy danh mục."));
        }

        if (category.IsActive ==
            isActive)
        {
            return Result.Success();
        }

        var utcNow =
            _clock.UtcNow;

        if (isActive)
        {
            category.Activate(
                utcNow);
        }
        else
        {
            category.Deactivate(
                utcNow);
        }

        return await SaveChangesSafelyAsync(
            cancellationToken);
    }

    private async Task<Result>
        SaveChangesSafelyAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork
                .SaveChangesAsync(
                    cancellationToken);

            return Result.Success();
        }
        catch (
            PersistenceConflictException exception)
        {
            return Result.Failure(
                MapPersistenceConflict(
                    exception));
        }
    }

    private static Error
        MapPersistenceConflict(
            PersistenceConflictException exception)
    {
        if (exception.Kind ==
                PersistenceConflictKind
                    .UniqueConstraint &&
            string.Equals(
                exception.Target,
                PersistenceConflictTargets
                    .CategoryName,
                StringComparison.Ordinal))
        {
            return new Error(
                ErrorCodes.Categories
                    .NameAlreadyExists,
                "Tên danh mục đã tồn tại. " +
                "Vui lòng sử dụng tên khác.");
        }

        if (exception.Kind ==
            PersistenceConflictKind.Concurrency)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                "Danh mục đã được người dùng hoặc cửa sổ khác " +
                "thay đổi. Hãy tải lại dữ liệu rồi thực hiện lại.");
        }

        return new Error(
            ErrorCodes.General.Conflict,
            "Không thể lưu danh mục do dữ liệu đang xung đột. " +
            "Hãy tải lại và thử lại.");
    }

    private static CategoryOptionDto
        MapToOption(
            Category category)
    {
        return new CategoryOptionDto(
            category.Id,
            category.Name,
            category.DisplayOrder);
    }

    private static CategoryListItemDto
        MapToListItem(
            Category category)
    {
        return new CategoryListItemDto(
            category.Id,
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);
    }

    private static CategoryDetailsDto
        MapToDetails(
            Category category)
    {
        return new CategoryDetailsDto(
            category.Id,
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);
    }

    private static Result<TValue>
        CategoryNotFound<TValue>()
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.Categories.NotFound,
                "Không tìm thấy danh mục."));
    }

    private static Result<TValue>
        NameAlreadyExists<TValue>(
            string categoryName)
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.Categories
                    .NameAlreadyExists,
                $"Tên danh mục '{categoryName}' đã tồn tại."));
    }

    private static Result<TValue>
        ValidationFailure<TValue>(
            string message)
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.General.Validation,
                message));
    }

    private static Result<TValue>
        DomainFailure<TValue>(
            DomainException exception)
    {
        return Result.Failure<TValue>(
            new Error(
                exception.Code,
                exception.Message));
    }
}