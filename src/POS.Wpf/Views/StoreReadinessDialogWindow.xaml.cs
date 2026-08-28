using POS.Application.Abstractions.StoreSetup;

namespace POS.Wpf.Views;

public partial class StoreReadinessDialogWindow : global::System.Windows.Window
{
    public StoreReadinessDialogWindow(IReadOnlyList<StoreSettingsIssue> issues)
    {
        InitializeComponent();
        Issues = issues ?? Array.Empty<StoreSettingsIssue>();
        DataContext = this;
    }

    public IReadOnlyList<StoreSettingsIssue> Issues { get; }
    public bool OpenSettingsRequested { get; private set; }

    private void OnOpenSettingsClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        OpenSettingsRequested = true;
        Close();
    }

    private void OnCloseClick(object sender, global::System.Windows.RoutedEventArgs e) => Close();
}
