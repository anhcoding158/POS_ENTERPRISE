namespace POS.Domain.Enums;

/// <summary>
/// Trạng thái bàn trong chế độ nhà hàng hoặc quán cafe.
/// </summary>
public enum TableStatus
{
    Available = 1,

    Occupied = 2,

    Reserved = 3,

    Cleaning = 4,

    Inactive = 5
}