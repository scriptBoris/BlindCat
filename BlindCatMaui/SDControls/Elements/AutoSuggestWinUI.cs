using System.Collections;
using System.Diagnostics;
using System.Windows.Input;

namespace BlindCatMaui.SDControls.Elements;

#if WINDOWS
public class AutoSuggestWinUI : View
{
    private Microsoft.UI.Xaml.Controls.AutoSuggestBox? native;
    
    // text changed command
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(AutoSuggestWinUI),
        "",
        BindingMode.TwoWay,
        propertyChanged: (b,o,n) =>
        {
            var nat = GetNat(b);
            if (nat != null)
                nat.Text = n as string;
        }
    );
    public string? Text
    {
        get => GetValue(TextProperty) as string;
        set => SetValue(TextProperty, value);
    }

    // text changed command
    public static readonly BindableProperty TextChangedCommandProperty = BindableProperty.Create(
        nameof(TextChangedCommand),
        typeof(ICommand),
        typeof(AutoSuggestWinUI),
        null,
        BindingMode.OneWay
    );
    public ICommand? TextChangedCommand
    {
        get => GetValue(TextChangedCommandProperty) as ICommand;
        set => SetValue(TextChangedCommandProperty, value);
    }

    // return command
    public static readonly BindableProperty ReturnCommandProperty = BindableProperty.Create(
        nameof(ReturnCommand),
        typeof(ICommand),
        typeof(AutoSuggestWinUI),
        null
    );
    public ICommand? ReturnCommand
    {
        get => GetValue(ReturnCommandProperty) as ICommand;
        set => SetValue(ReturnCommandProperty, value);
    }

    // selected suggestion
    public static readonly BindableProperty SelectedSuggestionProperty = BindableProperty.Create(
        nameof(SelectedSuggestion),
        typeof(ICommand),
        typeof(AutoSuggestWinUI),
        null
    );
    public ICommand? SelectedSuggestion
    {
        get => GetValue(SelectedSuggestionProperty) as ICommand;
        set => SetValue(SelectedSuggestionProperty, value);
    }

    // placeholder
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(AutoSuggestWinUI),
        null,
        propertyChanged:(b,o,n) =>
        {
            var nat = GetNat(b);
            if (nat != null)
                nat.PlaceholderText = n as string;
        }
    );
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty) as string;
        set => SetValue(PlaceholderProperty, value);
    }

    // items source
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList),
        typeof(AutoSuggestWinUI),
        null,
        propertyChanged: (b, o, n) =>
        {
            var nat = GetNat(b);
            if (nat != null)
                nat.ItemsSource = n;
        }
    );
    public IList? ItemsSource
    {
        get => GetValue(ItemsSourceProperty) as IList;
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        native = Handler?.PlatformView as Microsoft.UI.Xaml.Controls.AutoSuggestBox;
        base.OnHandlerChanged();
    }

    private static Microsoft.UI.Xaml.Controls.AutoSuggestBox? GetNat(BindableObject b)
    {
        if (b is AutoSuggestWinUI self && self.native != null)
            return self.native;
        return null;
    }

    //protected override void OnHandlerChanged()
    //{
    //    base.OnHandlerChanged();
    //    var nev = Handler?.PlatformView as Microsoft.UI.Xaml.Controls.AutoSuggestBox;

    //    // old
    //    if (old != null)
    //    {
    //        old.QuerySubmitted -= Handler_QuerySubmitted;
    //        old.TextChanged -= _TextChanged;
    //    }

    //    // new
    //    //native = Handler?.PlatformView as Microsoft.UI.Xaml.Controls.AutoSuggestBox;
    //    if (nev != null)
    //    {
    //        nev.QuerySubmitted += Handler_QuerySubmitted;
    //        nev.TextChanged += _TextChanged;
    //    }

    //    old = nev;
    //}

    //private void _TextChanged(Microsoft.UI.Xaml.Controls.AutoSuggestBox sender, Microsoft.UI.Xaml.Controls.AutoSuggestBoxTextChangedEventArgs args)
    //{
    //    Debug.WriteLine($"REASON: {args.Reason}");
    //    switch (args.Reason)
    //    {
    //        case Microsoft.UI.Xaml.Controls.AutoSuggestionBoxTextChangeReason.UserInput:
    //            TextChangedCommand?.Execute(sender.Text);
    //            break;
    //        case Microsoft.UI.Xaml.Controls.AutoSuggestionBoxTextChangeReason.ProgrammaticChange:
    //            break;
    //        case Microsoft.UI.Xaml.Controls.AutoSuggestionBoxTextChangeReason.SuggestionChosen:
    //            break;
    //        default:
    //            break;
    //    }
    //}

    //private void Handler_QuerySubmitted(Microsoft.UI.Xaml.Controls.AutoSuggestBox sender, Microsoft.UI.Xaml.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
    //{
    //    this.ReturnCommand?.Execute(Text);
    //}

    //private void AutoSuggest_TextChanged(object? sender, AutoCompleteEntryTextChangedEventArgs e)
    //{
    //    var tt = sender as AutoCompleteEntry;
    //    if (tt != null && e.Reason == AutoCompleteEntryTextChangeReason.UserInput)
    //    {
    //        string text = tt.Text;
    //        TextChangedCommand?.Execute(text);
    //    }
    //}
}
#endif