using BlindCatCore.Controllers;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Xml.Linq;

namespace BlindCatCore.ViewModels;

public class MediaPresentVm : BaseVm, IFileUnlocker
{
    private readonly IStorageService _storageService;
    private readonly IDeclaratives _declaratives;
    private readonly IViewModelResolver _viewModelResolver;
    private readonly ICrypto _crypto;
    private readonly LocalDir? _localDir;
    private readonly StorageDir? _storageDir;
    private readonly ISourceDir? _sourceDir;
    private readonly ISourceFile _sourceFile;
    private CancellationTokenSource _cancellationTokenSource = new();

    public event EventHandler<ISourceFile>? OnFileLoading;
    public event EventHandler<ISourceFile>? OnFileLoaded;

    public class Key
    {
        public ISourceDir? SourceDir { get; set; }
        public IReadOnlyList<ISourceFile>? Album { get; set; }
        public required ISourceFile SourceFile { get; set; }
    }
    public MediaPresentVm(Key key, IStorageService storageService, IDeclaratives declaratives, IViewModelResolver viewModelResolver, ICrypto crypto)
    {
        _storageService = storageService;
        _declaratives = declaratives;
        _viewModelResolver = viewModelResolver;
        _crypto = crypto;
        _sourceDir = key.SourceDir;
        _sourceFile = key.SourceFile;
        Album = key.Album;
        CurrentFile = key.SourceFile;
        CommandNext = new Cmd(ActionNext);
        CommandPrevious = new Cmd(ActionPrevious);

        switch (_sourceFile)
        {
            case StorageFile sf:
                Controller = new StoragePresentController(this, _sourceDir as StorageDir, sf, _crypto, _storageService, _declaratives, _viewModelResolver);
                break;
            case LocalFile lf:
                Controller = new LocalPresentController(this, _sourceDir as LocalDir, _declaratives, _viewModelResolver);
                break;
            default:
                throw new NotImplementedException();
                //break;
        }

        TopButtons = Controller.TopButtons;
        IsIndexed = _sourceFile.TempStorageFile?.IsIndexed ?? false;
    }

