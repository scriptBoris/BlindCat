using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatMaui.Panels;
using System.Collections.ObjectModel;

namespace BlindCatMaui.Controllers;

public class MediaPresentStorage : IMediaPresentController
{
    private readonly IMetaDataAnalyzer _metaDataAnalyzer;
    private readonly ICrypto _crypto;
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IDeclaratives _declaratives;
    private readonly MediaPresentVm _vm;
    private readonly MediaPresentVm.IPresentedView _view;
    private readonly StorageDir _storageCell;
    private CancellationTokenSource _cancellationTokenSource = new();
    private StorageFile _currentFile;
    private ObservableCollection<MPButtonContext> _buttonContexts;
    private bool _showMeta;
    private MetaPanel? _metapanel;
    private object? _rightViewPanel;

    public MediaPresentStorage(MediaPresentVm vm, StorageDir storageCell, StorageFile storageFile,
        IMetaDataAnalyzer metaDataAnalyzer,
        ICrypto crypto,
        IStorageService storageService,
        IViewPlatforms viewPlatforms,
        IDeclaratives declaratives)
    {
        _metaDataAnalyzer = metaDataAnalyzer;
        _crypto = crypto;
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        _declaratives = declaratives;
        _vm = vm;
        _view = (MediaPresentVm.IPresentedView)_vm.View;
        _storageCell = storageCell;

        _currentFile = storageFile;
        Launch(storageFile);
        _buttonContexts = new ObservableCollection<MPButtonContext>
        {
            new MPButtonContext
            {
                Name = "Meta",
                Command = new Cmd(ActionShowMeta),
            },
        };
        TopButtons = new(_buttonContexts);
    }

    private StorageFile CurrentFile
    {
        get => _currentFile;
        set
        {
            _currentFile = value;
        }
    }
    public string? FileName => CurrentFile?.Name ?? "_NO_NAME_";
    public bool? IsIndexed => CurrentFile?.IsIndexed;
    public object? CurrentMediaObject => CurrentFile;
    public ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; }
    public object? RightViewPanel
    {
        get => _rightViewPanel; 
        private set
        {
            _rightViewPanel = value;
        }
    }

    public void Next()
    {
        if (_storageCell.IsClose)
            throw new InvalidOperationException("У хранилища не проинициализирована коллекция");

        var next = _storageCell.Controller.GetNext(CurrentFile);
        if (next == null)
            return;

        Stop();
        CurrentFile = next;
        Launch(CurrentFile);
    }

    public void Previous()
    {
        if (_storageCell.Controller?.StorageFiles == null)
            throw new InvalidOperationException("У хранилища не проинициализирована коллекция");

        var prev = _storageCell.Controller.GetPrevious(CurrentFile);
        if (prev == null)
            return;

        Stop();
        CurrentFile = prev;
        Launch(CurrentFile);
    }

    public void ZoomMinus()
    {
        _view.ZoomMinus();
    }

    public void ZoomPlus()
    {
        _view.ZoomPlus();
    }

    private void Stop()
    {
        _view.Stop();
    }

    private async void Launch(StorageFile storageFile)
    {
        using var busy = _vm.Loading();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();

        if (!storageFile.Storage.IsOpen)
        {
            var res = await _declaratives.TryOpenStorage(storageFile.Storage);
            if (res.IsFault)
            {
                await _vm.HandleError(res);
                return;
            }
        }

        if (_showMeta)
            RefreshMetaPanel(storageFile);

        if (storageFile.CachedMediaFormat == MediaFormats.Unknown)
        {
            using var decode = await _crypto.DecryptFile(storageFile.FilePath, storageFile.Storage.Password, _cancellationTokenSource.Token);
            if (decode.IsFault)
            {
                await _vm.HandleError(decode);
                return;
            }

            var form = await _metaDataAnalyzer.GetFormat(decode.Result, _cancellationTokenSource.Token);
            if (form.IsFault)
            {
                await _vm.HandleError(form);
                return;
            }

            storageFile.CachedMediaFormat = form.Result;
        }

        await _view.SetSource(storageFile, storageFile.CachedMediaFormat, _cancellationTokenSource.Token);
    }

    private void ActionShowMeta()
    {
        _showMeta = !_showMeta;
        if (_showMeta)
        {
            RefreshMetaPanel(CurrentFile);
        }
        else
        {
            RightViewPanel = null;
        }
    }

    private void RefreshMetaPanel(StorageFile file)
    {
        // old
        //if (_metapanel?.BindingContext is StorageFileController old)
        //    old.PresentPropChanged -= Newest_PresentPropChanged;

        // newest
        var newest = new StorageFileController(_vm, file, _storageService, _viewPlatforms);
        //newest.PresentPropChanged += Newest_PresentPropChanged;

        // setup
        if (_metapanel == null)
        {
            _metapanel = new MetaPanel(newest, null);
        }
        else
        {
            _metapanel.ChangeController(newest);
        }

        RightViewPanel = _metapanel;
    }

    //private void Newest_PresentPropChanged(object? sender, MediaPresentControllerProps e)
    //{
    //    PropsChanged?.Invoke(this, e);
    //}
}