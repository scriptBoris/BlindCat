using BlindCatCore.Services;
using ScaffoldLib.Maui;

namespace BlindCatMauiMobile.Services;

public class NavigationService : INavigationService
{
    private readonly IScaffold _scaffold;
    
    public NavigationService()
    {
        _scaffold = new Scaffold();
        MainPage = new ContentPage
        {
            Content = (View)_scaffold,
        };
    }
    
    public object MainPage { get; private init; }
    
    public IReadOnlyList<object> Stack => _scaffold.NavigationStack;
    public object CurrentView => _scaffold.NavigationStack.Last();
    
    public void UseRootView(object view, bool animation)
    {
        // _scaffold.PopToRootAndSetRootAsync((View)view, animation);
        _scaffold.PushAsync((View)view, false);
    }

    public Task Push(object view, bool animation)
    {
        return _scaffold.PushAsync((View)view, animation);
    }

    public void Pop(bool animation)
    {
        _scaffold.PopAsync(animation);
    }

    public void Pop(object view, bool animation)
    {
        _scaffold.RemoveView((View)view, animation);
    }

    public Task PopAsync(object view, bool animation)
    {
        return _scaffold.RemoveView((View)view, animation);
    }

    public Task<object?> Popup(object view, object? viewFor)
    {
        throw new NotImplementedException();
    }

    public Task PopupClose(object view)
    {
        throw new NotImplementedException();
    }
}