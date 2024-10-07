using Microsoft.Maui.Platform;

namespace BlindCatMaui.SDControls;

public class CheckBox : Microsoft.Maui.Controls.CheckBox
{
    public static readonly BindableProperty ForegroundThemeProperty = BindableProperty.Create(
        nameof(ForegroundTheme), 
        typeof(AppTheme),
        typeof(CheckBox),
        AppTheme.Unspecified, 
        propertyChanged: (b,o,n) =>
        {
            if (b is CheckBox self)
                self.UpdateFg();
        }
    );
    public AppTheme ForegroundTheme
    {
        get => (AppTheme)GetValue(ForegroundThemeProperty);
        set => SetValue(ForegroundThemeProperty, value);
    }

    private void UpdateFg()
    {
#if WINDOWS
        var ch = Handler?.PlatformView as global::Microsoft.UI.Xaml.Controls.CheckBox;
        if (ch != null)
        {
            switch (ForegroundTheme)
            {
                case AppTheme.Unspecified:
                    ch.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Default;
                    break;
                case AppTheme.Light:
                    ch.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light;
                    break;
                case AppTheme.Dark:
                    ch.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark;
                    break;
                default:
                    break;
            }
        }
#endif
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateFg();
    }
}