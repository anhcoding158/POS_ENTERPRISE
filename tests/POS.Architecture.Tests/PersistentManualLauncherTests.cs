using Xunit;

namespace POS.Architecture.Tests;

public sealed class PersistentManualLauncherTests
{
    [Fact]
    public void Persistent_manual_launcher_uses_one_local_appdata_database_without_copying()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "scripts",
                "Start-POS-PersistentManual.ps1"));

        Assert.Contains("LocalApplicationData", source, StringComparison.Ordinal);
        Assert.Contains("POS Enterprise\\ManualAcceptance", source, StringComparison.Ordinal);
        Assert.Contains("pos-enterprise-manual.db", source, StringComparison.Ordinal);
        Assert.Contains("POS_RUNTIME_MODE", source, StringComparison.Ordinal);
        Assert.Contains("Infrastructure__DatabasePath", source, StringComparison.Ordinal);
        Assert.Contains("Test-ExistingPathChainHasReparsePoint", source, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $databasePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[IO.File]::Copy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NewGuid", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-Date", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
