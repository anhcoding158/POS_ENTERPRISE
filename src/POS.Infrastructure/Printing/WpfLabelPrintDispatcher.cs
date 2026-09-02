using System.ComponentModel;
using System.Printing;
using System.Windows.Controls;
using POS.Application.Abstractions.Printing;
using POS.Application.Common;
using POS.Application.DTOs.Printing;
using POS.Application.Printing;

namespace POS.Infrastructure.Printing;

public sealed class WpfLabelPrintDispatcher : ILabelPrintDispatcher
{
    public Task<Result> DispatchAsync(
        LabelPrintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult(Failure("Không hỗ trợ in tem ngoài Windows."));
            }
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                return Task.FromResult(Failure("Tác vụ in tem phải chạy trên luồng giao diện Windows."));
            }
            using var server = new LocalPrintServer();
            using var queue = FindQueue(server, request.PrinterName);
            if (queue is null)
            {
                return Task.FromResult(Failure("Máy in tem đã chọn không còn khả dụng."));
            }
            queue.Refresh();
            if (queue.IsOffline || queue.IsNotAvailable || queue.IsPaused || queue.IsInError)
            {
                return Task.FromResult(Failure($"Máy in '{queue.Name}' hiện không khả dụng."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var dialog = new PrintDialog { PrintQueue = queue };
            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
            ticket.PageOrientation = PageOrientation.Portrait;
            var desiredWidth = MillimetreConverter.ToDip(request.Job.Template.WidthMm);
            var desiredHeight = MillimetreConverter.ToDip(request.Job.Template.HeightMm);
            try
            {
                var capabilities = queue.GetPrintCapabilities(ticket);
                if (capabilities.PageMediaSizeCapability.Count > 0 &&
                    !capabilities.PageMediaSizeCapability.Any(media =>
                        media.Width.HasValue &&
                        Math.Abs(media.Width.Value - desiredWidth) <= 6 &&
                        (!media.Height.HasValue || Math.Abs(media.Height.Value - desiredHeight) <= 6)))
                {
                    return Task.FromResult(Failure(
                        $"Máy in này không xác nhận khổ {request.Job.Template.WidthMm:0.##} × {request.Job.Template.HeightMm:0.##} mm. Hãy in một tem kiểm tra trước khi in số lượng lớn."));
                }
            }
            catch (PrintSystemException)
            {
                // Một số driver không công bố capability đầy đủ; dùng ticket tùy chỉnh.
            }
            ticket.PageMediaSize = new PageMediaSize(
                desiredWidth,
                desiredHeight);
            dialog.PrintTicket = ticket;

            var paginator = LabelDocumentBuilder.Build(request.Job, request.IsTestPrint);
            dialog.PrintDocument(paginator, request.IsTestPrint
                ? "In thử 1 tem giá"
                : $"In {request.EffectiveLabelCount:N0} tem giá");
            return Task.FromResult(Result.Success());
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(Failure("Không có quyền truy cập máy in tem."));
        }
        catch (PrintSystemException)
        {
            return Task.FromResult(Failure("Windows không thể gửi tem tới máy in đã chọn."));
        }
        catch (Win32Exception)
        {
            return Task.FromResult(Failure("Windows gặp lỗi khi gửi tem tới máy in."));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(Failure("Khổ tem hoặc PrintTicket không hợp lệ cho máy in đã chọn."));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(Failure("Máy in không thể tiếp nhận lệnh tem lúc này."));
        }
    }

    private static PrintQueue? FindQueue(LocalPrintServer server, string printerName)
    {
        var queueTypes = new[]
        {
            EnumeratedPrintQueueTypes.Local,
            EnumeratedPrintQueueTypes.Connections
        };
        var queues = server.GetPrintQueues(
            queueTypes);
        PrintQueue? selected = null;
        foreach (var queue in queues)
        {
            if (selected is null && string.Equals(queue.Name, printerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                selected = queue;
            }
            else
            {
                queue.Dispose();
            }
        }
        return selected;
    }

    private static Result Failure(string message) =>
        Result.Failure(new AppError(ErrorCodes.Printing.Failed, message));
}
