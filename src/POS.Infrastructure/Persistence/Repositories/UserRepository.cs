using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của IUserRepository.
/// </summary>
public sealed class UserRepository :
    IUserRepository
{
    private const string LikeEscapeCharacter =
        "\\";

    private readonly PosDbContext
        _dbContext;

    public UserRepository(
        PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public Task<User?> GetByIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Task.FromResult<User?>(
                null);
        }

        /*
         * Không dùng AsNoTracking.
         *
         * AuthService cần cập nhật:
         * - FailedLoginAttempts;
         * - LockedUntilUtc;
         * - LastLoginAtUtc.
         */
        return _dbContext.Users
            .SingleOrDefaultAsync(
                user =>
                    user.Id == userId,
                cancellationToken);
    }

    public Task<User?>
        GetByNormalizedUsernameAsync(
            string normalizedUsername,
            CancellationToken cancellationToken = default)
    {
        var normalized =
            NormalizeUsername(
                normalizedUsername);

        if (normalized.Length == 0)
        {
            return Task.FromResult<User?>(
                null);
        }

        /*
         * Entity phải được tracking vì đăng nhập
         * có thể thay đổi trạng thái khóa và audit.
         */
        return _dbContext.Users
            .SingleOrDefaultAsync(
                user =>
                    user.NormalizedUsername ==
                    normalized,
                cancellationToken);
    }

    public async Task<PagedResult<User>>
        SearchAsync(
            string? searchTerm,
            Role? role,
            bool? isActive,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        ValidatePaging(
            pageNumber,
            pageSize);

        if (role.HasValue &&
            !Enum.IsDefined(
                role.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "Vai trò tìm kiếm không hợp lệ.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        IQueryable<User> query =
            _dbContext.Users
                .AsNoTracking();

        if (role.HasValue)
        {
            query =
                query.Where(
                    user =>
                        user.Role ==
                        role.Value);
        }

        if (isActive.HasValue)
        {
            query =
                query.Where(
                    user =>
                        user.IsActive ==
                        isActive.Value);
        }

        var normalizedSearchTerm =
            string.IsNullOrWhiteSpace(
                searchTerm)
                ? null
                : searchTerm.Trim();

        if (normalizedSearchTerm is not null)
        {
            var escapedSearchTerm =
                EscapeLikePattern(
                    normalizedSearchTerm);

            var likePattern =
                $"%{escapedSearchTerm}%";

            query =
                query.Where(
                    user =>
                        EF.Functions.Like(
                            user.Username,
                            likePattern,
                            LikeEscapeCharacter)

                        ||

                        EF.Functions.Like(
                            user.FullName,
                            likePattern,
                            LikeEscapeCharacter));
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .OrderByDescending(
                    user =>
                        user.IsActive)
                .ThenBy(
                    user =>
                        user.FullName)
                .ThenBy(
                    user =>
                        user.Username)
                .ThenBy(
                    user =>
                        user.Id)
                .Skip(
                    (pageNumber - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .ToArrayAsync(
                    cancellationToken);

        return new PagedResult<User>(
            items,
            pageNumber,
            pageSize,
            totalCount);
    }

    public Task<bool>
        NormalizedUsernameExistsAsync(
            string normalizedUsername,
            int? excludeUserId = null,
            CancellationToken cancellationToken = default)
    {
        var normalized =
            NormalizeUsername(
                normalizedUsername);

        if (normalized.Length == 0)
        {
            return Task.FromResult(
                false);
        }

        var query =
            _dbContext.Users
                .AsNoTracking()
                .Where(
                    user =>
                        user.NormalizedUsername ==
                        normalized);

        if (excludeUserId.HasValue)
        {
            query =
                query.Where(
                    user =>
                        user.Id !=
                        excludeUserId.Value);
        }

        return query.AnyAsync(
            cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            user);

        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);
    }

    private static string NormalizeUsername(
        string? username)
    {
        return string.IsNullOrWhiteSpace(
                username)
            ? string.Empty
            : username
                .Trim()
                .ToUpperInvariant();
    }

    private static string EscapeLikePattern(
        string value)
    {
        return value
            .Replace(
                LikeEscapeCharacter,
                LikeEscapeCharacter +
                LikeEscapeCharacter,
                StringComparison.Ordinal)
            .Replace(
                "%",
                LikeEscapeCharacter + "%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                LikeEscapeCharacter + "_",
                StringComparison.Ordinal);
    }

    private static void ValidatePaging(
        int pageNumber,
        int pageSize)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Kích thước trang phải lớn hơn 0.");
        }

        if (pageSize > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Kích thước trang tối đa là 200.");
        }
    }
}