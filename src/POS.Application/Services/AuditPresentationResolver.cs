using System.Globalization;
using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Resolves immutable audit data into safe, user-facing labels. Legacy bulk rows
/// are recognized only when their persisted metadata is an exact bulk signature.
/// </summary>
public static class AuditPresentationResolver
{
    public const string LegacyBulkBusinessArea = "Sản phẩm và thao tác hàng loạt";
    public const string BulkBusinessArea = "Sản phẩm";
    public const string LegacyBulkTargetType = "Product bulk operation";
    public const string ProductImportBusinessArea = "Sản phẩm và nhập dữ liệu";
    public const string ProductImportTargetType = "Product import batch";

    public static string ActionText(SecurityAuditAction action) => action switch
    {
        SecurityAuditAction.EmployeeCreated => "Tạo nhân viên",
        SecurityAuditAction.EmployeeUpdated => "Cập nhật nhân viên",
        SecurityAuditAction.EmployeeDeactivated => "Ngừng hoạt động nhân viên",
        SecurityAuditAction.EmployeeReactivated => "Kích hoạt lại nhân viên",
        SecurityAuditAction.AccountCreated => "Tạo tài khoản",
        SecurityAuditAction.PasswordReset => "Đặt lại mật khẩu",
        SecurityAuditAction.AccountLocked => "Khóa tài khoản",
        SecurityAuditAction.AccountUnlocked => "Mở khóa tài khoản",
        SecurityAuditAction.RoleChanged => "Thay đổi vai trò",
        SecurityAuditAction.ForcedPasswordChangeCompleted => "Hoàn tất đổi mật khẩu",
        SecurityAuditAction.LoginFailed => "Đăng nhập thất bại",
        SecurityAuditAction.BulkProductPricesUpdated => "Cập nhật giá hàng loạt",
        SecurityAuditAction.BulkProductCategoryChanged => "Chuyển danh mục hàng loạt",
        SecurityAuditAction.BulkProductActiveStateChanged => "Đổi trạng thái bán hàng loạt",
        SecurityAuditAction.BulkProductMinimumStockChanged => "Đặt tồn tối thiểu hàng loạt",
        SecurityAuditAction.BulkProductOperation => "Thao tác sản phẩm hàng loạt",
        _ => "Hoạt động không xác định"
    };

    public static string ActionText(SecurityAuditAction action, string? businessArea, string? targetType)
    {
        if (IsProductImportContext(action, businessArea, targetType))
            return "Nhập dữ liệu sản phẩm";

        if (string.Equals(businessArea, "Nhân viên và tài khoản", StringComparison.Ordinal) &&
            string.Equals(targetType, "Tài khoản", StringComparison.Ordinal))
        {
            return action switch
            {
                SecurityAuditAction.EmployeeDeactivated => "Vô hiệu hóa tài khoản",
                SecurityAuditAction.EmployeeReactivated => "Kích hoạt lại tài khoản",
                _ => ActionText(action)
            };
        }

        return ActionText(action);
    }

    public static string ResultText(string? result) => result?.Trim() switch
    {
        "Success" => "Thành công",
        "Failed" => "Thất bại",
        "Cancelled" => "Đã hủy",
        "Pending" => "Đang xử lý",
        null or "" => "Không xác định",
        _ => result.Trim()
    };

    public static SecurityAuditAction ResolveAction(SecurityAuditEvent audit) =>
        IsProductImportContext(audit)
            ? SecurityAuditAction.BulkProductOperation
            : IsLegacyBulkCandidate(audit)
            ? ResolveLegacyBulkAction(audit)
            : audit.Action;

    public static string ResolveBusinessArea(SecurityAuditEvent audit) =>
        IsLegacyBulkCandidate(audit) ? BulkBusinessArea :
        string.IsNullOrWhiteSpace(audit.BusinessArea) ? "Nhân viên và tài khoản" : audit.BusinessArea;

