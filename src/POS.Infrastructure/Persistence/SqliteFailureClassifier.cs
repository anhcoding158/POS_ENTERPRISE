using Microsoft.Data.Sqlite;
using POS.Application.Common;
using POS.Application.Abstractions.Persistence;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Provider adapter duy nhất dùng SQLite numeric error codes.
/// </summary>
public sealed class SqliteFailureClassifier : IDatabaseFailureClassifier
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1822:Mark members as static",
        Justification = "Classifier is an injectable provider adapter.")]
    public DatabaseFailureKind? Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
        {
            return null;
        }

        var sqlite = Find(exception);
        return sqlite?.SqliteErrorCode switch
        {
            5 => DatabaseFailureKind.Busy,
            6 => DatabaseFailureKind.Locked,
            13 => DatabaseFailureKind.DiskFull,
            11 or 26 => DatabaseFailureKind.Corruption,
            not null => DatabaseFailureKind.Unknown,
            null => null
        };
    }

    public DatabaseOperationException Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var kind = Classify(exception) ?? DatabaseFailureKind.Unknown;
        return new DatabaseOperationException(kind, SafeMessage(kind), exception);
    }

    private static SqliteException? Find(Exception exception)
    {
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        Exception? current = exception;
        while (current is not null && visited.Add(current))
        {
            if (current is SqliteException sqlite)
            {
                return sqlite;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static string SafeMessage(DatabaseFailureKind kind) => kind switch
    {
        DatabaseFailureKind.Busy => "Dữ liệu đang bận tạm thời.",
        DatabaseFailureKind.Locked => "Dữ liệu đang bị khóa bởi một thao tác khác.",
        DatabaseFailureKind.DiskFull => "Không đủ dung lượng để lưu dữ liệu.",
        DatabaseFailureKind.Corruption => "Tệp dữ liệu không còn ở trạng thái an toàn.",
        _ => "Không thể truy cập dữ liệu an toàn."
    };
}
