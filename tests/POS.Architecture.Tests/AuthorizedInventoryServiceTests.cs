using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Inventory;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra lớp bảo vệ nghiệp vụ tồn kho.
///
/// Các test phải chứng minh rằng service thật
/// không được gọi khi người dùng không có quyền.
/// </summary>
public sealed class AuthorizedInventoryServiceTests
{
    private static readonly Error
        InnerServiceError =
            new(
                "TEST.INNER_SERVICE_REACHED",
                "Service tồn kho thật đã được gọi.");

    [Fact]
    public async Task Anonymous_user_must_not_adjust_inventory()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                role: null);

        var result =
            await service.AdjustAsync(
                request: null!);

        Assert.True(result.IsFailure);

        Assert.Equal(
            0,
            innerService.AdjustCallCount);
    }

    [Fact]
    public async Task Anonymous_user_must_not_view_inventory_history()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                role: null);

        var result =
            await service.SearchAsync(
                request: null!);

        Assert.True(result.IsFailure);

        Assert.Equal(
            0,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task Cashier_must_not_access_inventory_operations()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                Role.Cashier);

        var adjustResult =
            await service.AdjustAsync(
                request: null!);

        var searchResult =
            await service.SearchAsync(
                request: null!);

        Assert.True(adjustResult.IsFailure);
        Assert.True(searchResult.IsFailure);

        Assert.Equal(
            0,
            innerService.AdjustCallCount);

        Assert.Equal(
            0,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task Inventory_staff_must_reach_inventory_operations()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                Role.InventoryStaff);

        var adjustResult =
            await service.AdjustAsync(
                request: null!);

        var searchResult =
            await service.SearchAsync(
                request: null!);

        Assert.Equal(
            InnerServiceError.Code,
            adjustResult.Error.Code);

        Assert.Equal(
            InnerServiceError.Code,
            searchResult.Error.Code);

        Assert.Equal(
            1,
            innerService.AdjustCallCount);

        Assert.Equal(
            1,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task Manager_must_reach_inventory_operations()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                Role.Manager);

        var adjustResult =
            await service.AdjustAsync(
                request: null!);

        var searchResult =
            await service.SearchAsync(
                request: null!);

        Assert.Equal(
            InnerServiceError.Code,
            adjustResult.Error.Code);

        Assert.Equal(
            InnerServiceError.Code,
            searchResult.Error.Code);

        Assert.Equal(
            1,
            innerService.AdjustCallCount);

        Assert.Equal(
            1,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task Administrator_must_reach_inventory_operations()
    {
        var innerService =
            new RecordingInventoryService();

        var service =
            CreateService(
                innerService,
                Role.Administrator);

        var adjustResult =
            await service.AdjustAsync(
                request: null!);

        var searchResult =
            await service.SearchAsync(
                request: null!);

        Assert.Equal(
            InnerServiceError.Code,
            adjustResult.Error.Code);

        Assert.Equal(
            InnerServiceError.Code,
            searchResult.Error.Code);

        Assert.Equal(
            1,
            innerService.AdjustCallCount);

        Assert.Equal(
            1,
            innerService.SearchCallCount);
    }

    private static AuthorizedInventoryService
        CreateService(
            RecordingInventoryService innerService,
            Role? role)
    {
        var currentUserService =
            new CurrentUserService();

        if (role.HasValue)
        {
            currentUserService.SetCurrentUser(
                new AuthenticatedUserDto(
                    1,
                    $"user-{role.Value}",
                    $"Người dùng {role.Value}",
                    role.Value,
                    DateTimeOffset.UtcNow));
        }

        var permissionService =
            new PermissionService(
                currentUserService);

        return new AuthorizedInventoryService(
            innerService,
            permissionService);
    }

    /// <summary>
    /// Service giả chỉ ghi lại số lần được gọi.
    ///
    /// Khi được gọi, nó trả lỗi TEST để test biết rằng
    /// decorator đã chuyển tiếp thành công.
    /// </summary>
    private sealed class RecordingInventoryService :
        IInventoryService
    {
        public int AdjustCallCount { get; private set; }

        public int SearchCallCount { get; private set; }

        public Task<
            Result<InventoryAdjustmentResultDto>>
            AdjustAsync(
                InventoryAdjustmentRequest request,
                CancellationToken cancellationToken = default)
        {
            AdjustCallCount++;

            return Task.FromResult(
                Result.Failure<
                    InventoryAdjustmentResultDto>(
                        InnerServiceError));
        }

        public Task<
            Result<
                PagedResult<
                    InventoryMovementDto>>>
            SearchAsync(
                InventorySearchRequest request,
                CancellationToken cancellationToken = default)
        {
            SearchCallCount++;

            return Task.FromResult(
                Result.Failure<
                    PagedResult<
                        InventoryMovementDto>>(
                            InnerServiceError));
        }
    }
}