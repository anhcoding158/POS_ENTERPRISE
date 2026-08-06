using POS.Application.Common;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Retry hữu hạn chỉ dành cho lời gọi được caller chứng minh là read-only/idempotent.
/// Transactional write và commit không được đưa qua executor này.
/// </summary>
public sealed class SqliteSafeOperationRetry
{
    public const int MaximumAttempts = 3;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);

    private readonly SqliteFailureClassifier _classifier;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public SqliteSafeOperationRetry(
        SqliteFailureClassifier classifier,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _delay = delay ?? Task.Delay;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> safeOperation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(safeOperation);

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await safeOperation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var kind = _classifier.Classify(exception);
                if (kind != DatabaseFailureKind.Busy || attempt >= MaximumAttempts)
                {
                    throw _classifier.Translate(exception);
                }

                await _delay(Delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
