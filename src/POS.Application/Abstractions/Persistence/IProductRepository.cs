using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu sản phẩm.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Lấy sản phẩm theo khóa chính.
    /// </summary>
    Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy sản phẩm theo mã sản phẩm.
    ///
    /// Repository chịu trách nhiệm chuẩn hóa mã trước khi tìm.
    /// </summary>
    Task<Product?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy sản phẩm theo mã vạch.
    /// </summary>
    Task<Product?> GetByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm và phân trang sản phẩm.
    ///
    /// searchTerm có thể được dùng để tìm theo:
    /// - mã sản phẩm;
    /// - mã vạch;
    /// - tên sản phẩm.
    /// </summary>
    Task<PagedResult<Product>> SearchAsync(
        string? searchTerm,
        int? categoryId,
        bool? isActive,
        bool? isLowStock,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã sản phẩm đã tồn tại hay chưa.
    /// </summary>
    Task<bool> CodeExistsAsync(
        string code,
        int? excludeProductId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã vạch đã tồn tại hay chưa.
    ///
    /// Barcode null hoặc chuỗi trống không được truyền vào đây.
    /// </summary>
    Task<bool> BarcodeExistsAsync(
        string barcode,
        int? excludeProductId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm sản phẩm mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default);
}