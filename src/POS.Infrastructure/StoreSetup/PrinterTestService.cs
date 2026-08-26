using System.Printing;
using POS.Application.Abstractions.StoreSetup;

namespace POS.Infrastructure.StoreSetup;

public sealed class PrinterTestService : IPrinterTestService
{
    public IReadOnlyList<PrinterInfo> Discover()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<PrinterInfo>();
        try
        {
            using var server = new LocalPrintServer();
            var defaultName = server.DefaultPrintQueue?.Name;
            return server.GetPrintQueues().Select(q => new PrinterInfo(q.Name, string.Equals(q.Name, defaultName, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (PrintSystemException) { return Array.Empty<PrinterInfo>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<PrinterInfo>(); }
    }

    public Task<PrinterTestResult> TestAsync(string? printerName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(printerName)) return Task.FromResult(new PrinterTestResult(PrinterTestStatus.NotConfigured, "Chưa chọn máy in."));
        try
        {
            if (!Discover().Any(x => string.Equals(x.Name, printerName.Trim(), StringComparison.OrdinalIgnoreCase))) return Task.FromResult(new PrinterTestResult(PrinterTestStatus.Unavailable, "Không tìm thấy máy in đã chọn hoặc máy in đang ngoại tuyến."));
            return Task.FromResult(new PrinterTestResult(PrinterTestStatus.Available, "Đã phát hiện máy in. Kiểm tra không gửi lệnh in thật khi chưa bấm xác nhận in."));
        }
        catch (UnauthorizedAccessException) { return Task.FromResult(new PrinterTestResult(PrinterTestStatus.AccessDenied, "Không có quyền truy cập máy in.")); }
    }
}
