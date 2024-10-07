using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;

namespace BlindCatAvalonia.Views;

public partial class MainWindow : Window, IWindowBusy
{
    private int fadeCounter;
    private int loadingCounter;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var loadings = this.DI<IViewPlatforms>().CurrentLoadings;
        foreach (var loading in loadings)
            MakeLoading(loading);

        var panl = ChromeOverlayLayer.GetOverlayLayer(this);
        if (panl != null)
            FicjDisableTabStopForButtons(panl);
    }

    private void FicjDisableTabStopForButtons(Visual visual)
    {
        if (visual is Control c)
            c.IsTabStop = false;

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Button button)
                button.IsTabStop = false;

            FicjDisableTabStopForButtons(child);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            var c = Content as Control;
            var stl = (WindowState)change.NewValue;
            if (stl == WindowState.Maximized)
            {
                c.Margin = new Thickness(9, 8, 9, 8);
            }
            else
            {
                c.Margin = new Thickness(0);
            }
        }
    }

    protected override void ExtendClientAreaToDecorationsChanged(bool isExtended)
    {
        base.ExtendClientAreaToDecorationsChanged(isExtended);

    }

    public IDisposable MakeFade()
    {
        var str = new LoadingStr(() =>
        {
            fadeCounter--;
            UpdateLoadingAndFade();
        });
        fadeCounter++;
        UpdateLoadingAndFade();
        return str;
    }

    public void MakeLoading(LoadingStrDesc loading)
    {
        loadingCounter++;
        loading.Disposed += Loading_Disposed;
        absLoading.SetDesc(loading);
        UpdateLoadingAndFade();
    }

    private void Loading_Disposed(object? sender, EventArgs e)
    {
        var loading = (LoadingStrDesc)sender!;
        loadingCounter--;
        UpdateLoadingAndFade();
        loading.Disposed -= Loading_Disposed;
    }

    public void UseContent(object content)
    {
        if (content is not Control c)
            throw new InvalidCastException();

        contentContainer.Children.Insert(0, c);
    }

    private void UpdateLoadingAndFade()
    {
        absLoading.IsVisible = fadeCounter > 0 || loadingCounter > 0;
        absLoading.ShowLoadingAnimation = loadingCounter > 0;
    }

    private struct LoadingStr(Action act) : IDisposable
    {
        public void Dispose()
        {
            act();
        }
    }
}
