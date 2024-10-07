using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "BlindCatAvalonia.SDcontrols")]
namespace BlindCatAvalonia.SDcontrols;

public class Scaffolt : AvaloniaObject
{
    static Scaffolt()
    {
        TitleProperty.Changed.AddClassHandler<Interactive>(ChangedTitle);
        HasNavigationBarProperty.Changed.AddClassHandler<Interactive>(ChangedHasNavigationBar);
    }

    // title
    public static readonly AttachedProperty<string?> TitleProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, string?>(
        "Title"
    );
    public static void SetTitle(AvaloniaObject element, string title) => element.SetValue(TitleProperty, title);
    public static string? GetTitle(AvaloniaObject element) => element.GetValue(TitleProperty);
    private static void ChangedTitle(Interactive control, AvaloniaPropertyChangedEventArgs e)
    {

    }

    // subtitle
    public static readonly AttachedProperty<string?> SubtitleProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, string?>(
        "Subtitle"
    );
    public static void SetSubtitle(AvaloniaObject element, string title) => element.SetValue(SubtitleProperty, title);
    public static string? GetSubtitle(AvaloniaObject element) => element.GetValue(SubtitleProperty);
    private static void ChangedSubtitle(Interactive control, AvaloniaPropertyChangedEventArgs e)
    {
    }

    // has navigation bar
    public static readonly AttachedProperty<bool> HasNavigationBarProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, bool>(
        "HasNavigationBar",
        true
    );
    public static void SetHasNavigationBar(AvaloniaObject element, bool flag) => element.SetValue(HasNavigationBarProperty, flag);
    public static bool GetHasNavigationBar(AvaloniaObject element) => element.GetValue(HasNavigationBarProperty);
    private static void ChangedHasNavigationBar(Interactive control, AvaloniaPropertyChangedEventArgs e)
    {
    }

    // background navigation bar
    public static readonly AttachedProperty<IBrush> BackgroundNavigationBarProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, IBrush>(
        "BackgroundNavigationBar",
        new SolidColorBrush(new Color(255, 32,32,32))
    );
    public static void SetBackgroundNavigationBar(AvaloniaObject element, IBrush brush) => element.SetValue(BackgroundNavigationBarProperty, brush);
    public static IBrush GetBackgroundNavigationBar(AvaloniaObject element) => element.GetValue(BackgroundNavigationBarProperty);
    private static void ChangedBackgroundNavigationBar(Interactive control, AvaloniaPropertyChangedEventArgs e)
    {
    }

    // content under navigation bar
    public static readonly AttachedProperty<bool> ContentUnderNavigationBarProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, bool>(
        "ContentUnderNavigationBar",
        false
    );
    public static void SetContentUnderNavigationBar(AvaloniaObject element, bool flag) => element.SetValue(ContentUnderNavigationBarProperty, flag);
    public static bool GetContentUnderNavigationBar(AvaloniaObject element) => element.GetValue(ContentUnderNavigationBarProperty);
    private static void ChangedContentUnderNavigationBar(Interactive control, AvaloniaPropertyChangedEventArgs e)
    {

    }

    // menu items
    public static readonly AttachedProperty<AvaloniaList<MenuItem>> MenuItemsProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, AvaloniaList<MenuItem>>(
        "MenuItems",
        null!
    );
    public static AvaloniaList<MenuItem> GetMenuItems(Interactive element)
    {
        var items = element.GetValue(MenuItemsProperty);
        if (items == null)
        {
            items = new AvaloniaList<MenuItem>();
            element.SetValue(MenuItemsProperty, items);
        }
        return items;
    }

    public static void SetMenuItems(Interactive element, AvaloniaList<MenuItem> value) => element.SetValue(MenuItemsProperty, value);

    // custom nav bar
    public static readonly AttachedProperty<Control?> CustomNavigationBarProperty = AvaloniaProperty.RegisterAttached<Scaffolt, Interactive, Control?>(
        "CustomNavigationBar",
        null
    );
    public static Control? GetCustomNavigationBar(Interactive element) => element.GetValue(CustomNavigationBarProperty);
    public static void SetCustomNavigationBar(Interactive element, Control? value) => element.SetValue(CustomNavigationBarProperty, value);
}