using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Bàn phục vụ trong chế độ F&B.
/// </summary>
public sealed class RestaurantTable : AuditableEntity
{
    private RestaurantTable()
    {
    }

    public RestaurantTable(
        int areaId,
        string code,
        string name,
        int capacity,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        SetAreaId(areaId);
        SetCode(code);
        SetName(name);
        SetCapacity(capacity);
        SetDisplayOrder(displayOrder);

        Status = TableStatus.Available;
        IsActive = true;

        MarkCreated(utcNow);
    }

    public int AreaId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public int DisplayOrder { get; private set; }

    public TableStatus Status { get; private set; }

    public bool IsActive { get; private set; }

    public Area? Area { get; private set; }

    public void UpdateDetails(
        string code,
        string name,
        int capacity,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        SetCode(code);
        SetName(name);
        SetCapacity(capacity);
        SetDisplayOrder(displayOrder);

        MarkUpdated(utcNow);
    }

    public void MoveToArea(
        int areaId,
        DateTimeOffset utcNow)
    {
        SetAreaId(areaId);
        MarkUpdated(utcNow);
    }

    public void ChangeStatus(
        TableStatus newStatus,
        DateTimeOffset utcNow)
    {
        if (!Enum.IsDefined(
                typeof(TableStatus),
                newStatus))
        {
            throw new DomainException(
                "TABLE.INVALID_STATUS",
                "Trạng thái bàn không hợp lệ.");
        }

        if (newStatus == TableStatus.Inactive)
        {
            Deactivate(utcNow);
            return;
        }

        if (!IsActive)
        {
            throw new DomainException(
                "TABLE.IS_INACTIVE",
                "Không thể thay đổi trạng thái của bàn đang ngừng hoạt động.");
        }

        if (Status == newStatus)
        {
            return;
        }

        if (!CanTransition(Status, newStatus))
        {
            throw new DomainException(
                "TABLE.INVALID_STATUS_TRANSITION",
                $"Không thể chuyển trạng thái bàn từ " +
                $"{Status} sang {newStatus}.");
        }

        Status = newStatus;

        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Status = TableStatus.Available;

        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive &&
            Status == TableStatus.Inactive)
        {
            return;
        }

        IsActive = false;
        Status = TableStatus.Inactive;

        MarkUpdated(utcNow);
    }

    private void SetAreaId(int areaId)
    {
        if (areaId <= 0)
        {
            throw new DomainException(
                "TABLE.INVALID_AREA_ID",
                "Khu vực của bàn không hợp lệ.");
        }

        AreaId = areaId;
    }

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(
                "TABLE.CODE_REQUIRED",
                "Mã bàn không được để trống.");
        }

        var normalized = code
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.RestaurantTables.CodeMaxLength)
        {
            throw new DomainException(
                "TABLE.CODE_TOO_LONG",
                "Mã bàn vượt quá giới hạn.");
        }

        Code = normalized;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "TABLE.NAME_REQUIRED",
                "Tên bàn không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.RestaurantTables.NameMaxLength)
        {
            throw new DomainException(
                "TABLE.NAME_TOO_LONG",
                "Tên bàn vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetCapacity(int capacity)
    {
        if (capacity <
                BusinessRules.RestaurantTables.MinimumCapacity ||
            capacity >
                BusinessRules.RestaurantTables.MaximumCapacity)
        {
            throw new DomainException(
                "TABLE.INVALID_CAPACITY",
                "Sức chứa của bàn không hợp lệ.");
        }

        Capacity = capacity;
    }

    private void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0 ||
            displayOrder >
            BusinessRules.Categories.MaximumDisplayOrder)
        {
            throw new DomainException(
                "TABLE.INVALID_DISPLAY_ORDER",
                "Thứ tự hiển thị bàn không hợp lệ.");
        }

        DisplayOrder = displayOrder;
    }

    private static bool CanTransition(
        TableStatus current,
        TableStatus next)
    {
        return current switch
        {
            TableStatus.Available =>
                next is TableStatus.Occupied
                    or TableStatus.Reserved
                    or TableStatus.Cleaning,

            TableStatus.Occupied =>
                next is TableStatus.Available
                    or TableStatus.Cleaning,

            TableStatus.Reserved =>
                next is TableStatus.Available
                    or TableStatus.Occupied,

            TableStatus.Cleaning =>
                next is TableStatus.Available,

            TableStatus.Inactive => false,

            _ => false
        };
    }
}