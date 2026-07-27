using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderHistoryAuthorizationTests
{
    [Fact]
    public async Task Search_must_require_view_reports_permission()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        var result = await service.SearchAsync(new());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Forbidden, result.Error.Code);
    }

    [Fact]
    public async Task Details_must_require_view_reports_permission()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        var result = await service.GetDetailsAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Forbidden, result.Error.Code);
    }

    [Fact]
    public async Task Reprint_must_require_view_reports_permission()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        var result = await service.GetReprintReceiptAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Forbidden, result.Error.Code);
    }

    [Fact]
    public async Task Denied_search_must_not_call_inner()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        await service.SearchAsync(new());

        Assert.Equal(0, inner.SearchCalls);
    }

    [Fact]
    public async Task Denied_details_must_not_call_inner()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        await service.GetDetailsAsync(1);

        Assert.Equal(0, inner.DetailsCalls);
    }

    [Fact]
    public async Task Denied_reprint_must_not_call_inner()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Cashier);

        await service.GetReprintReceiptAsync(1);

        Assert.Equal(0, inner.ReprintCalls);
    }

    [Fact]
    public async Task Authorized_search_must_delegate_once()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.SearchAsync(new());

        Assert.Equal(1, inner.SearchCalls);
    }

    [Fact]
    public async Task Authorized_details_must_delegate_once()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.GetDetailsAsync(1);

        Assert.Equal(1, inner.DetailsCalls);
    }

    [Fact]
    public async Task Authorized_reprint_must_delegate_once()
    {
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.GetReprintReceiptAsync(1);

        Assert.Equal(1, inner.ReprintCalls);
    }

    [Fact]
    public async Task Search_must_forward_cancellation_token()
    {
        using var source = new CancellationTokenSource();
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.SearchAsync(new(), source.Token);

        Assert.Equal(source.Token, inner.SearchToken);
    }

    [Fact]
    public async Task Details_must_forward_cancellation_token()
    {
        using var source = new CancellationTokenSource();
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.GetDetailsAsync(1, source.Token);

        Assert.Equal(source.Token, inner.DetailsToken);
    }

    [Fact]
    public async Task Reprint_must_forward_cancellation_token()
    {
        using var source = new CancellationTokenSource();
        var inner = new RecordingService();
        var service = CreateService(inner, Role.Manager);

        await service.GetReprintReceiptAsync(1, source.Token);

        Assert.Equal(source.Token, inner.ReprintToken);
    }

    [Fact]
    public void Authorization_must_not_hardcode_role()
    {
        Assert.True(RolePermissionPolicy.HasPermission(
            Role.Manager,
            SystemPermission.ViewReports));
        Assert.True(RolePermissionPolicy.HasPermission(
            Role.Administrator,
            SystemPermission.ViewReports));
        Assert.False(RolePermissionPolicy.HasPermission(
            Role.Cashier,
            SystemPermission.ViewReports));
    }

    private static AuthorizedOrderHistoryService CreateService(
        RecordingService inner,
        Role role)
    {
        var currentUser = new CurrentUserService();
        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                1,
                "history-user",
                "Người dùng lịch sử",
                role,
                DateTimeOffset.UtcNow));

        return new AuthorizedOrderHistoryService(
            inner,
            new PermissionService(currentUser));
    }

    private sealed class RecordingService : IOrderHistoryService
    {
        private static readonly Error InnerError =
            new("TEST.INNER", "Inner service was called.");

        public int SearchCalls { get; private set; }
        public int DetailsCalls { get; private set; }
        public int ReprintCalls { get; private set; }
        public CancellationToken SearchToken { get; private set; }
        public CancellationToken DetailsToken { get; private set; }
        public CancellationToken ReprintToken { get; private set; }

        public Task<Result<PagedResult<OrderHistoryListItemDto>>> SearchAsync(
            OrderHistorySearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            SearchToken = cancellationToken;
            return Task.FromResult(
                Result.Failure<PagedResult<OrderHistoryListItemDto>>(InnerError));
        }

        public Task<Result<OrderHistoryDetailsDto>> GetDetailsAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            DetailsCalls++;
            DetailsToken = cancellationToken;
            return Task.FromResult(
                Result.Failure<OrderHistoryDetailsDto>(InnerError));
        }

        public Task<Result<ReceiptRequest>> GetReprintReceiptAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReprintCalls++;
            ReprintToken = cancellationToken;
            return Task.FromResult(
                Result.Failure<ReceiptRequest>(InnerError));
        }
    }
}
