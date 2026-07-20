using System;
using System.Text.Json;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Ngăn cấu hình phát hành quay lại trạng thái
/// chứa credential mặc định hoặc demo seed không kiểm soát.
/// </summary>
public sealed class SecurityConfigurationTests
{
    [Fact]
    public void Production_configuration_must_not_seed_default_admin()
    {
        using var document =
            LoadJsonDocument(
                "src",
                "POS.Wpf",
                "appsettings.json");

        var infrastructure =
            document.RootElement
                .GetProperty(
                    "Infrastructure");

        Assert.False(
            infrastructure
                .GetProperty(
                    "SeedDefaultAdministrator")
                .GetBoolean());

        var password =
            infrastructure
                .GetProperty(
                    "DefaultAdminPassword")
                .GetString();

        Assert.True(
            string.IsNullOrEmpty(password),
            "appsettings.json không được chứa " +
            "mật khẩu quản trị mặc định.");
    }

    [Fact]
    public void Production_configuration_must_not_seed_demo_catalog()
    {
        using var document =
            LoadJsonDocument(
                "src",
                "POS.Wpf",
                "appsettings.json");

        var infrastructure =
            document.RootElement
                .GetProperty(
                    "Infrastructure");

        Assert.False(
            infrastructure
                .GetProperty(
                    "SeedDemoProductCatalog")
                .GetBoolean());
    }

    [Fact]
    public void Development_configuration_may_seed_demo_but_not_admin()
    {
        using var document =
            LoadJsonDocument(
                "src",
                "POS.Wpf",
                "appsettings.Development.json");

        var infrastructure =
            document.RootElement
                .GetProperty(
                    "Infrastructure");

        Assert.True(
            infrastructure
                .GetProperty(
                    "SeedDemoProductCatalog")
                .GetBoolean());

        Assert.False(
            infrastructure
                .GetProperty(
                    "SeedDefaultAdministrator")
                .GetBoolean());

        var password =
            infrastructure
                .GetProperty(
                    "DefaultAdminPassword")
                .GetString();

        Assert.True(
            string.IsNullOrEmpty(password));
    }

    [Fact]
    public void Gitignore_must_exclude_sqlite_runtime_files()
    {
        var gitIgnorePath =
            RepositoryLocator.GetPath(
                ".gitignore");

        Assert.True(
            File.Exists(gitIgnorePath));

        var lines =
            File.ReadAllLines(
                    gitIgnorePath)
                .Select(
                    line =>
                        line.Trim())
                .Where(
                    line =>
                        !string.IsNullOrWhiteSpace(line))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        Assert.Contains(
            "*.db",
            lines);

        Assert.Contains(
            "*.db-shm",
            lines);

        Assert.Contains(
            "*.db-wal",
            lines);

        Assert.Contains(
            "*.db-journal",
            lines);
    }

    private static JsonDocument LoadJsonDocument(
        params string[] pathParts)
    {
        var path =
            RepositoryLocator.GetPath(
                pathParts);

        Assert.True(
            File.Exists(path),
            $"Không tìm thấy file cấu hình: {path}");

        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling =
                    JsonCommentHandling.Skip
            });
    }
}