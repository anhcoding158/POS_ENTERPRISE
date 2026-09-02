using POS.Application.Abstractions.Printing;
using POS.Application.Common;
using POS.Application.DTOs.Printing;

namespace POS.Infrastructure.Printing;

public sealed class WpfLabelPrintingService : ILabelPrintingService, IDisposable
{
    private readonly ILabelPrinterCatalog _catalog;
    private readonly ILabelPrintDispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public WpfLabelPrintingService(
        ILabelPrinterCatalog catalog,
        ILabelPrintDispatcher dispatcher)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result> PrintAsync(
        LabelPrintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (request.Job.Products.Count == 0 || request.EffectiveLabelCount <= 0)
        {
            return Result.Failure(new AppError(
                ErrorCodes.Printing.Failed,
                "Job in tem không có sản phẩm hoặc số lượng hợp lệ."));
        }
        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            return Result.Failure(new AppError(
                ErrorCodes.Printing.PrinterNotConfigured,
                "Hãy chọn máy in tem trước khi in."));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var printer = _catalog.Discover().FirstOrDefault(x =>
                string.Equals(x.Name, request.PrinterName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (printer is null || !printer.IsAvailable)
            {
                return Result.Failure(new AppError(
                    ErrorCodes.Printing.PrinterNotFound,
                    "Máy in tem đã chọn không còn khả dụng. Hãy tải lại và chọn lại."));
            }
            return await _dispatcher.DispatchAsync(request, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
