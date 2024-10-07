using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Tools;

public class DesignNavigationService : INavigationService
{
    public object MainPage => null!;

    public IReadOnlyList<object> Stack => null!;

    public object CurrentView => null!;

    public void Pop(bool animation)
    {
    }

    public void Pop(object view, bool animation)
    {
    }

    public Task PopAsync(object view, bool animation)
    {
        return Task.CompletedTask;
    }

    public Task<object?> Popup(object view, object? viewFor)
    {
        throw new NotImplementedException();
    }

    public Task PopupClose(object view)
    {
        return Task.CompletedTask;
    }

    public Task Push(object view, bool animation)
    {
        return Task.CompletedTask;
    }

    public void UseRootView(object view, bool animation)
    {
    }
}