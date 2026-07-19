namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Điểm lưu thay đổi và mở transaction cho một use case.
///
/// Repository chỉ thêm, sửa hoặc xóa entity.
/// IUnitOfWork chịu trách nhiệm lưu toàn bộ thay đổi.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}