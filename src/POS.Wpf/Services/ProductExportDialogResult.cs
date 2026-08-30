namespace POS.Wpf.Services;

public enum ProductExportDialogOutcome
{
    Saved,
    Canceled,
    Failed
}

public sealed record ProductExportDialogResult(
    ProductExportDialogOutcome Outcome,
    string? FileName,
    string? DestinationPath,
    int RowCount,
    string? Message);
