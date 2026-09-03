using POS.Application.Common;
using POS.Application.DTOs.Employees;

namespace POS.Application.Abstractions.Services;

public interface IEmployeeAccountService
{
    Task<Result<PagedResult<EmployeeListItemDto>>> SearchAsync(
        EmployeeSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeSummaryDto>> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> GetAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> UpdateEmployeeAsync(
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> CreateAccountAsync(
        CreateEmployeeAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        ResetEmployeePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetAccountLockAsync(
        SetAccountLockRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetEmployeeActiveAsync(
        SetEmployeeActiveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetAccountActiveAsync(
        SetAccountActiveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ChangeRoleAsync(
        ChangeEmployeeRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> CompletePasswordChangeAsync(
        CompletePasswordChangeRequest request,
        CancellationToken cancellationToken = default);
}
