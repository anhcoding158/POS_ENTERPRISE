using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Printing;
using POS.Application.Common;
using POS.Application.DTOs.Printing;
using System.ComponentModel;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Dịch vụ gửi hóa đơn WPF đến máy in được cấu hình.
///
/// Dịch vụ này:
/// - dựng tài liệu từ ReceiptRequest bất biến;
/// - tìm máy in theo tên cấu hình;
/// - kiểm tra trạng thái cơ bản của máy in;
/// - gửi print job sau khi checkout đã commit;
/// - không thay đổi Order hoặc dữ liệu giao dịch.
///
/// Dịch vụ không được gọi từ CheckoutService.
/// </summary>
public sealed class WpfReceiptService :
    IReceiptService,
    IDisposable
{
    private const double
        K80WidthTolerance = 30;

    private readonly ReceiptDocumentBuilder
        _documentBuilder;

    private readonly ReceiptPrinterOptions
        _printerOptions;

    private readonly ILogger<WpfReceiptService>
        _logger;

    private readonly SemaphoreSlim
        _printLock =
            new(
                initialCount:
                    1,

                maxCount:
                    1);

    private bool
        _disposed;

    public WpfReceiptService(
        ReceiptDocumentBuilder documentBuilder,
        IOptions<ReceiptPrinterOptions> printerOptions,
        ILogger<WpfReceiptService> logger)
    {
        _documentBuilder =
            documentBuilder ??
            throw new ArgumentNullException(
                nameof(documentBuilder));

        ArgumentNullException.ThrowIfNull(
            printerOptions);

        _printerOptions =
            printerOptions.Value ??
            throw new ArgumentException(
                "Không đọc được cấu hình máy in.",
                nameof(printerOptions));

        _printerOptions.Validate();

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task<Result> PrintAsync(
        ReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        cancellationToken
            .ThrowIfCancellationRequested();

        await _printLock.WaitAsync(
            cancellationToken);

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var applicationDispatcher =
                System.Windows.Application
                    .Current?
                    .Dispatcher;

            /*
             * PrintDialog và FlowDocument phải được sử dụng
             * trên STA/WPF Dispatcher.
             */
            if (applicationDispatcher is not null &&
                !applicationDispatcher.CheckAccess())
            {
                var operation =
                    applicationDispatcher.InvokeAsync(
                        () =>
                            PrintCore(
                                request,
                                cancellationToken),

                        DispatcherPriority.Normal,
                        cancellationToken);

                return await operation
                    .Task
                    .ConfigureAwait(
                        false);
            }

            if (Thread.CurrentThread
                    .GetApartmentState() !=
                ApartmentState.STA)
            {
                return Failure(
                    ErrorCodes.General.Conflict,
                    "Không thể khởi tạo tác vụ in ngoài " +
                    "luồng giao diện WPF.");
            }

            return PrintCore(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(
                exception,
                "Không có quyền truy cập máy in {PrinterName}.",
                _printerOptions.PrinterName);

            return Failure(
                ErrorCodes.General.Forbidden,
                "Ứng dụng không có quyền truy cập máy in. " +
                "Hãy kiểm tra quyền Windows hoặc cài đặt máy in.");
        }
        catch (PrintQueueException exception)
        {
            _logger.LogError(
                exception,
                "Lỗi hàng đợi máy in {PrinterName}.",
                _printerOptions.PrinterName);

            return Failure(
                ErrorCodes.General.Conflict,
                "Hàng đợi máy in đang gặp lỗi. " +
                "Hãy kiểm tra máy in và thử lại.");
        }
        catch (PrintSystemException exception)
        {
            _logger.LogError(
                exception,
                "Windows Print System gặp lỗi với máy in " +
                "{PrinterName}.",
                _printerOptions.PrinterName);

            return Failure(
                ErrorCodes.General.Conflict,
                "Windows không thể gửi hóa đơn đến máy in. " +
                "Hãy kiểm tra kết nối và hàng đợi in.");
        }
        catch (Win32Exception exception)
        {
            _logger.LogError(
                exception,
                "Windows trả về lỗi khi in hóa đơn qua " +
                "{PrinterName}.",
                _printerOptions.PrinterName);

            return Failure(
                ErrorCodes.General.Unexpected,
                "Windows gặp lỗi khi gửi lệnh in hóa đơn.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể in hóa đơn {OrderCode} qua " +
                "{PrinterName}.",
                request.OrderCode,
                _printerOptions.PrinterName);

            return Failure(
                ErrorCodes.General.Unexpected,
                "Không thể in hóa đơn. " +
                "Giao dịch đã được lưu và có thể in lại sau.");
        }
        finally
        {
            _printLock.Release();
        }
    }

    private Result PrintCore(
        ReceiptRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        if (!request.Store.IsConfigured)
        {
            return Failure(
                ErrorCodes.General.Validation,
                "Chưa cấu hình thông tin cửa hàng nên " +
                "không thể in hóa đơn.");
        }

        var printerName =
            _printerOptions
                .GetNormalizedPrinterName();

        using var printServer =
            new LocalPrintServer();

        using var printQueue =
            FindPrintQueue(
                printServer,
                printerName);

        if (printQueue is null)
        {
            return Failure(
                ErrorCodes.General.NotFound,
                $"Không tìm thấy máy in '{printerName}'. " +
                "Hãy kiểm tra Hardware:PrinterName.");
        }

        printQueue.Refresh();

        var readinessError =
            GetReadinessError(
                printQueue);

        if (readinessError is not null)
        {
            return Result.Failure(
                readinessError);
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var printDialog =
            new PrintDialog
            {
                PrintQueue =
                    printQueue,

                PrintTicket =
                    CreatePrintTicket(
                        printQueue)
            };

        var document =
            _documentBuilder.Build(
                request);

        ConfigureDocumentPage(
            document,
            printDialog);

        var paginator =
            ((IDocumentPaginatorSource)document)
                .DocumentPaginator;

        if (IsValidDimension(
                document.PageWidth) &&
            IsValidDimension(
                document.PageHeight))
        {
            paginator.PageSize =
                new Size(
                    document.PageWidth,
                    document.PageHeight);
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        printDialog.PrintDocument(
            paginator,
            $"Hóa đơn {request.OrderCode}");

        _logger.LogInformation(
            "Đã gửi hóa đơn {OrderCode} đến máy in " +
            "{PrinterName}.",
            request.OrderCode,
            printerName);

        return Result.Success();
    }

    private PrintTicket CreatePrintTicket(
        PrintQueue printQueue)
    {
        var printTicket =
            printQueue.UserPrintTicket ??
            printQueue.DefaultPrintTicket ??
            new PrintTicket();

        printTicket.PageOrientation =
            PageOrientation.Portrait;

        /*
         * Ưu tiên media size gần 80 mm nếu driver
         * máy in công bố khổ tương ứng.
         *
         * Nếu driver không công bố K80, FlowDocument
         * vẫn giữ chiều rộng K80 do builder thiết lập.
         */
        try
        {
            var capabilities =
                printQueue.GetPrintCapabilities(
                    printTicket);

            var k80MediaSize =
                FindBestK80MediaSize(
                    capabilities);

            if (k80MediaSize is not null)
            {
                printTicket.PageMediaSize =
                    k80MediaSize;
            }
        }
        catch (PrintSystemException exception)
        {
            /*
             * Không chặn in chỉ vì driver không trả được
             * capabilities. PrintDocument vẫn dùng ticket
             * mặc định và FlowDocument K80.
             */
            _logger.LogWarning(
                exception,
                "Không đọc được khả năng khổ giấy của " +
                "máy in {PrinterName}.",
                printQueue.Name);
        }

        return printTicket;
    }

    private static PageMediaSize?
        FindBestK80MediaSize(
            PrintCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(
            capabilities);

        PageMediaSize? bestMatch =
            null;

        var bestDifference =
            double.MaxValue;

        foreach (var mediaSize in
                 capabilities
                     .PageMediaSizeCapability)
        {
            if (!mediaSize.Width.HasValue ||
                !IsValidDimension(
                    mediaSize.Width.Value))
            {
                continue;
            }

            var difference =
                Math.Abs(
                    mediaSize.Width.Value -
                    ReceiptDocumentBuilder
                        .K80PageWidth);

            if (difference >= bestDifference)
            {
                continue;
            }

            bestDifference =
                difference;

            bestMatch =
                mediaSize;
        }

        return bestDifference <=
            K80WidthTolerance
                ? bestMatch
                : null;
    }

    private static void ConfigureDocumentPage(
        FlowDocument document,
        PrintDialog printDialog)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            printDialog);

        var printableWidth =
            printDialog.PrintableAreaWidth;

        var printableHeight =
            printDialog.PrintableAreaHeight;

        /*
         * Không kéo hóa đơn giãn ra theo giấy A4.
         *
         * Chỉ thu nhỏ khi vùng in thật của máy nhỏ hơn
         * chiều rộng K80 mà builder đang sử dụng.
         */
        if (IsValidDimension(
                printableWidth) &&
            printableWidth <
            document.PageWidth)
        {
            document.PageWidth =
                printableWidth;
        }

        if (IsValidDimension(
                printableHeight))
        {
            document.PageHeight =
                printableHeight;
        }

        document.ColumnWidth =
            double.PositiveInfinity;
    }

    private static PrintQueue? FindPrintQueue(
        LocalPrintServer printServer,
        string printerName)
    {
        var queueTypes =
            new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            };

        PrintQueue? selectedQueue =
            null;

        var queues =
            printServer.GetPrintQueues(
                queueTypes);

        foreach (var queue in queues)
        {
            var nameMatches =
                string.Equals(
                    queue.Name,
                    printerName,
                    StringComparison.OrdinalIgnoreCase);

            var fullNameMatches =
                string.Equals(
                    queue.FullName,
                    printerName,
                    StringComparison.OrdinalIgnoreCase);

            if (selectedQueue is null &&
                (nameMatches ||
                 fullNameMatches))
            {
                selectedQueue =
                    queue;

                continue;
            }

            queue.Dispose();
        }

        return selectedQueue;
    }

    private static Error? GetReadinessError(
        PrintQueue printQueue)
    {
        var queueStatus =
            printQueue.QueueStatus;

        if (printQueue.IsOffline)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Máy in '{printQueue.Name}' đang offline.");
        }

        if (printQueue.IsNotAvailable)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Máy in '{printQueue.Name}' hiện không khả dụng.");
        }

        if (HasQueueStatus(
                queueStatus,
                PrintQueueStatus.PaperOut))
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Máy in '{printQueue.Name}' đang hết giấy.");
        }

        if (printQueue.IsPaused)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Hàng đợi máy in '{printQueue.Name}' " +
                "đang bị tạm dừng.");
        }

        if (printQueue.NeedUserIntervention)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Máy in '{printQueue.Name}' cần người dùng " +
                "kiểm tra.");
        }

        if (printQueue.IsInError)
        {
            return new Error(
                ErrorCodes.General.Conflict,
                $"Máy in '{printQueue.Name}' đang báo lỗi.");
        }

        return null;
    }

    private static bool HasQueueStatus(
        PrintQueueStatus currentStatus,
        PrintQueueStatus expectedStatus)
    {
        return (currentStatus & expectedStatus) ==
               expectedStatus;
    }

    private static bool IsValidDimension(
        double value)
    {
        return double.IsFinite(
                   value) &&
               value > 0;
    }

    private static Result Failure(
        string code,
        string message)
    {
        return Result.Failure(
            new Error(
                code,
                message));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _printLock.Dispose();

        _disposed =
            true;

        GC.SuppressFinalize(
            this);
    }
}