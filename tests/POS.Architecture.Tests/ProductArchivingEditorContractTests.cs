using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingEditorContractTests
{
    [Theory]
    [InlineData("Archived_editor_view_model_must_be_read_only", "public bool CanEdit =>")]
    [InlineData("Archived_editor_must_not_allow_save", "return !IsBusy &&\n               CanEdit;")]
    [InlineData("Archived_editor_must_not_allow_active_state_change", "if (!CanEdit)")]
    [InlineData("Restored_product_editor_must_be_editable", "IsArchived =\n                product.IsArchived;")]
    [InlineData("New_product_editor_must_be_editable", "IsArchived = false;")]
    [InlineData("Product_dialog_must_pass_archive_state_to_editor", "ApplyProduct(\n                    productResult.Value);")]
    public void Editor_view_model_contracts(
        string contract,
        string expected)
    {
        _ = contract;
        var source = Read(
            "src", "POS.Wpf", "ViewModels", "ProductEditorViewModel.cs");

        Assert.Contains(expected, Normalize(source));
    }

    [Fact]
    public void Archived_editor_must_not_allow_image_selection_or_removal()
    {
        var codeBehind = Read(
            "src", "POS.Wpf", "Views", "ProductEditorWindow.xaml.cs");
        var xaml = Read(
            "src", "POS.Wpf", "Views", "ProductEditorWindow.xaml");

        Assert.Equal(
            2,
            Normalize(codeBehind).Split("if (!_viewModel.CanEdit)").Length - 1);
        Assert.Contains(
            "IsEnabled=\"{Binding CanEdit}\"",
            xaml);
    }

    private static string Read(params string[] path)
    {
        return File.ReadAllText(
            Path.Combine([RepositoryLocator.Root, .. path]));
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}