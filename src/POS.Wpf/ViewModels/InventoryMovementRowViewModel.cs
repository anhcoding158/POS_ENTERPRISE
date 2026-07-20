using System.Globalization;
using POS.Application.DTOs.Inventory;
using POS.Domain.Enums;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Mô hình hiển thị một dòng lịch sử tồn kho.
///
/// DTO Application giữ dữ liệu thuần.
/// Việc định dạng ngày giờ, số lượng và tên nghiệp vụ
/// thuộc trách nhiệm của WPF.
/// </summary>
public sealed class InventoryMovementRowViewModel
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    public InventoryMovementRowViewModel(
        InventoryMovementDto movement)
    {
        ArgumentNullException.ThrowIfNull(
            movement);

        Id = movement.Id;
        ProductId = movement.ProductId;

        ProductCode = movement.ProductCode;
        ProductName = movement.ProductName;
        UnitName = movement.UnitName;

        MovementType = movement.MovementType;

        QuantityBefore = movement.QuantityBefore;
        QuantityDelta = movement.QuantityDelta;
        QuantityAfter = movement.QuantityAfter;

        Reason = movement.Reason;

        ReferenceType = movement.ReferenceType;
        ReferenceId = movement.ReferenceId;

        PerformedByUserId =
            movement.PerformedByUserId;

        OccurredAtUtc =
            movement.OccurredAtUtc
                .ToUniversalTime();

        OccurredAtLocal =
            OccurredAtUtc
                .ToLocalTime();
    }

    public int Id { get; }

    public int ProductId { get; }

    public string ProductCode { get; }

    public string ProductName { get; }

    public string UnitName { get; }

    public InventoryMovementType MovementType { get; }

    public int QuantityBefore { get; }

    public int QuantityDelta { get; }

    public int QuantityAfter { get; }

    public string Reason { get; }

    public string? ReferenceType { get; }

    public string? ReferenceId { get; }

    public int? PerformedByUserId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public DateTimeOffset OccurredAtLocal { get; }

    public bool IsIncrease =>
        QuantityDelta > 0;

    public bool IsDecrease =>
        QuantityDelta < 0;

    public bool IsNeutral =>
        QuantityDelta == 0;

    public string ProductIdentityText =>
        $"{ProductCode}  •  {ProductName}";

    public string MovementTypeText =>
        MovementType switch
        {
            InventoryMovementType.StockIn =>
                "Nhập kho",

            InventoryMovementType.StockOut =>
                "Xuất kho",

            InventoryMovementType.Adjustment =>
                "Điều chỉnh",

            InventoryMovementType.Stocktake =>
                "Kiểm kê",

            InventoryMovementType.Sale =>
                "Bán hàng",

            InventoryMovementType.Refund =>
                "Hoàn hàng",

            InventoryMovementType.OpeningBalance =>
                "Tồn đầu kỳ",

            _ =>
                "Không xác định"
        };

    public string MovementDirectionText =>
        QuantityDelta switch
        {
            > 0 =>
                "Tăng kho",

            < 0 =>
                "Giảm kho",

            _ =>
                "Không chênh lệch"
        };

    public string OccurredDateText =>
        OccurredAtLocal.ToString(
            "dd/MM/yyyy",
            VietnameseCulture);

    public string OccurredTimeText =>
        OccurredAtLocal.ToString(
            "HH:mm:ss",
            VietnameseCulture);

    public string OccurredAtText =>
        OccurredAtLocal.ToString(
            "dd/MM/yyyy HH:mm:ss",
            VietnameseCulture);

    public string QuantityBeforeText =>
        FormatQuantity(
            QuantityBefore);

    public string QuantityDeltaText =>
        QuantityDelta switch
        {
            > 0 =>
                $"+{FormatQuantity(QuantityDelta)}",

            _ =>
                FormatQuantity(QuantityDelta)
        };

    public string QuantityAfterText =>
        FormatQuantity(
            QuantityAfter);

    public string ReferenceText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                    ReferenceType) ||
                string.IsNullOrWhiteSpace(
                    ReferenceId))
            {
                return "Không có chứng từ";
            }

            return
                $"{ReferenceType} • {ReferenceId}";
        }
    }

    public string PerformedByText =>
        PerformedByUserId.HasValue
            ? $"Người dùng #{PerformedByUserId.Value:N0}"
            : "Hệ thống / chưa đăng nhập";

    private string FormatQuantity(
        int quantity)
    {
        var number =
            quantity.ToString(
                "N0",
                VietnameseCulture);

        return string.IsNullOrWhiteSpace(
            UnitName)
                ? number
                : $"{number} {UnitName}";
    }
}