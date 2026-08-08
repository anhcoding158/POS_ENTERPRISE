using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;
using System.Text;
using POS.Infrastructure.Persistence;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(
    "POS.Architecture.Tests")]

namespace POS.Infrastructure.Platform;

/// <summary>
/// Owns the database-scoped Windows mutex and the activation pipe.
/// </summary>
public sealed class WindowsSingleInstanceCoordinator :
    IDisposable,
    IAsyncDisposable
{
    private static readonly byte[] ActivationPayload =
        Encoding.UTF8.GetBytes("ACTIVATE");

    private readonly DatabaseIdentity _identity;
    private readonly Action<Exception>? _listenerErrorHandler;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private bool _disposed;

    public WindowsSingleInstanceCoordinator(
        DatabaseIdentity identity,
        Action<Exception>? listenerErrorHandler = null)
    {
        _identity =
            identity ??
            throw new ArgumentNullException(nameof(identity));
        _listenerErrorHandler =
            listenerErrorHandler ??
            (exception =>
                Trace.WriteLine(
                    $"Single-instance activation listener fault. " +
                    $"ExceptionType={exception.GetType().FullName}"));
    }

    public DatabaseIdentity Identity =>
        _identity;

    public bool IsOwner =>
        _ownsMutex;

    public bool TryAcquire()
    {
        ThrowIfDisposed();

        if (_mutex is not null)
        {
            throw new InvalidOperationException(
                "Single-instance ownership đã được thử trước đó.");
        }

        var mutex =
            new Mutex(
                initiallyOwned: false,
                name: _identity.MutexName,
                createdNew: out _);

        try
        {
            bool acquired;

            try
            {
                acquired =
                    mutex.WaitOne(
                        TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                // The current thread has taken ownership of the abandoned mutex.
                acquired = true;
            }

            if (!acquired)
            {
                mutex.Dispose();

                return false;
            }

            _mutex = mutex;
            _ownsMutex = true;

            return true;
        }
        catch
        {
            mutex.Dispose();

            throw;
        }
    }

    public void StartActivationListener(
        Func<Task> activationHandler)
    {
        ArgumentNullException.ThrowIfNull(
            activationHandler);

        ThrowIfDisposed();

        if (!_ownsMutex)
        {
            throw new InvalidOperationException(
                "Chỉ instance sở hữu mutex mới được mở activation listener.");
        }

        if (_listenerTask is not null)
        {
            throw new InvalidOperationException(
                "Activation listener đã được khởi động.");
        }

        _listenerCancellation =
            new CancellationTokenSource();

        _listenerTask =
            ListenForActivationAsync(
                activationHandler,
                _listenerCancellation.Token);
    }

    public async Task StopActivationListenerAsync()
    {
        var cancellation =
            _listenerCancellation;

        var listenerTask =
            _listenerTask;

        if (cancellation is null ||
            listenerTask is null)
        {
            return;
        }

        cancellation.Cancel();

        try
        {
            await listenerTask
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal listener shutdown path.
        }
        finally
        {
            cancellation.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;
        }
    }

    public static async Task<bool> RequestActivationAsync(
        DatabaseIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        const int maxAttempts = 5;

        for (var attempt = 0;
             attempt < maxAttempts;
             attempt++)
        {
            try
            {
                using var client =
                    new NamedPipeClientStream(
                        ".",
                        identity.PipeName,
                        PipeDirection.Out,
                        PipeOptions.Asynchronous);

                await client
                    .ConnectAsync(
                        500,
                        cancellationToken)
                    .ConfigureAwait(false);

                await client
                    .WriteAsync(
                        ActivationPayload,
                        cancellationToken)
                    .ConfigureAwait(false);

                await client
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                return true;
            }
            catch (TimeoutException)
            {
                // The owner may still be creating the listener.
            }
            catch (IOException)
            {
                // The owner may be shutting down or not yet listening.
            }
            catch (UnauthorizedAccessException)
            {
                // Treat an unavailable activation endpoint as a failed request.
            }

            if (attempt + 1 < maxAttempts)
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(100),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopActivationListenerAsync()
            .GetAwaiter()
            .GetResult();

        ReleaseMutex();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopActivationListenerAsync()
            .ConfigureAwait(false);

        ReleaseMutex();
    }

    private async Task ListenForActivationAsync(
        Func<Task> activationHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server =
                    CreateActivationServer();

                try
                {
                    await server
                        .WaitForConnectionAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                    var payload =
                        new byte[ActivationPayload.Length];

                    await server
                        .ReadExactlyAsync(
                            payload,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (payload.AsSpan().SequenceEqual(
                            ActivationPayload))
                    {
                        await activationHandler()
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (EndOfStreamException)
                {
                    // Ignore an incomplete or malformed client payload.
                }
                catch (IOException)
                {
                    // Ignore a client disconnect and continue listening.
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _listenerErrorHandler?
                .Invoke(exception);
        }
    }

    private NamedPipeServerStream CreateActivationServer()
    {
        var currentUser =
            WindowsIdentity.GetCurrent().User;

        if (currentUser is null)
        {
            throw new InvalidOperationException(
                "Không xác định được Windows identity cho activation pipe.");
        }

        return NamedPipeServerStreamAcl.Create(
            _identity.PipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous,
            inBufferSize: ActivationPayload.Length,
            outBufferSize: ActivationPayload.Length,
            pipeSecurity:
                CreateActivationPipeSecurity(
                    currentUser),
            inheritability: HandleInheritability.None,
            additionalAccessRights: (PipeAccessRights)0);
    }

    internal static PipeSecurity CreateActivationPipeSecurity(
        SecurityIdentifier currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var security =
            new PipeSecurity();

        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.SetOwner(
            currentUser);

        security.SetAccessRule(
            new PipeAccessRule(
                currentUser,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        return security;
    }

    private void ReleaseMutex()
    {
        var mutex =
            _mutex;

        _mutex = null;

        if (mutex is null)
        {
            return;
        }

        try
        {
            if (_ownsMutex)
            {
                mutex.ReleaseMutex();
                _ownsMutex = false;
            }
        }
        finally
        {
            mutex.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            nameof(WindowsSingleInstanceCoordinator));
    }
}
