using BlindCatCore.Core;
using BlindCatCore.Services;
using BlindCatMaui.Core;
using Microsoft.Maui.Controls.Shapes;
using RGPopup.Maui.Services;
using ScaffoldLib.Maui;
using System.ComponentModel;

namespace BlindCatMaui.Services;

public class NavigationService : INavigationService
{
    private readonly Page _mainPage;
    private readonly Scaffold _mainScaffold;
    public NavigationService()
    {
        _mainScaffold = new Scaffold();
        _mainPage = new ContentPage
        {
            Content = _mainScaffold,
        };
    }

    public object MainPage => _mainPage;
    
    public IReadOnlyList<object> Stack => _mainScaffold.NavigationStack;

    public object CurrentView
    {
        get
        {
            var last = _mainScaffold.NavigationStack.Last();
            return last;
        }
    }

    public void Pop()
    {
        PopAsync();
    }

    public void Pop(object view)
    {
        PopAsync(view);
    }

    public Task PopAsync()
    {
        return _mainScaffold.PopAsync();
    }

    public Task PopAsync(object view)
    {
        var v = (View)view;
        var context = v.GetContext();
        return context!.RemoveView(v);
    }

    public async Task<object?> Popup(object view, object? viewFor)
    {
        var popup = new WrapperPopup((View)view);
        if (viewFor != null)
        {
            var v = (View)viewFor;
            v.GetContext()!.AddCustomLayer(popup, 333, v);
        }
        else
        {
            _mainScaffold.AddCustomLayer(popup, 333);
        }
        await popup.GetResult();
        return null;
    }

    public async Task PopupClose(object view)
    {
        var v = (View)view;
        var b = (LinkBehavior)v.Behaviors.First(x => x is LinkBehavior);
        var popup = b.Direct;
        popup.Close();
        await Task.Delay(250);
    }

    public async Task Push(object view)
    {
        var v = (View)view;
        await _mainScaffold.PushAsync(v);
        if (v.BindingContext is BaseVm vm)
        {
            vm.OnConnectToNavigation();
        }
    }

    public async void UseRootView(object view)
    {
        if (!_mainScaffold.IsLoaded)
        {
            var tsc = new TaskCompletionSource();
            _mainScaffold.Loaded += Loaded;
            await tsc.Task;
            _mainScaffold.Loaded -= Loaded;
            void Loaded(object? o, EventArgs e) { tsc.SetResult(); }
        }
        await _mainScaffold.PopToRootAsync();
        await Push(view);
    }
}