using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu tài khoản người dùng.
///
/// Repository quản lý truy xuất entity.
/// IUnitOfWork chịu trách nhiệm lưu thay đổi.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Kiểm tra database đã có tài khoản hay chưa.
    ///
    /// Dùng cho quy trình thiết lập Administrator
    /// trong lần chạy đầu tiên.
    /// </summary>
    Task<bool> AnyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy người dùng theo khóa chính.
    ///
    /// Entity trả về được tracking để có thể
    /// cập nhật trong cùng một use case.
    /// </summary>
    Task<User?> GetByIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm người dùng theo username đã chuẩn hóa.
    ///
    /// Ví dụ:
    /// admin → ADMIN
    /// </summary>
    Task<User?> GetByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm người dùng có phân trang.
    /// </summary>
    Task<PagedResult<User>> SearchAsync(
        string? searchTerm,
        Role? role,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra username chuẩn hóa đã tồn tại.
    ///
    /// excludeUserId dùng khi cập nhật để bỏ qua
    /// chính bản ghi hiện tại.
    /// </summary>
    Task<bool> NormalizedUsernameExistsAsync(
        string normalizedUsername,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm User vào persistence context.
    ///
    /// Không tự gọi SaveChanges.
    /// </summary>
    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}