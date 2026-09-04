using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Security;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Suppliers;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

public sealed class SupplierService : ISupplierService
{
    private const string BusinessArea = "Nhà cung cấp";
    private const string TargetType = "Nhà cung cấp";
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITerminalIdentityProvider? _terminalIdentityProvider;

    public SupplierService(ISupplierRepository supplierRepository, ISecurityAuditRepository auditRepository, IUnitOfWork unitOfWork, IClock clock, ICurrentUserService currentUserService, ITerminalIdentityProvider? terminalIdentityProvider = null)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _terminalIdentityProvider = terminalIdentityProvider;
    }

    public async Task<Result<PagedResult<SupplierListItemDto>>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var page = await _supplierRepository.SearchAsync(request.SearchTerm, request.IsActive, request.PageNumber, request.PageSize, cancellationToken);
            return Result.Success(page.Map(MapList));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failure<PagedResult<SupplierListItemDto>>(ErrorCodes.General.Validation, exception.Message);
        }
    }

    public async Task<Result<SupplierDetailsDto>> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        if (supplierId <= 0) return Failure<SupplierDetailsDto>(ErrorCodes.General.Validation, "Mã nhà cung cấp phải lớn hơn 0.");
        var supplier = await _supplierRepository.GetByIdReadOnlyAsync(supplierId, cancellationToken);
        return supplier is null
            ? Failure<SupplierDetailsDto>(ErrorCodes.Suppliers.NotFound, "Không tìm thấy nhà cung cấp.")
            : Result.Success(MapDetails(supplier));
    }

    public async Task<Result<SupplierDetailsDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Supplier supplier;
        try
        {
            supplier = new Supplier(request.Code, request.Name, request.TaxCode, request.ContactName, request.PhoneNumber,
                request.EmailAddress, request.Address, request.Notes, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Failure<SupplierDetailsDto>(exception.Code, exception.Message);
        }

        if (await _supplierRepository.NormalizedCodeExistsAsync(supplier.NormalizedCode, cancellationToken: cancellationToken))
            return Failure<SupplierDetailsDto>(ErrorCodes.Suppliers.CodeAlreadyExists, "Mã nhà cung cấp đã tồn tại.");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _supplierRepository.AddAsync(supplier, cancellationToken);
            await _auditRepository.AddAsync(CreateAudit(supplier, SecurityAuditAction.SupplierCreated,
                [new SecurityAuditChange("Mã", null, supplier.Code), new SecurityAuditChange("Tên", null, supplier.Name)]), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(supplier));
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<SupplierDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    public async Task<Result<SupplierDetailsDto>> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SupplierId <= 0) return Failure<SupplierDetailsDto>(ErrorCodes.General.Validation, "Mã nhà cung cấp phải lớn hơn 0.");
        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null) return Failure<SupplierDetailsDto>(ErrorCodes.Suppliers.NotFound, "Không tìm thấy nhà cung cấp.");
        if (!IsExpectedVersion(supplier.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict<SupplierDetailsDto>();

        Supplier candidate;
        try
        {
            candidate = new Supplier(request.Code, request.Name, request.TaxCode, request.ContactName, request.PhoneNumber,
                request.EmailAddress, request.Address, request.Notes, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Failure<SupplierDetailsDto>(exception.Code, exception.Message);
        }

        if (await _supplierRepository.NormalizedCodeExistsAsync(candidate.NormalizedCode, supplier.Id, cancellationToken))
            return Failure<SupplierDetailsDto>(ErrorCodes.Suppliers.CodeAlreadyExists, "Mã nhà cung cấp đã tồn tại.");
        if (SameProfile(supplier, candidate)) return Result.Success(MapDetails(supplier));

        var changes = ChangedFields(supplier, candidate);
        try
        {
            supplier.UpdateProfile(candidate.Code, candidate.Name, candidate.TaxCode, candidate.ContactName, candidate.PhoneNumber,
                candidate.EmailAddress, candidate.Address, candidate.Notes, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Failure<SupplierDetailsDto>(exception.Code, exception.Message);
        }

        return await SaveMutationAsync(supplier, SecurityAuditAction.SupplierUpdated, changes, cancellationToken);
    }

    public async Task<Result> SetActiveStateAsync(SetSupplierActiveStateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SupplierId <= 0) return Failure(ErrorCodes.General.Validation, "Mã nhà cung cấp phải lớn hơn 0.");
        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null) return Failure(ErrorCodes.Suppliers.NotFound, "Không tìm thấy nhà cung cấp.");
        if (!IsExpectedVersion(supplier.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict();
        if (supplier.IsActive == request.IsActive) return Result.Success();

        var wasActive = supplier.IsActive;
        if (request.IsActive) supplier.Activate(_clock.UtcNow); else supplier.Deactivate(_clock.UtcNow);
        return await SaveMutationAsync(supplier,
            request.IsActive ? SecurityAuditAction.SupplierReactivated : SecurityAuditAction.SupplierDeactivated,
            [new SecurityAuditChange("Trạng thái", wasActive ? "Đang hoạt động" : "Ngừng hoạt động", request.IsActive ? "Đang hoạt động" : "Ngừng hoạt động")],
            cancellationToken);
    }

    private async Task<Result<SupplierDetailsDto>> SaveMutationAsync(Supplier supplier, SecurityAuditAction action, IReadOnlyList<SecurityAuditChange> changes, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _auditRepository.AddAsync(CreateAudit(supplier, action, changes), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(supplier));
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<SupplierDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    private SecurityAuditEvent CreateAudit(Supplier supplier, SecurityAuditAction action, IReadOnlyList<SecurityAuditChange> changes) =>
        new(_currentUserService.UserId, null, null, action, "Success", Guid.NewGuid(), _clock.UtcNow,
            _currentUserService.FullName, $"{supplier.Code} — {supplier.Name}", BusinessArea, TargetType,
            _terminalIdentityProvider?.TerminalId ?? "TERM-UNKNOWN", changes);

    private static bool SameProfile(Supplier left, Supplier right) =>
        left.Code == right.Code && left.Name == right.Name && left.TaxCode == right.TaxCode && left.ContactName == right.ContactName &&
        left.PhoneNumber == right.PhoneNumber && left.EmailAddress == right.EmailAddress && left.Address == right.Address && left.Notes == right.Notes;

    private static IReadOnlyList<SecurityAuditChange> ChangedFields(Supplier before, Supplier after)
    {
        var names = new List<string>();
        if (before.Code != after.Code) names.Add("Mã");
        if (before.Name != after.Name) names.Add("Tên");
        if (before.TaxCode != after.TaxCode) names.Add("Mã số thuế");
        if (before.ContactName != after.ContactName) names.Add("Người liên hệ");
        if (before.PhoneNumber != after.PhoneNumber) names.Add("Số điện thoại");
        if (before.EmailAddress != after.EmailAddress) names.Add("Email");
        if (before.Address != after.Address) names.Add("Địa chỉ");
        if (before.Notes != after.Notes) names.Add("Ghi chú");
        return [new SecurityAuditChange("Trường thay đổi", null, string.Join(", ", names))];
    }

    private static SupplierListItemDto MapList(Supplier supplier) => new(supplier.Id, supplier.Code, supplier.Name, supplier.TaxCode, supplier.ContactName, supplier.PhoneNumber, supplier.IsActive, supplier.CreatedAtUtc, supplier.UpdatedAtUtc);
    private static SupplierDetailsDto MapDetails(Supplier supplier) => new(supplier.Id, supplier.Code, supplier.Name, supplier.TaxCode, supplier.ContactName, supplier.PhoneNumber, supplier.EmailAddress, supplier.Address, supplier.Notes, supplier.IsActive, supplier.CreatedAtUtc, supplier.UpdatedAtUtc);
    private static bool IsExpectedVersion(DateTimeOffset actual, DateTimeOffset expected) => expected != default && actual.ToUniversalTime() == expected.ToUniversalTime();
    private static Result<T> Conflict<T>() => Failure<T>(ErrorCodes.Suppliers.ConcurrencyConflict, "Nhà cung cấp đã được thay đổi. Vui lòng tải lại dữ liệu rồi thử lại.");
    private static Result Conflict() => Failure(ErrorCodes.Suppliers.ConcurrencyConflict, "Nhà cung cấp đã được thay đổi. Vui lòng tải lại dữ liệu rồi thử lại.");
    private static string MapConflictCode(PersistenceConflictException exception) => exception.Kind == PersistenceConflictKind.Concurrency ? ErrorCodes.Suppliers.ConcurrencyConflict : exception.Target == PersistenceConflictTargets.SupplierNormalizedCode ? ErrorCodes.Suppliers.CodeAlreadyExists : ErrorCodes.Suppliers.PersistenceConflict;
    private static string MapConflictMessage(PersistenceConflictException exception) => exception.Kind == PersistenceConflictKind.Concurrency ? "Nhà cung cấp đã được thay đổi. Vui lòng tải lại dữ liệu rồi thử lại." : exception.Target == PersistenceConflictTargets.SupplierNormalizedCode ? "Mã nhà cung cấp đã tồn tại." : "Không thể lưu nhà cung cấp do dữ liệu đang xung đột.";
    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new AppError(code, message));
    private static Result Failure(string code, string message) => Result.Failure(new AppError(code, message));
}
