using System.Windows;
using System.Windows.Media;

namespace POS.Wpf.Views;

public sealed class LabelPreviewHost : FrameworkElement
{
    private readonly VisualCollection _children;

    public LabelPreviewHost()
    {
        _children = new VisualCollection(this);
        Width = 1;
        Height = 1;
    }

    public void SetVisual(Visual? visual, double width, double height)
    {
        _children.Clear();
        if (visual is not null) _children.Add(visual);
        Width = width;
        Height = height;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];
    protected override Size MeasureOverride(Size availableSize) => new(Width, Height);
    protected override Size ArrangeOverride(Size finalSize)
    {
        return new Size(Width, Height);
    }
}
