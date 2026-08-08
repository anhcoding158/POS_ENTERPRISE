using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace POS.Infrastructure.Logging;

public sealed class SafeFileLoggerProvider : ILoggerProvider
{
    private const string FilePrefix = "pos-enterprise-";
    private readonly object _sync = new();
    private readonly SafeFileLoggerOptions _options;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _root;
    private StreamWriter? _writer;
    private DateOnly _activeDate;
    private int _activeSequence;
    private long _activeBytes;
    private bool _disabled;
    private bool _disposed;

    public SafeFileLoggerProvider(SafeFileLoggerOptions options)
        : this(options, static () => DateTimeOffset.UtcNow) { }

    internal SafeFileLoggerProvider(
        SafeFileLoggerOptions options,
        Func<DateTimeOffset> utcNow)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _root = _options.ResolveLogDirectory();
    }

    public ILogger CreateLogger(string categoryName) =>
        new SafeFileLogger(this, categoryName ?? string.Empty);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            CloseWriter();
        }
        GC.SuppressFinalize(this);
    }

    internal void Write(
        string category,
        LogLevel level,
        EventId eventId,
        string message)
    {
        if (_disposed || _disabled || level == LogLevel.None) return;

        try
        {
            var now = _utcNow().ToUniversalTime();
            var line = FormatLine(now, level, eventId, category, message);
            var bytes = Encoding.UTF8.GetByteCount(line);
            while (bytes > _options.MaxFileSizeBytes && message.Length > 0)
            {
                message = message[..(message.Length / 2)];
                line = FormatLine(now, level, eventId, category,
                    $"{message} [TRUNCATED]");
                bytes = Encoding.UTF8.GetByteCount(line);
            }

            lock (_sync)
            {
                if (_disposed || _disabled) return;
                EnsureWriter(now, bytes);
                if (_writer is null) return;
                _writer.Write(line);
                _activeBytes += bytes;
            }
        }
        catch
        {
            DisableSafely();
        }
    }

    private void EnsureWriter(DateTimeOffset now, int incomingBytes)
    {
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        if (_writer is not null &&
            (date != _activeDate || _activeBytes + incomingBytes > _options.MaxFileSizeBytes))
        {
            CloseWriter();
        }

        if (_writer is not null) return;

        Directory.CreateDirectory(_root);
        Cleanup(now, incomingBytes);
        _activeDate = date;
        _activeSequence = FindNextSequence(date);
        var stream = CreateNewManagedStream(date);
        _activeBytes = 0;
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    private FileStream CreateNewManagedStream(DateOnly date)
    {
        while (_activeSequence <= 9999)
        {
            var path = Path.Combine(
                _root,
                CreateFileName(date, _activeSequence));
            try
            {
                return new FileStream(
                    path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                    4096, FileOptions.SequentialScan);
            }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path))
            {
                _activeSequence++;
            }
        }

        throw new IOException("No managed log sequence is available for the UTC date.");
    }

    private int FindNextSequence(DateOnly date)
    {
        var max = -1;
        foreach (var file in EnumerateManagedFiles())
        {
            if (file.Date == date)
            {
                max = Math.Max(max, file.Sequence);
            }
        }
        return max + 1;
    }

    private void Cleanup(DateTimeOffset now, int incomingBytes)
    {
        var files = EnumerateManagedFiles()
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToList();
        var oldestAllowed = now.UtcDateTime.AddDays(-_options.MaxAgeDays);

        foreach (var file in files.Where(file => file.LastWriteTimeUtc < oldestAllowed).ToArray())
        {
            TryDeleteManagedFile(file.Path);
            files.Remove(file);
        }

        long total = files.Sum(file => file.Length);
        while (files.Count >= _options.MaxSegmentCount ||
               total + incomingBytes > _options.MaxDirectorySizeBytes)
        {
            var oldest = files[0];
            var length = oldest.Length;
            TryDeleteManagedFile(oldest.Path);
            files.RemoveAt(0);
            total = Math.Max(0, total - length);
        }
    }

    private List<ManagedLogFile> EnumerateManagedFiles()
    {
        if (!Directory.Exists(_root)) return [];
        var files = new List<ManagedLogFile>();
        foreach (var entry in new DirectoryInfo(_root)
                     .EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            if (TryReadManagedLogFile(entry.FullName, out var file))
            {
                files.Add(file);
            }
        }
        return files;
    }

    private bool TryReadManagedLogFile(
        string candidatePath,
        out ManagedLogFile file)
    {
        file = default;
        try
        {
            var attributes = File.GetAttributes(candidatePath);
            if (!ManagedLogPolicy.IsManagedRegularFileCandidate(_root, candidatePath, attributes) ||
                !ManagedLogPolicy.TryParseName(Path.GetFileName(candidatePath),
                    out var date, out var sequence))
            {
                return false;
            }

            var info = new FileInfo(candidatePath);
            var lastWriteTimeUtc = info.LastWriteTimeUtc;
            var length = info.Length;

            attributes = File.GetAttributes(candidatePath);
            if (!ManagedLogPolicy.IsManagedRegularFileCandidate(_root, candidatePath, attributes))
            {
                return false;
            }

            file = new ManagedLogFile(
                Path.GetFullPath(candidatePath),
                Path.GetFileName(candidatePath),
                date,
                sequence,
                lastWriteTimeUtc,
                length);
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool IsManagedRegularFileCandidate(
        string logRoot,
        string candidatePath,
        FileAttributes attributes) =>
        ManagedLogPolicy.IsManagedRegularFileCandidate(logRoot, candidatePath, attributes);

    private static string CreateFileName(DateOnly date, int sequence) =>
        $"{FilePrefix}{date:yyyyMMdd}-{sequence:D4}.log";

    private static string FormatLine(
        DateTimeOffset now, LogLevel level, EventId eventId,
        string category, string message) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{now:O} [{level}] EventId={eventId.Id}:{SafeToken(eventId.Name)} " +
            $"Category={SafeToken(category)} {SingleLine(message)}{Environment.NewLine}");

    private static string SafeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : SingleLine(value).Replace(' ', '_');

    private static string SingleLine(string? value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    private bool TryDeleteManagedFile(string candidatePath)
    {
        try
        {
            var attributes = File.GetAttributes(candidatePath);
            if (!ManagedLogPolicy.IsManagedRegularFileCandidate(_root, candidatePath, attributes))
            {
                return false;
            }

            File.Delete(candidatePath);
            return true;
        }
        catch (Exception exception) when (exception is
            FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private void CloseWriter()
    {
        try { _writer?.Flush(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
        _activeBytes = 0;
    }

    private void DisableSafely()
    {
        lock (_sync)
        {
            _disabled = true;
            CloseWriter();
        }
    }

    private readonly record struct ManagedLogFile(
        string Path,
        string Name,
        DateOnly Date,
        int Sequence,
        DateTime LastWriteTimeUtc,
        long Length);

    private sealed class SafeFileLogger(
        SafeFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || formatter is null) return;
            try { provider.Write(category, logLevel, eventId, formatter(state, null)); }
            catch { /* ILogger contract must never affect business work. */ }
        }
    }
}
