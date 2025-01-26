using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace BlindCatAvalonia.SDcontrols;

public class AutoSuggestion : Avalonia.Controls.AutoCompleteBox
{
    private ICommand? _commandTextChanged;
    private ICommand? _commandCompleted;
    private TextBox? _textBox;

    public AutoSuggestion()
    {
        FilterMode = AutoCompleteFilterMode.None;
    }

    #region bindable props
    // text changed command
    public static readonly DirectProperty<AutoSuggestion, ICommand?> TextChangedCommandProperty = AvaloniaProperty.RegisterDirect<AutoSuggestion, ICommand?>(
        nameof(TextChangedCommand),
        (self) => self._commandTextChanged,
        (self, nev) =>
        {
            self._commandTextChanged = nev;
        }
    );
    public ICommand? TextChangedCommand
    {
        get => GetValue(TextChangedCommandProperty);
        set => SetAndRaise(TextChangedCommandProperty, ref _commandTextChanged, value);
    }

    // command completed
    public static readonly DirectProperty<AutoSuggestion, ICommand?> CommandCompletedProperty = AvaloniaProperty.RegisterDirect<AutoSuggestion, ICommand?>(
        nameof(CommandCompleted),
        (self) => self._commandCompleted,
        (self, nev) =>
        {
            self._commandCompleted = nev;
        }
    );
    public ICommand? CommandCompleted
    {
        get => GetValue(CommandCompletedProperty);
        set => SetAndRaise(CommandCompletedProperty, ref _commandCompleted, value);
    }
    #endregion bindable props

    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.AutoCompleteBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
    }

    protected override ISelectionAdapter? GetSelectionAdapterPart(INameScope nameScope)
    {
        ISelectionAdapter? adapter = null;
        SelectingItemsControl? selector = nameScope.Find<SelectingItemsControl>("PART_SelectingItemsControl");
        if (selector != null)
        {
            // Check if it is already an IItemsSelector
            adapter = selector as ISelectionAdapter;
            if (adapter == null)
            {
                // Built in support for wrapping a Selector control
                adapter = new SelectingItemsControlSelectionAdapter(selector, this);
            }
        }
        if (adapter == null)
        {
            adapter = nameScope.Find<ISelectionAdapter>("PART_SelectionAdapter");
        }
        return adapter;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TextChanged += OnTextChanged;
        KeyDown += OnKeyDown;
        DropDownOpened += AutoSuggestion_DropDownOpened;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        TextChanged -= OnTextChanged;
        KeyDown -= OnKeyDown;
        DropDownOpened -= AutoSuggestion_DropDownOpened;
    }

    private void AutoSuggestion_DropDownOpened(object? sender, EventArgs e)
    {
        navigatedSuggestion = null;
    }

    public void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool nav = false;
        switch (e.Key)
        {
            case Key.Escape:
                IsDropDownOpen = false;
                break;
            case Key.Space:
                if (App.IsButtonCtrlPressed)
                {
                    IsDropDownOpen = true;
                    e.Handled = true;
                }
                break;
            case Key.Tab:
                if (IsDropDownOpen && ItemsSource is IList list && list.Count > 0)
                {
                    Text = list[0]!.ToString();
                    _textBox!.Text = Text;
                    CaretIndex = Text!.Length;
                    IsDropDownOpen = false;
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                if (IsDropDownOpen && navigatedSuggestion != null)
                {
                    Text = navigatedSuggestion.ToString();
                    _textBox!.Text = Text;
                    IsDropDownOpen = false;
                    CaretIndex = Text!.Length;
                }
                else
                {
                    CommandCompleted?.Execute(Text);
                    Text = null;
                }
                break;
            case Key.Up:
            case Key.Down:
                nav = true;
                break;
            default:
                break;
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        switch (e.Route)
        {
            case RoutingStrategies.Direct:
                break;
            case RoutingStrategies.Tunnel:
                break;
            case RoutingStrategies.Bubble:
                TextChangedCommand?.Execute(Text);
                break;
            default:
                break;
        }
    }

    private object? navigatedSuggestion;

    private void OnNavigationSuggestion(SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            navigatedSuggestion = e.AddedItems[0];
        }
        else
        {
            navigatedSuggestion = null;
        }

        Debug.WriteLine($"navigated suggestion: {navigatedSuggestion}");
    }

    private void OnClickedSuggestion(object invoker, PointerReleasedEventArgs e)
    {
        if (invoker is ListBox v)
        {
            Text = v.SelectedItem?.ToString();
            _textBox!.Text = Text;
            IsDropDownOpen = false;
            CaretIndex = Text!.Length;
        }
    }

    private class SelectingItemsControlSelectionAdapter : ISelectionAdapter
    {
        private SelectingItemsControl? _selector;
        private readonly AutoSuggestion _host;

        private bool IgnoringSelectionChanged { get; set; }

        public SelectingItemsControl? SelectorControl
        {
            get => _selector;

            set
            {
                if (_selector != null)
                {
                    _selector.SelectionChanged -= OnSelectionChanged;
                    _selector.PointerReleased -= OnSelectorPointerReleased;
                }

                _selector = value;

                if (_selector != null)
                {
                    _selector.SelectionChanged += OnSelectionChanged;
                    _selector.PointerReleased += OnSelectorPointerReleased;
                }
            }
        }

        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

        public event EventHandler<RoutedEventArgs>? Commit;

        public event EventHandler<RoutedEventArgs>? Cancel;

        //public SelectingItemsControlSelectionAdapter()
        //{
        //}

        public SelectingItemsControlSelectionAdapter(SelectingItemsControl selector, AutoSuggestion host)
        {
            SelectorControl = selector;
            _host = host;
        }

        public object? SelectedItem
        {
            get => SelectorControl?.SelectedItem;

            set
            {
                IgnoringSelectionChanged = true;
                if (SelectorControl != null)
                {
                    SelectorControl.SelectedItem = value;
                }

                // Attempt to reset the scroll viewer's position
                if (value == null)
                {
                    ResetScrollViewer();
                }

                IgnoringSelectionChanged = false;
            }
        }

        public IEnumerable? ItemsSource
        {
            get => SelectorControl?.ItemsSource;
            set
            {
                if (SelectorControl != null)
                {
                    SelectorControl.ItemsSource = value;
                }
            }
        }

        private void ResetScrollViewer()
        {
            if (SelectorControl != null)
            {
                var sv = SelectorControl.GetLogicalDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (sv != null)
                {
                    sv.Offset = new Vector(0, 0);
                }
            }
        }

        private void OnSelectorPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _host.OnClickedSuggestion(sender, e);
                //OnCommit();
            }
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (IgnoringSelectionChanged)
            {
                return;
            }

            _host.OnNavigationSuggestion(e);
            //SelectionChanged?.Invoke(sender, e);
        }

        protected void SelectedIndexIncrement()
        {
            if (SelectorControl != null)
            {
                SelectorControl.SelectedIndex = SelectorControl.SelectedIndex + 1 >= SelectorControl.ItemCount ? -1 : SelectorControl.SelectedIndex + 1;
            }
        }

        protected void SelectedIndexDecrement()
        {
            if (SelectorControl != null)
            {
                int index = SelectorControl.SelectedIndex;
                if (index >= 0)
                {
                    SelectorControl.SelectedIndex--;
                }
                else if (index == -1)
                {
                    SelectorControl.SelectedIndex = SelectorControl.ItemCount - 1;
                }
            }
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    _host.OnKeyDown(this, e);
                    //OnCommit();
                    //_host.IsDropDownOpen = false;
                    e.Handled = true;
                    break;

                case Key.Up:
                    SelectedIndexDecrement();
                    e.Handled = true;
                    break;

                case Key.Down:
                    if ((e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.None)
                    {
                        SelectedIndexIncrement();
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    _host.OnKeyDown(this, e);
                    e.Handled = true;
                    break;
                //    _host.IsDropDownOpen = false;
                //    e.Handled = true;
                //    break;

                default:
                    break;
            }
        }

        protected virtual void OnCommit()
        {
            OnCommit(this, new RoutedEventArgs());
        }

        private void OnCommit(object? sender, RoutedEventArgs e)
        {
            Commit?.Invoke(sender, e);

            AfterAdapterAction();
        }

        protected virtual void OnCancel()
        {
            OnCancel(this, new RoutedEventArgs());
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Cancel?.Invoke(sender, e);

            AfterAdapterAction();
        }

        private void AfterAdapterAction()
        {
            IgnoringSelectionChanged = true;
            if (SelectorControl != null)
            {
                SelectorControl.SelectedItem = null;
                SelectorControl.SelectedIndex = -1;
            }
            IgnoringSelectionChanged = false;
        }
    }
}