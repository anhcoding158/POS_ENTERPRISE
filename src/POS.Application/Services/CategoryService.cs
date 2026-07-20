using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Categories;

namespace POS.Application.Services;

/// <summary>
/// Triển khai use case đọc danh mục sản phẩm.
/// </summary>
public sealed class CategoryService :
    ICategoryService
{
    private readonly ICategoryRepository
        _categoryRepository;

    public CategoryService(
        ICategoryRepository categoryRepository)
    {
        _categoryRepository =
            categoryRepository ??
            throw new ArgumentNullException(
                nameof(categoryRepository));
    }

    public async Task<
        Result<IReadOnlyList<CategoryOptionDto>>>
        ListActiveAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var categories =
            await _categoryRepository.ListActiveAsync(
                cancellationToken);

        var items =
            categories
                .OrderBy(
                    category =>
                        category.DisplayOrder)
                .ThenBy(
                    category =>
                        category.Name)
                .ThenBy(
                    category =>
                        category.Id)
                .Select(
                    category =>
                        new CategoryOptionDto(
                            category.Id,
                            category.Name,
                            category.DisplayOrder))
                .ToArray();

        return Result.Success<
            IReadOnlyList<CategoryOptionDto>>(
                items);
    }
}