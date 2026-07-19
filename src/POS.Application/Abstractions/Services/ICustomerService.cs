using POS.Application.Common;
using POS.Application.DTOs.Customers;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case quản lý khách hàng.
/// </summary>
public interface ICustomerService
{
    Task<Result<PagedResult<CustomerListItemDto>>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailsDto>> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailsDto>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailsDto>> UpdateAsync(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveStateAsync(
        int customerId,
        bool isActive,
        CancellationToken cancellationToken = default);
}