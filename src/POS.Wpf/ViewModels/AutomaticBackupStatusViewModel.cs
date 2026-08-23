using System.Globalization;
using POS.Application.Abstractions.Services;

namespace POS.Wpf.ViewModels;

public sealed class AutomaticBackupStatusViewModel : ViewModelBase, IDisposable
{
    private readonly IAutomaticBackupStatusSource _source;
    private readonly global::System.Windows.Threading.Dispatcher _dispatcher;
    private string _statusText = "Automatic backup: đang khởi tạo";
    private string _detailText = "Chưa có lần sao lưu tự động đã verify.";
    private bool _disposed;

    public AutomaticBackupStatusViewModel(IAutomaticBackupStatusSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _dispatcher = global::System.Windows.Threading.Dispatcher.CurrentDispatcher;
        Apply(source.Current);
        source.StatusChanged += OnStatusChanged;
    }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DetailText { get => _detailText; private set => SetProperty(ref _detailText, value); }

    private void OnStatusChanged(object? sender, AutomaticBackupStatusChangedEventArgs args)
    {
        if (_disposed) return;
        if (_dispatcher.CheckAccess()) Apply(args.Status);
        else _dispatcher.BeginInvoke(() => { if (!_disposed) Apply(args.Status); });
    }

    private void Apply(AutomaticBackupStatusSnapshot value)
    {
        StatusText = value.Status switch
        {
            AutomaticBackupStatus.Running => "Automatic backup: đang sao lưu và verify…",
            AutomaticBackupStatus.Success => "Automatic backup: thành công",
            AutomaticBackupStatus.SuccessWithRetentionWarning => "Automatic backup: thành công, có cảnh báo retention",
            AutomaticBackupStatus.DeferredBusy => "Automatic backup: tạm hoãn vì tác vụ sao lưu khác đang chạy",
            AutomaticBackupStatus.Failed => "Automatic backup: chưa hoàn tất",
            AutomaticBackupStatus.StateCorrupt => "Automatic backup: không đọc được lịch sử đã lưu",
            AutomaticBackupStatus.StateRecovered => "Automatic backup: lịch sử đã được phục hồi",
            AutomaticBackupStatus.Cancelled => "Automatic backup: đã dừng khi ứng dụng đóng",
            AutomaticBackupStatus.NotDue => "Automatic backup: chưa đến hạn",
            _ => "Automatic backup: chưa có lịch sử đã verify"
        };
        DetailText = value.LastVerifiedSuccessUtc is null
            ? value.Warning ?? "Chưa có lần sao lưu tự động đã verify."
            : $"Gần nhất: {value.LastVerifiedSuccessUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}" +
              (string.IsNullOrWhiteSpace(value.ArtifactIdentifier) ? string.Empty : $" · {value.ArtifactIdentifier}") +
              (string.IsNullOrWhiteSpace(value.Warning) ? string.Empty : $" · {value.Warning}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.StatusChanged -= OnStatusChanged;
        GC.SuppressFinalize(this);
    }
}
