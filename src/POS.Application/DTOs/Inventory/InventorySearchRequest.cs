using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Inventory;

/// <summary>
/// Điều kiện tìm kiếm lịch sử tồn kho.
///
/// Thời gian được chuẩn hóa sang UTC.
/// PageNumber bắt đầu từ 1.
/// </summary>
public sealed class InventorySearchRequest
{
    public InventorySearchRequest(
        int? productId = null,
        InventoryMovementType? movementType = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? referenceType = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        if (productId.HasValue &&
            productId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        if (movementType.HasValue &&
            (
                movementType.Value ==
                    InventoryMovementType.Unknown ||
                !Enum.IsDefined(movementType.Value)
            ))
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementType),
                movementType,
                "Loại biến động tồn kho không hợp lệ.");
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0 ||
            pageSize >
                BusinessRules.Inventory.MaximumSearchPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Kích thước trang phải từ 1 đến " +
                $"{BusinessRules.Inventory.MaximumSearchPageSize}.");
        }

        var normalizedFrom =
            NormalizeOptionalUtc(fromUtc);

        var normalizedTo =
            NormalizeOptionalUtc(toUtc);

        if (normalizedFrom.HasValue &&
            normalizedTo.HasValue &&
            normalizedFrom.Value >
            normalizedTo.Value)
        {
            throw new ArgumentException(
                "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
        }

        ProductId = productId;
        MovementType = movementType;

        FromUtc = normalizedFrom;
        ToUtc = normalizedTo;

        ReferenceType =
            NormalizeOptionalReferenceType(
                referenceType);

        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public int? ProductId { get; }

    public InventoryMovementType? MovementType { get; }

    public DateTimeOffset? FromUtc { get; }

    public DateTimeOffset? ToUtc { get; }

    public string? ReferenceType { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    private static DateTimeOffset? NormalizeOptionalUtc(
        DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value == default)
        {
            throw new ArgumentException(
                "Thời điểm tìm kiếm không hợp lệ.",
                nameof(value));
        }

        return value.Value.ToUniversalTime();
    }

    private static string? NormalizeOptionalReferenceType(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Inventory.ReferenceTypeMaxLength)
        {
            throw new ArgumentException(
                "Loại tham chiếu vượt quá giới hạn.",
                nameof(value));
        }

        return normalized;
    }
}