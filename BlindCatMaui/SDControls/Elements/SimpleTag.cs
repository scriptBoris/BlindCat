namespace BlindCatMaui.SDControls.Elements;

public class SimpleTag : Border
{
    private readonly Label label;

    public SimpleTag()
    {
        label = new Label
        {
            TextColor = Color.FromArgb("#333"),
        };
        BackgroundColor = Color.FromRgba("#3cade8");
        StrokeThickness = 0;
        Padding = new Thickness(5, 2);
        Content = label;
        VerticalOptions = LayoutOptions.Start;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = 3,
        };
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        string? str = BindingContext?.ToString();
        label.Text = str;
    }
}