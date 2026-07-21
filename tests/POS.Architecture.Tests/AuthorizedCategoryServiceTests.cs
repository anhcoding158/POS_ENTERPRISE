using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Categories;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra lớp bảo vệ phân quyền danh mục.
///
/// Test chứng minh service thật không được gọi
/// khi phiên hiện tại không có quyền.
/// </summary>
public sealed class AuthorizedCategoryServiceTests
{
    private static readonly DateTimeOffset
        AuthenticatedAtUtc =
            new(
                2026,
                7,
                21,
                8,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Anonymous_user_must_not_list_active_categories()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                role: null,
                innerService);

        var result =
            await service.ListActiveAsync();

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Unauthorized,
            result.Error.Code);

        Assert.Equal(
            0,
            innerService.ListActiveCallCount);
    }

    [Fact]
    public async Task
        Cashier_must_reach_active_category_list()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                Role.Cashier,
                innerService);

        var result =
            await service.ListActiveAsync();

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            result.Error.Code);

        Assert.Equal(
            1,
            innerService.ListActiveCallCount);
    }

    [Fact]
    public async Task
        Cashier_must_not_manage_categories()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                Role.Cashier,
                innerService);

        var searchResult =
            await service.SearchAsync(
                request: null!);

        var detailsResult =
            await service.GetByIdAsync(
                categoryId: 10);

        var createResult =
            await service.CreateAsync(
                request: null!);

        var updateResult =
            await service.UpdateAsync(
                request: null!);

        var activeResult =
            await service.SetActiveStateAsync(
                categoryId: 10,
                isActive: false);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            searchResult.Error.Code);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            detailsResult.Error.Code);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            createResult.Error.Code);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            updateResult.Error.Code);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            activeResult.Error.Code);

        Assert.Equal(
            0,
            innerService.SearchCallCount);

        Assert.Equal(
            0,
            innerService.GetByIdCallCount);

        Assert.Equal(
            0,
            innerService.CreateCallCount);

        Assert.Equal(
            0,
            innerService.UpdateCallCount);

        Assert.Equal(
            0,
            innerService.SetActiveStateCallCount);
    }

    [Fact]
    public async Task
        Inventory_staff_must_list_active_but_not_manage()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                Role.InventoryStaff,
                innerService);

        var listResult =
            await service.ListActiveAsync();

        var searchResult =
            await service.SearchAsync(
                request: null!);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            listResult.Error.Code);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            searchResult.Error.Code);

        Assert.Equal(
            1,
            innerService.ListActiveCallCount);

        Assert.Equal(
            0,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task
        Manager_must_reach_every_management_operation()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                Role.Manager,
                innerService);

        await ExecuteEveryManagementOperationAsync(
            service);

        AssertAllManagementOperationsReached(
            innerService);
    }

    [Fact]
    public async Task
        Administrator_must_reach_every_management_operation()
    {
        var innerService =
            new RecordingCategoryService();

        var service =
            CreateService(
                Role.Administrator,
                innerService);

        await ExecuteEveryManagementOperationAsync(
            service);

        AssertAllManagementOperationsReached(
            innerService);
    }

    private static async Task
        ExecuteEveryManagementOperationAsync(
            AuthorizedCategoryService service)
    {
        var searchResult =
            await service.SearchAsync(
                request: null!);

        var detailsResult =
            await service.GetByIdAsync(
                categoryId: 10);

        var createResult =
            await service.CreateAsync(
                request: null!);

        var updateResult =
            await service.UpdateAsync(
                request: null!);

        var activeResult =
            await service.SetActiveStateAsync(
                categoryId: 10,
                isActive: false);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            searchResult.Error.Code);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            detailsResult.Error.Code);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            createResult.Error.Code);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            updateResult.Error.Code);

        Assert.Equal(
            RecordingCategoryService
                .InnerErrorCode,
            activeResult.Error.Code);
    }

    private static void
        AssertAllManagementOperationsReached(
            RecordingCategoryService innerService)
    {
        Assert.Equal(
            1,
            innerService.SearchCallCount);

        Assert.Equal(
            1,
            innerService.GetByIdCallCount);

        Assert.Equal(
            1,
            innerService.CreateCallCount);

        Assert.Equal(
            1,
            innerService.UpdateCallCount);

        Assert.Equal(
            1,
            innerService.SetActiveStateCallCount);
    }

    private static AuthorizedCategoryService
        CreateService(
            Role? role,
            RecordingCategoryService innerService)
    {
        var currentUserService =
            new CurrentUserService();

        if (role.HasValue)
        {
            currentUserService.SetCurrentUser(
                new AuthenticatedUserDto(
                    1,
                    $"category.{role.Value}",
                    $"Người dùng {role.Value}",
                    role.Value,
                    AuthenticatedAtUtc));
        }

        var permissionService =
            new PermissionService(
                currentUserService);

        return new AuthorizedCategoryService(
            innerService,
            permissionService);
    }

    private sealed class RecordingCategoryService :
        ICategoryService
    {
        public const string InnerErrorCode =
            "TEST.CATEGORY_INNER_REACHED";

        private static readonly Error
            InnerError =
                new(
                    InnerErrorCode,
                    "CategoryService thật đã được gọi.");

        public int ListActiveCallCount
        {
            get;
            private set;
        }

        public int SearchCallCount
        {
            get;
            private set;
        }

        public int GetByIdCallCount
        {
            get;
            private set;
        }

        public int CreateCallCount
        {
            get;
            private set;
        }

        public int UpdateCallCount
        {
            get;
            private set;
        }

        public int SetActiveStateCallCount
        {
            get;
            private set;
        }

        public Task<
            Result<IReadOnlyList<CategoryOptionDto>>>
            ListActiveAsync(
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ListActiveCallCount++;

            return Task.FromResult(
                Result.Failure<
                    IReadOnlyList<CategoryOptionDto>>(
                        InnerError));
        }

        public Task<
            Result<PagedResult<CategoryListItemDto>>>
            SearchAsync(
                CategorySearchRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SearchCallCount++;

            return Task.FromResult(
                Result.Failure<
                    PagedResult<CategoryListItemDto>>(
                        InnerError));
        }

        public Task<Result<CategoryDetailsDto>>
            GetByIdAsync(
                int categoryId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetByIdCallCount++;

            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    InnerError));
        }

        public Task<Result<CategoryDetailsDto>>
            CreateAsync(
                CreateCategoryRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CreateCallCount++;

            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    InnerError));
        }

        public Task<Result<CategoryDetailsDto>>
            UpdateAsync(
                UpdateCategoryRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            UpdateCallCount++;

            return Task.FromResult(
                Result.Failure<CategoryDetailsDto>(
                    InnerError));
        }

        public Task<Result> SetActiveStateAsync(
            int categoryId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SetActiveStateCallCount++;

            return Task.FromResult(
                Result.Failure(
                    InnerError));
        }
    }
}