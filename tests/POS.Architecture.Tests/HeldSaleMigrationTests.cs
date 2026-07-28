using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleMigrationTests
{
    [Fact]
    public void HeldSaleMigration_up_and_down_are_scoped_to_held_sale_tables()
    {
        var root = FindRoot();
        var path = Directory.GetFiles(
            Path.Combine(root, "src", "POS.Infrastructure", "Persistence", "Migrations"),
            "*_AddHeldSales.cs").Single();
        var source = File.ReadAllText(path);
        Assert.Contains("CreateTable(", source, StringComparison.Ordinal);
        Assert.Contains("name: \"HeldSales\"", source, StringComparison.Ordinal);
        Assert.Contains("name: \"HeldSaleLines\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Rename", source, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Không tìm thấy solution root.");
    }
}
