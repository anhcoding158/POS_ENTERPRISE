using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class PaymentIntent : AuditableEntity
{
    private PaymentIntent() { }

    public PaymentIntent(
        Guid clientRequestId,
        string displayCode,
        long amount,
        string transferContent,
        string payloadText,
        string payloadHash,
        string bankCodeSnapshot,
        string accountNumberSnapshot,
        string accountNameSnapshot,
        string quoteFingerprint,
        string checkoutRequestJson,
        int createdByUserId,
        DateTimeOffset utcNow,
        DateTimeOffset? expiresAtUtc,
        int? heldSaleId = null)
    {
        if (clientRequestId == Guid.Empty) Throw("PAYMENT_INTENT.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(displayCode)) Throw("PAYMENT_INTENT.DISPLAY_CODE_REQUIRED", "Mã hiển thị không được rỗng.");
        if (amount <= 0) Throw("PAYMENT_INTENT.AMOUNT_INVALID", "Số tiền phải lớn hơn 0.");
        if (createdByUserId <= 0) Throw("PAYMENT_INTENT.USER_REQUIRED", "Người tạo không hợp lệ.");
        ValidateRequired(transferContent, "PAYMENT_INTENT.CONTENT_REQUIRED");
        ValidateRequired(payloadText, "PAYMENT_INTENT.PAYLOAD_REQUIRED");
        ValidateHash(payloadHash, nameof(payloadHash));
        ValidateHash(quoteFingerprint, nameof(quoteFingerprint));
        ValidateRequired(checkoutRequestJson, "PAYMENT_INTENT.CHECKOUT_REQUEST_REQUIRED");
        ValidateRequired(bankCodeSnapshot, "PAYMENT_INTENT.BANK_REQUIRED");
        ValidateRequired(accountNumberSnapshot, "PAYMENT_INTENT.ACCOUNT_REQUIRED");
        ValidateRequired(accountNameSnapshot, "PAYMENT_INTENT.ACCOUNT_NAME_REQUIRED");
        if (heldSaleId is <= 0) Throw("PAYMENT_INTENT.HELD_SALE_INVALID", "HeldSaleId không hợp lệ.");

        ClientRequestId = clientRequestId;
        DisplayCode = displayCode.Trim().ToUpperInvariant();
        Provider = PaymentProvider.VietQr;
        Status = PaymentIntentStatus.Created;
        Amount = amount;
        Currency = "VND";
        TransferContent = transferContent.Trim();
        PayloadText = payloadText.Trim();
        PayloadHash = payloadHash;
        BankCodeSnapshot = bankCodeSnapshot.Trim();
        AccountNumberSnapshot = accountNumberSnapshot.Trim();
        AccountNameSnapshot = accountNameSnapshot.Trim();
        QuoteFingerprint = quoteFingerprint;
        CheckoutRequestJson = checkoutRequestJson.Trim();
        HeldSaleId = heldSaleId;
        CreatedByUserId = createdByUserId;
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
        ConcurrencyToken = Guid.NewGuid();
        MarkCreated(utcNow);
    }

    public Guid ClientRequestId { get; private set; }
    public string DisplayCode { get; private set; } = string.Empty;
    public PaymentProvider Provider { get; private set; }
    public PaymentIntentStatus Status { get; private set; }
    public long Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string TransferContent { get; private set; } = string.Empty;
    public string PayloadText { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string BankCodeSnapshot { get; private set; } = string.Empty;
    public string AccountNumberSnapshot { get; private set; } = string.Empty;
    public string AccountNameSnapshot { get; private set; } = string.Empty;
    public string QuoteFingerprint { get; private set; } = string.Empty;
    public string CheckoutRequestJson { get; private set; } = string.Empty;
    public int? HeldSaleId { get; private set; }

    public void LockCheckoutSnapshot(string checkoutRequestJson)
    {
        ValidateRequired(checkoutRequestJson, "PAYMENT_INTENT.CHECKOUT_REQUIRED");
        CheckoutRequestJson = checkoutRequestJson.Trim();
    }
    public int CreatedByUserId { get; private set; }
    public int? ConfirmedByUserId { get; private set; }
    public int? CompletedOrderId { get; private set; }
    public DateTimeOffset? PresentedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset? ExpiredAtUtc { get; private set; }
    public string? ExpirationReason { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public User? CreatedByUser { get; private set; }
    public User? ConfirmedByUser { get; private set; }
    public Order? CompletedOrder { get; private set; }
    public HeldSale? HeldSale { get; private set; }

    public bool IsExpiredAt(DateTimeOffset utcNow) =>
        ExpiresAtUtc.HasValue && utcNow.ToUniversalTime() >= ExpiresAtUtc.Value;

    public void MarkPresented(DateTimeOffset utcNow)
    {
        if (Status == PaymentIntentStatus.Presented) return;
        Ensure(PaymentIntentStatus.Created);
        if (IsExpiredAt(utcNow))
            Throw("PAYMENT_INTENT.EXPIRED", "Payment intent has expired.");
        Status = PaymentIntentStatus.Presented;
        PresentedAtUtc = utcNow.ToUniversalTime();
        Touch(utcNow);
    }

    public void MarkConfirmed(int actorId, DateTimeOffset utcNow)
    {
        if (Status == PaymentIntentStatus.Confirmed) return;
        Ensure(PaymentIntentStatus.Presented);
        if (actorId <= 0) Throw("PAYMENT_INTENT.USER_REQUIRED", "Người xác nhận không hợp lệ.");
        if (IsExpiredAt(utcNow)) Throw("PAYMENT_INTENT.EXPIRED", "Mã VietQR đã hết hạn.");
        Status = PaymentIntentStatus.Confirmed;
        ConfirmedByUserId = actorId;
        ConfirmedAtUtc = utcNow.ToUniversalTime();
        Touch(utcNow);
    }

    public void Complete(int orderId, DateTimeOffset utcNow)
    {
        if (Status == PaymentIntentStatus.Completed && CompletedOrderId == orderId) return;
        Ensure(PaymentIntentStatus.Confirmed);
        if (orderId <= 0) Throw("PAYMENT_INTENT.ORDER_REQUIRED", "Order hoàn tất không hợp lệ.");
        CompletedOrderId = orderId;
        CompletedAtUtc = utcNow.ToUniversalTime();
        Status = PaymentIntentStatus.Completed;
        Touch(utcNow);
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        if (Status == PaymentIntentStatus.Cancelled) return;
        if (Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.Presented))
            Throw("PAYMENT_INTENT.INVALID_TRANSITION", "Chỉ mã chưa xác nhận mới được hủy.");
        Status = PaymentIntentStatus.Cancelled;
        CancelledAtUtc = utcNow.ToUniversalTime();
        Touch(utcNow);
    }

    public void Expire(DateTimeOffset utcNow, string reason)
    {
        if (Status == PaymentIntentStatus.Expired) return;
        if (Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.Presented))
            Throw("PAYMENT_INTENT.INVALID_TRANSITION", "Trạng thái hiện tại không thể hết hạn.");
        if (string.IsNullOrWhiteSpace(reason))
            Throw("PAYMENT_INTENT.EXPIRATION_REASON_REQUIRED", "Expiration reason is required.");
        Status = PaymentIntentStatus.Expired;
        ExpiredAtUtc = utcNow.ToUniversalTime();
        ExpiresAtUtc ??= ExpiredAtUtc;
        ExpirationReason = reason.Trim();
        Touch(utcNow);
    }

    private void Ensure(PaymentIntentStatus expected)
    {
        if (Status != expected) Throw("PAYMENT_INTENT.INVALID_TRANSITION", $"Không thể chuyển từ trạng thái {Status}.");
    }

    private void Touch(DateTimeOffset utcNow)
    {
        ConcurrencyToken = Guid.NewGuid();
        MarkUpdated(utcNow);
    }

    private static void ValidateRequired(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) Throw(code, "Dữ liệu bắt buộc không được rỗng.");
    }

    private static void ValidateHash(string? value, string name)
    {
        if (value is null || value.Length != 64 ||
            value.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            Throw("PAYMENT_INTENT.HASH_INVALID", $"{name} phải là SHA-256 uppercase hex.");
    }

    private static void Throw(string code, string message) => throw new DomainException(code, message);
}
