using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu Order aggregate.
///
/// Khi lấy một Order để xử lý nghiệp vụ, Infrastructure phải tải:
/// - Order;
/// - OrderItem;
/// - OrderItemModifier.
///
/// Không được trả về một aggregate bị thiếu dữ liệu.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Lấy đầy đủ Order aggregate theo khóa chính.
    /// </summary>
    Task<Order?> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy đầy đủ Order aggregate theo mã đơn hàng.
    /// </summary>
    Task<Order?> GetByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm đơn hàng có phân trang.
    ///
    /// searchTerm có thể tìm theo:
    /// - mã đơn;
    /// - tên khách hàng;
    /// - số điện thoại khách hàng.
    /// </summary>
    Task<PagedResult<Order>> SearchAsync(
        string? searchTerm,
        OrderStatus? status,
        int? customerId,
        int? cashierUserId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã đơn hàng đã tồn tại hay chưa.
    /// </summary>
    Task<bool> CodeExistsAsync(
        string orderCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm Order aggregate mới nhưng chưa lưu database.
    ///
    /// Các OrderItem và OrderItemModifier thuộc aggregate
    /// sẽ được lưu theo quan hệ của Order.
    /// </summary>
    Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default);
}