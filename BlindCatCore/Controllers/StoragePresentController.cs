using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatCore.ViewModels.Panels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatCore.Controllers;

/// <summary>
/// Контроллер для отображения StorageFile
/// </summary>
public class StoragePresentController : IMediaPresentController, INotifyPropertyChanged
{
    private readonly IStorageService _storageService;
    private readonly IDeclaratives _declaratives;
    private readonly IViewModelResolver _viewModelResolver;
    private readonly ICrypto _crypto;
    private readonly MediaPresentVm _vm;
    private readonly StorageDir? _storageCell;
    private CancellationTokenSource _cancellationTokenSource = new();
    private StorageFile _currentFile;
    private ObservableCollection<MPButtonContext> _buttonContexts;
    private bool _showMeta;
    private StorageFileInfoPanelVm? _metapanel;
    private object? _rightViewPanel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public StoragePresentController(MediaPresentVm vm, StorageDir? storageCell, StorageFile storageFile,
        ICrypto crypto,
        IStorageService storageService,
        IDeclaratives declaratives,
        IViewModelResolver viewModelResolver)
    {
        _storageService = storageService;
        _declaratives = declaratives;
        _viewModelResolver = viewModelResolver;
        _vm = vm;
        _crypto = crypto;
        _vm.PropertyChanged += _vm_PropertyChanged;
        _storageCell = storageCell;

        _currentFile = storageFile;
        _buttonContexts = new ObservableCollection<MPButtonContext>
        {
            new MPButtonContext
            {
                Name = "Meta",
                Command = new Cmd(ActionShowMeta),
            },
        };
        TopButtons = new(_buttonContexts);
        CommandEditMeta = new Cmd(ActionEditMeta);
        CommandChangeEncryption = new Cmd<EncryptionMethods>(ActionChangeEncryption);

        Title = $"Storage: {storageCell?.Name ?? "<NULL>" }";
    }

    #region props
    private StorageFile CurrentFile => (StorageFile)_vm.CurrentFile;
    public ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; }
    public object? RightViewPanel
    {
        get => _rightViewPanel;
        private set
        {
            _rightViewPanel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightViewPanel)));
        }
    }
    public string? Title { get; }
    #endregion props

    #region commands
    public ICommand CommandEditMeta { get; }
    private async Task ActionEditMeta()
    {
        await _vm.ShowPopup(new EditMetaVm.Key
        {
            File = CurrentFile,
        });

        RefreshMetaPanel(CurrentFile);
    }

    public ICommand CommandChangeEncryption { get; }
    private async Task ActionChangeEncryption(EncryptionMethods encryption)
    {
        _vm.Stop();
        var file = CurrentFile;
        var from = file.EncryptionMethod;
        string password = file.Storage.Password!;
        string path = file.FilePath;
        var storage = file.Storage;

        // преобразование в CENC видео
        if (encryption == EncryptionMethods.CENC)
        {
            using var busy = _vm.Loading("encrypting", "Change encrypting to CENC...", null);
            var res = await _crypto.EncryptFile(path, path, password, from, EncryptionMethods.CENC);
            if (res.IsFault)
            {
                await _vm.HandleError(res);
                return;
            }
            file.CachedMediaFormat = MediaFormats.Mp4;
            file.EncryptionMethod = EncryptionMethods.CENC;

            var saveRes = await _storageService.UpdateStorageFile(storage, file, password);
            if (saveRes.IsFault)
            {
                await _vm.HandleError(saveRes);
                return;
            }
        }

        RefreshMetaPanel(file);
    }

    private void ActionShowMeta()
    {
        ShowHideFileInfo();
    }
    #endregion commands

    private void _vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaPresentVm.CurrentFile))
        {
            var f = (StorageFile)_vm.CurrentFile;
            RefreshMetaPanel(f);
        }
    }

    private void RefreshMetaPanel(StorageFile file)
    {
        if (!_showMeta)
            return;

        _metapanel ??= _viewModelResolver.Resolve(new StorageFileInfoPanelVm.Key
        {
            CommandChangeEncryption = CommandChangeEncryption,
            File = file,
        });
        RightViewPanel = _metapanel.View;
    }

    public void ShowHideFileInfo()
    {
        _showMeta = !_showMeta;
        if (_showMeta)
        {
            var f = (StorageFile)_vm.CurrentFile;
            RefreshMetaPanel(f);
        }
        else
        {
            RightViewPanel = null;
        }
    }

    public void OnMetaReceived(object? sender, FileMetaData[] e)
    {
    }

    public void OnConnected()
    {
    }

    public void OnDisconnected()
    {
    }
}