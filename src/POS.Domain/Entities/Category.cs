using POS.Domain.Common;
using POS.Domain.Constants;
using static POS.Domain.Constants.BusinessRules;

namespace POS.Domain.Entities;

/// <summary>
/// Danh mục sản phẩm.
/// </summary>
public sealed class Category : AuditableEntity
{
    private readonly List<Product> _products = [];

    private Category()
    {
    }

    public Category(
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

    public IReadOnlyCollection<Product> Products =>
        _products.AsReadOnly();

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
                "CATEGORY.NAME_REQUIRED",
                "Tên danh mục không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.Categories.NameMaxLength)
        {
            throw new DomainException(
                "CATEGORY.NAME_TOO_LONG",
                "Tên danh mục vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.Categories.DescriptionMaxLength)
        {
            throw new DomainException(
                "CATEGORY.DESCRIPTION_TOO_LONG",
                "Mô tả danh mục vượt quá giới hạn.");
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
                "CATEGORY.INVALID_DISPLAY_ORDER",
                "Thứ tự hiển thị danh mục không hợp lệ.");
        }

        DisplayOrder = displayOrder;
    }
}