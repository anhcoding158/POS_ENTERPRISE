using POS.Domain.Common;
using POS.Domain.Constants;
using static POS.Domain.Constants.BusinessRules;

namespace POS.Domain.Entities;

/// <summary>
/// Nhóm lựa chọn bổ sung như Size, Đường, Đá, Topping.
/// </summary>
public sealed class ModifierGroup : AuditableEntity
{
    private readonly List<Modifier> _modifiers = [];

    private ModifierGroup()
    {
    }

    public ModifierGroup(
        string name,
        bool isRequired,
        bool allowMultiple,
        int minimumSelections,
        int maximumSelections,
        int displayOrder,
        DateTimeOffset utcNow,
        string? description = null)
    {
        SetName(name);
        SetDescription(description);

        SetSelectionRules(
            isRequired,
            allowMultiple,
            minimumSelections,
            maximumSelections);

        SetDisplayOrder(displayOrder);

        IsActive = true;

        MarkCreated(utcNow);
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsRequired { get; private set; }

    public bool AllowMultiple { get; private set; }

    public int MinimumSelections { get; private set; }

    public int MaximumSelections { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Modifier> Modifiers =>
        _modifiers.AsReadOnly();

    public void Update(
        string name,
        string? description,
        bool isRequired,
        bool allowMultiple,
        int minimumSelections,
        int maximumSelections,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        SetName(name);
        SetDescription(description);

        SetSelectionRules(
            isRequired,
            allowMultiple,
            minimumSelections,
            maximumSelections);

        SetDisplayOrder(displayOrder);

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

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "MODIFIER_GROUP.NAME_REQUIRED",
                "Tên nhóm lựa chọn không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.ModifierGroups.NameMaxLength)
        {
            throw new DomainException(
                "MODIFIER_GROUP.NAME_TOO_LONG",
                "Tên nhóm lựa chọn vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.ModifierGroups.DescriptionMaxLength)
        {
            throw new DomainException(
                "MODIFIER_GROUP.DESCRIPTION_TOO_LONG",
                "Mô tả nhóm lựa chọn vượt quá giới hạn.");
        }

        Description = normalized;
    }

    private void SetSelectionRules(
        bool isRequired,
        bool allowMultiple,
        int minimumSelections,
        int maximumSelections)
    {
        if (minimumSelections < 0)
        {
            throw new DomainException(
                "MODIFIER_GROUP.INVALID_MINIMUM",
                "Số lựa chọn tối thiểu không được nhỏ hơn 0.");
        }

        if (maximumSelections <= 0 ||
            maximumSelections >
            BusinessRules.ModifierGroups.MaximumSelections)
        {
            throw new DomainException(
                "MODIFIER_GROUP.INVALID_MAXIMUM",
                "Số lựa chọn tối đa không hợp lệ.");
        }

        if (minimumSelections > maximumSelections)
        {
            throw new DomainException(
                "MODIFIER_GROUP.MIN_EXCEEDS_MAX",
                "Số lựa chọn tối thiểu không được lớn hơn tối đa.");
        }

        if (isRequired && minimumSelections == 0)
        {
            throw new DomainException(
                "MODIFIER_GROUP.REQUIRED_WITHOUT_MINIMUM",
                "Nhóm bắt buộc phải yêu cầu ít nhất một lựa chọn.");
        }

        if (!allowMultiple && maximumSelections != 1)
        {
            throw new DomainException(
                "MODIFIER_GROUP.SINGLE_SELECTION_MAXIMUM",
                "Nhóm chọn một phải có số lựa chọn tối đa bằng 1.");
        }

        IsRequired = isRequired;
        AllowMultiple = allowMultiple;
        MinimumSelections = minimumSelections;
        MaximumSelections = maximumSelections;
    }

    private void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0 ||
            displayOrder >
            BusinessRules.Categories.MaximumDisplayOrder)
        {
            throw new DomainException(
                "MODIFIER_GROUP.INVALID_DISPLAY_ORDER",
                "Thứ tự hiển thị nhóm lựa chọn không hợp lệ.");
        }

        DisplayOrder = displayOrder;
    }
}