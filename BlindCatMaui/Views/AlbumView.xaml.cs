using BlindCatCore.ViewModels;
using BlindCatMaui.Core;
using Microsoft.Maui.Controls;

namespace BlindCatMaui.Views;

public partial class AlbumView
{
	public AlbumView()
	{
		InitializeComponent();
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        int cols = MakeGridItemsLayout(widthConstraint, heightConstraint);
        gridItemsLayout.Span = cols;
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    public static int MakeGridItemsLayout(double widthConstraint, double heightConstraint)
    {
        int cols = 0;
        if (widthConstraint >= 1000)
        {
            cols = 5;
        }
        else
        {
            double col = widthConstraint / 200;
            cols = (int)col;
        }

        if (cols < 1)
            cols = 1;

        return cols;
    }
}