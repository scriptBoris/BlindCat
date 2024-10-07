using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;

namespace BlindCatCore.ExternalApi;

public interface IBlindCatApi
{
    Task<string?> ShowPromtRequest(string desciption, string placeholder = "", string initValue = "");
    Task ShowError(string textError);
    Task ShowError(AppResponse response);
    Task GoToAlbum(AlbumArgs args);
    Task<AppResponse<string>> GetHtml(string pageUrl, RequestOptions options, CancellationToken cancel);

    void BusyAppend(string text);
    void BusyAppendNewLine(string text);
    void BusySetBody(string text);

    internal LoadingStrDesc? BusyContext { get; set; }
    internal void Dispose();
}

internal class BlindCatApi : IBlindCatApi
{
    private readonly BaseVm _viewModel;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IPlugin _target;
    private readonly IHttpLauncher _httpLauncher;
    private bool _isDisposed;
    
    public BlindCatApi(IPlugin target, 
        BaseVm viewModel, 
        IViewPlatforms viewPlatforms,
        CancellationToken cancellationToken)
    {
        _viewModel = viewModel;
        _viewPlatforms = viewPlatforms;
        _target = target;
        _httpLauncher = new HttpLauncher();
    }

    public Task<AppResponse<string>> GetHtml(string pageUrl, RequestOptions options, CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _httpLauncher.GetHtml(pageUrl, options, cancel);
    }

    public async Task GoToAlbum(AlbumArgs args)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _viewModel.GoTo(new AlbumVm.Key
        {
            Title = $"{_target.Name} | {args.Subtitle}",
            Items = args.Items.ToArray(),
            Dir = args.Dir,
        });
    }

    public Task ShowError(string textError)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _viewModel.ShowError(textError);
    }

    public Task ShowError(AppResponse response)
    {
        if (response.IsCanceled)
            return Task.CompletedTask;

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _viewModel.HandleError(response);
    }

    public Task<string?> ShowPromtRequest(string desciption, string placeholder = "", string initValue = "")
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        string title = $"Plugin \"{_target.Name}\"";
        return _viewModel.ShowDialogPromt(title, desciption, placeholder: placeholder, initValue: initValue);
    }

    public void BusyAppend(string text)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_loadingStrDesc != null)
            _viewPlatforms.InvokeInMainThread(() =>
            {
                _loadingStrDesc.Body += text;
            });
    }

    public void BusyAppendNewLine(string text)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_loadingStrDesc != null)
            _viewPlatforms.InvokeInMainThread(() =>
            {
                _loadingStrDesc.Body += $"{text}\n";
            });
    }

    public void BusySetBody(string text)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_loadingStrDesc != null)
            _viewPlatforms.InvokeInMainThread(() =>
            {
                _loadingStrDesc.Body += text;
            });
    }

    private LoadingStrDesc? _loadingStrDesc;
    LoadingStrDesc? IBlindCatApi.BusyContext
    {
        get => _loadingStrDesc;
        set => _loadingStrDesc = value;
    }

    void IBlindCatApi.Dispose()
    {
        if (_isDisposed) 
            return;

        _isDisposed = true;
        _httpLauncher.Dispose(); 
    }
}