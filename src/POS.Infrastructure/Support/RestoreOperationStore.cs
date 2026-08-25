using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("POS.Enterprise")]

namespace POS.Infrastructure.Support;

public sealed class RestoreOperationStore
{
    internal const int CurrentFormatVersion = 1;
    internal const string OperationsDirectoryName = "restore-operations";
    internal const string MarkerFileName = "operation.json";
    internal const string ResultFileName = "result.json";
    internal const string AcknowledgementFileName = "result.acknowledged";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _activeDatabasePath;
    private readonly string _operationsRoot;

    public RestoreOperationStore(IOptions<InfrastructureOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _activeDatabasePath = DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(
            options.Value.DatabasePath);
        var databaseDirectory = Path.GetDirectoryName(_activeDatabasePath)
            ?? throw new InvalidOperationException("Restore.Operation.InvalidDatabaseDirectory");
        _operationsRoot = Path.GetFullPath(Path.Combine(databaseDirectory, OperationsDirectoryName));
    }

    internal string OperationsRoot => _operationsRoot;

    internal string GetOperationDirectory(Guid operationId) =>
        Path.Combine(_operationsRoot, operationId.ToString("D"));

    internal string GetPlanPath(Guid operationId) =>
        Path.Combine(GetOperationDirectory(operationId), MarkerFileName);

    internal void CreateOperationDirectory(Guid operationId)
    {
        if (operationId == Guid.Empty) throw new InvalidOperationException("Restore.Operation.InvalidId");
        EnsureSafeExistingAncestors(Path.GetDirectoryName(_operationsRoot)!);
        Directory.CreateDirectory(_operationsRoot);
        EnsureRegularDirectory(_operationsRoot);
        var operationDirectory = GetOperationDirectory(operationId);
        Directory.CreateDirectory(operationDirectory);
        EnsureRegularDirectory(operationDirectory);
    }

    internal async Task WriteNewAsync(
        RestoreOperationPlan plan,
        CancellationToken cancellationToken)
    {
        ValidatePlan(plan, GetPlanPath(plan.OperationId));
        if (plan.State != RestoreOperationState.Prepared)
            throw new InvalidOperationException("Restore.Operation.InitialStateInvalid");
        await WriteAtomicAsync(plan.OperationMarkerPath, plan, overwrite: false, cancellationToken);
    }

