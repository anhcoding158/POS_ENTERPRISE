using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu khách hàng.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Lấy khách hàng theo khóa chính.
    /// </summary>
    Task<Customer?> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy khách hàng theo mã khách hàng.
    /// </summary>
    Task<Customer?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy khách hàng theo số điện thoại đã chuẩn hóa.
    ///
    /// Ví dụ:
    /// +84 912 345 678 -> 0912345678
    /// </summary>
    Task<Customer?> GetByNormalizedPhoneNumberAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm và phân trang khách hàng.
    ///
    /// searchTerm có thể tìm theo:
    /// - mã khách hàng;
    /// - họ tên;
    /// - số điện thoại.
    /// </summary>
    Task<PagedResult<Customer>> SearchAsync(
        string? searchTerm,
        CustomerTier? tier,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã khách hàng đã tồn tại hay chưa.
    /// </summary>
    Task<bool> CodeExistsAsync(
        string code,
        int? excludeCustomerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra số điện thoại chuẩn hóa đã tồn tại hay chưa.
    /// </summary>
    Task<bool> NormalizedPhoneNumberExistsAsync(
        string normalizedPhoneNumber,
        int? excludeCustomerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm khách hàng mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        Customer customer,
        CancellationToken cancellationToken = default);
}