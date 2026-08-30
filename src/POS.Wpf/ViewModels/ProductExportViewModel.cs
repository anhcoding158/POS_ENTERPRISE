using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using POS.Application.Abstractions.Exports;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Exports;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed record ProductExportReportOption(ProductExportReportType Type, string DisplayName, string Description);
public sealed record ProductExportFormatOption(ProductExportFormat Format, string DisplayName);

public sealed class ProductExportViewModel : ViewModelBase, IDisposable
{
    private readonly IProductExportService _exportService;
    private readonly IProductExportWriter _writer;
    private readonly ProductSearchRequest? _productFilters;
    private readonly InventorySearchRequest? _historyFilters;
    private bool _isBusy;
    private string _statusMessage = "Chọn loại dữ liệu và định dạng tệp.";
    private ProductExportReportOption _selectedReport;
    private ProductExportFormatOption _selectedFormat;

    public ProductExportViewModel(
        IProductExportService exportService,
        IProductExportWriter writer,
        ProductSearchRequest? productFilters = null,
        InventorySearchRequest? historyFilters = null,
        ProductExportReportType? initialReport = null)
    {
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _productFilters = productFilters;
        _historyFilters = historyFilters;

        Reports =
        [
            new(ProductExportReportType.ProductCatalog, "Danh sách sản phẩm", "Thông tin sản phẩm được phép xem."),
            new(ProductExportReportType.CurrentStock, "Tồn hiện tại", "Số tồn đang có trong hệ thống."),
            new(ProductExportReportType.LowStock, "Sản phẩm dưới/chạm tồn tối thiểu", "Theo đúng cảnh báo tồn hiện hữu."),
            new(ProductExportReportType.ArchivedProducts, "Sản phẩm đã lưu trữ", "Chỉ sản phẩm đã lưu trữ."),
            new(ProductExportReportType.InventoryHistory, "Lịch sử kho", "Các biến động theo bộ lọc lịch sử."),
            new(ProductExportReportType.ProductImportTemplate, "Mẫu nhập sản phẩm", "Mẫu trống gồm đúng 11 trường nhập.")
        ];
        Formats =
        [
            new(ProductExportFormat.Xlsx, "Excel (.xlsx)"),
            new(ProductExportFormat.Csv, "CSV (.csv)")
        ];
        _selectedReport = Reports.FirstOrDefault(report => report.Type == initialReport) ?? Reports[0];
        _selectedFormat = Formats[0];
        ExportCommand = new AsyncRelayCommand(ChooseDestinationAsync, () => !IsBusy, HandleException);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => !IsBusy, HandleException);
    }

    public event Action<bool?>? RequestClose;
    public IReadOnlyList<ProductExportReportOption> Reports { get; }
    public IReadOnlyList<ProductExportFormatOption> Formats { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    public ProductExportReportOption SelectedReport
    {
        get => _selectedReport;
        set { if (SetProperty(ref _selectedReport, value)) OnPropertyChanged(nameof(SelectedDescription)); }
    }

    public ProductExportFormatOption SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    public string SelectedDescription => SelectedReport.Description;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) ExportCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public async Task<bool> ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true;
        try
        {
            StatusMessage = "Đang chuẩn bị dữ liệu xuất...";
            var request = new ProductExportRequest(
                SelectedReport.Type,
                SelectedReport.Type == ProductExportReportType.InventoryHistory ? null : _productFilters,
                SelectedReport.Type == ProductExportReportType.InventoryHistory ? _historyFilters : null);
            var result = await _exportService.ExportAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                StatusMessage = result.AppError.Message;
                return false;
            }

            if (result.Value.RowCount == 0 && result.Value.ReportType != ProductExportReportType.ProductImportTemplate)
            {
                StatusMessage = "Không có dữ liệu phù hợp để xuất.";
                return false;
            }

            StatusMessage = "Đang tạo tệp; vui lòng chờ...";
            await _writer.WriteAsync(result.Value, SelectedFormat.Format, destinationPath, cancellationToken);
            StatusMessage = $"Đã tạo tệp {result.Value.RowCount:N0} dòng. Tệp cũ chỉ được thay thế sau khi ghi thành công.";
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy xuất tệp; tệp cũ không bị thay đổi.";
            return false;
        }
        catch (IOException)
        {
            StatusMessage = "Không thể ghi tệp. Hãy đóng tệp đang mở hoặc chọn thư mục có quyền ghi.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChooseDestinationAsync()
    {
        var extension = SelectedFormat.Format == ProductExportFormat.Xlsx ? "xlsx" : "csv";
        var dialog = new SaveFileDialog
        {
            Title = "Lưu tệp xuất",
            Filter = SelectedFormat.Format == ProductExportFormat.Xlsx ? "Excel (*.xlsx)|*.xlsx" : "CSV (*.csv)|*.csv",
            FileName = $"{SelectedReport.Type switch
            {
                ProductExportReportType.ProductCatalog => "danh-sach-san-pham",
                ProductExportReportType.CurrentStock => "ton-hien-tai",
                ProductExportReportType.LowStock => "san-pham-duoi-nguong-ton",
                ProductExportReportType.ArchivedProducts => "san-pham-da-luu-tru",
                ProductExportReportType.InventoryHistory => "lich-su-ton-kho",
                _ => "mau-nhap-san-pham"
            }}.{extension}",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(global::System.Windows.Application.Current?.MainWindow) == true)
        {
            await ExportAsync(dialog.FileName);
        }
    }

    private Task CloseAsync()
    {
        RequestClose?.Invoke(false);
        return Task.CompletedTask;
    }

    private void HandleException(Exception exception)
    {
        StatusMessage = exception is UnauthorizedAccessException
            ? "Không có quyền ghi vào thư mục đã chọn."
            : "Không thể xuất tệp. Hãy kiểm tra lại dữ liệu và thư mục đích.";
    }

    public void Dispose() { }
}
