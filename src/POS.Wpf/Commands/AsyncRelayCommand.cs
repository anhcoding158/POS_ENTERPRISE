using System.Diagnostics;
using System.Windows.Input;

namespace POS.Wpf.Commands;

/// <summary>
/// ICommand hỗ trợ tác vụ bất đồng bộ.
///
/// Command tự ngăn double-click trong khi đang chạy
/// và không để exception thoát lên WPF Dispatcher.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onException;

    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onException = null)
    {
        _execute =
            execute ??
            throw new ArgumentNullException(
                nameof(execute));

        _canExecute = canExecute;
        _onException = onException;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting &&
               (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;

            NotifyCanExecuteChanged();

            await _execute();
        }
        catch (OperationCanceledException)
        {
            /*
             * Cancellation là kết thúc có chủ ý,
             * không phải lỗi cần hiển thị.
             */
        }
        catch (Exception exception)
        {
            if (_onException is not null)
            {
                _onException(exception);
            }
            else
            {
                Trace.TraceError(
                    "AsyncRelayCommand failed: {0}",
                    exception);
            }
        }
        finally
        {
            _isExecuting = false;

            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}