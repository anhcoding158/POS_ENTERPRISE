using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Payments;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AuthorizedPaymentIntentServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Denied_payment_intent_operations_do_not_delegate()
    {
        var inner = new RecordingService();
        var service = new AuthorizedPaymentIntentService(
            inner, new PermissionService(new CurrentUserService()));

        Assert.True((await service.CreateAsync(null!)).IsFailure);
        Assert.True((await service.MarkPresentedAsync(1)).IsFailure);
        Assert.True((await service.ConfirmReceivedAsync(1)).IsFailure);
        Assert.True((await service.CancelAsync(1)).IsFailure);
        Assert.True((await service.ExpireAsync(1, "timeout")).IsFailure);
        Assert.True((await service.GetByIdAsync(1)).IsFailure);
        Assert.True((await service.GetPendingAsync()).IsFailure);
        Assert.True((await service.RecoverPendingAsync()).IsFailure);
        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task Authorized_wrapper_delegates_once_and_forwards_cancellation()
    {
        var current = new CurrentUserService();
        current.SetCurrentUser(new AuthenticatedUserDto(
            1, "cashier", "Cashier", Role.Cashier, Now));
        var inner = new RecordingService();
        var service = new AuthorizedPaymentIntentService(
            inner, new PermissionService(current));
        using var source = new CancellationTokenSource();

        await service.GetPendingAsync(7, source.Token);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(source.Token, inner.Token);
    }

    private sealed class RecordingService : IPaymentIntentService
    {
        public int CallCount { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<Result<PaymentIntentDto>> CreateAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<PaymentIntentDto>> MarkPresentedAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<PaymentIntentDto>> ConfirmReceivedAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<PaymentIntentDto>> CancelAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<PaymentIntentDto>> ExpireAsync(int paymentIntentId, string reason, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<PaymentIntentDto>> GetByIdAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentDto>(cancellationToken);
        public Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> GetPendingAsync(int limit = 25, CancellationToken cancellationToken = default) =>
            Record<IReadOnlyList<PaymentIntentPendingDto>>(cancellationToken);
        public Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> RecoverPendingAsync(int limit = 25, CancellationToken cancellationToken = default) =>
            Record<IReadOnlyList<PaymentIntentPendingDto>>(cancellationToken);
        public Task<Result<PaymentIntentManualResolutionDto>> ResolveManuallyAsync(
            ResolvePaymentIntentManuallyRequest request, CancellationToken cancellationToken = default) =>
            Record<PaymentIntentManualResolutionDto>(cancellationToken);
        public Task<Result<IReadOnlyList<PaymentIntentManualResolutionDto>>> GetManualResolutionHistoryAsync(
            int limit = 100, CancellationToken cancellationToken = default) =>
            Record<IReadOnlyList<PaymentIntentManualResolutionDto>>(cancellationToken);

        private Task<Result<T>> Record<T>(CancellationToken token)
        {
            CallCount++;
            Token = token;
            return Task.FromResult(Result.Failure<T>(new AppError("TEST", "recorded")));
        }
    }
}
