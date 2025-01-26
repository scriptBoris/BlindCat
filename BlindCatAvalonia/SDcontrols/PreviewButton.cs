using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatCore.Core;

namespace BlindCatAvalonia.SDcontrols;

public class PreviewButton : Button, IErrorListener, ILoadingListener, IVirtualGridRecycle
{
    private readonly Grid2 _grid;
    private readonly CheckBox _check;
    private readonly ItemsControl _tags;
    private readonly TextBlock _name;
    private readonly TextBlock _loading;
    private TextBlock? _errorView;
    private Window? _window;

    private bool isPointerOver;
    private bool isPressed;
    private bool checkBoxIgnore;
    private bool checkBoxIgnoreData;

    private bool _isVirtualAttach;
    private bool _isVirtualDeattach;

    private static int instancesCount;

    public PreviewButton()
    {
        instancesCount++;
        Debug.WriteLine($"PreviewButton instances: {instancesCount}");

        // tags
        _tags = MakeTags();
        _tags.IsVisible = false;

        // check
        _check = new CheckBox
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        _check.IsCheckedChanged += _check_IsCheckedChanged;

        // name
        _name = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(new Color(140, 33, 33, 33)),
            Padding = new Thickness(5),
            Foreground = new SolidColorBrush(Colors.White),
            IsVisible = false,
            TextWrapping = TextWrapping.WrapWithOverflow,
        };

