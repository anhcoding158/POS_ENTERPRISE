using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentApplicationTests
{
    [Fact]
    public async Task Create_persists_created_payment_intent()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var result = await database.PaymentIntentService(context).CreateAsync(Request(database));
        Assert.True(result.IsSuccess, result.IsFailure ? result.AppError.Message : null);
        Assert.Equal(PaymentIntentStatus.Created, result.Value.Status);
        Assert.False(result.Value.IsReplay);
        Assert.Equal(70_000, result.Value.Amount);
        Assert.Equal(1, await context.PaymentIntents.CountAsync());
    }

    [Fact]
    public async Task Creating_intent_creates_no_order_stock_movement_or_receipt()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var stock = await context.Products.Select(x => x.StockQuantity).SingleAsync();
        var result = await database.PaymentIntentService(context).CreateAsync(Request(database));
        Assert.True(result.IsSuccess);
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(stock, await context.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(0, await context.InventoryMovements.CountAsync());
        Assert.Equal(0, await context.OrderReceiptSnapshots.CountAsync());
        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());
    }

    [Fact]
    public async Task Creating_VietQR_intent_must_not_create_checkout_journal()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();

        Assert.True((await database.PaymentIntentService(context)
            .CreateAsync(Request(database))).IsSuccess);

        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());
    }

    [Fact]
    public async Task Presenting_VietQR_intent_must_not_create_checkout_journal()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var service = database.PaymentIntentService(context);
        var created = await service.CreateAsync(Request(database));

        Assert.True((await service.MarkPresentedAsync(created.Value.Id)).IsSuccess);

        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());
    }

    [Fact]
    public async Task Presenting_intent_creates_no_order_stock_movement_or_receipt()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var stock = await context.Products.Select(x => x.StockQuantity).SingleAsync();
        var service = database.PaymentIntentService(context);
        var created = await service.CreateAsync(Request(database));

        Assert.True((await service.MarkPresentedAsync(created.Value.Id)).IsSuccess);

        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(stock, await context.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(0, await context.InventoryMovements.CountAsync());
        Assert.Equal(0, await context.OrderReceiptSnapshots.CountAsync());
        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());
    }

    [Fact]
    public async Task Same_request_replays_existing_intent()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        int firstId;
        await using (var first = database.Context())
        {
            var created = await database.PaymentIntentService(first).CreateAsync(Request(database, requestId));
            firstId = created.Value.Id;
        }
        await using (var second = database.Context())
        {
            var replay = await database.PaymentIntentService(second).CreateAsync(Request(database, requestId));
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Value.IsReplay);
            Assert.Equal(firstId, replay.Value.Id);
            Assert.Equal(1, await second.PaymentIntents.CountAsync());
        }
    }

    [Fact]
    public async Task Same_id_different_quote_conflicts()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        await using (var first = database.Context())
            Assert.True((await database.PaymentIntentService(first)
                .CreateAsync(Request(database, requestId))).IsSuccess);
        await using (var second = database.Context())
        {
            var conflict = await database.PaymentIntentService(second)
                .CreateAsync(Request(database, requestId, quantity: 1));
            Assert.True(conflict.IsFailure);
            Assert.Equal("PAYMENT_INTENT.IDEMPOTENCY_CONFLICT", conflict.AppError.Code);
            Assert.Equal(1, await second.PaymentIntents.CountAsync());
        }
    }

    [Fact]
    public async Task Present_confirm_cancel_and_pending_follow_lifecycle()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var service = database.PaymentIntentService(context);
        var created = await service.CreateAsync(Request(database));
        var presented = await service.MarkPresentedAsync(created.Value.Id);
        Assert.Equal(PaymentIntentStatus.Presented, presented.Value.Status);
        var confirmed = await service.ConfirmReceivedAsync(created.Value.Id);
        Assert.True(confirmed.IsSuccess, confirmed.IsFailure ? confirmed.AppError.Message : null);
        Assert.Equal(PaymentIntentStatus.Confirmed, confirmed.Value.Status);
        Assert.Equal(database.UserId,
            await context.PaymentIntents.Select(x => x.ConfirmedByUserId).SingleAsync());
        var cancel = await service.CancelAsync(created.Value.Id);
        Assert.True(cancel.IsFailure);
        var pending = await service.GetPendingAsync();
        Assert.Single(pending.Value);
        Assert.True(pending.Value[0].CanRetryCheckout);
    }

    [Fact]
    public async Task Cart_change_makes_intent_stale()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int id;
        await using (var create = database.Context())
        {
            var service = database.PaymentIntentService(create);
            var intent = await service.CreateAsync(Request(database));
            id = intent.Value.Id;
            Assert.True((await service.MarkPresentedAsync(id)).IsSuccess);
        }
        await using (var mutate = database.Context())
        {
            var product = await mutate.Products.SingleAsync();
            product.ChangePrices(product.CostPrice, product.SalePrice + 1_000, HeldSaleTestDatabase.Now);
            await mutate.SaveChangesAsync();
        }
        await using (var confirm = database.Context())
        {
            var result = await database.PaymentIntentService(confirm).ConfirmReceivedAsync(id);
            Assert.True(result.IsFailure);
            Assert.Equal("PAYMENT_INTENT.STALE", result.AppError.Code);
            Assert.Equal(PaymentIntentStatus.Presented,
                await confirm.PaymentIntents.Select(x => x.Status).SingleAsync());
        }
    }

    [Fact]
    public async Task Pending_state_survives_new_scope_and_terminal_states_are_excluded()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int id;
        await using (var first = database.Context())
        {
            var created = await database.PaymentIntentService(first).CreateAsync(Request(database));
            id = created.Value.Id;
        }
        await using (var second = database.Context())
            Assert.Single((await database.PaymentIntentService(second).RecoverPendingAsync()).Value);
        await using (var third = database.Context())
            Assert.True((await database.PaymentIntentService(third).CancelAsync(id)).IsSuccess);
        await using (var fourth = database.Context())
            Assert.Empty((await database.PaymentIntentService(fourth).GetPendingAsync()).Value);
    }

    private static CreatePaymentIntentRequest Request(
        HeldSaleTestDatabase database,
        Guid? requestId = null,
        int quantity = 2)
    {
        var id = requestId ?? Guid.NewGuid();
        return new(id, new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, quantity)],
            PaymentMethod.VietQr,
            0,
            confirmedPaymentAmount: 1,
            clientRequestId: Guid.NewGuid()));
    }
}

