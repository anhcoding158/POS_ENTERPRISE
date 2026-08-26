using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using POS.Application.Abstractions.StoreSetup;

namespace POS.Application.Validation;

public sealed partial class StoreSettingsValidator : IStoreSettingsValidator
{
    public StoreSettingsValidationResult Validate(StoreSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var issues = new List<StoreSettingsIssue>();
        RequiredText(settings.StoreName, "StoreName", "Tên cửa hàng là bắt buộc.", 160, issues);
        OptionalText(settings.Address, "Address", "Địa chỉ không hợp lệ.", 320, issues, normalizeLineEndings: true);
        if (!string.IsNullOrWhiteSpace(settings.Hotline) &&
            !HotlineRegex().IsMatch(NormalizeSpaces(settings.Hotline!)))
            Error(issues, "Hotline.Invalid", "Hotline", "Hotline chỉ được chứa số và định dạng điện thoại Việt Nam hợp lệ.");
        if (!string.IsNullOrWhiteSpace(settings.TaxCode) &&
            !TaxCodeRegex().IsMatch(settings.TaxCode!.Trim()))
            Error(issues, "TaxCode.Invalid", "TaxCode", "Mã số thuế phải có dạng 10 hoặc 13 chữ số.");
        if (!Enum.IsDefined(settings.Currency)) Error(issues, "Currency.Invalid", "Currency", "Loại tiền tệ không được hỗ trợ.");
        if (!Enum.IsDefined(settings.PaperSize)) Error(issues, "PaperSize.Invalid", "PaperSize", "Khổ giấy không được hỗ trợ.");
        if (!Enum.IsDefined(settings.Scanner)) Error(issues, "Scanner.Invalid", "Scanner", "Chế độ máy quét không được hỗ trợ.");
        if (!Enum.IsDefined(settings.CashDrawer)) Error(issues, "CashDrawer.Invalid", "CashDrawer", "Chế độ két tiền không được hỗ trợ.");
        if (settings.PrintCopyCount is < 1 or > 5) Error(issues, "Copies.Range", "PrintCopyCount", "Số bản in phải từ 1 đến 5.");
        if (!ValidateTimeZone(settings.TimeZoneId)) Error(issues, "TimeZone.Invalid", "TimeZoneId", "Múi giờ không tồn tại trên máy này.");
        ValidatePathText(settings.DatabaseDirectory, "DatabaseDirectory", issues);
        ValidatePathText(settings.BackupDirectory, "BackupDirectory", issues);
        ValidateRetention(settings.Retention, issues);
        ValidateVietQr(settings, issues);
        if (settings.AutoPrint && string.IsNullOrWhiteSpace(settings.DefaultPrinter))
            Error(issues, "Printer.Required", "DefaultPrinter", "Phải chọn máy in khi bật tự động in.");
        if (settings.CashDrawer == CashDrawerMode.PrinterPulse && string.IsNullOrWhiteSpace(settings.DefaultPrinter))
            Error(issues, "CashDrawer.PrinterRequired", "DefaultPrinter", "Két tiền theo xung máy in cần máy in mặc định.");
        if (!string.IsNullOrWhiteSpace(settings.DefaultPrinter) && settings.DefaultPrinter!.Any(char.IsControl))
            Error(issues, "Printer.Invalid", "DefaultPrinter", "Tên máy in không hợp lệ.");
        return new StoreSettingsValidationResult(new ReadOnlyCollection<StoreSettingsIssue>(issues));
    }

    private static void ValidateVietQr(StoreSettingsSnapshot s, List<StoreSettingsIssue> issues)
    {
        if (!s.VietQrEnabled) return;
        if (string.IsNullOrWhiteSpace(s.BankBin) || !BankBinRegex().IsMatch(RemoveSpaces(s.BankBin!))) Error(issues, "VietQr.BankBin", "BankBin", "Mã BIN ngân hàng phải gồm đúng 6 chữ số.");
        if (string.IsNullOrWhiteSpace(s.BankAccountNumber) || !AccountRegex().IsMatch(RemoveSpaces(s.BankAccountNumber!))) Error(issues, "VietQr.AccountNumber", "BankAccountNumber", "Số tài khoản phải gồm 3 đến 19 chữ số.");
        RequiredText(s.BankAccountName, "BankAccountName", "Tên tài khoản VietQR là bắt buộc.", 100, issues);
        RequiredText(s.VietQrContent, "VietQrContent", "Nội dung VietQR là bắt buộc.", 100, issues);
    }

    private static void ValidateRetention(StoreRetentionPolicy p, List<StoreSettingsIssue> issues)
    {
        if (p.LatestCount is < 1 or > 100) Error(issues, "Retention.Latest", "Retention.LatestCount", "Giữ lại bản gần nhất phải từ 1 đến 100.");
        if (p.WeeklyCount is < 0 or > 52) Error(issues, "Retention.Weekly", "Retention.WeeklyCount", "Số bản theo tuần phải từ 0 đến 52.");
        if (p.MonthlyCount is < 0 or > 24) Error(issues, "Retention.Monthly", "Retention.MonthlyCount", "Số bản theo tháng phải từ 0 đến 24.");
        if (p.LatestCount == 0 && p.WeeklyCount == 0 && p.MonthlyCount == 0) Error(issues, "Retention.Empty", "Retention", "Chính sách lưu backup không được để trống.");
        if (p.MaximumTotalBytes is < 1_048_576 or > 1_099_511_627_776) Error(issues, "Retention.Quota", "Retention.MaximumTotalBytes", "Dung lượng retention không hợp lệ.");
    }

    private static void ValidatePathText(string? value, string field, List<StoreSettingsIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Path.IsPathFullyQualified(value.Trim()) || value.Any(char.IsControl)) Error($"{field}.Invalid", field, "Đường dẫn phải là đường dẫn cục bộ tuyệt đối hợp lệ.", issues);
    }

    private static bool ValidateTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); return true; } catch (TimeZoneNotFoundException) { return false; } catch (InvalidTimeZoneException) { return false; }
    }

    private static void RequiredText(string? value, string field, string message, int max, List<StoreSettingsIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) { Error(field + ".Required", field, message, issues); return; }
        OptionalText(value, field, message, max, issues);
    }

    private static void OptionalText(string? value, string field, string message, int max, List<StoreSettingsIssue> issues, bool normalizeLineEndings = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = normalizeLineEndings ? value!.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') : value!.Trim();
        if (normalized.Length > max || normalized.Any(c => char.IsControl(c) && c is not '\n' and not '\t')) Error(field + ".Invalid", field, message, issues);
    }

    private static string NormalizeSpaces(string value) => string.Join(" ", value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string RemoveSpaces(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
    private static void Error(string code, string field, string message, List<StoreSettingsIssue> issues) => issues.Add(new(code, field, message));
    private static void Error(List<StoreSettingsIssue> issues, string code, string field, string message) => Error(code, field, message, issues);

    [GeneratedRegex("^\\+?[0-9][0-9 .()\\-]{5,22}$", RegexOptions.CultureInvariant)] private static partial Regex HotlineRegex();
    [GeneratedRegex("^[0-9]{10}(?:-[0-9]{3})?$", RegexOptions.CultureInvariant)] private static partial Regex TaxCodeRegex();
    [GeneratedRegex("^[0-9]{6}$", RegexOptions.CultureInvariant)] private static partial Regex BankBinRegex();
    [GeneratedRegex("^[0-9]{3,19}$", RegexOptions.CultureInvariant)] private static partial Regex AccountRegex();
}
