using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;

namespace BlindCatCore.Core;

public delegate void LoadingChangedHandler(BaseVm vm, bool flag, LoadingStrDesc? token);

public interface IKey<T> where T : BaseVm
{
}

public abstract class BaseVm : BaseNotify
{
    private readonly ObservableCollection<BaseVm> _childrens = new();
    private readonly List<LoadingStrDesc> _tokens = [];
    private readonly List<string> _manualLoadingHandlers = [];
    private int _loadingCounter = 0;
    private object? _view;

    public event LoadingChangedHandler? LoadingChanged;

    public required IViewPlatforms ViewPlatforms { get; set; }
    public required IViewModelResolver ViewModelResolver { get; set; }
    public required INavigationService NavigationService { get; set; }

    public BaseVm? Parent { get; set; }
    public IReadOnlyCollection<BaseVm> Children => _childrens;
    public bool IsPopup { get; set; }
    public string? Title { get; set; }
    public Type? ViewType { get; set; }
    public object View => BuildView(ViewType);
    public object? ViewWithoutBuilding => _view;
    public bool IsLoading { get; private set; }

    public virtual string[] ManualLoadings { get; } = [];

    public Task<BaseVm?> GoTo(object navKey, bool animation = true)
    {
        if (NavigationService == null)
            return Task.FromResult<BaseVm?>(null);

        var vm = ViewModelResolver?.Resolve(navKey);
        if (vm == null)
            return Task.FromResult<BaseVm?>(null);

        return NavigationService.Push(vm.View, animation).ContinueWith<BaseVm?>(x =>
        {
            return vm;
        });
    }

    public Task<T?> GoTo<T>(IKey<T> navKey, bool animation = true) where T : BaseVm
    {
        return this.GoTo(navKey as object).ContinueWith<T?>(x =>
        {
            var vm = x.Result as T;
            return vm;
        });
    }

    public Task Close(bool animation = true)
    {
        if (ViewWithoutBuilding == null)
            return Task.CompletedTask;

        Task task;
        if (IsPopup)
        {
            // OnDisconnectedFromNavigation вызывается в WrapperPoup.xaml.cs, в методе OnRemoved
            task = NavigationService.PopupClose(View);
        }
        else
        {
            task = NavigationService
                .PopAsync(ViewWithoutBuilding, animation)
                .ContinueWith(x =>
                {
                    ViewPlatforms.InvokeInMainThread(() =>
                    {
                        OnDisconnectedFromNavigation();
                    });
                });
        }

        return task;
    }

    public Task ShowMessage(string title, string message, string ok)
    {
        return ViewPlatforms.ShowDialog(title, message, ok, _view);
    }

    public Task<bool> ShowMessage(string title, string message, string ok, string cancel)
    {
        return ViewPlatforms.ShowDialog(title, message, ok, cancel, _view);
    }

    public Task<int?> ShowDialogSheet(string title, string cancel, params string[] items)
    {
        return ViewPlatforms.ShowDialogSheetId(title, cancel, items, _view);
    }

    public Task<string?> ShowDialogPromt(string title, string message, string ok = "OK", string cancel = "Cancel", string placeholder = "", string initValue = "")
    {
        return ViewPlatforms.ShowDialogPromt(title, message, ok, cancel, placeholder, initValue, _view);
    }

    public Task<string?> ShowDialogPromtPassword(string title, string message, string ok = "OK", string cancel = "Cancel", string placeholder = "")
    {
        return ViewPlatforms.ShowDialogPromtPassword(title, message, ok, cancel, placeholder, _view);
    }

    public Task ShowPopup(object popupNavKey)
    {
        if (NavigationService == null)
            return Task.CompletedTask;

        var vm = ViewModelResolver?.Resolve(popupNavKey);
        if (vm == null)
            return Task.CompletedTask;

        vm.IsPopup = true;
        vm.Parent = this;
        _childrens.Add(vm);
        return NavigationService.Popup(vm.View, View);
    }

