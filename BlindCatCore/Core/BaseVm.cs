using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;

namespace BlindCatCore.Core;

public delegate void LoadingChangedHandler(BaseVm invoker, LoadingToken token);

public interface IKey<T> where T : BaseVm
{
}

public abstract class BaseVm : BaseNotify
{
    private readonly ObservableCollection<BaseVm> _childrens = new();
    private readonly List<LoadingToken> _tokens = [];
    private readonly List<string> _manualLoadingHandlers = [];
    private int _loadingCounter = 0;
    private object? _view;

    public event LoadingChangedHandler? LoadingPushed;
    public event LoadingChangedHandler? LoadingPoped;

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

    public IDisposableNotify LoadingGlobal(string token = "default", string? description = null, CancellationTokenSource? cancel = null)
    {
        if (_view == null)
            throw new InvalidOperationException("View has not been initialized yet");

        _loadingCounter++;
        IsLoading = _loadingCounter > 0;

        var loading = new LoadingToken
        {
            Token = token,
            Title = description,
            Cancellation = cancel,
        };
        loading.Disposed += Loading_Disposed;
        ViewPlatforms.UseGlobalLoading(_view, loading);

        void Loading_Disposed(object? sender, EventArgs e)
        {
            loading.Disposed -= Loading_Disposed;
            _loadingCounter--;
            IsLoading = _loadingCounter > 0;
        }

        return loading;
    }

    public IDisposableNotify Loading(string token = "default")
    {
        return Loading(token, null, null);
    }

    public IDisposableNotify Loading(string token, string? description, CancellationTokenSource? cancel)
    {
        var loading = new LoadingToken
        {
            Token = token,
            Title = description,
            Cancellation = cancel,
        };

        if (_tokens.Contains(loading))
            throw new InvalidOperationException();

        _tokens.Add(loading);

        _loadingCounter++;
        IsLoading = _loadingCounter > 0;
        loading.Disposed += Loading_Disposed;

        void Loading_Disposed(object? sender, EventArgs e)
        {
            loading.Disposed -= Loading_Disposed;
            _loadingCounter--;
            IsLoading = _loadingCounter > 0;
            _tokens.Remove(loading);
            LoadingPoped?.Invoke(this, loading);
        }

        LoadingPushed?.Invoke(this, loading);

        return loading;
    }

    public LoadingToken? LoadingCheck(string token)
    {
        var match = _tokens.LastOrDefault(x => x.Token == token);
        if (match == null)
            return null;

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
}