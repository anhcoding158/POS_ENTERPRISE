using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdWithAccountAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Employee>> SearchAsync(
        string? searchTerm,
        EmployeeStatus? employeeStatus,
        AccountStatus? accountStatus,
        Role? role,
        int pageNumber,
        int pageSize,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> NormalizedEmployeeCodeExistsAsync(
        string normalizedEmployeeCode,
        int? excludeEmployeeId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountUsableAdministratorsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Employee employee,
        CancellationToken cancellationToken = default);
}