    internal async Task<RestoreOperationPlan> ReadAndValidateAsync(
        string planPath,
        Guid operationId,
        string oneTimeToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oneTimeToken))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidOperationToken);
        var expected = GetPlanPath(operationId);
        ValidatePlanPath(planPath, expected);
        RestoreOperationPlan? plan;
        try
        {
            await using var stream = new FileStream(expected, FileMode.Open, FileAccess.Read,
                FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            plan = await JsonSerializer.DeserializeAsync<RestoreOperationPlan>(
                stream, JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or NotSupportedException)
        {
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        }
        if (plan is null) throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        ValidatePlan(plan, expected);
        ValidateToken(plan.OperationTokenSha256Hex, oneTimeToken);
        return plan;
    }

    internal async Task<RestoreOperationPlan> TransitionAsync(
        RestoreOperationPlan plan,
        RestoreOperationState nextState,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        if (plan.State != nextState && !IsLegalTransition(plan.State, nextState))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        var updated = plan with
        {
            State = nextState,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            FailureCode = failureCode
        };
        ValidatePlan(updated, updated.OperationMarkerPath);
        await WriteAtomicAsync(updated.OperationMarkerPath, updated, overwrite: true, cancellationToken);
        return updated;
    }

    internal static Task WriteResultAsync(
        RestoreOperationPlan plan,
        RestoreExecutionResult result,
        CancellationToken cancellationToken) =>
        WriteAtomicAsync(plan.ResultMarkerPath, result,
            overwrite: File.Exists(plan.ResultMarkerPath), cancellationToken);

    internal static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    internal async Task<RestoreStartupDiscovery> DiscoverStartupOperationAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_operationsRoot))
            return RestoreStartupDiscovery.None;

        ValidateSafePathChain(_operationsRoot);
        EnsureRegularDirectory(_operationsRoot);
        var discovered = new List<RestoreStartupOperation>();
        foreach (var directory in Directory.EnumerateDirectories(_operationsRoot, "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRegularDirectory(directory);
            if (!Guid.TryParseExact(Path.GetFileName(directory), "D", out var operationId) ||
                operationId == Guid.Empty)
                return RestoreStartupDiscovery.Blocked("Restore.Startup.AmbiguousOperations");

            var planPath = GetPlanPath(operationId);
            if (!File.Exists(planPath)) continue;
            RestoreOperationPlan? plan;
            try
            {
                await using var stream = new FileStream(planPath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                plan = await JsonSerializer.DeserializeAsync<RestoreOperationPlan>(
                    stream, JsonOptions, cancellationToken);
                if (plan is null) return RestoreStartupDiscovery.Blocked("Restore.Startup.InvalidOperation");
                ValidatePlan(plan, planPath);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                return RestoreStartupDiscovery.Blocked("Restore.Startup.InvalidOperation");
            }

            var acknowledged = File.Exists(Path.Combine(directory, AcknowledgementFileName));
            if (acknowledged && plan.State is RestoreOperationState.Verified or RestoreOperationState.RolledBack)
                continue;
            discovered.Add(new(plan, acknowledged));
        }

        if (discovered.Count == 0) return RestoreStartupDiscovery.None;
        if (discovered.Count != 1)
            return RestoreStartupDiscovery.Blocked("Restore.Startup.AmbiguousOperations");
        return new(discovered[0], null);
    }

    internal async Task<string> AuthorizeTrustedStartupRecoveryAsync(
        RestoreOperationPlan discoveredPlan,
        CancellationToken cancellationToken)
    {
        var expected = GetPlanPath(discoveredPlan.OperationId);
        ValidatePlan(discoveredPlan, expected);
        if (discoveredPlan.State is RestoreOperationState.Verified or
            RestoreOperationState.RolledBack or RestoreOperationState.RollbackFailed)
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var updated = discoveredPlan with
        {
            OperationTokenSha256Hex = HashToken(token),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
        await WriteAtomicAsync(expected, updated, overwrite: true, cancellationToken);
        return token;
    }

    internal static async Task AcknowledgeTerminalResultAsync(
        RestoreOperationPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.State is not (RestoreOperationState.Verified or RestoreOperationState.RolledBack))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        var directory = Path.GetDirectoryName(plan.OperationMarkerPath)!;
        EnsureRegularDirectory(directory);
        var path = Path.Combine(directory, AcknowledgementFileName);
        if (File.Exists(path)) return;
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 1, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(new byte[] { 1 }, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    internal static bool IsLegalTransition(RestoreOperationState current, RestoreOperationState next) =>
        current switch
        {
            RestoreOperationState.Prepared => next is RestoreOperationState.WaitingForParentExit or
                RestoreOperationState.ReplacementStarted,
            RestoreOperationState.WaitingForParentExit => next == RestoreOperationState.ReplacementStarted,
            RestoreOperationState.ReplacementStarted => next is RestoreOperationState.CandidateInstalled or
                RestoreOperationState.RollbackStarted,
            RestoreOperationState.CandidateInstalled => next is RestoreOperationState.Verified or
                RestoreOperationState.RollbackStarted,
            RestoreOperationState.RollbackStarted => next is RestoreOperationState.RolledBack or
                RestoreOperationState.RollbackFailed,
            _ => false
        };

    private void ValidatePlan(RestoreOperationPlan plan, string expectedPlanPath)
    {
        if (plan.FormatVersion != CurrentFormatVersion || plan.OperationId == Guid.Empty ||
            plan.ParentProcessId <= 0 || plan.ParentProcessStartTimeUtcTicks <= 0 ||
            plan.CandidateByteLength <= 0 || plan.SafetyBackupByteLength <= 0 ||
            plan.OriginalDatabaseByteLength <= 0 ||
            !IsSha256(plan.OperationTokenSha256Hex) || !IsSha256(plan.CandidateSha256Hex) ||
            !IsSha256(plan.SafetyBackupSha256Hex) || !IsSha256(plan.OriginalDatabaseSha256Hex))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);

        var operationDirectory = GetOperationDirectory(plan.OperationId);
        RequireExactPath(plan.OperationMarkerPath, expectedPlanPath);
        RequireExactPath(plan.ResultMarkerPath, Path.Combine(operationDirectory, ResultFileName));
        RequireExactPath(plan.StagedCandidatePath, Path.Combine(operationDirectory, "candidate.db"));
        RequireExactPath(plan.RollbackPath, Path.Combine(operationDirectory, "original.rollback.db"));
        RequireExactPath(plan.FailedCandidatePath, Path.Combine(operationDirectory, "failed-candidate.db"));
        RequireExactPath(plan.ActiveDatabasePath, _activeDatabasePath);
        RequireDirectChildBoundary(plan.SafetyBackupPath,
            Path.Combine(Path.GetDirectoryName(_activeDatabasePath)!, "backups", "pre-restore"));
        ValidateSafePathChain(operationDirectory);
    }

    private static void ValidateToken(string expectedHex, string rawToken)
    {
        byte[] expected;
        try { expected = Convert.FromHexString(expectedHex); }
        catch (FormatException) { throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan); }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidOperationToken);
    }

    private static async Task WriteAtomicAsync<T>(
        string path, T value, bool overwrite, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        EnsureRegularDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." +
            Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            if (overwrite)
            {
                if (!File.Exists(path)) throw new IOException("Restore marker disappeared.");
                File.Replace(temporary, path, null, ignoreMetadataErrors: true);
            }
            else File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void ValidatePlanPath(string supplied, string expected)
    {
        if (string.IsNullOrWhiteSpace(supplied) || !Path.IsPathFullyQualified(supplied) ||
            ContainsTraversal(supplied))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
        RequireExactPath(supplied, expected);
        ValidateSafePathChain(Path.GetDirectoryName(expected)!);
    }

    private static void RequireDirectChildBoundary(string path, string expectedParent)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(expectedParent),
                StringComparison.OrdinalIgnoreCase))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
    }

    private static void RequireExactPath(string actual, string expected)
    {
        if (!Path.IsPathFullyQualified(actual) || ContainsTraversal(actual) ||
            !string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.InvalidPlan);
    }

    private static void ValidateSafePathChain(string path)
    {
        if (path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        EnsureSafeExistingAncestors(path);
    }

    private static void EnsureSafeExistingAncestors(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
        {
            if (!current.Exists) continue;
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        }
    }

    private static void EnsureRegularDirectory(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0 ||
            (attributes & FileAttributes.ReparsePoint) != 0)
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
    }

    private static bool ContainsTraversal(string path) => path.Split(
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
        StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..");

    private static bool IsSha256(string value) => value.Length == 64 &&
        value.All(character => char.IsAsciiHexDigit(character));
}

internal sealed class RestoreOperationStoreException(RestoreExecutionStatus status) : Exception
{
    internal RestoreExecutionStatus Status { get; } = status;
}

internal sealed record RestoreStartupOperation(
    RestoreOperationPlan Plan,
    bool IsAcknowledged);

internal sealed record RestoreStartupDiscovery(
    RestoreStartupOperation? Operation,
    string? BlockingMessageKey)
{
    internal static RestoreStartupDiscovery None { get; } = new(null, null);
    internal bool IsBlocked => BlockingMessageKey is not null;
    internal static RestoreStartupDiscovery Blocked(string key) => new(null, key);
}
