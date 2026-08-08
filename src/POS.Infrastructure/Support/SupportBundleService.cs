using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Infrastructure.Logging;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Support;

public sealed class SupportBundleService : ISupportBundleService
{
    private const string ManifestEntry = "manifest.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly PosDbContext _dbContext;
    private readonly InfrastructureOptions _infrastructure;
    private readonly SafeFileLoggerOptions _logging;
    private readonly SupportBundleOptions _options;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<Guid> _newId;

    public SupportBundleService(
        PosDbContext dbContext,
        IOptions<InfrastructureOptions> infrastructure,
        SafeFileLoggerOptions logging,
        IOptions<SupportBundleOptions> options)
        : this(dbContext, infrastructure.Value, logging, options.Value,
            static () => DateTimeOffset.UtcNow, static () => Guid.NewGuid()) { }

    internal SupportBundleService(
        PosDbContext dbContext,
        InfrastructureOptions infrastructure,
        SafeFileLoggerOptions logging,
        SupportBundleOptions options,
        Func<DateTimeOffset> utcNow,
        Func<Guid> newId)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _infrastructure = infrastructure ?? throw new ArgumentNullException(nameof(infrastructure));
        _logging = logging ?? throw new ArgumentNullException(nameof(logging));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logging.Validate();
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _newId = newId ?? throw new ArgumentNullException(nameof(newId));
    }

    public async Task<SupportBundleResult> ExportAsync(
        SupportBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return SupportBundleResult.Failure(SupportBundleStatus.InvalidDestination);
        if (request.IncludeDatabase)
            return SupportBundleResult.Failure(
                SupportBundleStatus.DatabaseInclusionNotSupported);
        if (cancellationToken.IsCancellationRequested)
            return SupportBundleResult.Failure(SupportBundleStatus.Cancelled);

        string destination;
        try
        {
            if (string.IsNullOrWhiteSpace(request.DestinationDirectory) ||
                !Path.IsPathFullyQualified(request.DestinationDirectory))
                return SupportBundleResult.Failure(SupportBundleStatus.InvalidDestination);
            destination = Path.GetFullPath(request.DestinationDirectory);
            if (!Directory.Exists(destination))
                return SupportBundleResult.Failure(SupportBundleStatus.InvalidDestination);
            var attributes = File.GetAttributes(destination);
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
                return SupportBundleResult.Failure(SupportBundleStatus.InvalidDestination);
        }
        catch (Exception exception) when (IsDestinationException(exception))
        {
            return SupportBundleResult.Failure(SupportBundleStatus.InvalidDestination);
        }

        var now = _utcNow().ToUniversalTime();
        var id = _newId();
        var finalName = $"POS-Enterprise-Support-{now:yyyyMMdd-HHmmss}-{id:N}.zip";
        var finalPath = Path.Combine(destination, finalName);
        var temporaryPath = Path.Combine(destination, $".{id:N}.support-bundle.tmp");
        var committed = false;
        var temporaryCreated = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var file = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                temporaryCreated = true;
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    await WriteJsonAsync(archive, ManifestEntry, new
                    {
                        schemaVersion = 1,
                        bundleId = id.ToString("N", CultureInfo.InvariantCulture),
                        createdUtc = now.ToString("O", CultureInfo.InvariantCulture),
                        databaseIncluded = false,
                        logPolicy = "newest-first-bounded-prefix-complete-records"
                    }, cancellationToken);

                    await WriteJsonAsync(archive, "diagnostics/version.json",
                        CollectVersion(), cancellationToken);
                    await WriteJsonAsync(archive, "diagnostics/migrations.json",
                        await CollectMigrationsAsync(cancellationToken), cancellationToken);
                    await WriteJsonAsync(archive, "diagnostics/integrity.json",
                        await CollectIntegrityAsync(cancellationToken), cancellationToken);
                    await WriteJsonAsync(archive, "diagnostics/configuration.json",
                        CollectConfiguration(), cancellationToken);
                    await WriteJsonAsync(archive, "diagnostics/runtime.json",
                        CollectRuntime(), cancellationToken);
                    await ExportLogsAsync(archive, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await file.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
                committed = true;
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                return SupportBundleResult.Failure(
                    SupportBundleStatus.ArchiveAlreadyExists);
            }

            return SupportBundleResult.Success(finalPath);
        }
        catch (OperationCanceledException)
        {
            return SupportBundleResult.Failure(SupportBundleStatus.Cancelled);
        }
        catch (UnauthorizedAccessException)
        {
            return SupportBundleResult.Failure(SupportBundleStatus.DestinationUnavailable);
        }
        catch (IOException)
        {
            return SupportBundleResult.Failure(SupportBundleStatus.ArchiveCreationFailure);
        }
        catch
        {
            return SupportBundleResult.Failure(SupportBundleStatus.UnexpectedFailure);
        }
        finally
        {
            if (!committed && temporaryCreated)
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }
    }

    private static object CollectVersion()
    {
        try
        {
            var assembly = typeof(SupportBundleService).Assembly;
            return new
            {
                status = "ok",
                product = "POS Enterprise",
                assemblyVersion = assembly.GetName().Version?.ToString() ?? "unavailable",
                informationalVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unavailable"
            };
        }
        catch { return new { status = "unavailable", product = "POS Enterprise", assemblyVersion = "unavailable", informationalVersion = "unavailable" }; }
    }

    private async Task<object> CollectMigrationsAsync(CancellationToken token)
    {
        try
        {
            var known = _dbContext.Database.GetMigrations().ToArray();
            var applied = (await _dbContext.Database.GetAppliedMigrationsAsync(token)).ToArray();
            var pending = (await _dbContext.Database.GetPendingMigrationsAsync(token)).ToArray();
            return new { status = "ok", known, applied, pending };
        }
        catch (OperationCanceledException) { throw; }
        catch { return new { status = "unavailable", known = Array.Empty<string>(), applied = Array.Empty<string>(), pending = Array.Empty<string>() }; }
    }

    private async Task<object> CollectIntegrityAsync(CancellationToken token)
    {
        System.Data.Common.DbConnection? connection = null;
        var openedHere = false;
        try
        {
            connection = _dbContext.Database.GetDbConnection();
            openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            await using var reader = await command.ExecuteReaderAsync(token);
            var result = await reader.ReadAsync(token) &&
                string.Equals(reader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase)
                ? "ok" : "failed";
            return new { status = "ok", result };
        }
        catch (OperationCanceledException) { throw; }
        catch { return new { status = "unavailable", result = "unknown" }; }
        finally
        {
            if (openedHere && connection is not null)
            {
                try { await connection.CloseAsync(); } catch { }
            }
        }
    }

    private object CollectConfiguration()
    {
        try
        {
            return new
            {
                status = "ok",
                databaseTimeoutSeconds = _infrastructure.DatabaseTimeoutSeconds,
                applyMigrationsOnStartup = _infrastructure.ApplyMigrationsOnStartup,
                safeFileMaxBytes = _logging.MaxFileSizeBytes,
                safeFileMaxSegments = _logging.MaxSegmentCount,
                safeFileMaxDirectoryBytes = _logging.MaxDirectorySizeBytes,
                safeFileMaxAgeDays = _logging.MaxAgeDays,
                exportedLogBudgetBytes = _options.MaxExportedLogBytes
            };
        }
        catch { return new { status = "unavailable" }; }
    }

    private static object CollectRuntime()
    {
        try
        {
            return new
            {
                status = "ok",
                framework = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                is64BitProcess = Environment.Is64BitProcess
            };
        }
        catch { return new { status = "unavailable" }; }
    }

    private async Task ExportLogsAsync(ZipArchive archive, CancellationToken token)
    {
        if (_options.MaxExportedLogBytes == 0) return;
        var root = _logging.ResolveLogDirectory();
        var files = SnapshotManagedLogs(root)
            .OrderByDescending(file => file.LastWriteUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        long remaining = _options.MaxExportedLogBytes;

        foreach (var snapshot in files)
        {
            token.ThrowIfCancellationRequested();
            if (remaining <= 0) break;
            var sourceLimit = Math.Min(snapshot.Length, remaining);
            if (sourceLimit <= 0) continue;
            try
            {
                var attributes = File.GetAttributes(snapshot.Path);
                if (!ManagedLogPolicy.IsManagedRegularFileCandidate(root, snapshot.Path, attributes))
                    continue;
                await using var source = new FileStream(snapshot.Path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                attributes = File.GetAttributes(snapshot.Path);
                if (!ManagedLogPolicy.IsManagedRegularFileCandidate(root, snapshot.Path, attributes))
                    continue;
                sourceLimit = Math.Min(sourceLimit, source.Length);
                attributes = File.GetAttributes(snapshot.Path);
                if (!ManagedLogPolicy.IsManagedRegularFileCandidate(root, snapshot.Path, attributes))
                    continue;

                var entry = archive.CreateEntry($"logs/{snapshot.Name}", CompressionLevel.Fastest);
                await using var target = entry.Open();
                var written = await CopySanitizedRecordsAsync(
                    source, target, sourceLimit, remaining, token);
                remaining -= written;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task<long> CopySanitizedRecordsAsync(
        Stream source, Stream target, long sourceLimit, long outputLimit,
        CancellationToken token)
    {
        await using var bounded = new BoundedReadStream(source, sourceLimit);
        using var reader = new StreamReader(bounded, Utf8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096, leaveOpen: true);
        var buffer = new char[1024];
        var record = new StringBuilder(Math.Min(_options.MaxLogRecordChars, 4096));
        var discarding = false;
        long written = 0;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) break;
            for (var i = 0; i < count; i++)
            {
                var value = buffer[i];
                if (value == '\n')
                {
                    var safe = discarding
                        ? SafeDiagnosticPolicy.OverlongRecord
                        : SafeDiagnosticPolicy.SanitizeText(record.ToString().TrimEnd('\r'));
                    var bytes = Utf8.GetBytes(safe + "\n");
                    if (written + bytes.Length > outputLimit) return written;
                    await target.WriteAsync(bytes, token);
                    written += bytes.Length;
                    record.Clear();
                    discarding = false;
                }
                else if (!discarding)
                {
                    if (record.Length < _options.MaxLogRecordChars) record.Append(value);
                    else { record.Clear(); discarding = true; }
                }
            }
        }

        if (record.Length > 0 || discarding)
        {
            var safe = discarding
                ? SafeDiagnosticPolicy.OverlongRecord
                : SafeDiagnosticPolicy.SanitizeText(record.ToString().TrimEnd('\r'));
            var bytes = Utf8.GetBytes(safe + "\n");
            if (written + bytes.Length <= outputLimit)
            {
                await target.WriteAsync(bytes, token);
                written += bytes.Length;
            }
        }
        return written;
    }

    private static List<LogSnapshot> SnapshotManagedLogs(string root)
    {
        var snapshots = new List<LogSnapshot>();
        try
        {
            if (!Directory.Exists(root)) return snapshots;
            foreach (var entry in new DirectoryInfo(root)
                .EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var attributes = File.GetAttributes(entry.FullName);
                    if (!ManagedLogPolicy.IsManagedRegularFileCandidate(root, entry.FullName, attributes))
                        continue;
                    var info = new FileInfo(entry.FullName);
                    var length = info.Length;
                    var lastWrite = info.LastWriteTimeUtc;
                    attributes = File.GetAttributes(entry.FullName);
                    if (!ManagedLogPolicy.IsManagedRegularFileCandidate(root, entry.FullName, attributes))
                        continue;
                    snapshots.Add(new LogSnapshot(Path.GetFullPath(entry.FullName),
                        Path.GetFileName(entry.FullName), length, lastWrite));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return snapshots;
    }

    private static async Task WriteJsonAsync(
        ZipArchive archive, string name, object value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, value.GetType(),
            JsonOptions, token);
    }

    private static bool IsDestinationException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private sealed record LogSnapshot(
        string Path, string Name, long Length, DateTime LastWriteUtc);

    private sealed class BoundedReadStream(Stream inner, long remaining) : Stream
    {
        private long _remaining = Math.Max(0, remaining);
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _remaining;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining == 0) return 0;
            var read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining == 0) return 0;
            var read = await inner.ReadAsync(
                buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) => base.Dispose(disposing);
        public override ValueTask DisposeAsync() => base.DisposeAsync();
    }
}
