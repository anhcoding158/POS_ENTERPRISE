using System.Diagnostics;
using System.Globalization;
using System.IO;
using POS.Application.Abstractions.Services;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public enum RestoreWizardState
{
    SelectArtifact,
    Inspecting,
    CompatibilityReady,
    ImpactWarning,
    AwaitingConfirmation,
    CreatingSafetyBackup,
    PreparingShutdown,
    RestoringExternally,
    VerifyingExternally,
    Restarting,
    Success,
    Failure,
    RollbackSucceeded,
    RollbackFailed
}

public sealed class RestoreWizardViewModel : ViewModelBase, IDisposable
{
    private readonly IRestoreArtifactFilePicker _picker;
    private readonly IRestoreArtifactInspector _inspector;
    private readonly IRestorePreparationService _preparation;
    private readonly IRestoreWorkerProcessLauncher _launcher;
    private readonly Action _requestShutdown;
    private CancellationTokenSource? _operationCancellation;
    private RestorePreparationResult? _prepared;
    private string? _selectedPath;
    private bool _confirmed;
    private bool _disposed;
    private bool _shutdownRequested;
    private RestoreWizardState _state = RestoreWizardState.SelectArtifact;
    private string _statusMessage = "Chọn một tệp backup .db để kiểm tra.";
    private string? _safeFileName;
    private string? _sizeText;
    private string? _sha256;
    private string? _compatibilityText;
    private string? _provenanceText;
    private string? _warningText;

    public RestoreWizardViewModel(
        IRestoreArtifactFilePicker picker,
        IRestoreArtifactInspector inspector,
        IRestorePreparationService preparation)
        : this(picker, inspector, preparation, new RestoreWorkerProcessLauncher(),
            () => global::System.Windows.Application.Current.Shutdown(0)) { }

