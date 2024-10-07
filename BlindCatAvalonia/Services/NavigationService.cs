using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.SDcontrols.Scaffold;
using BlindCatCore.Core;
using BlindCatCore.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Services;

public abstract class NavigationService : INavigationService
{
    private readonly ScaffoldView container;

    public NavigationService()
    {
        container = new();
    }

    public object MainPage => container;
    public IReadOnlyList<object> Stack => container.NavigationStack;
    public object CurrentView => Stack.Last();

    public void Pop(bool animation)
    {
        _ = container.PopAsync(animation);
    }

    public void Pop(object view, bool animation)
    {
        _ = container.PopAsync(animation);
    }

    public Task PopAsync(object view, bool animation)
    {
        return container.PopAsync(animation);
    }

    public async Task Push(object view, bool animation)
    {
        var v = (Control)view;
        await container.PushAsync(v, animation);

        var vm = (BaseVm)v.DataContext!;
        vm.OnConnectToNavigation();
    }

    public async void UseRootView(object view, bool animation)
    {
        var v = (Control)view;
        var vm = (BaseVm)v.DataContext!;
        await container.PopToRootAsync();
        await container.PushAsync(v, animation);

        //await v.AwaitLoading();
        vm.OnConnectToNavigation();
    }

    public abstract Task<object?> Popup(object view, object? viewFor);
    public abstract Task PopupClose(object view);
}