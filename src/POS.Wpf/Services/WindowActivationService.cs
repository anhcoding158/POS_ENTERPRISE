using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace POS.Wpf.Services;

public interface IWindowActivationTarget
{
    bool IsVisible { get; }

    bool IsMinimized { get; }

    void Restore();

    bool Activate();

    void RequestAttention();
}

public interface IWindowActivationService
{
    bool TryActivate(
        IWindowActivationTarget target);
}

public sealed class WindowActivationCoordinator
{
    private readonly IWindowActivationService _activationService;
    private IWindowActivationTarget? _target;
    private bool _pending;

    public WindowActivationCoordinator(
        IWindowActivationService activationService)
    {
        _activationService =
            activationService ??
            throw new ArgumentNullException(nameof(activationService));
    }

    public bool HasPendingActivation =>
        _pending;

    public void SetTarget(
        IWindowActivationTarget target)
    {
        _target =
            target ??
            throw new ArgumentNullException(nameof(target));

        TryProcessPendingActivation();
    }

    public void ClearTarget()
    {
        _target = null;
    }

    public void RequestActivation()
    {
        _pending = true;
        TryProcessPendingActivation();
    }

    public void NotifyTargetReady()
    {
        TryProcessPendingActivation();
    }

    private void TryProcessPendingActivation()
    {
        var target =
            _target;

        if (!_pending ||
            target is null ||
            !target.IsVisible)
        {
            return;
        }

        if (_activationService.TryActivate(target))
        {
            _pending = false;
        }
    }
}

public sealed class WpfWindowActivationTarget :
    IWindowActivationTarget
{
    private const int ShowWindowRestore = 9;

    private readonly Window _window;

    public WpfWindowActivationTarget(Window window)
    {
        _window =
            window ??
            throw new ArgumentNullException(nameof(window));
    }

    public bool IsVisible =>
        _window.IsVisible;

    public bool IsMinimized =>
        _window.WindowState == WindowState.Minimized;

    public void Restore()
    {
        _window.WindowState =
            WindowState.Normal;
    }

    public bool Activate()
    {
        try
        {
            if (_window.Activate())
            {
                return true;
            }

            var handle =
                new WindowInteropHelper(_window)
                    .Handle;

            if (handle == IntPtr.Zero)
            {
                return false;
            }

            NativeMethods.ShowWindow(
                handle,
                ShowWindowRestore);

            return NativeMethods.SetForegroundWindow(
                handle);
        }
        catch (
            Exception exception)
            when (exception is
                InvalidOperationException or
                Win32Exception or
                DllNotFoundException or
                EntryPointNotFoundException)
        {
            return false;
        }
    }

    public void RequestAttention()
    {
        try
        {
            var handle =
                new WindowInteropHelper(_window)
                    .Handle;

            if (handle != IntPtr.Zero)
            {
                NativeMethods.FlashWindow(
                    handle,
                    true);
            }
        }
        catch (
            Exception exception)
            when (exception is
                InvalidOperationException or
                Win32Exception or
                DllNotFoundException or
                EntryPointNotFoundException)
        {
            // Foreground fallback is best-effort and must not crash WPF.
        }
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(
            IntPtr hWnd);

        [DllImport(
            "user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(
            IntPtr hWnd,
            int nCmdShow);

        [DllImport(
            "user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindow(
            IntPtr hWnd,
            bool bInvert);
    }
}

public sealed class WindowActivationService :
    IWindowActivationService
{
    public bool TryActivate(
        IWindowActivationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsMinimized)
        {
            target.Restore();
        }

        var activated =
            target.Activate();

        if (!activated)
        {
            target.RequestAttention();
        }

        return activated;
    }
}