    public static string ResolveTarget(SecurityAuditEvent audit)
    {
        if (!IsProductImportContext(audit) && !IsLegacyBulkCandidate(audit) && audit.Action is >= SecurityAuditAction.BulkProductPricesUpdated and <= SecurityAuditAction.BulkProductOperation)
        {
            var requested = ChangeValue(audit, "requested_count");
            if (int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
                return $"{count:N0} sản phẩm";
        }

        if (IsLegacyBulkCandidate(audit))
        {
            var requested = ChangeValue(audit, "requested_count");
            if (int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
                return $"{count:N0} sản phẩm";
        }

        return string.IsNullOrWhiteSpace(audit.TargetDisplayNameSnapshot)
            ? audit.TargetEmployeeId.HasValue ? $"Nhân viên #{audit.TargetEmployeeId}" : audit.TargetUserId.HasValue ? $"Tài khoản #{audit.TargetUserId}" : "Không xác định"
            : audit.TargetDisplayNameSnapshot;
    }

    public static string ResolveTargetType(SecurityAuditEvent audit) =>
        IsProductImportContext(audit)
            ? "Lô nhập sản phẩm"
            : IsLegacyBulkCandidate(audit) || audit.Action is >= SecurityAuditAction.BulkProductPricesUpdated and <= SecurityAuditAction.BulkProductOperation
            ? "Sản phẩm"
            : string.IsNullOrWhiteSpace(audit.TargetType) ? "Không xác định" : audit.TargetType;

    public static string TechnicalTarget(SecurityAuditEvent audit) =>
        IsProductImportContext(audit)
            ? audit.TargetDisplayNameSnapshot
            : IsLegacyBulkCandidate(audit) || audit.Action is >= SecurityAuditAction.BulkProductPricesUpdated and <= SecurityAuditAction.BulkProductOperation
            ? audit.TargetDisplayNameSnapshot
            : string.Empty;

    public static bool IsProductImportContext(SecurityAuditEvent audit) =>
        IsProductImportContext(audit.Action, audit.BusinessArea, audit.TargetType);

    public static bool IsLegacyBulkCandidate(SecurityAuditEvent audit) =>
        audit.Action == SecurityAuditAction.EmployeeUpdated &&
        audit.TargetEmployeeId is null && audit.TargetUserId is null &&
        audit.BusinessArea == LegacyBulkBusinessArea &&
        audit.TargetType == LegacyBulkTargetType &&
        audit.TargetDisplayNameSnapshot.StartsWith("Batch ", StringComparison.Ordinal) &&
        ChangeValue(audit, "operation") is not null;

    private static bool IsProductImportContext(SecurityAuditAction action, string? businessArea, string? targetType) =>
        (action is SecurityAuditAction.EmployeeUpdated or SecurityAuditAction.BulkProductOperation) &&
        string.Equals(businessArea, ProductImportBusinessArea, StringComparison.Ordinal) &&
        (string.Equals(targetType, ProductImportTargetType, StringComparison.Ordinal) ||
         string.Equals(targetType, "Lô nhập sản phẩm", StringComparison.Ordinal));

    private static SecurityAuditAction ResolveLegacyBulkAction(SecurityAuditEvent audit) => ChangeValue(audit, "operation") switch
    {
        nameof(BulkProductOperationType.SetPrices) => SecurityAuditAction.BulkProductPricesUpdated,
        nameof(BulkProductOperationType.SetCategory) => SecurityAuditAction.BulkProductCategoryChanged,
        nameof(BulkProductOperationType.SetActiveState) => SecurityAuditAction.BulkProductActiveStateChanged,
        nameof(BulkProductOperationType.SetMinimumStock) => SecurityAuditAction.BulkProductMinimumStockChanged,
        _ => SecurityAuditAction.BulkProductOperation
    };

    private static string? ChangeValue(SecurityAuditEvent audit, string fieldKey) =>
        SecurityAuditChangeSet.Deserialize(audit.BeforeValuesJson)
            .FirstOrDefault(change => change.FieldKey == fieldKey)?.AfterValue;
}
