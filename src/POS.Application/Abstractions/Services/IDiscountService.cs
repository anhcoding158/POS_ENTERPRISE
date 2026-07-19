using POS.Application.Common;
using POS.Application.DTOs.Discounts;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case quản lý và kiểm tra chương trình giảm giá.
/// </summary>
public interface IDiscountService
{
    Task<Result<PagedResult<DiscountListItemDto>>> SearchAsync(
        DiscountSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DiscountDetailsDto>> GetByIdAsync(
        int discountId,
        CancellationToken cancellationToken = default);

    Task<Result<DiscountDetailsDto>> GetByCodeAsync(
        string discountCode,
        CancellationToken cancellationToken = default);

    Task<Result<DiscountDetailsDto>> CreateAsync(
        CreateDiscountRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveStateAsync(
        int discountId,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tính thử số tiền được giảm mà chưa tăng UsedCount.
    /// </summary>
    Task<Result<long>> CalculateDiscountAsync(
        string discountCode,
        long orderSubtotal,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}