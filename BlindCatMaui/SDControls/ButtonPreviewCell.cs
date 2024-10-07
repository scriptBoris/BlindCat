using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatMaui.Core;
using BlindCatMaui.SDControls.Elements;
using ButtonSam.Maui.Core;
using System.Collections;
using System.Windows.Input;

namespace BlindCatMaui.SDControls;

[ContentProperty("Content")]
public class ButtonPreviewCell : Button, IDisposable
{
    private readonly Grid _zlayer = new();
    private readonly Label _label;
    private readonly CheckBox _checkbox;
    private readonly HorizontalStackLayout _selectionLayer;
    private readonly TagsLayout _tagsLayout;

    private static Color _accentColor => Color.FromRgba("#1f5aa2");
    private static bool _isAlt => App.IsAltPressed;

    public ButtonPreviewCell()
    {
        _selectionLayer = new();
        _zlayer.Add(_selectionLayer);

        // label
        _label = new Label
        {
            TextColor = Colors.White,
            Padding = new Thickness(3),
            VerticalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromRgba("#222"),
            Opacity = 0,
        };
        _label.SizeChanged += _label_SizeChanged;
        _zlayer.Add(_label);

        // tags
        _tagsLayout = new TagsLayout
        {
            Margin = new Thickness(3),
            Spacing = 3,
            VerticalOptions = LayoutOptions.End,
            Opacity = 0,
            ItemTemplate = TagTempate,
        };
        _zlayer.Add(_tagsLayout);

        // selection box
        _checkbox = new CheckBox
        {
            ForegroundTheme = AppTheme.Light,
            Opacity = 0.0,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Color = _accentColor,
        };
        _zlayer.Add(_checkbox);
        base.Content = _zlayer;
    }

    private void _label_SizeChanged(object? sender, EventArgs e)
    {
        _tagsLayout.TranslationY = -_label.Height;
    }

    private static DataTemplate TagTempate => new(() => new SimpleTag());

    #region bindable props
    // content
    public static new readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(View), 
        typeof(ButtonPreviewCell), 
        null,
        propertyChanged: (b,o,n) =>
        {
            if (b is ButtonPreviewCell self)
                self.UpdateContent(o as View, n as View);
        }
    );
    public new View? Content
    {
        get => GetValue(ContentProperty) as View;
        set => SetValue(ContentProperty, value);
    }

    // name
    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name),
        typeof(string),
        typeof(ButtonPreviewCell),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is ButtonPreviewCell self)
                self._label.Text = n as string;
        }
    );
    public string? Name
    {
        get => GetValue(NameProperty) as string;
        set => SetValue(NameProperty, value);
    }

    // tags
    public static readonly BindableProperty TagsProperty = BindableProperty.Create(
        nameof(Tags),
        typeof(IEnumerable<string>),
        typeof(ButtonPreviewCell),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is ButtonPreviewCell self)
            {
                var value = n as IList;
                self._tagsLayout.ItemsSource = value;
            }
        }
    );
    public IEnumerable<string>? Tags
    {
        get => GetValue(TagsProperty) as IEnumerable<string>;
        set => SetValue(TagsProperty, value);
    }

    // is selected
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected),
        typeof(bool),
        typeof(ButtonPreviewCell),
        false,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (b, o, n) =>
        {
            if (b is ButtonPreviewCell self)
            {
                bool isSelected = (bool)n;
                self._checkbox.IsChecked = isSelected;
                self.UpdateVisual(isSelected, self.IsMouseOver, _isAlt);
            }
        }
    );
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // selected command
    public static readonly BindableProperty SelectedChangedCommandProperty = BindableProperty.Create(
        nameof(SelectedChangedCommand),
        typeof(ICommand),
        typeof(ButtonPreviewCell),
        null
    );
    public ICommand? SelectedChangedCommand
    {
        get => GetValue(SelectedChangedCommandProperty) as ICommand;
        set => SetValue(SelectedChangedCommandProperty, value);
    }
    #endregion bindable props

    private void UpdateContent(View? viewOld, View? viewNew)
    {
        if (viewOld != null) 
            _zlayer.Remove(viewOld);

        if (viewNew != null)
            _zlayer.Insert(0, viewNew);
    }

    //protected override bool OnGesturePressed(InteractiveEventArgs args)
    //{
    //    if (args.DeviceInputType == DeviceInputTypes.Mouse && args.InputType == InputTypes.MouseMiddleButton)
    //        return false;

    //    if (!IsPressed)
    //    {
    //        bool isPressedToInteractive = this.HitTestToInteractive(new Point(args.X, args.Y));
    //        if (isPressedToInteractive)
    //            return false;
    //    }

    //    return IsEnabled;
    //}

    protected override void CallbackPressed(CallbackEventArgs args)
    {
#if WINDOWS
        if (App.IsCtrlPressed)
        {
            IsSelected = !IsSelected;
            SelectedChangedCommand?.Execute(this.BindingContext);
            return;
        }
#endif

        var point = new Point(args.X, args.Y);
        if (_checkbox.Frame.Contains(point))
        {
            IsSelected = !IsSelected;
            SelectedChangedCommand?.Execute(this.BindingContext);
            return;
        }
        base.CallbackPressed(args);
    }

    protected override void AnimationFrame(double x)
    {
        base.AnimationFrame(x);
        if (Content != null)
        {
            Content.Opacity = 1 - (x * 0.5);
        }
    }

    protected override void AnimationMouseOverRestore()
    {
        base.AnimationMouseOverRestore();
        UpdateVisual(IsSelected, false, _isAlt);
    }

    protected override void AnimationMouseOverStart()
    {
        base.AnimationMouseOverStart();
        UpdateVisual(IsSelected, true, _isAlt);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if WINDOWS
        if (Handler != null)
        {
            App.KeyAlt_Down += App_KeyAlt_Down;
            App.KeyAlt_Up += App_KeyAlt_Up;
        }
        else
        {
            App.KeyAlt_Down -= App_KeyAlt_Down;
            App.KeyAlt_Up -= App_KeyAlt_Up;
        }
#endif
    }

    private void App_KeyAlt_Up(object? sender, bool e)
    {
        UpdateVisual(IsSelected, IsMouseOver, false);
    }

    private void App_KeyAlt_Down(object? sender, bool e)
    {
        UpdateVisual(IsSelected, IsMouseOver, true);
    }

    private void UpdateVisual(bool isSelected, bool isMouseOver, bool isAltPress)
    {
        if (IsSelected)
        {
            base.Content!.Scale = 0.98;
            BackgroundColor = _accentColor;
            _selectionLayer.BackgroundColor = Colors.White.WithAlpha(0.4f);
        }
        else
        {
            base.Content!.Scale = 1;
            BackgroundColor = Colors.White;
            _selectionLayer.BackgroundColor = null;
        }

        if (IsSelected || IsMouseOver)
        {
            Content.Opacity = 0.5;
            _checkbox.Opacity = 1;
        }
        else
        {
            Content.Opacity = 1;
            _checkbox.Opacity = 0;
            _label.Opacity = 0;
        }

        if (isSelected || isMouseOver || isAltPress)
        {
            _tagsLayout.Opacity = 1;
            _label.Opacity = 1;
        }
        else
        {
            _tagsLayout.Opacity = 0;
            _label.Opacity = 0;
        }
    }

    public void Dispose()
    {
        Handler = null;
        App.KeyAlt_Down -= App_KeyAlt_Down;
        App.KeyAlt_Up -= App_KeyAlt_Up;
        _label.SizeChanged -= _label_SizeChanged;
        GC.SuppressFinalize(this);
    }
}