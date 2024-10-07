using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using System.Collections.ObjectModel;

namespace BlindCatMaui.Controllers;

public class MediaPresentLocal : IMediaPresentController, IFileUnlocker
{
    private readonly MediaPresentVm _vm;
    private readonly MediaPresentVm.IPresentedView _view;
    private readonly LocalDir? _workDir;
    private readonly IDeclaratives _declaratives;
    private readonly IViewPlatforms _viewPlatforms;
    private CancellationTokenSource _cancellationTokenSource = new();
    private LocalFile _currentFile;
    private ObservableCollection<MPButtonContext> _buttons;

    public MediaPresentLocal(MediaPresentVm vm, LocalDir? workDir, LocalFile startFile, IDeclaratives declaratives, IViewPlatforms viewPlatforms)
    {
        _vm = vm;
        _view = (MediaPresentVm.IPresentedView)_vm.View;
        _workDir = workDir;
        _declaratives = declaratives;
        _viewPlatforms = viewPlatforms;
        _currentFile = startFile;

        // try open
        Launch(startFile);
        _buttons = new ObservableCollection<MPButtonContext> 
        {
            new MPButtonContext
            {
                Name = "Save",
                Command = new Cmd(Save),
            },
            new MPButtonContext
            {
                Name = "Save&Meta",
                Command = new Cmd(SaveAndMeta),
            },
        };
        TopButtons = new(_buttons);
    }

    private LocalFile CurrentFile
    {
        get => _currentFile;
        set
        {
            _currentFile = value;
        }
    }

    public string? FileName => CurrentFile?.FileName;
    public bool? IsIndexed => null;
    public object? CurrentMediaObject => CurrentFile;
    public ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; private set; }
    public object? RightViewPanel => null;

    public void Next()
    {
        var next = _workDir?.GetNext(CurrentFile);
        if (next == null)
            return;

        Stop();

        CurrentFile = next;
        Launch(CurrentFile);
    }

    public void Previous()
    {
        var prev = _workDir?.GetPrevious(CurrentFile);
        if (prev == null)
            return;

        Stop();

        CurrentFile = prev;
        Launch(CurrentFile);
    }

    public async Task Save()
    {
        using var load = _vm.Loading();

        _view.Pause();
        var currentFile = CurrentFile;
        var mediaPlayer = _view.MediaBase;
        var res = await _declaratives.SaveLocalFiles(_vm, [currentFile], null, unlocker:this);
        if (res.IsFault)
        {
            await _vm.HandleError(res);
            return;
        }

        await TryNext(currentFile, _vm);
        _vm.OnFileDeleted(currentFile);
    }

    private async Task SaveAndMeta()
    {
        using var loading = _vm.Loading();
        _view.Pause();

        var currentFile = CurrentFile;
        var mediaPlayer = _view.MediaBase;
        var res = await _declaratives.SaveLocalFilesWithPopup(_vm, [currentFile], null, unlocker:this);
        if (res.IsFault)
        {
            await _vm.HandleError(res);
            return;
        }

        await TryNext(currentFile, _vm);
        _vm.OnFileDeleted(currentFile);
    }

    private async Task TryNext(LocalFile currentFile, BaseVm _vm)
    {
        var next = _workDir?.GetNext(currentFile);
        if (next == null)
        {
            var prev = _workDir?.GetPrevious(currentFile);
            if (prev == null)
            {
                Stop();
                await _vm.Close();
                return;
            }
            next = prev;
        }

        Stop();
        await Task.Delay(400);
        CurrentFile = next;
        Launch(next);
    }

    public void ZoomMinus()
    {
        _view.ZoomMinus();
    }

    public void ZoomPlus()
    {

        _view.ZoomPlus();
    }

    private async void Launch(LocalFile file)
    {
        using var busy = _vm.Loading();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();

        var format = MediaPresentVm.ResolveFormat(file.FilePath);
        await _view.SetSource(file, format, _cancellationTokenSource.Token);
    }

    private void Stop()
    {
        _view.Stop();
    }

    public async Task<AppResponse> UnlockFile(string filePath)
    {
        if (filePath == CurrentFile.FilePath)
        {
            _view.Stop();
            await Task.Delay(50);
        }
        return AppResponse.OK;
    }
}