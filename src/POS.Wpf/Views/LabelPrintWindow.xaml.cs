using System.ComponentModel;
using System.Windows;
using POS.Application.Printing;
using POS.Infrastructure.Printing;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class LabelPrintWindow : Window
{
    private readonly LabelPrintViewModel _viewModel;

    public LabelPrintWindow(LabelPrintViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.PreviewChanged += RenderPreview;
        _viewModel.RequestClose += OnRequestClose;
        Loaded += (_, _) => RenderPreview();
    }

    private void RenderPreview()
    {
        var product = _viewModel.PreviewProduct;
        if (product is null)
        {
            PreviewHost.SetVisual(null, 1, 1);
            return;
        }
        var template = _viewModel.PreviewTemplate;
        PreviewHost.SetVisual(
            LabelDocumentBuilder.Render(product, template, _viewModel.PreviewDateText),
            MillimetreConverter.ToDip(template.WidthMm),
            MillimetreConverter.ToDip(template.HeightMm));
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { _viewModel.CloseCommand.Execute(null); e.Handled = true; }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Đang gửi lệnh in. Vui lòng chờ hoàn tất.", "Đang in tem", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnRequestClose(bool result)
    {
        try
        {
            // ShowDialog() callers receive true only after a successful print.
            DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            // The STA harness may use Show(); keep the same close path there.
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PreviewChanged -= RenderPreview;
        _viewModel.RequestClose -= OnRequestClose;
        base.OnClosed(e);
    }
}
