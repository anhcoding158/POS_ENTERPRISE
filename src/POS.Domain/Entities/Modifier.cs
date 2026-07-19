using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Một lựa chọn trong ModifierGroup.
/// Ví dụ Size L, Thêm trân châu, Ít đá.
/// </summary>
public sealed class Modifier : AuditableEntity
{
    private Modifier()
    {
    }

    public Modifier(
        int modifierGroupId,
        string name,
        long additionalPrice,
        int displayOrder,
        DateTimeOffset utcNow,
        string? description = null)
    {
        SetModifierGroupId(modifierGroupId);
        SetName(name);
        SetDescription(description);
        SetAdditionalPrice(additionalPrice);
        SetDisplayOrder(displayOrder);

        IsActive = true;

        MarkCreated(utcNow);
    }

    public int ModifierGroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public long AdditionalPrice { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public ModifierGroup? ModifierGroup { get; private set; }

    public void Update(
        string name,
        string? description,
        long additionalPrice,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        SetName(name);
        SetDescription(description);
        SetAdditionalPrice(additionalPrice);
        SetDisplayOrder(displayOrder);

        MarkUpdated(utcNow);
    }

    public void MoveToGroup(
        int modifierGroupId,
        DateTimeOffset utcNow)
    {
        SetModifierGroupId(modifierGroupId);
        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(utcNow);
    }

    private void SetModifierGroupId(int modifierGroupId)
    {
        if (modifierGroupId <= 0)
        {
            throw new DomainException(
                "MODIFIER.INVALID_GROUP_ID",
                "Nhóm lựa chọn không hợp lệ.");
        }

        ModifierGroupId = modifierGroupId;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "MODIFIER.NAME_REQUIRED",
                "Tên lựa chọn không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.Modifiers.NameMaxLength)
        {
            throw new DomainException(
                "MODIFIER.NAME_TOO_LONG",
                "Tên lựa chọn vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.Modifiers.DescriptionMaxLength)
        {
            throw new DomainException(
                "MODIFIER.DESCRIPTION_TOO_LONG",
                "Mô tả lựa chọn vượt quá giới hạn.");
        }

        Description = normalized;
    }

    private void SetAdditionalPrice(long additionalPrice)
    {
        if (additionalPrice < 0 ||
            additionalPrice >
            BusinessRules.Modifiers.MaximumAdditionalPrice)
        {
            throw new DomainException(
                "MODIFIER.INVALID_ADDITIONAL_PRICE",
                "Giá cộng thêm không hợp lệ.");
        }

        AdditionalPrice = additionalPrice;
    }

    private void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0 ||
            displayOrder >
            BusinessRules.Categories.MaximumDisplayOrder)
        {
            throw new DomainException(
                "MODIFIER.INVALID_DISPLAY_ORDER",
                "Thứ tự hiển thị lựa chọn không hợp lệ.");
        }

        DisplayOrder = displayOrder;
    }
}