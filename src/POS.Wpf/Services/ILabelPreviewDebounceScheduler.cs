using System.Windows.Threading;

namespace POS.Wpf.Services;

public interface ILabelPreviewDebounceScheduler
{
    IDisposable Schedule(TimeSpan delay, Action callback);
}

public sealed class DispatcherLabelPreviewDebounceScheduler : ILabelPreviewDebounceScheduler
{
    private readonly Dispatcher _dispatcher;

    public DispatcherLabelPreviewDebounceScheduler(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var registration = new Registration(_dispatcher, delay, callback);
        registration.Start();
        return registration;
    }

    private sealed class Registration : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _callback;
        private bool _disposed;

        public Registration(Dispatcher dispatcher, TimeSpan delay, Action callback)
        {
            _callback = callback;
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = delay
            };
            _timer.Tick += OnTick;
        }

        public void Start() => _timer.Start();

        private void OnTick(object? sender, EventArgs e)
        {
            Dispose();
            _callback();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
    }
}
