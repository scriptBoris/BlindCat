namespace BlindCatMaui.Views;

public partial class StoragePresentView
{
    public StoragePresentView()
    {
        InitializeComponent();
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        int cols = DirPresentView.MakeGridItemsLayout(widthConstraint, heightConstraint);
        gridItemsLayout.Span = cols;
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }
}