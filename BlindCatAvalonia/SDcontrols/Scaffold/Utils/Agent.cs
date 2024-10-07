using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using BlindCatAvalonia.SDcontrols.Scaffold.Args;
using BlindCatCore.Core;
using BlindCatCore.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.SDcontrols.Scaffold.Utils;

[DebuggerDisplay($"Agent :: {{{nameof(ViewType)}}}")]
public class Agent : Panel
{
    private readonly Control _content;
    private readonly ScaffoldView _scaffoldView;
    private NavBar? _navBar;
    private LoadingLayout? overlayLoading;

    public Agent(Control content, AgentArgs args)
    {
        ViewType = content.GetType();
        args.Agent = this;
        _content = content;
        _scaffoldView = args.ScaffoldView;
        Children.Add(content);

        UpdateHasNavBar(args);
        Background = new SolidColorBrush(Color.FromRgb(33, 33, 33));

        if (content.DataContext is BaseVm vm)
        {
            vm.LoadingChanged += Vm_LoadingChanged;
        }
    }

    public Control View => _content;
    public Type ViewType { get; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double navBarHeight = 0;

        if (_navBar != null)
        {
            navBarHeight = _navBar.DesiredSize.Height;
            var navbarRect = new Rect(0, 0, finalSize.Width, navBarHeight);
            _navBar.Arrange(navbarRect);
        }

        // body
        double bodyY = navBarHeight;
        double bodyHeight = finalSize.Height - navBarHeight;

        if (Scaffolt.GetContentUnderNavigationBar(_content))
        {
            bodyY = 0;
            bodyHeight = finalSize.Height;
        }

        var contentRect = new Rect(0, bodyY, finalSize.Width, bodyHeight);
        _content.Arrange(contentRect);

        // loading
        overlayLoading?.Arrange(contentRect);

        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double freeHeight = availableSize.Height;
        double freeWidth = availableSize.Width;
        double navBarHeight = 0;

        if (_navBar != null)
        {
            _navBar.Measure(availableSize);
            navBarHeight = _navBar.DesiredSize.Height;
            freeHeight -= navBarHeight;
        }

        // body
        var bodySize = new Size(freeWidth, freeHeight);
        _content.Measure(bodySize);

        // loading
        overlayLoading?.Measure(bodySize);
        return availableSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
            return;

        if (this.GetVisualRoot() is Window w)
        {
            var p = e.GetCurrentPoint(w);
            if (p.Position.Y > NavBar.NAVBARHEIGHT)
                return;

            if (!p.Properties.IsLeftButtonPressed)
                return;

            //var m = w.InputHitTest(p.Position) as Control;
            //if (m != null && m != this && m != _navBar && m.IsHitTestVisible)
            //    return;

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
    }

    public void UpdateHasNavBar(AgentArgs args)
    {
        bool old = _navBar != null;
        bool nev = Scaffolt.GetHasNavigationBar(_content);
        if (old == nev)
            return;

        // show
        if (nev)
        {
            var ag = args ?? new AgentArgs
            {
                Agent = this,
                ScaffoldView = _scaffoldView,
                HideBackButton = false,
            };
            _navBar = new(_content, ag);
            Children.Insert(1, _navBar);
        }
        else
        {
            Children.Remove(_navBar!);
            _navBar = null;
        }
    }

    private void Vm_LoadingChanged(BaseVm vm, bool flag, LoadingStrDesc? tokenDesc)
    {
        string? token = tokenDesc?.Token;
        // ignore manual tokens
        if (vm.ManualLoadings.Contains(token))
            return;

        if (flag)
        {
            if (overlayLoading == null)
            {
                overlayLoading = new LoadingLayout();
                Children.Add(overlayLoading);
            }
        }

        if (overlayLoading != null)
        {
            if (flag)
            {
                overlayLoading.SetDesc(tokenDesc);
            }
            overlayLoading.IsVisible = flag;
        }
    }
}