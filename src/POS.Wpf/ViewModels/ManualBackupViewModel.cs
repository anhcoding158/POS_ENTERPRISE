using System.IO;
using System.Globalization;
using POS.Application.Abstractions.Services;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public enum ManualBackupUiState
{
    Idle,
    Ready,
    Running,
    Success,
    Cancelled,
    Failed
}

public sealed class ManualBackupViewModel : ViewModelBase, IDisposable
{
    public const string ConsentText =
        "Tôi đã chọn đúng thư mục đích và muốn tạo backup dữ liệu đã verify.";

    private readonly IManualBackupService _service;
    private readonly IManualBackupFolderPicker _folderPicker;
    private readonly object _sync = new();
    private CancellationTokenSource? _operationCancellation;
    private bool _destinationWasSelected;
    private bool _consentAccepted;
    private bool _closeWhenComplete;
    private bool _disposed;
    private string? _destinationDirectory;
    private string? _backupPath;
    private string? _backupSizeText;
    private string? _backupSha256Text;
    private string? _completedAtText;
    private string _statusMessage =
        "Chọn thư mục đích và xác nhận trước khi sao lưu.";
    private ManualBackupUiState _state = ManualBackupUiState.Idle;

    public ManualBackupViewModel(
        IManualBackupService service,
        IManualBackupFolderPicker folderPicker)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));

        PickDestinationCommand = new AsyncRelayCommand(
            PickDestinationAsync, () => CanPickDestination, HandleCommandFailure);
        BackupCommand = new AsyncRelayCommand(
            BackupAsync, () => CanBackup, HandleCommandFailure);
        ResetCommand = new AsyncRelayCommand(
            ResetAsync, () => CanReset, HandleCommandFailure);
    }

    public event EventHandler? CloseReady;

    public AsyncRelayCommand PickDestinationCommand { get; }
    public AsyncRelayCommand BackupCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }

    public ManualBackupUiState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            NotifyPresentation();
        }
    }

    public string? DestinationDirectory
    {
        get => _destinationDirectory;
        private set
        {
            if (!SetProperty(ref _destinationDirectory, value)) return;
            OnPropertyChanged(nameof(DestinationDisplay));
            NotifyCommandStates();
        }
    }

    public string DestinationDisplay =>
        string.IsNullOrWhiteSpace(DestinationDirectory)
            ? "Chưa chọn thư mục đích"
            : DestinationDirectory;

    public bool ConsentAccepted
    {
        get => _consentAccepted;
        set
        {
            if (IsBusy || !SetProperty(ref _consentAccepted, value)) return;
            RefreshReadyState();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? BackupPath
    {
        get => _backupPath;
        private set => SetProperty(ref _backupPath, value);
    }

    public string? BackupSizeText
    {
        get => _backupSizeText;
        private set => SetProperty(ref _backupSizeText, value);
    }

    public string? BackupSha256Text
    {
        get => _backupSha256Text;
        private set => SetProperty(ref _backupSha256Text, value);
    }

    public string? CompletedAtText
    {
        get => _completedAtText;
        private set => SetProperty(ref _completedAtText, value);
    }

    public bool IsBusy => State == ManualBackupUiState.Running;
    public bool IsProgressVisible => IsBusy;
    public bool IsProgressIndeterminate => IsBusy;
    public bool CanPickDestination => !IsBusy;
    public bool CanChangeConsent => !IsBusy;
    public bool CanBackup => State == ManualBackupUiState.Ready &&
        _destinationWasSelected && ConsentAccepted && !IsBusy;
    public bool CanReset => State is ManualBackupUiState.Success or
        ManualBackupUiState.Cancelled or ManualBackupUiState.Failed;
    public bool ShowBackupButton => State is ManualBackupUiState.Idle or ManualBackupUiState.Ready;
    public bool ShowCloseButton => State is ManualBackupUiState.Success or
        ManualBackupUiState.Cancelled or ManualBackupUiState.Failed;

    public bool RequestClose()
    {
        if (!IsBusy) return true;
        _closeWhenComplete = true;
        return false;
    }

    internal async Task PickDestinationAsync()
    {
        if (!CanPickDestination) return;
        var selected = _folderPicker.PickDestination();
        if (string.IsNullOrWhiteSpace(selected) || !Path.IsPathFullyQualified(selected))
            return;

        DestinationDirectory = Path.GetFullPath(selected);
        _destinationWasSelected = true;
        BackupPath = null;
        BackupSizeText = null;
        BackupSha256Text = null;
        CompletedAtText = null;
        StatusMessage = "Đã chọn thư mục đích. Xác nhận rồi bấm Sao lưu dữ liệu.";
        RefreshReadyState();
        await Task.CompletedTask;
    }

    internal async Task BackupAsync()
    {
        if (!CanBackup || _operationCancellation is not null) return;

        var destination = DestinationDirectory!;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        BackupPath = null;
        BackupSizeText = null;
        BackupSha256Text = null;
        CompletedAtText = null;
        State = ManualBackupUiState.Running;
        StatusMessage = "Đang sao lưu và verify...";

        ManualBackupResult result;
        try
        {
            result = await _service.BackupAsync(
                new ManualBackupRequest(destination),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            result = ManualBackupResult.Failure(ManualBackupStatus.Cancelled);
        }
        catch
        {
            result = ManualBackupResult.Failure(ManualBackupStatus.UnexpectedFailure);
        }
        finally
        {
            _operationCancellation = null;
            cancellation.Dispose();
        }

        ApplyResult(result);
        ConsentAccepted = false;

        if (_closeWhenComplete)
        {
            _closeWhenComplete = false;
            CloseReady?.Invoke(this, EventArgs.Empty);
        }
    }

    internal Task ResetAsync()
    {
        if (!CanReset) return Task.CompletedTask;
        _destinationWasSelected = false;
        DestinationDirectory = null;
        ConsentAccepted = false;
        BackupPath = null;
        BackupSizeText = null;
        BackupSha256Text = null;
        CompletedAtText = null;
        State = ManualBackupUiState.Idle;
        StatusMessage = "Chọn thư mục đích và xác nhận trước khi sao lưu.";
        return Task.CompletedTask;
    }

    private void ApplyResult(ManualBackupResult result)
    {
        if (result.Status == ManualBackupStatus.Success &&
            !string.IsNullOrWhiteSpace(result.BackupFilePath) &&
            !string.IsNullOrWhiteSpace(result.Sha256Hex) &&
            result.BackupFileSizeBytes is not null &&
            result.CompletedAtUtc is not null)
        {
            State = ManualBackupUiState.Success;
            BackupPath = result.BackupFilePath;
            BackupSizeText = FormatBytes(result.BackupFileSizeBytes.Value) +
                $" ({result.BackupFileSizeBytes.Value:N0} byte)";
            BackupSha256Text = result.Sha256Hex;
            CompletedAtText = result.CompletedAtUtc.Value.ToString(
                "dd/MM/yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            StatusMessage = "Sao lưu dữ liệu thành công.";
            return;
        }

        BackupPath = null;
        BackupSizeText = null;
        BackupSha256Text = null;
        CompletedAtText = null;

        switch (result.Status)
        {
            case ManualBackupStatus.Cancelled:
                State = ManualBackupUiState.Cancelled;
                StatusMessage = "Đã hủy sao lưu.";
                break;
            case ManualBackupStatus.InvalidDestination:
                Fail("Thư mục đích không hợp lệ. Vui lòng chọn lại.");
                break;
            case ManualBackupStatus.DestinationUnavailable:
                Fail("Không thể ghi vào thư mục đã chọn. Vui lòng chọn thư mục khác.");
                break;
            case ManualBackupStatus.SourceUnavailable:
                Fail("Không thể mở database nguồn.");
                break;
            case ManualBackupStatus.ArchiveAlreadyExists:
                Fail("Tệp backup đã tồn tại. Vui lòng thử lại.");
                break;
            case ManualBackupStatus.VerificationFailed:
                Fail("Backup tạo xong nhưng không vượt qua verify.");
                break;
            default:
                Fail("Không thể sao lưu dữ liệu. Vui lòng thử lại.");
                break;
        }
    }

    private void Fail(string message)
    {
        State = ManualBackupUiState.Failed;
        StatusMessage = message;
    }

    private void RefreshReadyState()
    {
        if (IsBusy || CanReset) return;
        State = _destinationWasSelected && ConsentAccepted
            ? ManualBackupUiState.Ready
            : ManualBackupUiState.Idle;
        NotifyCommandStates();
    }

    private void HandleCommandFailure(Exception exception)
    {
        if (IsBusy) return;
        BackupPath = null;
        BackupSizeText = null;
        BackupSha256Text = null;
        CompletedAtText = null;
        Fail("Không thể thực hiện thao tác. Vui lòng thử lại.");
    }

    private void NotifyPresentation()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        OnPropertyChanged(nameof(CanPickDestination));
        OnPropertyChanged(nameof(CanChangeConsent));
        OnPropertyChanged(nameof(CanBackup));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(ShowBackupButton));
        OnPropertyChanged(nameof(ShowCloseButton));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        PickDestinationCommand.NotifyCanExecuteChanged();
        BackupCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    private static string FormatBytes(long bytes)
    {
        var value = (decimal)bytes;
        string unit;
        decimal divisor;
        if (bytes >= 1L << 40) { unit = "TB"; divisor = 1L << 40; }
        else if (bytes >= 1L << 30) { unit = "GB"; divisor = 1L << 30; }
        else if (bytes >= 1L << 20) { unit = "MB"; divisor = 1L << 20; }
        else if (bytes >= 1L << 10) { unit = "KB"; divisor = 1L << 10; }
        else { unit = "B"; divisor = 1; }
        return $"{value / divisor:0.##} {unit}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        CloseReady = null;
        GC.SuppressFinalize(this);
    }
}
