namespace BlindCatCore.Services;

public interface INavigationService
{
    object MainPage { get; }
    IReadOnlyList<object> Stack { get; }
    object CurrentView { get; }

    void UseRootView(object view, bool animation);
    Task Push(object view, bool animation);
    void Pop(bool animation);
    void Pop(object view, bool animation);
    Task PopAsync(object view, bool animation);

    Task<object?> Popup(object view, object? viewFor);
    Task PopupClose(object view);
}