using POS.Application.Common;

namespace POS.Application.DTOs.Suppliers;

public sealed record SupplierListItemDto(
    int Id,
    string Code,
    string Name,
    string? TaxCode,
    string? ContactName,
    string? PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SupplierDetailsDto(
    int Id,
    string Code,
    string Name,
    string? TaxCode,
    string? ContactName,
    string? PhoneNumber,
    string? EmailAddress,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SupplierSearchRequest(
    string? SearchTerm = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record CreateSupplierRequest(
    string Code,
    string Name,
    string? TaxCode = null,
    string? ContactName = null,
    string? PhoneNumber = null,
    string? EmailAddress = null,
    string? Address = null,
    string? Notes = null);

public sealed record UpdateSupplierRequest(
    int SupplierId,
    string Code,
    string Name,
    string? TaxCode,
    string? ContactName,
    string? PhoneNumber,
    string? EmailAddress,
    string? Address,
    string? Notes,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record SetSupplierActiveStateRequest(
    int SupplierId,
    bool IsActive,
    DateTimeOffset ExpectedUpdatedAtUtc);
