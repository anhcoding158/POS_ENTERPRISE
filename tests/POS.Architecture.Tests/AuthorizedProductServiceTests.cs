using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AuthorizedProductServiceTests
{
    private static readonly DateTimeOffset
        AuthenticatedAtUtc =
            new(
                2026,
                7,
                21,
                0,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Unauthenticated_user_must_not_read_products()
    {
        var innerService =
            new RecordingProductService();

        var service =
            CreateService(
                role: null,
                innerService);

        var result =
            await service.SearchAsync(
                request: null!,
                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Unauthorized,
            result.Error.Code);

        Assert.Equal(
            0,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task
        Cashier_must_be_allowed_to_read_products()
    {
        var innerService =
            new RecordingProductService();

        var service =
            CreateService(
                Role.Cashier,
                innerService);

        var result =
            await service.SearchAsync(
                request: null!,
                TestContext
                    .Current
                    .CancellationToken);

        /*
         * Fake inner service trả lỗi TEST.INNER.
         * Điều cần chứng minh ở đây là lời gọi đã đi qua
         * lớp phân quyền và tới được inner service.
         */
        Assert.True(result.IsFailure);

        Assert.Equal(
            RecordingProductService
                .InnerErrorCode,
            result.Error.Code);

        Assert.Equal(
            1,
            innerService.SearchCallCount);
    }

    [Fact]
    public async Task
        Cashier_must_not_manage_products()
    {
        var innerService =
            new RecordingProductService();

        var service =
            CreateService(
                Role.Cashier,
                innerService);

        var result =
            await service.CreateAsync(
                request: null!,
                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            result.Error.Code);

        Assert.Equal(
            0,
            innerService.CreateCallCount);
    }

    [Fact]
    public async Task
        Manager_must_be_allowed_to_manage_products()
    {
        var innerService =
            new RecordingProductService();

        var service =
            CreateService(
                Role.Manager,
                innerService);

        var result =
            await service.SetActiveStateAsync(
                productId: 10,
                isActive: false,
                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.Equal(
            1,
            innerService
                .SetActiveStateCallCount);
    }

    private static AuthorizedProductService
        CreateService(
            Role? role,
            RecordingProductService innerService)
    {
        var currentUserService =
            new CurrentUserService();

        if (role.HasValue)
        {
            currentUserService.SetCurrentUser(
                new AuthenticatedUserDto(
                    id: 1,
                    username: "test.user",
                    fullName: "Người dùng kiểm thử",
                    role: role.Value,
                    authenticatedAtUtc:
                        AuthenticatedAtUtc));
        }

        var permissionService =
            new PermissionService(
                currentUserService);

        return new AuthorizedProductService(
            innerService,
            permissionService);
    }

    private sealed class RecordingProductService :
        IProductService
    {
        public const string InnerErrorCode =
            "TEST.INNER";

        private static readonly Error
            InnerError =
                new(
                    InnerErrorCode,
                    "Lỗi giả lập từ inner service.");

        public int SearchCallCount { get; private set; }

        public int CreateCallCount { get; private set; }

        public int SetActiveStateCallCount
        {
            get;
            private set;
        }

        public Task<
            Result<PagedResult<ProductListItemDto>>>
            SearchAsync(
                ProductSearchRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SearchCallCount++;

            return Task.FromResult(
                Result.Failure<
                    PagedResult<ProductListItemDto>>(
                    InnerError));
        }

        public Task<Result<ProductDetailsDto>>
            GetByIdAsync(
                int productId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    InnerError));
        }

        public Task<Result<ProductDetailsDto>>
            CreateAsync(
                CreateProductRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CreateCallCount++;

            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    InnerError));
        }

        public Task<Result<ProductDetailsDto>>
            UpdateAsync(
                UpdateProductRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                Result.Failure<ProductDetailsDto>(
                    InnerError));
        }

        public Task<Result> SetActiveStateAsync(
            int productId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SetActiveStateCallCount++;

            return Task.FromResult(
                Result.Success());
        }
    }
}