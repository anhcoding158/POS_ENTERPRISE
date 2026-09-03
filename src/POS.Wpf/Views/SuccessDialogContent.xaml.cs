namespace POS.Wpf.Views;

public partial class SuccessDialogContent : global::System.Windows.Controls.UserControl
{
    public SuccessDialogContent()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        FindVisualDescendants<global::System.Windows.Controls.Button>(this)
            .FirstOrDefault(button =>
                global::System.Windows.Automation.AutomationProperties.GetName(button) == "Đã hiểu")
            ?.Focus();
    }

    private void OnAcknowledgeClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        global::System.Windows.Window.GetWindow(this)?.Close();
    }

    private static IEnumerable<T> FindVisualDescendants<T>(global::System.Windows.DependencyObject root)
        where T : global::System.Windows.DependencyObject
    {
        for (var index = 0; index < global::System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = global::System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }
}
