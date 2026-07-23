using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;
using POS.Infrastructure.Printing;
using POS.Wpf.Views;
using System.Windows;
using System.Windows.Threading;

namespace POS.Wpf.Services;

/// <summary>
/// Mở dialog xem trước hóa đơn sau khi checkout đã thành công.
///
/// Service thuộc tầng Presentation:
/// - không thay đổi Order;
/// - không giữ DbContext;
/// - không tạo receipt snapshot mới;
/// - chỉ hiển thị và cho phép in snapshot đã chốt.
/// </summary>
public interface IReceiptPreviewService
{
    Task ShowAsync(
        ReceiptRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// WPF implementation của receipt preview.
///
/// Mỗi lần gọi tạo một ReceiptPreviewWindow mới.
/// Builder và print service được DI quản lý.
/// </summary>
public sealed class ReceiptPreviewService :
    IReceiptPreviewService
{
    private readonly ReceiptDocumentBuilder
        _documentBuilder;

    private readonly IReceiptService
        _receiptService;

    public ReceiptPreviewService(
        ReceiptDocumentBuilder documentBuilder,
        IReceiptService receiptService)
    {
        _documentBuilder =
            documentBuilder ??
            throw new ArgumentNullException(
                nameof(documentBuilder));

        _receiptService =
            receiptService ??
            throw new ArgumentNullException(
                nameof(receiptService));
    }

    public async Task ShowAsync(
        ReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        if (!request.Store.IsConfigured)
        {
            throw new InvalidOperationException(
                "Không thể xem trước hóa đơn vì thông tin " +
                "cửa hàng chưa được cấu hình.");
        }

        var application =
            global::System.Windows.Application
                .Current ??
            throw new InvalidOperationException(
                "Không tìm thấy WPF Application hiện tại.");

        var dispatcher =
            application.Dispatcher;

        if (dispatcher.CheckAccess())
        {
            ShowCore(
                application,
                request);

            return;
        }

        var operation =
            dispatcher.InvokeAsync(
                () =>
                    ShowCore(
                        application,
                        request),

                DispatcherPriority.Normal,
                cancellationToken);

        await operation
            .Task
            .ConfigureAwait(
                false);
    }

    private void ShowCore(
        global::System.Windows.Application application,
        ReceiptRequest request)
    {
        var owner =
            FindActiveOwner(
                application);

        var previewWindow =
            new ReceiptPreviewWindow(
                request,
                _documentBuilder,
                _receiptService);

        if (owner is not null &&
            !ReferenceEquals(
                owner,
                previewWindow))
        {
            previewWindow.Owner =
                owner;
        }

        previewWindow.ShowDialog();
    }

    private static Window? FindActiveOwner(
        global::System.Windows.Application application)
    {
        ArgumentNullException.ThrowIfNull(
            application);

        var activeWindow =
            application.Windows
                .OfType<Window>()
                .FirstOrDefault(
                    window =>
                        window.IsActive &&
                        window.IsVisible &&
                        window is not
                            ReceiptPreviewWindow);

        if (activeWindow is not null)
        {
            return activeWindow;
        }

        var mainWindow =
            application.MainWindow;

        return mainWindow is not null &&
               mainWindow.IsVisible
            ? mainWindow
            : null;
    }
}