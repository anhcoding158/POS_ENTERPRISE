using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using POS.Application.Abstractions.Services;

namespace POS.Infrastructure.Support;

public sealed class AutomaticBackupStateStore(
    AutomaticBackupPathProvider paths) : IAutomaticBackupStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AutomaticBackupStateReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!paths.IsManagedRootSafe())
            return new(AutomaticBackupStateReadStatus.Corrupt, null);
        if (!File.Exists(paths.StatePath))
            return new(AutomaticBackupStateReadStatus.Missing, null);
        try
        {
            await using var stream = new FileStream(paths.StatePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var state = await JsonSerializer.DeserializeAsync<AutomaticBackupState>(stream, JsonOptions, cancellationToken);
            if (state?.FormatVersion != AutomaticBackupState.CurrentFormatVersion)
                return new(AutomaticBackupStateReadStatus.UnsupportedVersion, null);
            return Validate(state)
                ? new(AutomaticBackupStateReadStatus.Valid, state)
                : new(AutomaticBackupStateReadStatus.Corrupt, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException) { return new(AutomaticBackupStateReadStatus.Corrupt, null); }
        catch (IOException) { return new(AutomaticBackupStateReadStatus.Corrupt, null); }
        catch (UnauthorizedAccessException) { return new(AutomaticBackupStateReadStatus.Corrupt, null); }
    }

    public async Task WriteAsync(AutomaticBackupState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Validate(state)) throw new ArgumentException("Invalid automatic backup state.", nameof(state));
        if (!paths.IsManagedRootSafe()) throw new IOException("Automatic backup root is not safe.");
        Directory.CreateDirectory(paths.Root);
        if (!paths.IsManagedRootSafe()) throw new IOException("Automatic backup root is not safe.");
        var tempName = StateFileNameFor(Guid.NewGuid());
        var tempPath = Path.Combine(paths.Root, tempName);
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(paths.StatePath) &&
                (File.GetAttributes(paths.StatePath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Automatic backup state target is not a regular file.");
            File.Move(tempPath, paths.StatePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    internal static string StateFileNameFor(Guid id) => $".automatic-backup-state-{id:N}.tmp";

    private bool Validate(AutomaticBackupState state)
    {
        if (state.FormatVersion != AutomaticBackupState.CurrentFormatVersion) return false;
        if (!IsUtc(state.LastVerifiedSuccessUtc) || !IsUtc(state.LastAttemptUtc) || !IsUtc(state.NextAttemptUtc)) return false;
        var anySuccessMetadata = state.LastVerifiedSuccessUtc is not null || state.LastVerifiedArtifact is not null ||
            state.LastVerifiedByteLength is not null || state.LastVerifiedSha256 is not null;
        if (anySuccessMetadata && (state.LastVerifiedSuccessUtc is null ||
            !paths.IsOwnedArtifactIdentifier(state.LastVerifiedArtifact) || state.LastVerifiedByteLength is null or <= 0 ||
            state.LastVerifiedSha256 is null || state.LastVerifiedSha256.Length != 64 ||
            !state.LastVerifiedSha256.All(Uri.IsHexDigit))) return false;
        return true;
    }

    private static bool IsUtc(DateTimeOffset? value) => value is null || value.Value.Offset == TimeSpan.Zero;
}