    public const string playerLoading = "playerLoading";
    public new IPresentedView View => (IPresentedView)base.View;
    public IMediaPresentController Controller { get; init; }
    public ISourceFile CurrentFile { get; private set; }
    public bool? IsIndexed { get; private set; }
    public IReadOnlyList<ISourceFile>? Album { get; set; }
    public override string[] ManualLoadings { get; } = [playerLoading];
    public ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; private set; }
    public string? DirName => Controller.Title;

    #region commands
    public ICommand CommandNext { get; set; }
    private Task ActionNext()
    {
        return TryNext(CurrentFile, this);
    }

    public ICommand CommandPrevious { get; set; }
    public Task ActionPrevious()
    {
        return TryPrev(CurrentFile, this);
    }

    public ICommand CommandZoomPlus => new Cmd(() =>
    {
        ZoomPlus();
    });

    public ICommand CommandZoomMinus => new Cmd(() =>
    {
        ZoomMinus();
    });

    public ICommand CommandPlayPause => new Cmd(() =>
    {
        if (View.MediaBase is IMediaPlayer mp)
        {
            if (mp.State == MediaPlayerStates.Playing)
                mp.Pause();
            else if (mp.State == MediaPlayerStates.Pause)
                mp.Play();
        }
    });

    public ICommand CommandFileInfo => new Cmd(() =>
    {
        Controller.ShowHideFileInfo();
    });
    #endregion commands

    public override void OnConnectToNavigation()
    {
        base.OnConnectToNavigation();
        Launch(CurrentFile);

        if (_sourceDir != null)
        {
            _sourceDir.FileDeleting += _sourceDir_FileDeleting;
            _sourceDir.FileDeleted += OnFileDeleted;
        }

        Controller.OnConnected();
    }

    public override void OnDisconnectedFromNavigation()
    {
        base.OnDisconnectedFromNavigation();
        _cancellationTokenSource.Cancel();

        if (_sourceDir != null)
        {
            _sourceDir.FileDeleting -= _sourceDir_FileDeleting;
            _sourceDir.FileDeleted -= OnFileDeleted;
        }

        Controller.OnDisconnected();
    }

    public override void OnKeyComboListener(KeyPressedArgs args)
    {
        base.OnKeyComboListener(args);
        switch (args.Key)
        {
            case "Right":
                ActionNext();
                args.Handled = true;
                break;
            case "Left":
                ActionPrevious();
                args.Handled = true;
                break;
            case "WheelUp":
                View.ZoomPlus();
                args.Handled = true;
                break;
            case "WheelDown":
                View.ZoomMinus();
                args.Handled = true;
                break;
            default:
                break;
        }

        foreach (var item in Controller.TopButtons)
        {
            if (item.KeyCombo == args.Key)
            {
                item.Command.Execute(null);
                break;
            }
        }
    }

    private void OnFileDeleted(object? invoker, ISourceFile deleted)
    {
        Stop();
        Launch(CurrentFile);
    }

    private void _sourceDir_FileDeleting(object? sender, ISourceFile e)
    {
        var next = _sourceDir?.GetNext(e);
        if (next == null)
        {
            Close();
            return;
        }

        CurrentFile = next;
    }

    private async Task TryPrev(ISourceFile currentFile, BaseVm _vm)
    {
        var prev = _sourceDir?.GetPrevious(currentFile);
        if (prev == null)
        {
            var last = Album?.LastOrDefault();
            if (last == null)
            {
                Stop();
                await _vm.Close();
                return;
            }
            prev = last;
        }

        Stop();
        await Task.Delay(100);
        CurrentFile = prev;
        Launch(prev);
    }

    public async Task TryNext(ISourceFile currentFile, BaseVm _vm)
    {
        var next = _sourceDir?.GetNext(currentFile);
        if (next == null)
        {
            var first = Album?.FirstOrDefault();
            if (first == null)
            {
                Stop();
                await _vm.Close();
                return;
            }
            next = first;
        }

        Stop();
        CurrentFile = next;
        Launch(next);
    }

    public void ZoomMinus()
    {
        View.ZoomMinus();
    }

    public void ZoomPlus()
    {
        View.ZoomPlus();
    }

    private async void Launch(ISourceFile file)
    {
        using var busy = Loading(playerLoading);
        var cancel = RefreshCancelation();

        OnFileLoading?.Invoke(this, file);
        var format = ResolveFormatByExt(file.FileExtension);
        await View.SetSource(file, format, cancel);

        if (!cancel.IsCancellationRequested)
            OnFileLoaded?.Invoke(this, file);
    }

    public void Stop()
    {
        RefreshCancelation();
        View.Stop();
    }

    public static MediaFormats ResolveFormat(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ResolveFormatByExt(ext);
    }

    public static MediaFormats ResolveFormatByExt(string ext)
    {
        switch (ext)
        {
            case ".jpg":
            case ".jpeg":
                return MediaFormats.Jpeg;
            case ".png":
                return MediaFormats.Png;
            case ".webp":
                return MediaFormats.Webp;
            case ".gif":
                return MediaFormats.Gif;
            case ".mp4":
                return MediaFormats.Mp4;
            case ".mov":
                return MediaFormats.Mov;
            case ".webm":
                return MediaFormats.Webm;
            case ".avi":
                return MediaFormats.Avi;
            case ".mkv":
                return MediaFormats.Mkv;
            case ".flv":
                return MediaFormats.Flv;
            default:
                return MediaFormats.Unknown;
        }
    }

    public async Task<AppResponse> UnlockFile(string filePath)
    {
        if (filePath == CurrentFile.FilePath)
        {
            Stop();
            await Task.Delay(50);
        }
        return AppResponse.OK;
    }

    public CancellationToken RefreshCancelation()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();
        return _cancellationTokenSource.Token;
    }

    public void OnMetaReceived(object? sender, FileMetaData[] e)
    {
        Controller.OnMetaReceived(sender, e);
    }

    public interface IPresentedView : IDisposable
    {
        IMediaBase? MediaBase { get; }
        Task SetSource(object source, MediaFormats handler, CancellationToken cancel);
        void Pause();
        void Stop();
        void ZoomMinus();
        void ZoomPlus();
        FileMetaData[]? GetMeta();
    }

    public enum ModeType
    {
        Storage,
        Local,
        ISourceFile,
    }
}