using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.SDcontrols.Scaffold.Utils;

public class MenuItemTemplateSelector : AvaloniaObject, IDataTemplate
{
    private DataTemplate? _defaultTemplate;

    // default template
    public static readonly DirectProperty<MenuItemTemplateSelector, DataTemplate?> DefaultTemplateProperty = AvaloniaProperty.RegisterDirect<MenuItemTemplateSelector, DataTemplate?>(
        nameof(DefaultTemplate),
        (self) => self._defaultTemplate,
        (self, nev) =>
        {
            self._defaultTemplate = nev;
        }
    );
    public DataTemplate? DefaultTemplate
    {
        get => GetValue(DefaultTemplateProperty);
        set => SetAndRaise(DefaultTemplateProperty, ref _defaultTemplate, value);
    }

    public Avalonia.Controls.Control? Build(object? param)
    {
        if (param is not MenuItem menu)
            throw new InvalidCastException();

        if (menu.CustomView != null)
            return menu.CustomView.Build(param);

        return DefaultTemplate.Build(param);
    }

    public bool Match(object? param)
    {
        if (param is not MenuItem menu)
            throw new InvalidCastException();

        if (menu.CustomView == null && DefaultTemplate == null)
            return false;

        return true;
    }
}