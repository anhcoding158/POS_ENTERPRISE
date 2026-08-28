using POS.Application.Common;
using POS.Application.Authorization;

namespace POS.Application.Abstractions.Services;

public interface IRolePermissionManagementService
{
    Task<Result<IReadOnlyList<RolePermissionSnapshot>>> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
