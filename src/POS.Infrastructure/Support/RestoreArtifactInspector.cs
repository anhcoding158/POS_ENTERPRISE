using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Support;

internal enum RestoreArtifactInspectionMode
{
    PublicSelection,
    WorkerActiveDatabaseVerification
}

public sealed class RestoreArtifactInspector(
    IOptions<InfrastructureOptions> infrastructureOptions,
    IAutomaticBackupStateStore automaticStateStore,
    AutomaticBackupPathProvider automaticPaths) : IRestoreArtifactInspector
{
    private const string SqliteHeader = "SQLite format 3\0";
    private static readonly string[] RequiredCoreTables = ["Categories", "Products"];

    public async Task<RestoreArtifactInspection> InspectAsync(
        string? selectedSourcePath,
        CancellationToken cancellationToken = default)
    {
        return await InspectInternalAsync(
            selectedSourcePath,
            RestoreArtifactInspectionMode.PublicSelection,
            cancellationToken);
    }

    internal async Task<RestoreArtifactInspection> InspectInternalAsync(
        string? selectedSourcePath,
        RestoreArtifactInspectionMode mode,
        CancellationToken cancellationToken = default)
    {
        var safeName = SafeName(selectedSourcePath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = ValidatePath(selectedSourcePath, mode);
            if (validation.Status is not null)
                return Failure(validation.Status.Value, validation.Kind, safeName);

            var path = validation.FullPath!;
            var kind = validation.Kind;
            var before = ReadMetadata(path);
            if (before.Length <= 0)
                return Failure(RestoreArtifactStatus.InvalidArtifact, kind, safeName);

            var firstHash = await ComputeSha256Async(path, cancellationToken);
            if (!await HasSqliteHeaderAsync(path, cancellationToken))
                return Failure(RestoreArtifactStatus.InvalidArtifact, kind, safeName);

            var compiled = GetCompiledMigrations(path);
            if (compiled.Length == 0)
                return Failure(RestoreArtifactStatus.UnexpectedFailure, kind, safeName);

            var database = await InspectDatabaseAsync(path, compiled, cancellationToken);
            if (database.Status is not null)
                return Failure(database.Status.Value, kind, safeName, database.Compatibility,
                    database.Applied.Count, Latest(database.Applied), compiled[^1]);

            var provenance = RestoreArtifactProvenance.LegacyUnattested;
            if (kind == RestoreArtifactKind.Automatic &&
                PathsEqual(Path.GetDirectoryName(path), automaticPaths.Root))
            {
                var stateRead = await automaticStateStore.ReadAsync(cancellationToken);
                if (stateRead.Status == AutomaticBackupStateReadStatus.Valid &&
                    stateRead.State is { } state &&
                    automaticPaths.TryGetOwnedArtifactPath(state.LastVerifiedArtifact, out var attestedPath) &&
                    PathsEqual(path, attestedPath))
                {
                    if (state.LastVerifiedByteLength != before.Length ||
                        !string.Equals(state.LastVerifiedSha256, firstHash,
                            StringComparison.OrdinalIgnoreCase))
                        return Failure(RestoreArtifactStatus.ChecksumMismatch, kind, safeName,
                            database.Compatibility, database.Applied.Count,
                            Latest(database.Applied), compiled[^1]);
                    provenance = RestoreArtifactProvenance.AutomaticStateAttested;
                }
            }

            var after = ReadMetadata(path);
            var finalHash = await ComputeSha256Async(path, cancellationToken);
            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
                !string.Equals(firstHash, finalHash, StringComparison.Ordinal))
                return Failure(RestoreArtifactStatus.SourceChangedDuringInspection, kind, safeName,
                    database.Compatibility, database.Applied.Count,
                    Latest(database.Applied), compiled[^1]);

            var status = provenance == RestoreArtifactProvenance.AutomaticStateAttested
                ? RestoreArtifactStatus.Valid
                : RestoreArtifactStatus.ValidLegacyUnattested;
            return new(status, kind, provenance, database.Compatibility, safeName,
                after.Length, finalHash, database.Applied.Count, Latest(database.Applied),
                compiled[^1], status == RestoreArtifactStatus.Valid
                    ? "Restore.Artifact.ValidAttested" : "Restore.Artifact.ValidLegacyUnattested");
        }
        catch (OperationCanceledException)
        {
            return Failure(RestoreArtifactStatus.Cancelled, ClassifyKind(safeName), safeName);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            return Failure(RestoreArtifactStatus.SourceLocked, ClassifyKind(safeName), safeName);
        }
        catch (IOException exception) when (IsSharingViolation(exception))
        {
            return Failure(RestoreArtifactStatus.SourceLocked, ClassifyKind(safeName), safeName);
        }
        catch (FileNotFoundException)
        {
            return Failure(RestoreArtifactStatus.SourceUnavailable, ClassifyKind(safeName), safeName);
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(RestoreArtifactStatus.SourceUnavailable, ClassifyKind(safeName), safeName);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(RestoreArtifactStatus.SourceUnavailable, ClassifyKind(safeName), safeName);
        }
        catch (SqliteException)
        {
            return Failure(RestoreArtifactStatus.InvalidArtifact, ClassifyKind(safeName), safeName);
        }
        catch (ArgumentException)
        {
            return Failure(RestoreArtifactStatus.InvalidPath, ClassifyKind(safeName), safeName);
        }
        catch (NotSupportedException)
        {
            return Failure(RestoreArtifactStatus.InvalidPath, ClassifyKind(safeName), safeName);
        }
        catch
        {
            return Failure(RestoreArtifactStatus.UnexpectedFailure, ClassifyKind(safeName), safeName);
        }
    }

    private PathValidation ValidatePath(
        string? selectedPath,
        RestoreArtifactInspectionMode mode)
    {
        var kind = ClassifyKind(SafeName(selectedPath));
        if (string.IsNullOrWhiteSpace(selectedPath) || !Path.IsPathFullyQualified(selectedPath) ||
            ContainsTraversalSegment(selectedPath))
            return new(RestoreArtifactStatus.InvalidPath, null, kind);
        if (selectedPath.StartsWith("\\\\", StringComparison.Ordinal))
            return new(RestoreArtifactStatus.NetworkPathUnsupported, null, kind);

        var fullPath = Path.GetFullPath(selectedPath);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) || PathsEqual(fullPath, root))
            return new(RestoreArtifactStatus.InvalidPath, null, kind);
        try
        {
            if (new DriveInfo(root).DriveType == DriveType.Network)
                return new(RestoreArtifactStatus.NetworkPathUnsupported, null, kind);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }

        var active = DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(
            infrastructureOptions.Value.DatabasePath);
        var activeDatabaseConflict = PathsEqual(fullPath, active);
        if ((activeDatabaseConflict && mode == RestoreArtifactInspectionMode.PublicSelection) ||
            PathsEqual(fullPath, active + "-wal") || PathsEqual(fullPath, active + "-shm"))
            return new(RestoreArtifactStatus.ActiveDatabaseConflict, null, kind);
        if (HasReparsePointInExistingChain(fullPath))
            return new(RestoreArtifactStatus.UnsafeReparsePath, null, kind);
        if (Directory.Exists(fullPath))
            return new(RestoreArtifactStatus.InvalidArtifact, null, kind);
        if (!string.Equals(Path.GetExtension(fullPath), ".db", StringComparison.OrdinalIgnoreCase))
            return new(RestoreArtifactStatus.InvalidArtifact, null, kind);
        if (!File.Exists(fullPath))
            return new(RestoreArtifactStatus.SourceUnavailable, null, kind);
        var attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.Directory) != 0)
            return new(RestoreArtifactStatus.InvalidArtifact, null, kind);
        return new(null, fullPath, kind);
    }

    private static async Task<DatabaseInspection> InspectDatabaseAsync(
        string path, string[] compiled, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            await using var reader = await integrity.ExecuteReaderAsync(cancellationToken);
            var count = 0;
            var ok = true;
            while (await reader.ReadAsync(cancellationToken))
            {
                count++;
                ok &= !reader.IsDBNull(0) && string.Equals(reader.GetString(0), "ok",
                    StringComparison.OrdinalIgnoreCase);
            }
            if (count != 1 || !ok)
                return new(RestoreArtifactStatus.IntegrityCheckFailed,
                    RestoreSchemaCompatibility.Unknown, []);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is not (5 or 6))
        {
            return new(RestoreArtifactStatus.IntegrityCheckFailed,
                RestoreSchemaCompatibility.Unknown, []);
        }

        if (!await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken))
            return new(RestoreArtifactStatus.MissingMigrationHistory,
                RestoreSchemaCompatibility.Unknown, []);

        var applied = new List<string>();
        await using (var history = connection.CreateCommand())
        {
            history.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY rowid;";
            await using var reader = await history.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0) || string.IsNullOrWhiteSpace(reader.GetString(0)))
                    return new(RestoreArtifactStatus.UnknownMigrationHistory,
                        RestoreSchemaCompatibility.Unknown, applied);
                applied.Add(reader.GetString(0));
            }
        }
        if (applied.Count == 0)
            return new(RestoreArtifactStatus.MissingMigrationHistory,
                RestoreSchemaCompatibility.Unknown, applied);
        if (applied.Count != applied.Distinct(StringComparer.Ordinal).Count())
            return new(RestoreArtifactStatus.UnknownMigrationHistory,
                RestoreSchemaCompatibility.Unknown, applied);
        foreach (var table in RequiredCoreTables)
            if (!await TableExistsAsync(connection, table, cancellationToken))
                return new(RestoreArtifactStatus.InvalidArtifact,
                    RestoreSchemaCompatibility.Unknown, applied);

        if (applied.SequenceEqual(compiled, StringComparer.Ordinal))
            return new(null, RestoreSchemaCompatibility.Current, applied);
        if (applied.Count < compiled.Length &&
            applied.SequenceEqual(compiled.Take(applied.Count), StringComparer.Ordinal))
            return new(null, RestoreSchemaCompatibility.OlderCompatible, applied);
        if (applied.Count > compiled.Length &&
            compiled.SequenceEqual(applied.Take(compiled.Length), StringComparer.Ordinal))
            return new(RestoreArtifactStatus.UnsupportedNewerSchema,
                RestoreSchemaCompatibility.UnsupportedNewer, applied);
        return new(RestoreArtifactStatus.UnknownMigrationHistory,
            RestoreSchemaCompatibility.Unknown, applied);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static string[] GetCompiledMigrations(string selectedPath)
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = selectedPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString()).Options;
        using var context = new PosDbContext(options);
        return context.Database.GetMigrations().ToArray();
    }

    private static async Task<bool> HasSqliteHeaderAsync(string path, CancellationToken token)
    {
        var header = new byte[16];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 16, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(header, token);
        return read == header.Length && header.AsSpan().SequenceEqual(System.Text.Encoding.ASCII.GetBytes(SqliteHeader));
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }

    private static FileSnapshot ReadMetadata(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        return new(file.Length, file.LastWriteTimeUtc);
    }

    private static bool ContainsTraversalSegment(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..");
    }

    private static bool HasReparsePointInExistingChain(string path)
    {
        FileSystemInfo start = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        for (FileSystemInfo? current = start; current is not null; current = current switch
        {
            FileInfo file => file.Directory,
            DirectoryInfo directory => directory.Parent,
            _ => null
        })
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    private static bool PathsEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;

    private static string? Latest(IReadOnlyList<string> values) =>
        values.Count == 0 ? null : values[^1];

    private static RestoreArtifactKind ClassifyKind(string name)
    {
        if (name.StartsWith("pos-enterprise-automatic-", StringComparison.OrdinalIgnoreCase))
            return RestoreArtifactKind.Automatic;
        if (name.StartsWith("pos-enterprise-pre-restore-", StringComparison.OrdinalIgnoreCase))
            return RestoreArtifactKind.PreRestore;
        if (name.StartsWith("pos-enterprise-pre-migration-", StringComparison.OrdinalIgnoreCase))
            return RestoreArtifactKind.Manual;
        return RestoreArtifactKind.LegacyOrUnknown;
    }

    private static string SafeName(string? path)
    {
        try { return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path); }
        catch { return string.Empty; }
    }

    private static RestoreArtifactInspection Failure(
        RestoreArtifactStatus status,
        RestoreArtifactKind kind,
        string safeName,
        RestoreSchemaCompatibility compatibility = RestoreSchemaCompatibility.Unknown,
        int appliedCount = 0,
        string? latestApplied = null,
        string? expectedLatest = null) =>
        new(status, kind, RestoreArtifactProvenance.LegacyUnattested, compatibility,
            safeName, null, null, appliedCount, latestApplied, expectedLatest,
            $"Restore.Artifact.{status}");

    private sealed record PathValidation(
        RestoreArtifactStatus? Status, string? FullPath, RestoreArtifactKind Kind);
    private sealed record DatabaseInspection(
        RestoreArtifactStatus? Status,
        RestoreSchemaCompatibility Compatibility,
        IReadOnlyList<string> Applied);
    private sealed record FileSnapshot(long Length, DateTime LastWriteTimeUtc);
}
