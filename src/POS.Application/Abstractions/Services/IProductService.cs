using POS.Application.Common;
using POS.Application.DTOs.Products;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case quản lý sản phẩm.
///
/// WPF chỉ làm việc với interface và DTO,
/// không truy cập repository hoặc entity trực tiếp.
/// </summary>
public interface IProductService
{
    Task<Result<PagedResult<ProductListItemDto>>> SearchAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDetailsDto>> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDetailsDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDetailsDto>> UpdateAsync(
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveStateAsync(
        int productId,
        bool isActive,
        CancellationToken cancellationToken = default);
}