using System.Globalization;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public enum StorageStatusUiState
{
    NotChecked,
    Loading,
    Allowed,
    AllowedWithWarning,
    Insufficient,
    MetricsUnavailable
}

public sealed class StorageStatusViewModel : ViewModelBase, IDisposable
{
    private readonly IDatabaseStorageMonitor _monitor;
    private readonly ILogger<StorageStatusViewModel> _logger;
    private readonly object _sync = new();
    private CancellationTokenSource? _refreshSource;
    private StorageStatusUiState _state = StorageStatusUiState.NotChecked;
    private string _mainDatabaseSizeText = "Chưa có dữ liệu";
    private string _sqliteFootprintText = "Chưa có dữ liệu";
    private string _availableFreeText = "Chưa có dữ liệu";
    private string _lastCheckedText = "Chưa kiểm tra";
    private long _generation;
    private bool _disposed;

    public StorageStatusViewModel(
        IDatabaseStorageMonitor monitor,
        ILogger<StorageStatusViewModel> logger)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => CanRefresh);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public StorageStatusUiState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(GuidanceText));
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsLoading => State == StorageStatusUiState.Loading;
    public bool CanRefresh => !_disposed && !IsLoading;

    public string StatusText => State switch
    {
        StorageStatusUiState.NotChecked => "Chưa kiểm tra dung lượng",
        StorageStatusUiState.Loading => "Đang kiểm tra dung lượng...",
        StorageStatusUiState.Allowed => "Dung lượng an toàn",
        StorageStatusUiState.AllowedWithWarning => "Dung lượng trống đang thấp",
        StorageStatusUiState.Insufficient => "Không đủ dung lượng an toàn",
        StorageStatusUiState.MetricsUnavailable => "Chưa đọc được trạng thái dung lượng",
        _ => "Chưa đọc được trạng thái dung lượng"
    };

    public string GuidanceText => State switch
    {
        StorageStatusUiState.Allowed =>
            "Dung lượng hiện tại đang ở mức an toàn.",
        StorageStatusUiState.AllowedWithWarning =>
            "Dung lượng trống đang thấp. Nên giải phóng dung lượng trước khi cập nhật hoặc tiếp tục lưu nhiều dữ liệu.",
        StorageStatusUiState.Insufficient =>
            "Một số hoạt động cần thêm dung lượng có thể bị chặn an toàn. Hãy giải phóng dung lượng rồi thử lại.",
        StorageStatusUiState.Loading =>
            "Vui lòng chờ trong khi ứng dụng đọc thông tin dung lượng.",
        StorageStatusUiState.MetricsUnavailable =>
            "Chưa đọc được trạng thái dung lượng. Hãy thử làm mới.",
        _ => "Nhấn Làm mới để kiểm tra trạng thái dung lượng."
    };

    public string MainDatabaseSizeText
    {
        get => _mainDatabaseSizeText;
        private set => SetProperty(ref _mainDatabaseSizeText, value);
    }

    public string SqliteFootprintText
    {
        get => _sqliteFootprintText;
        private set => SetProperty(ref _sqliteFootprintText, value);
    }

    public string AvailableFreeText
    {
        get => _availableFreeText;
        private set => SetProperty(ref _availableFreeText, value);
    }

    public string LastCheckedText
    {
        get => _lastCheckedText;
        private set => SetProperty(ref _lastCheckedText, value);
    }

    public async Task RefreshAsync()
    {
        CancellationTokenSource source;
        StorageStatusUiState previousState;
        long generation;
        lock (_sync)
        {
            if (_disposed || _refreshSource is not null) return;
            source = new CancellationTokenSource();
            _refreshSource = source;
            generation = ++_generation;
            previousState = State;
        }

        State = StorageStatusUiState.Loading;
        try
        {
            var snapshot = await _monitor.GetSnapshotAsync(source.Token);
            source.Token.ThrowIfCancellationRequested();
            var result = _monitor.EvaluatePreflight(
                snapshot, new StoragePreflightRequest(0));
            source.Token.ThrowIfCancellationRequested();

            if (!IsCurrent(generation, source)) return;
            MainDatabaseSizeText = FormatBytes(snapshot.MainDatabaseBytes);
            SqliteFootprintText = FormatBytes(snapshot.TotalStorageFootprintBytes);
            AvailableFreeText = FormatBytes(snapshot.AvailableFreeBytes);
            LastCheckedText = snapshot.CapturedAtUtc.ToString(
                "dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
            State = Map(result.Status);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (IsCurrent(generation, source)) State = previousState;
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Warning(
                _logger, exception, "Không thể làm mới trạng thái dung lượng.");
            if (IsCurrent(generation, source))
            {
                ClearMetrics();
                State = StorageStatusUiState.MetricsUnavailable;
            }
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_refreshSource, source)) _refreshSource = null;
            }
            source.Dispose();
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public void CancelRefresh()
    {
        lock (_sync) _refreshSource?.Cancel();
    }

    public static string FormatBytes(long? bytes)
    {
        if (bytes is null) return "Không có dữ liệu";
        var value = (decimal)bytes.Value;
        string unit;
        decimal divisor;
        if (bytes.Value >= 1L << 40) { unit = "TB"; divisor = 1L << 40; }
        else if (bytes.Value >= 1L << 30) { unit = "GB"; divisor = 1L << 30; }
        else if (bytes.Value >= 1L << 20) { unit = "MB"; divisor = 1L << 20; }
        else if (bytes.Value >= 1L << 10) { unit = "KB"; divisor = 1L << 10; }
        else { unit = "B"; divisor = 1; }
        return string.Create(CultureInfo.InvariantCulture,
            $"{value / divisor:0.##} {unit}");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _refreshSource?.Cancel();
        }
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private bool IsCurrent(long generation, CancellationTokenSource source)
    {
        lock (_sync)
            return !_disposed && generation == _generation &&
                ReferenceEquals(_refreshSource, source);
    }

    private void ClearMetrics()
    {
        MainDatabaseSizeText = "Không có dữ liệu";
        SqliteFootprintText = "Không có dữ liệu";
        AvailableFreeText = "Không có dữ liệu";
        LastCheckedText = "Chưa kiểm tra";
    }

    private static StorageStatusUiState Map(StoragePreflightStatus status) => status switch
    {
        StoragePreflightStatus.Allowed => StorageStatusUiState.Allowed,
        StoragePreflightStatus.AllowedWithWarning => StorageStatusUiState.AllowedWithWarning,
        StoragePreflightStatus.Insufficient => StorageStatusUiState.Insufficient,
        StoragePreflightStatus.MetricsUnavailable => StorageStatusUiState.MetricsUnavailable,
        _ => StorageStatusUiState.MetricsUnavailable
    };
}
