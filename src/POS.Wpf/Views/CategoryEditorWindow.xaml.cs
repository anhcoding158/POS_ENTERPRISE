using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ thêm và chỉnh sửa danh mục.
/// </summary>
public partial class CategoryEditorWindow :
    global::System.Windows.Window
{
    private readonly CategoryEditorViewModel
        _viewModel;

    public CategoryEditorWindow(
        CategoryEditorViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext =
            _viewModel;

        _viewModel.RequestClose +=
            OnRequestClose;

        Closed +=
            OnWindowClosed;
    }

    private void OnRequestClose(
        bool? dialogResult)
    {
        DialogResult =
            dialogResult;
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        _viewModel.RequestClose -=
            OnRequestClose;

        Closed -=
            OnWindowClosed;
    }
}