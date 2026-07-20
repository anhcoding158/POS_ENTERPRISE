namespace POS.Domain.Constants;

/// <summary>
/// Các giới hạn dùng chung của Domain.
///
/// EF Core Configuration phải sử dụng cùng các giá trị này
/// để giới hạn Domain và database không bị lệch nhau.
/// </summary>
public static class BusinessRules
{
    public static class Users
    {
        public const int UsernameMinLength = 3;
        public const int UsernameMaxLength = 50;

        public const int PasswordHashMaxLength = 200;
        public const int FullNameMaxLength = 150;

        public const int FailedLoginLimit = 5;
    }

    public static class Customers
    {
        public const int CodeMaxLength = 30;
        public const int FullNameMaxLength = 150;

        public const int PhoneNumberMinLength = 9;
        public const int PhoneNumberMaxLength = 15;

        public const int AddressMaxLength = 500;
        public const int NotesMaxLength = 1_000;
    }

    public static class Areas
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
    }

    public static class RestaurantTables
    {
        public const int CodeMaxLength = 30;
        public const int NameMaxLength = 100;

        public const int MinimumCapacity = 1;
        public const int MaximumCapacity = 100;
    }

    public static class Categories
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
        public const int MaximumDisplayOrder = 100_000;
    }

    public static class Products
    {
        public const int CodeMaxLength = 50;
        public const int BarcodeMaxLength = 50;
        public const int NameMaxLength = 200;
        public const int DescriptionMaxLength = 2_000;
        public const int UnitNameMaxLength = 50;
        public const int ImagePathMaxLength = 500;

        public const long MaximumPrice = 999_999_999_999;
        public const int MaximumStockQuantity = 999_999_999;
    }

    public static class Inventory
    {
        public const int ReasonMaxLength = 500;

        public const int ReferenceTypeMaxLength = 100;
        public const int ReferenceIdMaxLength = 100;

        public const int MaximumSearchPageSize = 200;

        /*
         * Một biến động lớn nhất có thể chuyển tồn kho từ:
         *
         * -999.999.999
         * sang
         * +999.999.999
         *
         * nên độ lệch tối đa là 1.999.999.998.
         */
        public const int MaximumQuantityDelta =
            Products.MaximumStockQuantity * 2;
    }

    public static class ModifierGroups
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
        public const int MaximumSelections = 100;
    }

    public static class Modifiers
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
        public const long MaximumAdditionalPrice = 999_999_999;
    }

    public static class Discounts
    {
        public const int CodeMaxLength = 50;
        public const int NameMaxLength = 150;
        public const int DescriptionMaxLength = 1_000;

        public const decimal MaximumPercent = 100m;
        public const long MaximumFixedAmount = 999_999_999_999;
    }

    public static class Orders
    {
        public const int CodeMaxLength = 50;
        public const int NotesMaxLength = 2_000;
        public const int CancelReasonMaxLength = 500;

        public const int MaximumLineQuantity = 999_999;
        public const int MaximumLinesPerOrder = 5_000;

        public const long MaximumOrderAmount = 999_999_999_999;
    }

    public static class Outbox
    {
        public const int AggregateTypeMaxLength = 200;
        public const int AggregateIdMaxLength = 100;
        public const int EventTypeMaxLength = 300;
        public const int ErrorMaxLength = 2_000;

        public const int MaximumRetryCount = 20;
    }
}