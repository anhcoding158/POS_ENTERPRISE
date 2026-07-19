using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu tài khoản người dùng.
///
/// Repository quản lý việc truy xuất entity.
/// Việc lưu thay đổi được thực hiện qua IUnitOfWork.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Lấy người dùng theo khóa chính.
    ///
    /// Entity trả về phải có thể được thay đổi và lưu lại
    /// bằng IUnitOfWork trong cùng một use case.
    /// </summary>
    Task<User?> GetByIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm người dùng theo username đã chuẩn hóa.
    ///
    /// Ví dụ:
    /// admin -> ADMIN
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
    /// Kiểm tra username chuẩn hóa đã tồn tại hay chưa.
    ///
    /// excludeUserId được dùng khi cập nhật tài khoản để bỏ qua
    /// chính bản ghi hiện tại.
    /// </summary>
    Task<bool> NormalizedUsernameExistsAsync(
        string normalizedUsername,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm người dùng mới vào persistence context.
    ///
    /// Phương thức này không tự gọi SaveChanges.
    /// </summary>
    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}