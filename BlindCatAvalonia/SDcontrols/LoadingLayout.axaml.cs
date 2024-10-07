using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using BlindCatAvalonia.SDcontrols.Scaffold.Utils;
using BlindCatCore.Core;
using BlindCatCore.Models;
using System;
using System.Linq;

namespace BlindCatAvalonia.SDcontrols;

public partial class LoadingLayout : UserControl
{
    private static readonly LoadingStrDesc Default = new LoadingStrDesc()
    {
        ActionDispose = null,
        Token = "default",
        Body = null,
        Description = null,
        Cancellation = new(),
    };

    public LoadingLayout()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
            DataContext = Default;
    }

    // IsVisible
    //public static new readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<LoadingLayout, bool>(
    //    nameof(IsVisible)
    //);
    //public new bool IsVisible
    //{
    //    get => GetValue(IsVisibleProperty);
    //    set => SetValue(IsVisibleProperty, value);
    //}

    // Subscribe for
    public static readonly StyledProperty<string> SubscribeForProperty = AvaloniaProperty.Register<LoadingLayout, string>(
        nameof(SubscribeFor)
    );
    public string SubscribeFor
    {
        get => GetValue(SubscribeForProperty);
        set => SetValue(SubscribeForProperty, value);
    }

    // Show loading animation
    public static readonly StyledProperty<bool> ShowLoadingAnimationProperty = AvaloniaProperty.Register<LoadingLayout, bool>(
        nameof(ShowLoadingAnimation),
        true
    );
    public bool ShowLoadingAnimation
    {
        get => GetValue(ShowLoadingAnimationProperty);
        set => SetValue(ShowLoadingAnimationProperty, value);
    }

    // Use dragable ability
    public static readonly StyledProperty<bool> UseDragableAbilityProperty = AvaloniaProperty.Register<LoadingLayout, bool>(
        nameof(UseDragableAbility),
        false
    );
    public bool UseDragableAbility
    {
        get => GetValue(ShowLoadingAnimationProperty);
        set => SetValue(ShowLoadingAnimationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SubscribeForProperty)
        {
            if (DataContext is BaseVm vm)
            {
            }
        }
        else if (change.Property == ShowLoadingAnimationProperty)
        {
            borderAnimation.IsVisible = (bool)change.NewValue!;
        }
        else if (change.Property == IsVisibleProperty)
        {
            IsHitTestVisible = (bool)change.NewValue!;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!UseDragableAbility || this.GetVisualRoot() is not Window w)
            return;

        var p = e.GetCurrentPoint(w);
        if (p.Position.Y > NavBar.NAVBARHEIGHT)
            return;

        if (e.ClickCount == 2)
        {
            switch (w.WindowState)
            {
                case WindowState.FullScreen:
                case WindowState.Maximized:
                    w.WindowState = WindowState.Normal;
                    break;
                default:
                    w.WindowState = WindowState.Maximized;
                    break;
            }
            return;
        }

        w.BeginMoveDrag(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is BaseVm vm && SubscribeFor != null)
        {
            var matchToken = vm.LoadingCheck(SubscribeFor);
            if (matchToken != null)
            {
                bool flag = matchToken.IsVisible;
                UpdateSubscriberFor(vm, flag, matchToken);
            }
            vm.LoadingChanged += UpdateSubscriberFor;
        }
    }

    //private void Vm_LoadingChanged(BaseVm vm, bool flag, LoadingStrDesc? token)
    //{
    //    if (token == SubscribeFor)
    //    {
    //        IsVisible = flag;
    //        UpdateSubscriberFor(vm, flag, );
    //    }
    //}

    private void UpdateSubscriberFor(BaseVm vm, bool flag, LoadingStrDesc? tokenDesc)
    {
        string? token = tokenDesc?.Token;
        if (token == SubscribeFor)
        {
            SetDesc(tokenDesc);
            IsVisible = flag;
        }
    }

    public void SetDesc(LoadingStrDesc? desc)
    {
        DataContext = desc ?? Default;
    }
}