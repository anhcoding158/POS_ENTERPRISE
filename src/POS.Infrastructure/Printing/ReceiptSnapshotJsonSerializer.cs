using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace POS.Infrastructure.Printing;

/// <summary>
/// JSON codec nghiêm ngặt dành riêng cho snapshot hóa đơn.
///
/// Không serialize trực tiếp ReceiptRequest vì DTO này có nhiều
/// constructor phục vụ tương thích. Thay vào đó, codec dùng wire
/// contract riêng và dựng lại DTO qua constructor nghiệp vụ để
/// mọi invariant đều được kiểm tra khi deserialize.
/// </summary>
public sealed class ReceiptSnapshotJsonSerializer :
    IReceiptSnapshotSerializer
{
    private const string
        ContractName =
            "POS.Enterprise.ReceiptSnapshot";

    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    false,

                WriteIndented =
                    false,

                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow,

                Encoder =
                    JavaScriptEncoder.Create(
                        UnicodeRanges.All)
            };

    public string Serialize(
        ReceiptRequest snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (snapshot.SnapshotVersion !=
            ReceiptRequest.CurrentSnapshotVersion)
        {
            throw new InvalidOperationException(
                "Không thể serialize phiên bản snapshot " +
                "không được hỗ trợ.");
        }

        var payload =
            MapToPayload(
                snapshot);

        return JsonSerializer.Serialize(
            payload,
            SerializerOptions);
    }

    public ReceiptRequest Deserialize(
        string json)
    {
        if (string.IsNullOrWhiteSpace(
                json))
        {
            throw new ArgumentException(
                "JSON snapshot không được để trống.",
                nameof(json));
        }

        try
        {
            var payload =
                JsonSerializer.Deserialize<
                    ReceiptSnapshotPayload>(
                        json,
                        SerializerOptions);

            if (payload is null)
            {
                throw new InvalidDataException(
                    "JSON snapshot không chứa dữ liệu.");
            }

            return MapFromPayload(
                payload);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "JSON snapshot hóa đơn không hợp lệ.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                "JSON snapshot chứa kiểu dữ liệu " +
                "không được hỗ trợ.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "Snapshot hóa đơn vi phạm invariant " +
                "nghiệp vụ.",
                exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                "Snapshot hóa đơn chứa giá trị " +
                "vượt giới hạn.",
                exception);
        }
    }

    private static ReceiptSnapshotPayload
        MapToPayload(
            ReceiptRequest snapshot)
    {
        return new ReceiptSnapshotPayload(
            Contract:
                ContractName,

            SnapshotVersion:
                snapshot.SnapshotVersion,

            Store:
                new ReceiptStorePayload(
                    Name:
                        snapshot.Store.Name,

                    Address:
                        snapshot.Store.Address,

                    Phone:
                        snapshot.Store.Phone,

                    TaxCode:
                        snapshot.Store.TaxCode,

                    FooterMessage:
                        snapshot.Store.FooterMessage,

                    IsConfigured:
                        snapshot.Store.IsConfigured),

            CopyKind:
                (int)snapshot.CopyKind,

            CopyNumber:
                snapshot.CopyNumber,

            OrderId:
                snapshot.OrderId,

            OrderCode:
                snapshot.OrderCode,

            CashierName:
                snapshot.CashierName,

            CustomerName:
                snapshot.CustomerName,

            RestaurantTableName:
                snapshot.RestaurantTableName,

            DiscountCode:
                snapshot.DiscountCode,

            Notes:
                snapshot.Notes,

            CreatedAtUtc:
                snapshot.CreatedAtUtc,

            PaidAtUtc:
                snapshot.PaidAtUtc,

            PaymentMethod:
                (int)snapshot.PaymentMethod,

            Subtotal:
                snapshot.Subtotal,

            DiscountAmount:
                snapshot.DiscountAmount,

            TotalAmount:
                snapshot.TotalAmount,

            CashReceived:
                snapshot.CashReceived,

            ChangeAmount:
                snapshot.ChangeAmount,

            Lines:
                snapshot.Lines
                    .Select(
                        MapLineToPayload)
                    .ToArray());
    }

    private static ReceiptLinePayload
        MapLineToPayload(
            ReceiptLineDto line)
    {
        return new ReceiptLinePayload(
            OrderItemId:
                line.OrderItemId,

            ProductId:
                line.ProductId,

            ProductCode:
                line.ProductCode,

            ProductName:
                line.ProductName,

            UnitName:
                line.UnitName,

            Quantity:
                line.Quantity,

            UnitSalePrice:
                line.UnitSalePrice,

            ModifierAmountPerUnit:
                line.ModifierAmountPerUnit,

            FinalUnitPrice:
                line.FinalUnitPrice,

            GrossAmount:
                line.GrossAmount,

            LineDiscountAmount:
                line.LineDiscountAmount,

            NetAmount:
                line.NetAmount,

            Notes:
                line.Notes,

            Modifiers:
                line.Modifiers
                    .Select(
                        MapModifierToPayload)
                    .ToArray());
    }

    private static ReceiptModifierPayload
        MapModifierToPayload(
            ReceiptModifierDto modifier)
    {
        return new ReceiptModifierPayload(
            ModifierId:
                modifier.ModifierId,

            ModifierGroupId:
                modifier.ModifierGroupId,

            ModifierGroupName:
                modifier.ModifierGroupName,

            Name:
                modifier.Name,

            Quantity:
                modifier.Quantity,

            UnitAdditionalPrice:
                modifier.UnitAdditionalPrice,

            AmountPerProductUnit:
                modifier.AmountPerProductUnit);
    }

    private static ReceiptRequest MapFromPayload(
        ReceiptSnapshotPayload payload)
    {
        if (!string.Equals(
                payload.Contract,
                ContractName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "JSON không phải contract snapshot hóa đơn " +
                "của POS Enterprise.");
        }

        if (payload.SnapshotVersion !=
            ReceiptRequest.CurrentSnapshotVersion)
        {
            throw new InvalidDataException(
                $"Phiên bản snapshot " +
                $"{payload.SnapshotVersion} không được hỗ trợ. " +
                $"Phiên bản hiện tại là " +
                $"{ReceiptRequest.CurrentSnapshotVersion}.");
        }

        var storePayload =
            payload.Store ??
            throw new InvalidDataException(
                "Snapshot thiếu thông tin cửa hàng.");

        var linePayloads =
            payload.Lines ??
            throw new InvalidDataException(
                "Snapshot thiếu danh sách dòng hàng.");

        var store =
            MapStoreFromPayload(
                storePayload);

        var lines =
            linePayloads
                .Select(
                    MapLineFromPayload)
                .ToArray();

        return new ReceiptRequest(
            store:
                store,

            copyKind:
                (ReceiptCopyKind)payload.CopyKind,

            copyNumber:
                payload.CopyNumber,

            orderId:
                payload.OrderId,

            orderCode:
                payload.OrderCode,

            cashierName:
                payload.CashierName,

            createdAtUtc:
                payload.CreatedAtUtc,

            paymentMethod:
                (PaymentMethod)payload.PaymentMethod,

            subtotal:
                payload.Subtotal,

            discountAmount:
                payload.DiscountAmount,

            totalAmount:
                payload.TotalAmount,

            cashReceived:
                payload.CashReceived,

            changeAmount:
                payload.ChangeAmount,

            lines:
                lines,

            customerName:
                payload.CustomerName,

            restaurantTableName:
                payload.RestaurantTableName,

            discountCode:
                payload.DiscountCode,

            notes:
                payload.Notes,

            paidAtUtc:
                payload.PaidAtUtc);
    }

    private static ReceiptStoreSnapshotDto
        MapStoreFromPayload(
            ReceiptStorePayload payload)
    {
        if (!payload.IsConfigured)
        {
            return ReceiptStoreSnapshotDto
                .Unconfigured;
        }

        return new ReceiptStoreSnapshotDto(
            name:
                payload.Name,

            address:
                payload.Address,

            phone:
                payload.Phone,

            taxCode:
                payload.TaxCode,

            footerMessage:
                payload.FooterMessage);
    }

    private static ReceiptLineDto
        MapLineFromPayload(
            ReceiptLinePayload? payload)
    {
        if (payload is null)
        {
            throw new InvalidDataException(
                "Snapshot chứa dòng hàng null.");
        }

        var modifierPayloads =
            payload.Modifiers ??
            throw new InvalidDataException(
                "Dòng hóa đơn thiếu danh sách modifier.");

        var modifiers =
            modifierPayloads
                .Select(
                    MapModifierFromPayload)
                .ToArray();

        return new ReceiptLineDto(
            orderItemId:
                payload.OrderItemId,

            productId:
                payload.ProductId,

            productCode:
                payload.ProductCode,

            productName:
                payload.ProductName,

            unitName:
                payload.UnitName,

            quantity:
                payload.Quantity,

            unitSalePrice:
                payload.UnitSalePrice,

            modifierAmountPerUnit:
                payload.ModifierAmountPerUnit,

            finalUnitPrice:
                payload.FinalUnitPrice,

            grossAmount:
                payload.GrossAmount,

            lineDiscountAmount:
                payload.LineDiscountAmount,

            netAmount:
                payload.NetAmount,

            notes:
                payload.Notes,

            modifiers:
                modifiers);
    }

    private static ReceiptModifierDto
        MapModifierFromPayload(
            ReceiptModifierPayload? payload)
    {
        if (payload is null)
        {
            throw new InvalidDataException(
                "Snapshot chứa modifier null.");
        }

        return new ReceiptModifierDto(
            modifierId:
                payload.ModifierId,

            modifierGroupId:
                payload.ModifierGroupId,

            modifierGroupName:
                payload.ModifierGroupName,

            name:
                payload.Name,

            quantity:
                payload.Quantity,

            unitAdditionalPrice:
                payload.UnitAdditionalPrice,

            amountPerProductUnit:
                payload.AmountPerProductUnit);
    }

    private sealed record ReceiptSnapshotPayload(
        string? Contract,
        int SnapshotVersion,
        ReceiptStorePayload? Store,
        int CopyKind,
        int CopyNumber,
        int OrderId,
        string? OrderCode,
        string? CashierName,
        string? CustomerName,
        string? RestaurantTableName,
        string? DiscountCode,
        string? Notes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset PaidAtUtc,
        int PaymentMethod,
        long Subtotal,
        long DiscountAmount,
        long TotalAmount,
        long CashReceived,
        long ChangeAmount,
        ReceiptLinePayload?[]? Lines);

    private sealed record ReceiptStorePayload(
        string? Name,
        string? Address,
        string? Phone,
        string? TaxCode,
        string? FooterMessage,
        bool IsConfigured);

    private sealed record ReceiptLinePayload(
        int OrderItemId,
        int ProductId,
        string? ProductCode,
        string? ProductName,
        string? UnitName,
        int Quantity,
        long UnitSalePrice,
        long ModifierAmountPerUnit,
        long FinalUnitPrice,
        long GrossAmount,
        long LineDiscountAmount,
        long NetAmount,
        string? Notes,
        ReceiptModifierPayload?[]? Modifiers);

    private sealed record ReceiptModifierPayload(
        int ModifierId,
        int ModifierGroupId,
        string? ModifierGroupName,
        string? Name,
        int Quantity,
        long UnitAdditionalPrice,
        long AmountPerProductUnit);
}