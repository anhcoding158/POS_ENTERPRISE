using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private const string LikeEscapeCharacter = "\\";
    private readonly PosDbContext _dbContext;

    public EmployeeRepository(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Employee?> GetByIdWithAccountAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return employeeId <= 0
            ? Task.FromResult<Employee?>(null)
            : _dbContext.Employees
                .Include(employee => employee.LoginAccount)
                .SingleOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
    }

    public async Task<PagedResult<Employee>> SearchAsync(
        string? searchTerm,
        EmployeeStatus? employeeStatus,
        AccountStatus? accountStatus,
        Role? role,
        int pageNumber,
        int pageSize,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0 || pageSize > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Phân trang không hợp lệ.");
        }

        if (employeeStatus.HasValue && !Enum.IsDefined(employeeStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(employeeStatus));
        }

        if (accountStatus.HasValue && !Enum.IsDefined(accountStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(accountStatus));
        }

        if (role.HasValue && !Enum.IsDefined(role.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        IQueryable<Employee> query = _dbContext.Employees
            .Include(employee => employee.LoginAccount)
            .AsNoTracking();

        if (employeeStatus == EmployeeStatus.Active)
            query = query.Where(employee => employee.IsActive);
        else if (employeeStatus == EmployeeStatus.Inactive)
            query = query.Where(employee => !employee.IsActive);

        if (role.HasValue)
            query = query.Where(employee => employee.LoginAccount != null && employee.LoginAccount.Role == role.Value);

        if (accountStatus.HasValue)
        {
            query = accountStatus.Value switch
            {
                AccountStatus.NoAccount => query.Where(employee => employee.LoginAccount == null),
                AccountStatus.Disabled => query.Where(employee => employee.LoginAccount != null && !employee.LoginAccount.IsActive),
                AccountStatus.Locked => query.Where(employee => employee.LoginAccount != null && employee.LoginAccount.IsActive &&
                    (employee.LoginAccount.IsManuallyLocked ||
                     (employee.LoginAccount.LockedUntilUtc.HasValue && employee.LoginAccount.LockedUntilUtc.Value > utcNow))),
                AccountStatus.ForcePasswordChange => query.Where(employee => employee.LoginAccount != null && employee.LoginAccount.IsActive &&
                    !employee.LoginAccount.IsManuallyLocked &&
                    (!employee.LoginAccount.LockedUntilUtc.HasValue || employee.LoginAccount.LockedUntilUtc.Value <= utcNow) &&
                    employee.LoginAccount.ForcePasswordChange),
                AccountStatus.Active => query.Where(employee => employee.LoginAccount != null && employee.LoginAccount.IsActive &&
                    !employee.LoginAccount.IsManuallyLocked &&
                    (!employee.LoginAccount.LockedUntilUtc.HasValue || employee.LoginAccount.LockedUntilUtc.Value <= utcNow) &&
                    !employee.LoginAccount.ForcePasswordChange),
                _ => query
            };
        }

        var term = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        if (term is not null)
        {
            var escaped = EscapeLikePattern(term);
            var pattern = $"%{escaped}%";
            query = query.Where(employee =>
                EF.Functions.Like(employee.EmployeeCode, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(employee.FullName, pattern, LikeEscapeCharacter) ||
                (employee.PhoneNumber != null && EF.Functions.Like(employee.PhoneNumber, pattern, LikeEscapeCharacter)) ||
                (employee.LoginAccount != null && EF.Functions.Like(employee.LoginAccount.Username, pattern, LikeEscapeCharacter)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(employee => employee.IsActive)
            .ThenBy(employee => employee.FullName)
            .ThenBy(employee => employee.EmployeeCode)
            .ThenBy(employee => employee.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<Employee>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> NormalizedEmployeeCodeExistsAsync(
        string normalizedEmployeeCode,
        int? excludeEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = normalizedEmployeeCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0) return Task.FromResult(false);

        var query = _dbContext.Employees.AsNoTracking()
            .Where(employee => employee.NormalizedEmployeeCode == normalized);
        if (excludeEmployeeId.HasValue)
            query = query.Where(employee => employee.Id != excludeEmployeeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountUsableAdministratorsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees
            .Where(employee => employee.IsActive && employee.LoginAccount != null &&
                employee.LoginAccount.IsActive &&
                employee.LoginAccount.Role == Role.Administrator &&
                !employee.LoginAccount.IsManuallyLocked &&
                (!employee.LoginAccount.LockedUntilUtc.HasValue || employee.LoginAccount.LockedUntilUtc.Value <= utcNow))
            .CountAsync(cancellationToken);
    }

    public Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);
        return _dbContext.Employees.AddAsync(employee, cancellationToken).AsTask();
    }

    private static string EscapeLikePattern(string value) => value
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
        .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
        .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);
}
