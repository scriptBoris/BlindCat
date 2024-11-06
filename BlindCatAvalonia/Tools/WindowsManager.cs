using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using BlindCatCore.Core;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Views.PopupsDesktop;

namespace BlindCatAvalonia.Tools;

public static class WindowsManager
{
    public static async Task<object> MakeWindow(object view, object? hostView)
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime classic)
            throw new NotSupportedException();

        Window parentWindow;
        IDisposable? token = null;

        if (hostView is Control hv)
        {
            var root = hv.GetVisualRoot()!;
            parentWindow = (Window)root;
        }
        else
        {
            parentWindow = classic.MainWindow!;
        }

        if (parentWindow is IWindowBusy busy)
            token = busy.MakeFade();

        try
        {
            var v = (Control)view;
            var vm = (BaseVm)v.DataContext!;
            var popupWindow = new BasePopupWindow(v, vm);
            popupWindow.DataContext = vm;

            App.Handle(popupWindow);
            await popupWindow.ShowDialog(parentWindow);
            App.Dishandle(popupWindow);

            vm.OnDisconnectedFromNavigation();
            return vm;
        }
        finally
        {
            token?.Dispose();
        }
    }

    public static async Task MakeWindow(Window newWindow, object? hostView)
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime classic)
            throw new NotSupportedException();

        Window parentWindow;
        IDisposable? token = null;

        if (hostView is Control hv)
        {
            var root = hv.GetVisualRoot()!;
            parentWindow = (Window)root;
        }
        else
        {
            parentWindow = classic.MainWindow!;
        }

        if (parentWindow is IWindowBusy busy)
        {
            token = busy.MakeFade();
        }

        try
        {
            await newWindow.ShowDialog(parentWindow);
        }
        finally
        {
            token?.Dispose();
        }
    }

    public static async Task<T> HandleWindow<T>(object? hostView, Func<Window, Task<object?>> handler)
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime classic)
            throw new NotSupportedException();

        Window parentWindow;
        IDisposable? token = null;

        if (hostView is Control hv)
        {
            var root = hv.GetVisualRoot()!;
            parentWindow = (Window)root;
        }
        else
        {
            parentWindow = classic.MainWindow!;
        }

        if (parentWindow is IWindowBusy busy)
            token = busy.MakeFade();

        try
        {

            var result = await handler(parentWindow);
            if (result is null)
            {
                return default!;
            }
            else
            {
                return (T)result;
            }
        }
        finally
        {
            token?.Dispose();
        }
    }
}