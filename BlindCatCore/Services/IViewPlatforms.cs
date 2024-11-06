using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.ViewModels;

namespace BlindCatCore.Services;

public interface IViewPlatforms
{
    bool AppLoading { get; }
    IEnumerable<LoadingToken> CurrentLoadings { get; }
    object BuildView(Type? viewType, BaseVm baseVm);
    void InvokeInMainThread(Action act);

    Task ShowDialog(string title, string body, string OK, object? hostView);
    Task<bool> ShowDialog(string title, string body, string OK, string cancel, object? hostView);
    Task<string?> ShowDialogSheet(string title, string cancel, string[] items, object? hostView);
    Task<int?> ShowDialogSheetId(string title, string cancel, string[] items, object? hostView);
    Task<string?> ShowDialogPromt(string title, string message, string OK, string cancel, string placeholder, string initValue, object? hostView);
    Task<string?> ShowDialogPromtPassword(string title, string message, string OK, string cancel, string placeholder, object? hostView);
    Task<IFileResult?> SelectMediaFile(object? hostView);
    Task<string?> SelectDirectory(object? hostView);
    ITimerCore MakeTimer();
    void Destroy(object view);
    AppResponse ShowFileOnExplorer(string filePath);
    void UseGlobalLoading(object viewHost, IDisposableNotify token);
}

public interface IFileResult
{
    public string Path { get; }
    public Stream Stream { get; }
}