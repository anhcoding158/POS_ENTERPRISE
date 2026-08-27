using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed class ScannerTestViewModel : ViewModelBase, IDisposable
{
    private string _captureText = string.Empty;
    private string _statusMessage =
        "Bấm “Quét thử mã”, sau đó quét một sản phẩm để kiểm tra.";
    private string _lastBarcode = string.Empty;
    private bool _isListening;
    private CancellationTokenSource? _timeout;

    public ScannerTestViewModel()
    {
        StartCommand = new AsyncRelayCommand(
            StartAsync,
            () => !IsListening);
        CancelCommand = new AsyncRelayCommand(
            CancelAsync,
            () => IsListening);
    }

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public string CaptureText
    {
        get => _captureText;
        set => SetProperty(ref _captureText, value);
    }

    public string LastBarcode
    {
        get => _lastBarcode;
        private set => SetProperty(ref _lastBarcode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (!SetProperty(ref _isListening, value))
                return;
            StartCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public event EventHandler? FocusRequested;

    private Task StartAsync()
    {
        CaptureText = string.Empty;
        LastBarcode = string.Empty;
        StatusMessage = "Đang chờ mã vạch…";
        IsListening = true;
        _timeout?.Cancel();
        _timeout?.Dispose();
        _timeout = new CancellationTokenSource();
        _ = ExpireAsync(_timeout.Token);
        FocusRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task CancelAsync()
    {
        _timeout?.Cancel();
        IsListening = false;
        StatusMessage = "Đã hủy kiểm tra máy quét.";
        return Task.CompletedTask;
    }

    public bool ReceiveScan(string? input)
    {
        if (!IsListening)
            return false;

        var normalized = BarcodeInputNormalizer.Normalize(input);
        if (normalized is null)
            return false;

        CaptureText = normalized;
        _timeout?.Cancel();
        LastBarcode = BarcodeInputNormalizer.ForDisplay(normalized);
        StatusMessage =
            $"Đã nhận mã: {LastBarcode}{Environment.NewLine}" +
            "Máy quét hoạt động bình thường.";
        IsListening = false;
        return true;
    }

    public void Timeout()
    {
        if (!IsListening)
            return;

        IsListening = false;
        StatusMessage = "Không nhận được mã trong thời gian chờ. Hãy thử lại.";
    }

    public void Dispose()
    {
        _timeout?.Cancel();
        _timeout?.Dispose();
        _timeout = null;
        GC.SuppressFinalize(this);
    }

    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            Timeout();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
