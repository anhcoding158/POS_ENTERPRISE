using POS.Domain.Common;
using POS.Domain.Constants;
using static POS.Domain.Constants.BusinessRules;

namespace POS.Domain.Entities;

/// <summary>
/// Khu vực bàn trong nhà hàng hoặc quán cafe.
/// Ví dụ: Tầng 1, Tầng 2, Ngoài trời.
/// </summary>
public sealed class Area : AuditableEntity
{
    private readonly List<RestaurantTable> _tables = [];

    private Area()
    {
    }

    public Area(
        string name,
        int displayOrder,
        DateTimeOffset utcNow,
        string? description = null)
    {
        SetName(name);
        SetDescription(description);
        SetDisplayOrder(displayOrder);

        IsActive = true;

        MarkCreated(utcNow);
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<RestaurantTable> Tables =>
        _tables.AsReadOnly();

    public void Update(
        string name,
        string? description,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        SetName(name);
        SetDescription(description);
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
                "AREA.NAME_REQUIRED",
                "Tên khu vực không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.Areas.NameMaxLength)
        {
            throw new DomainException(
                "AREA.NAME_TOO_LONG",
                "Tên khu vực vượt quá giới hạn cho phép.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.Areas.DescriptionMaxLength)
        {
            throw new DomainException(
                "AREA.DESCRIPTION_TOO_LONG",
                "Mô tả khu vực vượt quá giới hạn.");
        }

        Description = normalized;
    }

    private void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0 ||
            displayOrder >
            BusinessRules.Categories.MaximumDisplayOrder)
        {
            throw new DomainException(
                "AREA.INVALID_DISPLAY_ORDER",
                "Thứ tự hiển thị khu vực không hợp lệ.");
        }

        DisplayOrder = displayOrder;
    }
}