    internal RestoreWizardViewModel(
        IRestoreArtifactFilePicker picker,
        IRestoreArtifactInspector inspector,
        IRestorePreparationService preparation,
        IRestoreWorkerProcessLauncher launcher,
        Action requestShutdown)
    {
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _requestShutdown = requestShutdown ?? throw new ArgumentNullException(nameof(requestShutdown));

        PickArtifactCommand = new AsyncRelayCommand(PickArtifactAsync, () => CanPick, OnCommandFailure);
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, () => CanContinue, OnCommandFailure);
        BackCommand = new AsyncRelayCommand(BackAsync, () => CanGoBack, OnCommandFailure);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => CanCancel, OnCommandFailure);
        RetryWorkerCommand = new AsyncRelayCommand(RetryWorkerAsync, () => CanRetryWorker, OnCommandFailure);
    }

    public AsyncRelayCommand PickArtifactCommand { get; }
    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand RetryWorkerCommand { get; }

    public RestoreWizardState State
    {
        get => _state;
        private set { if (SetProperty(ref _state, value)) NotifyPresentation(); }
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? SafeFileName { get => _safeFileName; private set => SetProperty(ref _safeFileName, value); }
    public string? SizeText { get => _sizeText; private set => SetProperty(ref _sizeText, value); }
    public string? Sha256 { get => _sha256; private set => SetProperty(ref _sha256, value); }
    public string? CompatibilityText { get => _compatibilityText; private set => SetProperty(ref _compatibilityText, value); }
    public string? ProvenanceText { get => _provenanceText; private set => SetProperty(ref _provenanceText, value); }
    public string? WarningText { get => _warningText; private set => SetProperty(ref _warningText, value); }

    public bool ConfirmationAccepted
    {
        get => _confirmed;
        set
        {
            if (HasDurablePreparation || IsBusy || !SetProperty(ref _confirmed, value)) return;
            if (State is RestoreWizardState.ImpactWarning or RestoreWizardState.AwaitingConfirmation)
                State = value ? RestoreWizardState.AwaitingConfirmation : RestoreWizardState.ImpactWarning;
            NotifyCommands();
        }
    }

    public bool HasDurablePreparation => _prepared?.IsPrepared == true;
    public bool IsBusy => State is RestoreWizardState.Inspecting or
        RestoreWizardState.CreatingSafetyBackup or RestoreWizardState.PreparingShutdown;
    public bool IsProgressVisible => IsBusy || HasDurablePreparation;
    public bool CanPick => State == RestoreWizardState.SelectArtifact && _operationCancellation is null;
    public bool CanContinue => State == RestoreWizardState.CompatibilityReady ||
        (State == RestoreWizardState.AwaitingConfirmation && ConfirmationAccepted);
    public bool CanGoBack => !HasDurablePreparation && !IsBusy && State is not RestoreWizardState.SelectArtifact;
    public bool CanCancel => !HasDurablePreparation;
    public bool CanRetryWorker => HasDurablePreparation && State == RestoreWizardState.Failure;
    public bool CanClose => !HasDurablePreparation;
    public bool ShowConfirmation => State is RestoreWizardState.ImpactWarning or
        RestoreWizardState.AwaitingConfirmation or RestoreWizardState.CreatingSafetyBackup;
    public string PrimaryButtonText => State switch
    {
        RestoreWizardState.CompatibilityReady => "Tiếp tục",
        RestoreWizardState.ImpactWarning or RestoreWizardState.AwaitingConfirmation => "Khôi phục dữ liệu",
        RestoreWizardState.CreatingSafetyBackup => "Đang chuẩn bị...",
        _ => "Tiếp tục"
    };

    public bool RequestClose()
    {
        if (HasDurablePreparation) return false;
        _operationCancellation?.Cancel();
        return _operationCancellation is null;
    }

    internal async Task PickArtifactAsync()
    {
        if (!CanPick) return;
        var selected = _picker.PickArtifact();
        if (string.IsNullOrWhiteSpace(selected)) return;
        _selectedPath = selected;
        SafeFileName = Path.GetFileName(selected);
        await InspectAsync(selected);
    }

    private async Task InspectAsync(string selected)
    {
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        State = RestoreWizardState.Inspecting;
        StatusMessage = "Đang kiểm tra tính toàn vẹn, schema và nguồn gốc backup...";
        RestoreArtifactInspection result;
        try { result = await _inspector.InspectAsync(selected, cancellation.Token); }
        catch (OperationCanceledException)
        {
            State = RestoreWizardState.SelectArtifact;
            StatusMessage = "Đã hủy kiểm tra.";
            return;
        }
        catch
        {
            Fail("Không thể kiểm tra tệp backup an toàn. Vui lòng chọn tệp khác.");
            return;
        }
        finally { _operationCancellation = null; }

        ApplyInspection(result);
    }

    private void ApplyInspection(RestoreArtifactInspection result)
    {
        SafeFileName = result.SafeDisplayFileName;
        SizeText = result.ByteLength is null ? null : $"{result.ByteLength.Value:N0} byte";
        Sha256 = result.Sha256Hex;
        CompatibilityText = result.SchemaCompatibility switch
        {
            RestoreSchemaCompatibility.Current => "Schema hiện tại",
            RestoreSchemaCompatibility.OlderCompatible => "Schema cũ tương thích",
            _ => "Schema không tương thích"
        };
        ProvenanceText = result.Provenance == RestoreArtifactProvenance.AutomaticStateAttested
            ? "Nguồn gốc: AutomaticStateAttested"
            : "Nguồn gốc: LegacyUnattested";

        if (!result.IsRestorable)
        {
            Fail(MapInspectionFailure(result.Status));
            return;
        }

        WarningText = string.Join(Environment.NewLine, new[]
        {
            result.Provenance == RestoreArtifactProvenance.LegacyUnattested
                ? "Bản sao lưu cũ không có checksum nguồn gốc được lưu kèm. Hệ thống đã kiểm tra toàn vẹn và cấu trúc dữ liệu."
                : null,
            result.SchemaCompatibility == RestoreSchemaCompatibility.OlderCompatible
                ? "Backup dùng schema cũ tương thích. Phần mềm sẽ nâng cấp schema khi khởi động lại."
                : null
        }.Where(value => value is not null));
        State = RestoreWizardState.CompatibilityReady;
        StatusMessage = "Backup hợp lệ và có thể khôi phục.";
    }

    internal async Task ContinueAsync()
    {
        if (State == RestoreWizardState.CompatibilityReady)
        {
            State = RestoreWizardState.ImpactWarning;
            StatusMessage = "Dữ liệu hiện tại sẽ được thay thế. Safety backup sẽ được tạo trước. Phần mềm sẽ đóng và tự mở lại. Không được tắt máy trong lúc restore.";
            return;
        }
        if (State != RestoreWizardState.AwaitingConfirmation || !ConfirmationAccepted ||
            _operationCancellation is not null || string.IsNullOrWhiteSpace(_selectedPath)) return;
        await PrepareAndLaunchAsync();
    }

    private async Task PrepareAndLaunchAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        State = RestoreWizardState.CreatingSafetyBackup;
        StatusMessage = "Đang tạo safety backup và chuẩn bị thao tác khôi phục...";
        try
        {
            using var current = Process.GetCurrentProcess();
            var result = await _preparation.PrepareAsync(new RestorePreparationRequest(
                _selectedPath!, current.Id, new DateTimeOffset(current.StartTime.ToUniversalTime())), cancellation.Token);
            if (!result.IsPrepared)
            {
                Fail(MapPreparationFailure(result.Status));
                return;
            }
            _prepared = result;
            OnPropertyChanged(nameof(HasDurablePreparation));
            await LaunchPreparedAsync();
        }
        catch (OperationCanceledException) { Fail("Đã hủy trước khi thao tác khôi phục được chốt."); }
        catch { Fail("Không thể chuẩn bị khôi phục an toàn. Dữ liệu hiện tại chưa bị thay đổi."); }
        finally { _operationCancellation = null; NotifyPresentation(); }
    }

    private async Task LaunchPreparedAsync()
    {
        if (_prepared?.IsPrepared != true) return;
        State = RestoreWizardState.PreparingShutdown;
        StatusMessage = "Đang khởi chạy tiến trình khôi phục an toàn...";
        var started = await _launcher.StartAsync(_prepared, CancellationToken.None);
        if (!started)
        {
            Fail("Không thể khởi chạy tiến trình khôi phục. Dữ liệu hiện tại chưa bị thay đổi. Có thể thử lại cùng thao tác đã chuẩn bị.");
            return;
        }
        State = RestoreWizardState.RestoringExternally;
        StatusMessage = "Tiến trình khôi phục đã khởi chạy. Phần mềm đang đóng an toàn.";
        if (!_shutdownRequested)
        {
            _shutdownRequested = true;
            _requestShutdown();
        }
    }

    internal Task RetryWorkerAsync() => HasDurablePreparation ? LaunchPreparedAsync() : Task.CompletedTask;

    internal Task BackAsync()
    {
        if (!CanGoBack) return Task.CompletedTask;
        ConfirmationAccepted = false;
        State = State is RestoreWizardState.ImpactWarning or RestoreWizardState.AwaitingConfirmation
            ? RestoreWizardState.CompatibilityReady : RestoreWizardState.SelectArtifact;
        return Task.CompletedTask;
    }

    internal Task CancelAsync() { _operationCancellation?.Cancel(); return Task.CompletedTask; }

    private void Fail(string safeMessage) { State = RestoreWizardState.Failure; StatusMessage = safeMessage; }
    private void OnCommandFailure(Exception _) => Fail("Không thể thực hiện thao tác an toàn. Vui lòng thử lại.");

    private static string MapInspectionFailure(RestoreArtifactStatus status) => status switch
    {
        RestoreArtifactStatus.ChecksumMismatch => "Checksum của backup không khớp. Không thể tiếp tục.",
        RestoreArtifactStatus.ActiveDatabaseConflict => "Không thể chọn database đang hoạt động để khôi phục.",
        RestoreArtifactStatus.UnsafeReparsePath => "Tệp hoặc thư mục liên kết không an toàn. Không thể tiếp tục.",
        RestoreArtifactStatus.SourceLocked => "Tệp backup đang được sử dụng. Vui lòng đóng ứng dụng khác rồi thử lại.",
        RestoreArtifactStatus.UnsupportedNewerSchema => "Backup được tạo bởi phiên bản mới hơn và không tương thích.",
        RestoreArtifactStatus.UnsupportedOlderSchema => "Schema backup quá cũ và không được hỗ trợ.",
        RestoreArtifactStatus.Cancelled => "Đã hủy kiểm tra.",
        _ => "Tệp backup không hợp lệ hoặc không thể kiểm tra an toàn."
    };

    private static string MapPreparationFailure(RestoreExecutionStatus status) => status switch
    {
        RestoreExecutionStatus.Cancelled => "Đã hủy trước khi thao tác khôi phục được chốt.",
        RestoreExecutionStatus.DatabaseBusy => "Database đang được sử dụng. Vui lòng đóng tác vụ khác rồi thử lại.",
        RestoreExecutionStatus.PreRestoreBackupFailed => "Không thể tạo safety backup. Khôi phục chưa bắt đầu.",
        RestoreExecutionStatus.SourceChanged => "Tệp backup đã thay đổi sau khi kiểm tra. Vui lòng chọn lại.",
        _ => "Không thể chuẩn bị khôi phục an toàn. Dữ liệu hiện tại chưa bị thay đổi."
    };

    private void NotifyPresentation()
    {
        foreach (var name in new[] { nameof(HasDurablePreparation), nameof(IsBusy), nameof(IsProgressVisible),
                     nameof(CanPick), nameof(CanContinue), nameof(CanGoBack), nameof(CanCancel),
                     nameof(CanRetryWorker), nameof(CanClose), nameof(ShowConfirmation), nameof(PrimaryButtonText) })
            OnPropertyChanged(name);
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        PickArtifactCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RetryWorkerCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        GC.SuppressFinalize(this);
    }
}

internal interface IRestoreWorkerProcessLauncher
{
    Task<bool> StartAsync(RestorePreparationResult prepared, CancellationToken cancellationToken);
}

internal sealed class RestoreWorkerProcessLauncher : IRestoreWorkerProcessLauncher
{
    public Task<bool> StartAsync(RestorePreparationResult prepared, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
                !File.Exists(executable)) return Task.FromResult(false);
            var attributes = File.GetAttributes(executable);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return Task.FromResult(false);

            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            startInfo.ArgumentList.Add("--restore-worker");
            startInfo.ArgumentList.Add("--plan");
            startInfo.ArgumentList.Add(prepared.OpaquePlanPath!);
            startInfo.ArgumentList.Add("--operation");
            startInfo.ArgumentList.Add(prepared.OperationId.ToString("D", CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--token");
            startInfo.ArgumentList.Add(prepared.OneTimeOperationToken!);
            using var process = Process.Start(startInfo);
            return Task.FromResult(process is not null && process.Id > 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }
}
