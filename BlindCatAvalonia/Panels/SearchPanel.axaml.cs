using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.SDcontrols;
using System.Windows.Input;

namespace BlindCatAvalonia.Panels;

public partial class SearchPanel : UserControl
{
    public SearchPanel()
    {
        InitializeComponent();

        // Установка привязки
        entrySearchBox.Bind(TextBox.TextProperty, new Binding
        {
            Path = nameof(SearchText),
            Mode = BindingMode.TwoWay,
            Source = this,
        });

        sortingDropdown.Bind(DropdownButton.SelectedItemProperty, new Binding
        {
            Path = nameof(SortingSelectedItem),
            Mode = BindingMode.TwoWay,
            Source = this,
        });
    }

    #region bindable props
    // close
    public static readonly StyledProperty<ICommand?> CommandCloseProperty = AvaloniaProperty.Register<SearchPanel, ICommand?>(
        nameof(CommandClose)
    );
    public ICommand? CommandClose
    {
        get => GetValue(CommandCloseProperty);
        set => SetValue(CommandCloseProperty, value);
    }

    // search text
    public static readonly StyledProperty<string?> SearchTextProperty = AvaloniaProperty.Register<SearchPanel, string?>(
        nameof(SearchText),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );
    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    // sorting
    public static readonly StyledProperty<ICommand?> CommandSortingProperty = AvaloniaProperty.Register<SearchPanel, ICommand?>(
        nameof(CommandSorting)
    );
    public ICommand? CommandSorting
    {
        get => GetValue(CommandSortingProperty);
        set => SetValue(CommandSortingProperty, value);
    }

    // sorting items source
    public static readonly StyledProperty<object?> SortingItemsSourceProperty = AvaloniaProperty.Register<SearchPanel, object?>(
        nameof(SortingItemsSource)
    );
    public object? SortingItemsSource
    {
        get => GetValue(SortingItemsSourceProperty);
        set => SetValue(SortingItemsSourceProperty, value);
    }

    // sorting selected item
    public static readonly StyledProperty<object?> SortingSelectedItemProperty = AvaloniaProperty.Register<SearchPanel, object?>(
        nameof(SortingSelectedItem),
        defaultBindingMode: BindingMode.TwoWay
    );
    public object? SortingSelectedItem
    {
        get => GetValue(SortingSelectedItemProperty);
        set => SetValue(SortingSelectedItemProperty, value);
    }
    #endregion bindable props

    private void ButtonClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CommandClose?.Execute(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ZIndexProperty)
        {
            int index = (int)change.NewValue!;
            if (index >= 0)
                entrySearchBox.Focus();
        }
        else if (change.Property == SortingItemsSourceProperty)
        {
            sortingDropdown.ItemsSource = change.NewValue;
        }
    }
}