using BlindCatCore.Models;
using ScaffoldLib.Maui;
using System.Collections;
using System.Collections.Specialized;

namespace BlindCatMaui.SDControls;

public class MenuItemController : BindableObject
{
    // menu items
    public static readonly BindableProperty MenuItemsProperty = BindableProperty.CreateAttached(
        "ControllerMenuItems",
        typeof(IList),
        typeof(MenuItemController),
        null,
        propertyChanged: (b, o, n) =>
        {
            var menus = Scaffold.GetMenuItems(b);
            Bind(b, menus, n as IList);
        }
    );
    public static IList GetMenuItems(BindableObject b)
    {
        return (IList)b.GetValue(MenuItemsProperty);
    }
    public static void SetMenuItems(BindableObject b, IList value)
    {
        b.SetValue(MenuItemsProperty, value);
    }

    private static void Bind(BindableObject b, ScaffoldLib.Maui.Core.MenuItemCollection menus, IList? bind)
    {
        if (b is INotifyCollectionChanged bb)
            bb.CollectionChanged += Menus_CollectionChanged;

        if (bind != null)
        {
            foreach (var item in bind)
            {
                Add(b, (MPButtonContext)item);
            }
        }
    }

    private static void Menus_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
    }

    private static void Add(BindableObject b, MPButtonContext context)
    {
        var m = Scaffold.GetMenuItems(b);
        m.Add(new ScaffoldMenuItem
        {
            BindingContext = context,
            Text = context.Name,
            Command = context.Command,
        });
    }
}