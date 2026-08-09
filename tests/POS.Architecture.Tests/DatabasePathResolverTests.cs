using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class DatabasePathResolverTests
{
    private static readonly string RepositoryRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

    [Fact]
    public void Debug_output_uses_solution_root_even_with_arbitrary_cwd()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "src", "POS.Wpf", "bin", "Debug", "net10.0-windows");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            Path.GetTempPath(),
            appBase);

        Assert.Equal(RepositoryRoot, resolved);
    }

    [Fact]
    public void Release_output_uses_solution_root_even_with_repository_cwd()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "src", "POS.Wpf", "bin", "Release", "net10.0-windows");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase);

        Assert.Equal(RepositoryRoot, resolved);
    }

    [Fact]
    public void Test_output_uses_solution_root()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "tests", "POS.Architecture.Tests", "bin", "Release", "net10.0-windows");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "testhost.exe",
            "testhost");

        Assert.Equal(RepositoryRoot, resolved);
    }

    [Fact]
    public void Standard_publish_under_bin_uses_local_application_data()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "src", "POS.Wpf", "bin", "Release", "net10.0-windows", "publish");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "POS.Enterprise.exe",
            "POS.Enterprise");

        Assert.Equal(LocalApplicationDataRoot(), resolved);
    }

    [Fact]
    public void Published_output_does_not_use_solution_root()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "_ci_artifacts", "publish", "POS.Wpf", "win-x64");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "POS.Enterprise.exe");

        var expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "POS Enterprise"));

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Published_output_inside_repository_is_safe_even_if_executable_is_renamed()
    {
        var appBase = Path.Combine(
            RepositoryRoot,
            "_ci_artifacts", "publish", "renamed-app");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "renamed-pos.exe",
            "Renamed.Pos");

        Assert.Equal(LocalApplicationDataRoot(), resolved);
    }

    [Fact]
    public void Published_entry_assembly_ignores_repository_cwd_even_if_executable_is_renamed()
    {
        var appBase = Path.Combine(
            Path.GetTempPath(),
            "POS-Published-Outside-Repository");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "renamed-pos.exe",
            "POS.Enterprise");

        Assert.Equal(LocalApplicationDataRoot(), resolved);
    }

    [Fact]
    public void Repository_tooling_keeps_solution_root()
    {
        var appBase = Path.Combine(
            Path.GetTempPath(),
            "POS-Tooling-Outside-Repository");

        var resolved = DatabasePathResolver.ResolveApplicationBaseDirectory(
            RepositoryRoot,
            appBase,
            "dotnet.exe",
            "dotnet-ef");

        Assert.Equal(RepositoryRoot, resolved);
    }

    [Fact]
    public void Development_marker_distinguishes_build_output_from_publish_output()
    {
        var buildOutput = Path.Combine(
            RepositoryRoot,
            "src", "POS.Wpf", "bin", "Release", "net10.0-windows");
        var publishOutput = Path.Combine(
            RepositoryRoot,
            "src", "POS.Wpf", "bin", "Release", "net10.0-windows", "publish");

        Assert.True(
            DatabasePathResolver.IsDevelopmentOutput(buildOutput));
        Assert.False(
            DatabasePathResolver.IsDevelopmentOutput(publishOutput));
    }

    private static string LocalApplicationDataRoot() =>
        Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "POS Enterprise"));
}