public sealed class PaymentIntentUiTests
{
    [Fact]
    public async Task Created_intent_is_persisted_as_presented_before_dialog()
    {
        var calls = new List<string>();

        var result = await PresentAsync(
            Intent(PaymentIntentStatus.Created),
            id =>
            {
                calls.Add($"mark:{id}");
                return Task.FromResult(Result.Success(
                    Intent(PaymentIntentStatus.Presented)));
            },
            _ =>
            {
                calls.Add("dialog");
                return Task.FromResult(DialogSuccess());
            });

        Assert.True(result.Result.IsSuccess);
        Assert.True(result.DialogAttempted);
        Assert.Equal(["mark:17", "dialog"], calls);
    }

    [Fact]
    public async Task Mark_presented_failure_does_not_open_dialog()
    {
        var calls = new List<string>();

        var result = await PresentAsync(
            Intent(PaymentIntentStatus.Created),
            _ =>
            {
                calls.Add("mark");
                return Task.FromResult(
                    Result.Failure<PaymentIntentDto>(
                        new AppError("TEST.MARK_FAILED", "mark failed")));
            },
            _ =>
            {
                calls.Add("dialog");
                return Task.FromResult(DialogSuccess());
            });

        Assert.True(result.Result.IsFailure);
        Assert.Equal("TEST.MARK_FAILED", result.Result.AppError.Code);
        Assert.False(result.DialogAttempted);
        Assert.Equal(["mark"], calls);
    }

    [Fact]
    public async Task Presented_replay_opens_dialog_without_marking_again()
    {
        var calls = new List<string>();

        var result = await PresentAsync(
            Intent(PaymentIntentStatus.Presented),
            _ =>
            {
                calls.Add("mark");
                return Task.FromResult(Result.Success(
                    Intent(PaymentIntentStatus.Presented)));
            },
            _ =>
            {
                calls.Add("dialog");
                return Task.FromResult(DialogSuccess());
            });

        Assert.True(result.Result.IsSuccess);
        Assert.True(result.DialogAttempted);
        Assert.Equal(["dialog"], calls);
    }

    [Fact]
    public async Task Dialog_failure_occurs_after_presented_state_is_persisted()
    {
        var calls = new List<string>();
        var persistedStatus = PaymentIntentStatus.Created;

        var result = await PresentAsync(
            Intent(PaymentIntentStatus.Created),
            _ =>
            {
                calls.Add("mark");
                persistedStatus = PaymentIntentStatus.Presented;
                return Task.FromResult(Result.Success(
                    Intent(persistedStatus)));
            },
            _ =>
            {
                calls.Add("dialog");
                Assert.Equal(PaymentIntentStatus.Presented, persistedStatus);
                return Task.FromResult(
                    Result.Failure<VietQrPaymentDialogResult>(
                        new AppError("TEST.DIALOG_FAILED", "dialog failed")));
            });

        Assert.True(result.Result.IsFailure);
        Assert.Equal("TEST.DIALOG_FAILED", result.Result.AppError.Code);
        Assert.True(result.DialogAttempted);
        Assert.Equal(PaymentIntentStatus.Presented, persistedStatus);
        Assert.Equal(["mark", "dialog"], calls);
    }