        // loading
        _loading = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Blue),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.WrapWithOverflow,
            Text = "Loading",
            IsVisible = false,
        };

        _grid = new Grid2()
        {
            ErrorListener = this,
            LoadingListener = this,
            Check = _check,
            Loading = _loading,
            NameLabel = _name,
            Tags = _tags,
        };
        _grid.Init();

        //Background = new SolidColorBrush(Colors.White);
        Padding = new Avalonia.Thickness(0);
        CornerRadius = new Avalonia.CornerRadius(0);
        Content = _grid;
        //base.Command = new Cmd(ActionPressed);
    }

    public bool IsVirtualAttach 
    {
        get => _isVirtualAttach;
        set
        {
            _isVirtualAttach = value;
            if (RecycleChildren != null)
                RecycleChildren.IsVirtualAttach = value;
        }
    }

    public bool IsVirtualDeattach
    {
        get => _isVirtualDeattach;
        set
        {
            _isVirtualDeattach = value;
            if (RecycleChildren != null)
                RecycleChildren.IsVirtualDeattach = value;
        }
    }

    #region bindable props
    // mirror
    public static readonly StyledProperty<Control?> MirrorProperty = AvaloniaProperty.Register<PreviewButton, Control?>(
        nameof(Mirror)
    );
    [Content]
    public Control? Mirror
    {
        get => GetValue(MirrorProperty);
        set => SetValue(MirrorProperty, value);
    }

    // recycle children
    public static readonly StyledProperty<IVirtualGridRecycle?> RecycleChildrenProperty = AvaloniaProperty.Register<PreviewButton, IVirtualGridRecycle?>(
        nameof(RecycleChildren)
    );
    public IVirtualGridRecycle? RecycleChildren
    {
        get => GetValue(RecycleChildrenProperty);
        set => SetValue(RecycleChildrenProperty, value);
    }

    // command
    //public static new readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<PreviewButton, ICommand?>(
    //    nameof(Command)
    //);
    //public new ICommand? Command
    //{
    //    get => GetValue(CommandProperty);
    //    set => SetValue(CommandProperty, value);
    //}

    // command parameter
    //public static new readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<PreviewButton, object?>(
    //    nameof(CommandParameter)
    //);
    //public new object? CommandParameter
    //{
    //    get => GetValue(CommandParameterProperty);
    //    set => SetValue(CommandParameterProperty, value);
    //}

    // selected changed command
    public static readonly StyledProperty<ICommand?> SelectedChangedCommandProperty = AvaloniaProperty.Register<PreviewButton, ICommand?>(
        nameof(SelectedChangedCommand)
    );
    public ICommand? SelectedChangedCommand
    {
        get => GetValue(SelectedChangedCommandProperty);
        set => SetValue(SelectedChangedCommandProperty, value);
    }

    // selected span command
    public static readonly StyledProperty<ICommand?> SelectedSpanCommandProperty = AvaloniaProperty.Register<PreviewButton, ICommand?>(
        nameof(SelectedSpanCommand)
    );
    public ICommand? SelectedSpanCommand
    {
        get => GetValue(SelectedSpanCommandProperty);
        set => SetValue(SelectedSpanCommandProperty, value);
    }

    // is selected
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<PreviewButton, bool>(
        nameof(IsSelected),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }


    // name
    public static readonly StyledProperty<string> FileNameProperty = AvaloniaProperty.Register<PreviewButton, string>(
        nameof(FileName)
    );
    public string FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    // tags
    public static readonly StyledProperty<string[]> TagsProperty = AvaloniaProperty.Register<PreviewButton, string[]>(
        nameof(Tags)
    );
    public string[] Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }
    #endregion bindable props

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        App.OnButtonCtrl += App_OnButtonCtrl;

        if (this.GetVisualRoot() is Window w)
        {
            _window = w;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        App.OnButtonCtrl -= App_OnButtonCtrl;
        if (_window != null)
        {
            _window = null;
        }
    }

    public void SetError(AppResponse? error)
    {
        _grid.ErrorView ??= new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Red),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.WrapWithOverflow,
            Margin = new Thickness(5),
        };

        _grid.ErrorView.IsVisible = error != null;
        _grid.ErrorView.Text = error?.Description;
        ToolTip.SetTip(_grid.ErrorView, error?.MessageForLog);
    }

    public void LoadingStart(bool flag)
    {
        _loading.IsVisible = flag;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MirrorProperty)
        {
            var nev = change.NewValue as Control;
            if (nev != null)
            {
                _grid.Mirror = nev;
            }
        }
        else if (change.Property == IsSelectedProperty)
        {
            if (checkBoxIgnore)
                return;

            checkBoxIgnoreData = true;
            _check.IsChecked = (bool)change.NewValue!;
            checkBoxIgnoreData = false;
            UpdateVisibleNameAndTags();
            UpdateContentTransparentcy();
        }
        else if (change.Property == TagsProperty)
        {
            var tags = change.NewValue as IList;
            _tags.ItemsSource = tags;
        }
        else if (change.Property == FileNameProperty)
        {
            _name.Text = change.NewValue as string;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed)
            return;

        isPressed = true;
        base.OnPointerPressed(e);
        UpdateContentTransparentcy();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased || !isPressed)
            return;

        isPressed = false;
        base.OnPointerReleased(e);
        UpdateContentTransparentcy();

        if (App.IsButtonCtrlPressed)
        {
            _check.IsChecked = !_check.IsChecked;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            SelectedSpanCommand?.Execute(CommandParameter);
        }
        else
        {
            Command?.Execute(CommandParameter);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        isPointerOver = true;
        base.OnPointerEntered(e);

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _check.IsChecked = true;
        }
        UpdateContentTransparentcy();
        UpdateVisibleNameAndTags();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        isPointerOver = false;
        base.OnPointerExited(e);
        UpdateContentTransparentcy();
        UpdateVisibleNameAndTags();
    }

    private void UpdateVisibleNameAndTags()
    {
        bool isVis = isPointerOver || App.IsButtonCtrlPressed || IsSelected;
        _tags.IsVisible = isVis;
        _name.IsVisible = isVis;
    }

    private void UpdateContentTransparentcy()
    {
        if (Mirror is not Control control)
            return;

        double mul = 0;

        if (isPressed)
            mul += 0.2;

        if (isPointerOver)
            mul += 0.2;

        if (IsSelected)
            mul += 0.4;

        control.Opacity = 1 - mul;
    }

    private void App_OnButtonCtrl(object? sender, bool e)
    {
        var root = this.GetVisualRoot();
        if (root is Window w && w != sender)
        {
            return;
        }
        UpdateVisibleNameAndTags();
    }

    private void _check_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (checkBoxIgnoreData)
            return;

        checkBoxIgnore = true;
        IsSelected = !IsSelected;
        checkBoxIgnore = false;

        SelectedChangedCommand?.Execute(DataContext);
        UpdateContentTransparentcy();
        UpdateVisibleNameAndTags();
    }

    private ItemsControl MakeTags()
    {
        var r = new ItemsControl
        {
            ItemsPanel = new Template<Panel?>(() => new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
            }),
            Margin = new Thickness(0, 0, 0, 3),
            ItemTemplate = new FuncDataTemplate<string>((x, _) =>
            {
                var txt = new TextBlock();
                var btn = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 99, 177)),
                    CornerRadius = new CornerRadius(2),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(5, 2),
                    Margin = new Thickness(3, 3, 0, 0),
                    Child = txt,
                };
                txt.Bind(TextBlock.TextProperty, new Binding("."));
                return btn;
            }),
        };
        return r;
    }

    //private class Grid2 : Grid, IErrorListener, ILoadingListener
    //{
    //    public required IErrorListener ErrorListener { get; set; }
    //    public required ILoadingListener LoadingListener { get; set; }

    //    public void LoadingStart(bool flag)
    //    {
    //        LoadingListener.LoadingStart(flag);
    //    }

    //    public void SetError(AppResponse? message)
    //    {
    //        ErrorListener.SetError(message);
    //    }
    //}

    private class Grid2 : Panel, IErrorListener, ILoadingListener
    {
        private Control? _mirror;
        private TextBlock? _errorView;

        public required TextBlock NameLabel { get; set; }
        public required CheckBox Check { get; set; }
        public required ItemsControl Tags { get; set; }
        public required TextBlock Loading { get; set; }
        public required IErrorListener ErrorListener { get; set; }
        public required ILoadingListener LoadingListener { get; set; }

        public void Init()
        {
            base.Children.Add(Check);
            base.Children.Add(NameLabel);
            base.Children.Add(Tags);
            base.Children.Add(Loading);
        }

        public TextBlock? ErrorView
        {
            get => _errorView;
            set
            {
                if (_errorView != value && _errorView != null)
                    base.Children.Remove(_errorView);

                _errorView = value;

                if (value != null)
                    base.Children.Add(value);
            }
        }

        public Control? Mirror
        {
            get => _mirror;
            set
            {
                if (_mirror != value && _mirror != null)
                    base.Children.Remove(_mirror);

                _mirror = value;

                if (value != null)
                {
                    base.Children.Insert(0, value);
                }
            }
        }

        [Obsolete("hiden", true)]
        public new List<Control>? Children { get; }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize.Width == 0 || finalSize.Height == 0)
                return finalSize;

            double labelHeight = NameLabel.DesiredSize.Height;
            double mainHeight = finalSize.Height - labelHeight;
            var rect1 = new Rect(0, 0, finalSize.Width, mainHeight);

            Check.Arrange(rect1);
            Tags.Arrange(rect1);

            var fill = new Rect(finalSize);
            Loading.Arrange(fill);
            ErrorView?.Arrange(fill);
            Mirror?.Arrange(fill);

            double labelYoffset = finalSize.Height - labelHeight;
            var rect2 = new Rect(0, labelYoffset, finalSize.Width, labelHeight);
            NameLabel.Arrange(rect2);

            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            NameLabel.Measure(availableSize);
            return availableSize;
        }

        public void LoadingStart(bool flag)
        {
            LoadingListener.LoadingStart(flag);
        }

        public void SetError(AppResponse? message)
        {
            ErrorListener.SetError(message);
        }
    }
}