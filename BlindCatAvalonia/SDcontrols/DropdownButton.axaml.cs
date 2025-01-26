using System;
using System.Collections;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatCore.Core;

namespace BlindCatAvalonia.SDcontrols;

public partial class DropdownButton : Grid
{
    private string? _placeholder;
    private object? _selectedItem;
    private object? _itemsSource;

    public event EventHandler<object>? SelectedItemChanged;

    public DropdownButton()
    {
        InitializeComponent();
        
        if (Design.IsDesignMode)
        {
            list.ItemsSource = new string[]
            {
                "Cat",
                "Big flying birds",
                "Fat a cows",
                "Skinny rat",
                "Strong gorilla",
            };
        }

        CommandClickItem = new Cmd<object>(OnItemSelected);
    }

    #region bindable props
    // selected item
    public static readonly DirectProperty<DropdownButton, object?> SelectedItemProperty = AvaloniaProperty.RegisterDirect<DropdownButton, object?>(
        nameof(SelectedItem),
        (self) => self._selectedItem,
        (self, nev) =>
        {
            self._selectedItem = nev;
            self.UpdateSelectedItem();
        },
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
    }

    // placeholder
    public static readonly DirectProperty<DropdownButton, string?> PlaceholderProperty = AvaloniaProperty.RegisterDirect<DropdownButton, string?>(
        nameof(Placeholder),
        (self) => self._placeholder,
        (self, nev) =>
        {
            self._placeholder = nev;
            self.labelPlaceholder.Text = nev;
        }
    );
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    // items source
    public static readonly DirectProperty<DropdownButton, object?> ItemsSourceProperty = AvaloniaProperty.RegisterDirect<DropdownButton, object?>(
        nameof(ItemsSource),
        (self) => self._itemsSource,
        (self, nev) =>
        {
            self._itemsSource = nev;

            IEnumerable res;
            if (nev is Type t)
            {
                res = Enum.GetValues(t).Cast<object>();
            }
            else if (nev is Enum en)
            {
                res = Enum.GetValues(nev.GetType()).Cast<object>();
            }
            else if (nev is IEnumerable enr)
            {
                res = enr;
            }
            else
            {
                throw new NotSupportedException();
            }

            self.list.ItemsSource = res;
        }
    );
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    #endregion bindable props

    public ICommand CommandClickItem { get; }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        button.Click += Button_Click;
        popup.Opened += Popup_Opened;
        popup.Closed += Popup_Closed;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        button.Click -= Button_Click;
        popup.Opened -= Popup_Opened;
        popup.Closed -= Popup_Closed;
    }

    private void OnItemSelected(object item)
    {
        popup.IsOpen = false;
        SelectedItem = item;
        SelectedItemChanged?.Invoke(this, item);
    }

    private void Popup_Closed(object? sender, System.EventArgs e)
    {
        button.CornerRadius = new CornerRadius(4);
        labelSelected.Opacity = 1;
        icon.Opacity = 1;
    }

    private void Popup_Opened(object? sender, System.EventArgs e)
    {
        button.CornerRadius = new CornerRadius(4, 4, 0, 0);
        labelSelected.Opacity = 0.5;
        icon.Opacity = 0.5;
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        popup.Width = Bounds.Width;
        popup.IsOpen = true;
    }

    private void UpdateSelectedItem()
    {
        if (SelectedItem == null)
        {
            labelSelected.IsVisible = false;
            labelPlaceholder.IsVisible = true;
        }
        else
        {
            labelSelected.Text = SelectedItem.ToString();
            labelSelected.IsVisible = true;
            labelPlaceholder.IsVisible = false;
        }
    }
}