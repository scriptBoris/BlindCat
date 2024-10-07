using BlindCatMaui.SDControls.Elements;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Controls;
using zoft.MauiExtensions.Controls.Handlers;
using zoft.MauiExtensions.Controls;
using zoft.MauiExtensions.Controls.Platform;
using Windows.System;
using Microsoft.UI.Xaml.Input;

namespace BlindCatMaui.Platforms.Windows.Handlers;

public class AutoSuggestWinUIHandler : ViewHandler<AutoSuggestWinUI, AutoSuggestBox>
{
    public static PropertyMapper<AutoCompleteEntry, AutoSuggestWinUIHandler> Mapper = new();
    private bool useSuggestionNavigation;

    public AutoSuggestWinUIHandler() : base(Mapper)
    {
    }

    protected override AutoSuggestBox CreatePlatformView()
    {
        var native = new AutoSuggestBox
        {
            AutoMaximizeSuggestionArea = false,
            UpdateTextOnSelect = true,
        };
        return native;
    }

    protected override void ConnectHandler(AutoSuggestBox platformView)
    {
        PlatformView.Loaded += OnLoaded;
        PlatformView.TextChanged += AutoSuggestBox_TextChanged;
        PlatformView.QuerySubmitted += Handler_QuerySubmitted;
        PlatformView.GotFocus += Control_GotFocus;
        //PlatformView.SuggestionChosen += PlatformView_SuggestionChosen;
        //PlatformView.KeyDown += PlatformView_KeyDown;
        //PlatformView.KeyUp += PlatformView_KeyUp;
    }

    protected override void DisconnectHandler(AutoSuggestBox platformView)
    {
        PlatformView.Loaded -= OnLoaded;
        PlatformView.TextChanged -= AutoSuggestBox_TextChanged;
        PlatformView.GotFocus -= Control_GotFocus;
        PlatformView.QuerySubmitted -= Handler_QuerySubmitted;
        //PlatformView.SuggestionChosen -= PlatformView_SuggestionChosen;
        //PlatformView.KeyDown -= PlatformView_KeyDown;
        //PlatformView.KeyUp -= PlatformView_KeyUp;

        base.DisconnectHandler(platformView);
    }

    //private void PlatformView_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    //{
    //    if (VirtualView != null && !useSuggestionNavigation)
    //    {
    //        string val = args.SelectedItem.ToString();
    //        VirtualView.ReturnCommand?.Execute(val);
    //    }
    //}

    private void Handler_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (VirtualView != null)
        {
            string txt = args.ChosenSuggestion?.ToString() ?? sender.Text;
            VirtualView.ReturnCommand?.Execute(txt);
        }
    }

    private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (VirtualView != null)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                VirtualView.Text = sender.Text;
                VirtualView.TextChangedCommand?.Execute(sender.Text);
            }
        }
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (VirtualView != null)
        {
            PlatformView.PlaceholderText = VirtualView.Placeholder;
        }
    }

    private void Control_GotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (VirtualView?.ItemsSource?.Count > 0)
        {
            PlatformView.IsSuggestionListOpen = true;
        }
    }
}