using Avalonia;
using Avalonia.Markup.Xaml.Templates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatAvalonia.SDcontrols.Scaffold.GlobalXmlns;

public class ScaffoldMenu : AvaloniaObject
{
    private string? _text;
    private ICommand? _command;
    private DataTemplate? _customView;

    public ScaffoldMenu()
    {
    }

    // text
    public static readonly DirectProperty<ScaffoldMenu, string?> TextProperty = AvaloniaProperty.RegisterDirect<ScaffoldMenu, string?>(
        nameof(Text),
        (self) => self._text,
        (self, nev) =>
        {
            self._text = nev;
        }
    );
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    // command
    public static readonly DirectProperty<ScaffoldMenu, ICommand?> CommandProperty = AvaloniaProperty.RegisterDirect<ScaffoldMenu, ICommand?>(
        nameof(Text),
        (self) => self._command,
        (self, nev) =>
        {
            self._command = nev;
        }
    );
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetAndRaise(CommandProperty, ref _command, value);
    }

    // custom view
    public static readonly DirectProperty<ScaffoldMenu, DataTemplate?> CustomViewProperty = AvaloniaProperty.RegisterDirect<ScaffoldMenu, DataTemplate?>(
        nameof(CustomView),
        (self) => self._customView,
        (self, nev) =>
        {
            self._customView = nev;
        }
    );
    public DataTemplate? CustomView
    {
        get => GetValue(CustomViewProperty);
        set => SetAndRaise(CustomViewProperty, ref _customView, value);
    }
}