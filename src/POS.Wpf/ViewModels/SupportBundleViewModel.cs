using System.IO;
using POS.Application.Abstractions.Services;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public enum SupportBundleUiState
{
    Idle,
    Ready,
    Running,
    Cancelling,
    Success,
    Cancelled,
    Failed
}

public sealed class SupportBundleViewModel : ViewModelBase, IDisposable
{
    public const string ConsentText =
        "Tôi đã đọc và đồng ý tạo gói chẩn đoán để hỗ trợ xử lý sự cố.";

    private readonly ISupportBundleService _service;
    private readonly ISupportBundleFolderPicker _folderPicker;
    private CancellationTokenSource? _operationCancellation;
    private bool _destinationWasSelected;
    private bool _consentAccepted;
    private bool _closeWhenComplete;
    private bool _disposed;
    private string? _destinationDirectory;
    private string? _archiveName;
    private string? _archivePath;
    private string _statusMessage = "Chọn thư mục lưu và đọc thông tin trước khi tiếp tục.";
    private SupportBundleUiState _state = SupportBundleUiState.Idle;

    public SupportBundleViewModel(
        ISupportBundleService service,
        ISupportBundleFolderPicker folderPicker)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));

        PickDestinationCommand = new AsyncRelayCommand(
            PickDestinationAsync, () => CanPickDestination, HandleCommandFailure);
        GenerateCommand = new AsyncRelayCommand(
            GenerateAsync, () => CanGenerate, HandleCommandFailure);
        CancelCommand = new AsyncRelayCommand(
            CancelAsync, () => CanCancel, HandleCommandFailure);
        ResetCommand = new AsyncRelayCommand(
            ResetAsync, () => CanReset, HandleCommandFailure);
    }

    public event EventHandler? CloseReady;

    public AsyncRelayCommand PickDestinationCommand { get; }
    public AsyncRelayCommand GenerateCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }

    public SupportBundleUiState State
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
            ? "Chưa chọn thư mục lưu"
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

    public string? ArchiveName
    {
        get => _archiveName;
        private set => SetProperty(ref _archiveName, value);
    }

    public string? ArchivePath
    {
        get => _archivePath;
        private set => SetProperty(ref _archivePath, value);
    }

    public bool IsBusy => State is SupportBundleUiState.Running or SupportBundleUiState.Cancelling;
    public bool IsProgressVisible => IsBusy;
    public bool IsProgressIndeterminate =>
        State is not (SupportBundleUiState)(-1);
    public bool CanPickDestination => !IsBusy;
    public bool CanChangeConsent => !IsBusy;
    public bool CanGenerate => State == SupportBundleUiState.Ready &&
        _destinationWasSelected && ConsentAccepted && !IsBusy;
    public bool CanCancel => State == SupportBundleUiState.Running;
    public bool CanReset => State is SupportBundleUiState.Success or
        SupportBundleUiState.Cancelled or SupportBundleUiState.Failed;
    public bool ShowGenerateButton => State is SupportBundleUiState.Idle or SupportBundleUiState.Ready;
    public bool ShowCloseButton => State is SupportBundleUiState.Success or
        SupportBundleUiState.Cancelled or SupportBundleUiState.Failed;

    public bool RequestClose()
    {
        if (!IsBusy) return true;
        _closeWhenComplete = true;
        RequestCancellation();
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
        ArchiveName = null;
        ArchivePath = null;
        StatusMessage = "Đã chọn thư mục lưu. Hãy xác nhận đồng ý để tạo gói hỗ trợ.";
        RefreshReadyState();
        await Task.CompletedTask;
    }

    internal async Task GenerateAsync()
    {
        if (!CanGenerate || _operationCancellation is not null) return;

        var destination = DestinationDirectory!;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        ArchiveName = null;
        ArchivePath = null;
        State = SupportBundleUiState.Running;
        StatusMessage = "Đang tạo gói hỗ trợ...";

        SupportBundleResult result;
        try
        {
            result = await _service.ExportAsync(
                new SupportBundleRequest(destination, IncludeDatabase: false),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            result = SupportBundleResult.Failure(SupportBundleStatus.Cancelled);
        }
        catch
        {
            result = SupportBundleResult.Failure(SupportBundleStatus.UnexpectedFailure);
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

    internal Task CancelAsync()
    {
        RequestCancellation();
        return Task.CompletedTask;
    }

    internal Task ResetAsync()
    {
        if (!CanReset) return Task.CompletedTask;
        _destinationWasSelected = false;
        DestinationDirectory = null;
        ConsentAccepted = false;
        ArchiveName = null;
        ArchivePath = null;
        State = SupportBundleUiState.Idle;
        StatusMessage = "Chọn thư mục lưu và đọc thông tin trước khi tiếp tục.";
        return Task.CompletedTask;
    }

    private void RequestCancellation()
    {
        if (State != SupportBundleUiState.Running || _operationCancellation is null) return;
        State = SupportBundleUiState.Cancelling;
        StatusMessage = "Đang hủy...";
        _operationCancellation.Cancel();
    }

    private void ApplyResult(SupportBundleResult result)
    {
        if (result.Status == SupportBundleStatus.Success &&
            !string.IsNullOrWhiteSpace(result.ArchivePath) &&
            Path.IsPathFullyQualified(result.ArchivePath))
        {
            State = SupportBundleUiState.Success;
            ArchivePath = result.ArchivePath;
            ArchiveName = Path.GetFileName(result.ArchivePath);
            StatusMessage = "Gói hỗ trợ đã được tạo thành công.";
            return;
        }

        ArchiveName = null;
        ArchivePath = null;
        switch (result.Status)
        {
            case SupportBundleStatus.Cancelled:
                State = SupportBundleUiState.Cancelled;
                StatusMessage = "Đã hủy tạo gói hỗ trợ.";
                break;
            case SupportBundleStatus.InvalidDestination:
                Fail("Thư mục lưu không hợp lệ. Vui lòng chọn lại.");
                break;
            case SupportBundleStatus.DestinationUnavailable:
                Fail("Không thể ghi vào thư mục đã chọn. Vui lòng chọn thư mục khác.");
                break;
            case SupportBundleStatus.ArchiveAlreadyExists:
                Fail("Tên tệp gói hỗ trợ đã tồn tại. Vui lòng thử tạo lại.");
                break;
            case SupportBundleStatus.DatabaseInclusionNotSupported:
                Fail("Yêu cầu tạo gói hỗ trợ không được hỗ trợ.");
                break;
            case SupportBundleStatus.ArchiveCreationFailure:
                Fail("Không thể tạo tệp gói hỗ trợ. Vui lòng kiểm tra thư mục lưu.");
                break;
            default:
                Fail("Không thể tạo gói hỗ trợ. Vui lòng thử lại.");
                break;
        }
    }

    private void Fail(string message)
    {
        State = SupportBundleUiState.Failed;
        StatusMessage = message;
    }

    private void RefreshReadyState()
    {
        if (IsBusy || CanReset) return;
        State = _destinationWasSelected && ConsentAccepted
            ? SupportBundleUiState.Ready
            : SupportBundleUiState.Idle;
        NotifyCommandStates();
    }

    private void HandleCommandFailure(Exception exception)
    {
        if (IsBusy) return;
        ArchiveName = null;
        ArchivePath = null;
        Fail("Không thể thực hiện thao tác. Vui lòng thử lại.");
    }

    private void NotifyPresentation()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        OnPropertyChanged(nameof(CanPickDestination));
        OnPropertyChanged(nameof(CanChangeConsent));
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(ShowGenerateButton));
        OnPropertyChanged(nameof(ShowCloseButton));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        PickDestinationCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _operationCancellation?.Cancel(); } catch { }
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        CloseReady = null;
        GC.SuppressFinalize(this);
    }
}