    [Theory]
    [InlineData(PaymentIntentStatus.Confirmed)]
    [InlineData(PaymentIntentStatus.Completed)]
    public async Task Confirmed_or_completed_intent_is_not_presented_or_opened(
        PaymentIntentStatus status)
    {
        var calls = new List<string>();

        var result = await PresentAsync(
            Intent(status),
            _ =>
            {
                calls.Add("mark");
                return Task.FromResult(Result.Success(
                    Intent(PaymentIntentStatus.Presented)));
            },
            _ =>
            {
                calls.Add("dialog");
                return Task.FromResult(DialogSuccess());
            });

        Assert.True(result.Result.IsFailure);
        Assert.Equal("PAYMENT_INTENT.INVALID_TRANSITION", result.Result.AppError.Code);
        Assert.False(result.DialogAttempted);
        Assert.Empty(calls);
    }

    [Fact]
    public void VietQR_checkout_routes_presentation_before_manual_confirmation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "POS.Wpf",
            "ViewModels",
            "SalesViewModel.cs"));

        var methodStart = source.IndexOf(
            "AuthorizePaymentIntentAsync(",
            source.IndexOf("private async Task<Result<SalesPaymentAuthorizationOutcome>>", StringComparison.Ordinal),
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0);

        var methodEnd = source.IndexOf(
            "private static async Task<(",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        var create = method.IndexOf("intentService.CreateAsync(", StringComparison.Ordinal);
        var render = method.IndexOf("gateway.RenderPng(created.Value.PayloadText)", StringComparison.Ordinal);
        var presentation = method.IndexOf("ShowPersistedVietQrPresentationAsync(", StringComparison.Ordinal);
        var confirmed = method.IndexOf("intentService.ConfirmReceivedAsync(", StringComparison.Ordinal);

        Assert.True(create >= 0);
        Assert.True(render >= 0);
        Assert.True(presentation >= 0);
        Assert.True(confirmed >= 0);
        Assert.True(create < render);
        Assert.True(render < presentation);
        Assert.True(presentation < confirmed);
        Assert.Equal(presentation,
            method.LastIndexOf("ShowPersistedVietQrPresentationAsync(", StringComparison.Ordinal));
        Assert.Contains("id => intentService.MarkPresentedAsync(id)", method,
            StringComparison.Ordinal);
        Assert.Equal(1,
            method.Split("intentService.MarkPresentedAsync(",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("await intentService.MarkPresentedAsync(", method,
            StringComparison.Ordinal);
        Assert.Contains("value => dialog.ShowPresentationAsync(value)", method,
            StringComparison.Ordinal);
        Assert.Equal(1,
            method.Split("dialog.ShowPresentationAsync(",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("await dialog.ShowPresentationAsync(", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VietQR_checkout_carries_payment_intent_id_and_does_not_use_legacy_authorizer()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "POS.Wpf",
            "ViewModels",
            "SalesViewModel.cs"));

        Assert.Contains(
            "paymentIntentId:",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "? await AuthorizePaymentIntentAsync(",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "POS.Enterprise.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private static async Task<(
        Result<VietQrPaymentDialogResult> Result,
        bool DialogAttempted)> PresentAsync(
        PaymentIntentDto intent,
        Func<int, Task<Result<PaymentIntentDto>>> markPresentedAsync,
        Func<VietQrPaymentPresentation, Task<Result<VietQrPaymentDialogResult>>>
            showPresentationAsync)
    {
        var method = typeof(SalesViewModel).GetMethod(
            "ShowPersistedVietQrPresentationAsync",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var task = method.Invoke(
            null,
            [intent, Presentation(), markPresentedAsync, showPresentationAsync]);

        return await Assert.IsType<Task<(
            Result<VietQrPaymentDialogResult>,
            bool)>>(task);
    }

    private static PaymentIntentDto Intent(PaymentIntentStatus status) =>
        new(
            17,
            "VQ-0017",
            status,
            35_000,
            "VND",
            "PAY persisted",
            "payload-persisted",
            "970415",
            "123",
            "POS TEST",
            new DateTimeOffset(2026, 7, 30, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 30, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 30, 1, 17, 3, TimeSpan.Zero),
            null,
            null,
            false);

    private static VietQrPaymentPresentation Presentation() =>
        new(35_000, "VQ-0017", "PAY persisted", [1, 2, 3]);

    private static Result<VietQrPaymentDialogResult> DialogSuccess() =>
        Result.Success(new VietQrPaymentDialogResult(
            false,
            "VQ-0017",
            "PAY persisted"));
}
