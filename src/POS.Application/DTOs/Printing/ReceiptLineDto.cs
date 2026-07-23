namespace POS.Application.DTOs.Printing;

/// <summary>
/// Snapshot bất biến của một modifier hoặc topping
/// thuộc dòng hóa đơn tại thời điểm checkout.
/// </summary>
public sealed class ReceiptModifierDto
{
    public ReceiptModifierDto(
        int modifierId,
        int modifierGroupId,
        string? modifierGroupName,
        string? name,
        int quantity,
        long unitAdditionalPrice,
        long amountPerProductUnit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            modifierId);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            modifierGroupId);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            quantity);

        ArgumentOutOfRangeException.ThrowIfNegative(
            unitAdditionalPrice);

        ArgumentOutOfRangeException.ThrowIfNegative(
            amountPerProductUnit);

        var expectedAmountPerProductUnit =
            MultiplyChecked(
                unitAdditionalPrice,
                quantity,
                nameof(amountPerProductUnit));

        if (amountPerProductUnit !=
            expectedAmountPerProductUnit)
        {
            throw new ArgumentException(
                "Thành tiền modifier không khớp " +
                "đơn giá và số lượng đã chốt.",
                nameof(amountPerProductUnit));
        }

        ModifierId = modifierId;
        ModifierGroupId = modifierGroupId;

        ModifierGroupName =
            NormalizeRequiredText(
                modifierGroupName,
                nameof(modifierGroupName));

        Name =
            NormalizeRequiredText(
                name,
                nameof(name));

        Quantity = quantity;
        UnitAdditionalPrice = unitAdditionalPrice;
        AmountPerProductUnit = amountPerProductUnit;
    }

    public int ModifierId { get; }

    public int ModifierGroupId { get; }

    public string ModifierGroupName { get; }

    public string Name { get; }

    public int Quantity { get; }

    public long UnitAdditionalPrice { get; }

    public long AmountPerProductUnit { get; }

    private static string NormalizeRequiredText(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        return value.Trim();
    }

    private static long MultiplyChecked(
        long left,
        int right,
        string parameterName)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Thành tiền modifier vượt giới hạn.");
        }
    }
}

/// <summary>
/// Snapshot bất biến của một dòng sản phẩm trên hóa đơn.
///
/// Mã, tên, đơn vị và giá bán được chốt tại thời điểm
/// checkout. Giá vốn không được đưa vào DTO hóa đơn vì
/// đây là thông tin nội bộ của cửa hàng.
/// </summary>
public sealed class ReceiptLineDto
{
    public ReceiptLineDto(
        int orderItemId,
        int productId,
        string? productCode,
        string? productName,
        string? unitName,
        int quantity,
        long unitSalePrice,
        long modifierAmountPerUnit,
        long finalUnitPrice,
        long grossAmount,
        long lineDiscountAmount,
        long netAmount,
        string? notes,
        IEnumerable<ReceiptModifierDto>? modifiers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            orderItemId);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            productId);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            quantity);

        ArgumentOutOfRangeException.ThrowIfNegative(
            unitSalePrice);

        ArgumentOutOfRangeException.ThrowIfNegative(
            modifierAmountPerUnit);

        ArgumentOutOfRangeException.ThrowIfNegative(
            finalUnitPrice);

        ArgumentOutOfRangeException.ThrowIfNegative(
            grossAmount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            lineDiscountAmount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            netAmount);

        var modifierSnapshots =
            modifiers?.ToArray() ??
            Array.Empty<ReceiptModifierDto>();

        foreach (var modifier in modifierSnapshots)
        {
            ArgumentNullException.ThrowIfNull(
                modifier);
        }

        var expectedModifierAmountPerUnit =
            SumChecked(
                modifierSnapshots,
                nameof(modifierAmountPerUnit));

        if (modifierAmountPerUnit !=
            expectedModifierAmountPerUnit)
        {
            throw new ArgumentException(
                "Tổng tiền modifier của dòng hàng " +
                "không khớp danh sách modifier đã chốt.",
                nameof(modifierAmountPerUnit));
        }

        var expectedFinalUnitPrice =
            AddChecked(
                unitSalePrice,
                modifierAmountPerUnit,
                nameof(finalUnitPrice));

        if (finalUnitPrice != expectedFinalUnitPrice)
        {
            throw new ArgumentException(
                "Đơn giá cuối cùng không khớp giá bán " +
                "và tổng tiền modifier.",
                nameof(finalUnitPrice));
        }

        var expectedGrossAmount =
            MultiplyChecked(
                finalUnitPrice,
                quantity,
                nameof(grossAmount));

        if (grossAmount != expectedGrossAmount)
        {
            throw new ArgumentException(
                "Thành tiền trước giảm giá không khớp " +
                "đơn giá cuối cùng và số lượng.",
                nameof(grossAmount));
        }

        if (lineDiscountAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineDiscountAmount),
                "Giảm giá dòng hàng không được lớn hơn " +
                "thành tiền trước giảm giá.");
        }

        var expectedNetAmount =
            grossAmount - lineDiscountAmount;

        if (netAmount != expectedNetAmount)
        {
            throw new ArgumentException(
                "Thành tiền cuối cùng không khớp " +
                "thành tiền và giảm giá dòng hàng.",
                nameof(netAmount));
        }

        OrderItemId = orderItemId;
        ProductId = productId;

        ProductCode =
            NormalizeRequiredText(
                productCode,
                nameof(productCode));

        ProductName =
            NormalizeRequiredText(
                productName,
                nameof(productName));

        UnitName =
            NormalizeRequiredText(
                unitName,
                nameof(unitName));

        Quantity = quantity;
        UnitSalePrice = unitSalePrice;
        ModifierAmountPerUnit = modifierAmountPerUnit;
        FinalUnitPrice = finalUnitPrice;
        GrossAmount = grossAmount;
        LineDiscountAmount = lineDiscountAmount;
        NetAmount = netAmount;
        Notes = NormalizeOptionalText(notes);

        Modifiers =
            Array.AsReadOnly(
                modifierSnapshots);
    }

    public int OrderItemId { get; }

    public int ProductId { get; }

    public string ProductCode { get; }

    public string ProductName { get; }

    public string UnitName { get; }

    public int Quantity { get; }

    public long UnitSalePrice { get; }

    public long ModifierAmountPerUnit { get; }

    public long FinalUnitPrice { get; }

    public long GrossAmount { get; }

    public long LineDiscountAmount { get; }

    public long NetAmount { get; }

    public string? Notes { get; }

    public IReadOnlyList<ReceiptModifierDto> Modifiers
    {
        get;
    }

    private static string NormalizeRequiredText(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static long SumChecked(
        IEnumerable<ReceiptModifierDto> modifiers,
        string parameterName)
    {
        try
        {
            var total = 0L;

            foreach (var modifier in modifiers)
            {
                total =
                    checked(
                        total +
                        modifier.AmountPerProductUnit);
            }

            return total;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Tổng tiền modifier vượt giới hạn.");
        }
    }

    private static long AddChecked(
        long left,
        long right,
        string parameterName)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Đơn giá cuối cùng vượt giới hạn.");
        }
    }

    private static long MultiplyChecked(
        long left,
        int right,
        string parameterName)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Thành tiền dòng hàng vượt giới hạn.");
        }
    }
}