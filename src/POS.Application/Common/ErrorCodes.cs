namespace POS.Application.Common;

/// <summary>
/// Danh sách mã lỗi ổn định của tầng Application.
///
/// Không dùng trực tiếp thông báo lỗi làm điều kiện xử lý.
/// Luôn so sánh bằng mã lỗi.
/// </summary>
public static class ErrorCodes
{
    public static class General
    {
        public const string Validation =
            "GENERAL.VALIDATION";

        public const string NotFound =
            "GENERAL.NOT_FOUND";

        public const string Conflict =
            "GENERAL.CONFLICT";

        public const string Unauthorized =
            "GENERAL.UNAUTHORIZED";

        public const string Forbidden =
            "GENERAL.FORBIDDEN";

        public const string Cancelled =
            "GENERAL.CANCELLED";

        public const string Unexpected =
            "GENERAL.UNEXPECTED";
    }

    public static class Authentication
    {
        public const string UsernameRequired =
            "AUTH.USERNAME_REQUIRED";

        public const string PasswordRequired =
            "AUTH.PASSWORD_REQUIRED";

        public const string InvalidCredentials =
            "AUTH.INVALID_CREDENTIALS";

        public const string UserNotFound =
            "AUTH.USER_NOT_FOUND";

        public const string AccountInactive =
            "AUTH.ACCOUNT_INACTIVE";

        public const string AccountLocked =
            "AUTH.ACCOUNT_LOCKED";

        public const string CurrentUserNotFound =
            "AUTH.CURRENT_USER_NOT_FOUND";
    }

    public static class Products
    {
        public const string NotFound =
            "PRODUCT.NOT_FOUND";

        public const string CodeAlreadyExists =
            "PRODUCT.CODE_ALREADY_EXISTS";

        public const string BarcodeAlreadyExists =
            "PRODUCT.BARCODE_ALREADY_EXISTS";

        public const string ConcurrencyConflict =
            "PRODUCT.CONCURRENCY_CONFLICT";

        public const string PersistenceConflict =
            "PRODUCT.PERSISTENCE_CONFLICT";

        public const string CategoryNotFound =
            "PRODUCT.CATEGORY_NOT_FOUND";

        public const string CategoryInactive =
            "PRODUCT.CATEGORY_INACTIVE";

        public const string Inactive =
            "PRODUCT.INACTIVE";

        public const string InsufficientStock =
            "PRODUCT.INSUFFICIENT_STOCK";
    }

    public static class Inventory
    {
        public const string ProductNotFound =
            "INVENTORY.PRODUCT_NOT_FOUND";

        public const string InventoryNotTracked =
            "INVENTORY.NOT_TRACKED";

        public const string InvalidMovementType =
            "INVENTORY.INVALID_MOVEMENT_TYPE";

        public const string UnsupportedManualMovement =
            "INVENTORY.UNSUPPORTED_MANUAL_MOVEMENT";

        public const string InvalidQuantity =
            "INVENTORY.INVALID_QUANTITY";

        public const string InsufficientStock =
            "INVENTORY.INSUFFICIENT_STOCK";

        public const string ConcurrencyConflict =
            "INVENTORY.CONCURRENCY_CONFLICT";

        public const string PersistenceConflict =
            "INVENTORY.PERSISTENCE_CONFLICT";
    }

    public static class Categories
    {
        public const string NotFound =
            "CATEGORY.NOT_FOUND";

        public const string NameAlreadyExists =
            "CATEGORY.NAME_ALREADY_EXISTS";
    }

    public static class Customers
    {
        public const string NotFound =
            "CUSTOMER.NOT_FOUND";

        public const string CodeAlreadyExists =
            "CUSTOMER.CODE_ALREADY_EXISTS";

        public const string PhoneAlreadyExists =
            "CUSTOMER.PHONE_ALREADY_EXISTS";
    }

    public static class Discounts
    {
        public const string NotFound =
            "DISCOUNT.NOT_FOUND";

        public const string CodeAlreadyExists =
            "DISCOUNT.CODE_ALREADY_EXISTS";

        public const string Inactive =
            "DISCOUNT.INACTIVE";

        public const string Expired =
            "DISCOUNT.EXPIRED";

        public const string NotStarted =
            "DISCOUNT.NOT_STARTED";

        public const string UsageLimitReached =
            "DISCOUNT.USAGE_LIMIT_REACHED";

        public const string NotApplicable =
            "DISCOUNT.NOT_APPLICABLE";
    }

    public static class Checkout
    {
        public const string EmptyCart =
            "CHECKOUT.EMPTY_CART";

        public const string InvalidQuantity =
            "CHECKOUT.INVALID_QUANTITY";

        public const string DuplicateProduct =
            "CHECKOUT.DUPLICATE_PRODUCT";

        public const string ProductNotFound =
            "CHECKOUT.PRODUCT_NOT_FOUND";

        public const string ProductInactive =
            "CHECKOUT.PRODUCT_INACTIVE";

        public const string InsufficientStock =
            "CHECKOUT.INSUFFICIENT_STOCK";

        public const string InvalidPaymentMethod =
            "CHECKOUT.INVALID_PAYMENT_METHOD";

        public const string InsufficientCash =
            "CHECKOUT.INSUFFICIENT_CASH";

        public const string CustomerNotFound =
            "CHECKOUT.CUSTOMER_NOT_FOUND";

        public const string DiscountNotApplicable =
            "CHECKOUT.DISCOUNT_NOT_APPLICABLE";

        public const string SaveFailed =
            "CHECKOUT.SAVE_FAILED";
    }

    public static class Persistence
    {
        public const string ConcurrencyConflict =
            "PERSISTENCE.CONCURRENCY_CONFLICT";

        public const string DatabaseUnavailable =
            "PERSISTENCE.DATABASE_UNAVAILABLE";

        public const string TransactionFailed =
            "PERSISTENCE.TRANSACTION_FAILED";

        public const string SaveFailed =
            "PERSISTENCE.SAVE_FAILED";
    }

    public static class Payments
    {
        public const string InvalidAmount =
            "PAYMENT.INVALID_AMOUNT";

        public const string VietQrNotConfigured =
            "PAYMENT.VIETQR_NOT_CONFIGURED";

        public const string VietQrInvalidPayload =
            "PAYMENT.VIETQR_INVALID_PAYLOAD";

        public const string VietQrGenerationFailed =
            "PAYMENT.VIETQR_GENERATION_FAILED";
    }

    public static class Printing
    {
        public const string PrinterNotConfigured =
            "PRINT.PRINTER_NOT_CONFIGURED";

        public const string PrinterNotFound =
            "PRINT.PRINTER_NOT_FOUND";

        public const string Cancelled =
            "PRINT.CANCELLED";

        public const string Failed =
            "PRINT.FAILED";
    }
}