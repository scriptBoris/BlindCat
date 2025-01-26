using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using BlindCatAvalonia.SDcontrols.Scaffold.Utils;
using BlindCatCore.Core;
using BlindCatCore.Models;

namespace BlindCatAvalonia.SDcontrols;

public partial class LoadingLayout : UserControl
{
    private object? _oldDataContext;
    private List<LoadingToken> _stack = [];

    public LoadingLayout()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
            DataContext = new LoadingToken()
            {
                Token = "default",
                Description = null,
                Title = null,
                Cancellation = new(),
            };
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

    #region bindable props
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
    #endregion bindable props

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

        // old
        if (_oldDataContext is LoadingToken oldSrc)
        {
            oldSrc.PropertyChanged -= LoadingStrDescPropChanged;
        }
        else if (_oldDataContext is BaseVm oldVm)
        {
            oldVm.LoadingPushed -= OnPushedLoadingToken;
            oldVm.LoadingPoped -= OnPopedLoadingToken;
        }

        _stack.Clear();

        // new
        if (DataContext is LoadingToken newSrc)
        {
            newSrc.PropertyChanged += LoadingStrDescPropChanged;
        }
        else if (DataContext is BaseVm vm && SubscribeFor != null)
        {
            var matchToken = vm.LoadingCheck(SubscribeFor);
            if (matchToken != null)
            {
                PushToken(matchToken);
            }
            vm.LoadingPushed += OnPushedLoadingToken;
            vm.LoadingPoped += OnPopedLoadingToken;
        }

        var current = _stack.LastOrDefault();
        UpdateLabelTitle(current);
        _oldDataContext = DataContext;
    }

    private void LoadingStrDescPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        var token = (LoadingToken)sender!;

        switch (token.Token)
        {
            case nameof(LoadingToken.Title):
                UpdateLabelTitle(token);
                break;
            // todo ����������� description ��� LoadingLayout
            //case nameof(LoadingToken.Description):
            //    UpdateLabelTitle(token);
            //    break;
            default:
                break;
        }
    }

    private void UpdateLabelTitle(LoadingToken? src)
    {
        if (src != null)
        {
            labelTitle.Text = src.Title;
            labelTitle.IsVisible = !string.IsNullOrEmpty(src.Title);
        }
        else
        {
            labelTitle.Text = "";
            labelTitle.IsVisible = false;
        }
    }

    private void OnPushedLoadingToken(BaseVm invoker, LoadingToken tokenDesc)
    {
        string token = tokenDesc.Token;
        if (token == SubscribeFor)
        {
            PushToken(tokenDesc);
        }
    }

    private void OnPopedLoadingToken(BaseVm invoker, LoadingToken tokenDesc)
    {
        string token = tokenDesc.Token;
        if (token == SubscribeFor)
        {
            PopToken(tokenDesc);
        }
    }

    public void PushToken(LoadingToken desc)
    {
        _stack.Add(desc);
        IsVisible = true;
        UpdateLabelTitle(desc);
    }

    public void PopToken(LoadingToken token)
    {
        _stack.Remove(token);
        var current = _stack.LastOrDefault();
        IsVisible = current != null;
        UpdateLabelTitle(current);
    }
}