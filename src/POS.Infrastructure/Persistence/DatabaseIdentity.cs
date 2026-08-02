using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Stable identity for the SQLite database used by one application instance.
/// </summary>
public sealed record DatabaseIdentity
{
    private DatabaseIdentity(
        string canonicalDatabasePath,
        string hash)
    {
        CanonicalDatabasePath = canonicalDatabasePath;
        Hash = hash;
        MutexName =
            "Local\\POS.Enterprise.SingleInstance." + hash;
        PipeName =
            "POS.Enterprise.Activation." + hash;
    }

    public string CanonicalDatabasePath { get; }

    public string Hash { get; }

    public string MutexName { get; }

    public string PipeName { get; }

    public static DatabaseIdentity FromResolvedPath(
        string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            throw new ArgumentException(
                "Đường dẫn database không được để trống.",
                nameof(resolvedPath));
        }

        var fullPath =
            Path.GetFullPath(resolvedPath.Trim());

        var normalizedPath =
            fullPath.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar);

        var root =
            Path.GetPathRoot(normalizedPath);

        if (!Path.IsPathFullyQualified(normalizedPath) ||
            string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException(
                "Đường dẫn database phải là đường dẫn tuyệt đối.",
                nameof(resolvedPath));
        }

        if (!string.Equals(
                normalizedPath,
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath =
                Path.TrimEndingDirectorySeparator(
                    normalizedPath);
        }

        normalizedPath =
            normalizedPath.ToUpperInvariant();

        var hashBytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(normalizedPath));

        var hash =
            Convert.ToHexString(hashBytes);

        return new DatabaseIdentity(
            normalizedPath,
            hash);
    }
}
