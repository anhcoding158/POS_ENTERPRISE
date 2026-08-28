using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Audit;

namespace POS.Application.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly ISecurityAuditQueryRepository _repository;
    private readonly IPermissionService _permissions;

    public AuditLogService(ISecurityAuditQueryRepository repository, IPermissionService permissions)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public async Task<Result<PagedResult<AuditListItemDto>>> SearchAsync(AuditSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissions.Authorize(SystemCapability.ViewAuditLog);
        if (authorization.IsFailure) return Result.Failure<PagedResult<AuditListItemDto>>(authorization.AppError);
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc > request.ToUtc)
            return Result.Failure<PagedResult<AuditListItemDto>>(new AppError(ErrorCodes.General.Validation, "Khoảng thời gian không hợp lệ."));
        try { return Result.Success(await _repository.SearchAsync(request, cancellationToken)); }
        catch (ArgumentOutOfRangeException) { return Result.Failure<PagedResult<AuditListItemDto>>(new AppError(ErrorCodes.General.Validation, "Phân trang nhật ký không hợp lệ.")); }
    }

    public async Task<Result<AuditDetailsDto>> GetDetailsAsync(int auditId, CancellationToken cancellationToken = default)
    {
        var authorization = _permissions.Authorize(SystemCapability.ViewAuditLog);
        if (authorization.IsFailure) return Result.Failure<AuditDetailsDto>(authorization.AppError);
        var value = await _repository.GetDetailsAsync(auditId, cancellationToken);
        return value is null
            ? Result.Failure<AuditDetailsDto>(new AppError(ErrorCodes.General.NotFound, "Không tìm thấy hoạt động."))
            : Result.Success(value);
    }
}
