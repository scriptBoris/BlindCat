using System.Runtime.CompilerServices;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using IClipboard = BlindCatCore.Services.IClipboard;

namespace BlindCatMauiMobile.Services;

public class ViewPlatform : IViewPlatforms
{
    public bool AppLoading { get; private set; }
    public IClipboard Clipboard => throw new NotImplementedException();
    public IEnumerable<LoadingToken> CurrentLoadings { get; private set; } = new List<LoadingToken>();

    public object BuildView(Type? viewType, BaseVm baseVm)
    {
        if (viewType == null)
            return new Frame
            {
                Background = Colors.DarkRed,
                Content = new Label()
                {
                    Text = $"For VM {baseVm.GetType().Name} ViewType is NULL",
                    TextColor = Colors.White,
                },
            };

        try
        {
            var inst = Activator.CreateInstance(viewType)!;
            var view = (View)inst;
            view.BindingContext = baseVm;
            return view;
        }
        catch (Exception ex)
        {
            return new Frame
            {
                Background = Colors.DarkRed,
                Content = new Label
                {
                    Text = $"Fail create View for VM {baseVm.GetType().Name}\n" +
                           $"{ex.Message}",
                    TextColor = Colors.White,
                },
            };
        }
    }

    public void InvokeInMainThread(Action act)
    {
        App.Current.Dispatcher.Dispatch(act);
    }

    public Task ShowDialog(string title, string body, string OK, object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ShowDialog(string title, string body, string OK, string cancel, object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<string?> ShowDialogSheet(string title, string cancel, string[] items, object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<int?> ShowDialogSheetId(string title, string cancel, string[] items, object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<string?> ShowDialogPromt(string title, string message, string OK, string cancel, string placeholder,
        string initValue,
        object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<string?> ShowDialogPromtPassword(string title, string message, string OK, string cancel,
        string placeholder,
        object? hostView)
    {
        throw new NotImplementedException();
    }

    public async Task<IFileResult?> SelectMediaFile(object? hostView)
    {
        var res = await FilePicker.PickAsync();
        if (res == null)
            return null;

        var str = await res.OpenReadAsync();
        return new FileResultPick
        {
            Path = res.FullPath,
            Stream = str,
        };
    }

    public Task<string?> SelectDirectory(object? hostView)
    {
        throw new NotImplementedException();
    }

    public Task<string?> SaveTo(string? defaultFileName, string? defaultDirectory)
    {
        throw new NotImplementedException();
    }

    public ITimerCore MakeTimer()
    {
        throw new NotImplementedException();
    }

    public void Destroy(object view)
    {
    }

    public AppResponse ShowFileOnExplorer(string filePath)
    {
        throw new NotImplementedException();
    }

    public void UseGlobalLoading(object viewHost, IDisposableNotify token)
    {
        throw new NotImplementedException();
    }

    private class FileResultPick : IFileResult
    {
        public required string Path { get; set; }
        public required Stream Stream { get; set; }
    }
}