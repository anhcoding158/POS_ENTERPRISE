using System;
using System.IO;

namespace POS.Architecture.Tests;

/// <summary>
/// Tìm thư mục gốc repository từ thư mục output của test.
///
/// Test không phụ thuộc current working directory của
/// Visual Studio, dotnet test hoặc CI.
/// </summary>
internal static class RepositoryLocator
{
    private const string SolutionFileName =
        "POS.Enterprise.slnx";

    public static string Root { get; } =
        FindRepositoryRoot();

    public static string GetPath(
        params string[] pathParts)
    {
        ArgumentNullException.ThrowIfNull(pathParts);

        return Path.Combine(
            [Root, .. pathParts]);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? currentDirectory =
            new(
                AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath =
                Path.Combine(
                    currentDirectory.FullName,
                    SolutionFileName);

            if (File.Exists(solutionPath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory =
                currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Không tìm thấy '{SolutionFileName}' " +
            $"khi bắt đầu từ '{AppContext.BaseDirectory}'.");
    }
}