    public Task<T> ShowPopup<T>(IKey<T> popupNavKey) where T : BaseVm
    {
        if (NavigationService == null)
            return Task.FromResult<T>(null!);

        var vm = ViewModelResolver?.Resolve(popupNavKey);
        if (vm == null)
            return Task.FromResult<T>(null!);

        vm.IsPopup = true;
        vm.Parent = this;
        _childrens.Add(vm);
        return NavigationService.Popup(vm.View, View).ContinueWith(t =>
        {
            return (T)vm;
        });
    }

    public Task ShowError(string message)
    {
        string title = "Error";
        string body = message;

        return ViewPlatforms.ShowDialog(title, body, "OK", _view);
    }

    public Task HandleError(AppResponse error)
    {
        if (error.IsCanceled)
            return Task.CompletedTask;

        string title = "Error";
        string body = error.Description;
        if (error.Exception != null)
            body += $"\n{error.Exception}";

        return ViewPlatforms.ShowDialog(title, body, "OK", _view);
    }

    public void InvokeInMainThread(Action act)
    {
        ViewPlatforms.InvokeInMainThread(act);
    }

    public virtual void OnConnectToNavigation()
    {
    }

    public virtual void OnDisconnectedFromNavigation()
    {
        if (Parent != null)
            Parent._childrens.Remove(this);

        for (int i = _childrens.Count - 1; i >= 0; i--)
            _childrens[i].OnDisconnectedFromNavigation();

        if (ViewWithoutBuilding != null)
            ViewPlatforms.Destroy(ViewWithoutBuilding);
    }

    public virtual void AppearingChanged(AppearingStates state)
    {
    }

    public virtual void SetResult_Backsroom(object? result)
    {
    }

    protected virtual object BuildView(Type? viewType)
    {
        _view ??= ViewPlatforms.BuildView(viewType, this);
        return _view;
    }

    public IDisposable Loading()
    {
        LoadingHandle(1, false);
        var str = new LoadingStr(() => LoadingHandle(-1, false));
        return str;
    }

    public LoadingStrDesc LoadingGlobal(string token = "default", string? description = null, CancellationTokenSource? cancel = null)
    {
        if (_view == null)
            throw new InvalidOperationException("View has not been initialized yet");

        return ViewPlatforms.UseGlobalLoading(_view, token, description, cancel);
    }

    public LoadingStrDesc Loading(string token)
    {
        return Loading(token, null, null);
    }

    public LoadingStrDesc Loading(string token, string? description, CancellationTokenSource? cancel)
    {
        var loading = new LoadingStrDesc
        {
            ActionDispose = (self) =>
            {
                _tokens.Remove(self);
                if (!_tokens.Contains(self))
                {
                    LoadingChanged?.Invoke(this, false, self);
                }
            },
            Token = token,
            Description = description,
            Cancellation = cancel,
        };
        bool isNew = !_tokens.Contains(loading);
        _tokens.Add(loading);

        if (isNew)
            LoadingChanged?.Invoke(this, true, loading);

        return loading;
    }

    private void LoadingHandle(int countAdd, bool isGlobal)
    {
        _loadingCounter += countAdd;
        IsLoading = _loadingCounter > 0;

        if (!isGlobal)
            LoadingChanged?.Invoke(this, IsLoading, null);
    }

    public LoadingStrDesc? LoadingCheck(string token)
    {
        var match = _tokens.LastOrDefault(x => x.Token == token);
        if (match == null)
            return null;

        int index = _tokens.IndexOf(match);
        match.IsVisible = index == _tokens.Count - 1;
        return match;
    }

    /// <summary>
    /// Like a "Ctrl+A"
    /// </summary>
    public virtual void OnKeyComboListener(KeyPressedArgs args)
    {
    }

    /// <summary>
    /// true - can close <br/>
    /// false - will stay
    /// </summary>
    public virtual Task<bool> TryClose()
    {
        return Task.FromResult(true);
    }

    private struct LoadingStr(Action act) : IDisposable
    {
        public void Dispose()
        {
            act();
        }
    }
}