using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatCore.ViewModels.Panels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatCore.Controllers;

public class LocalPresentController : IMediaPresentController, INotifyPropertyChanged
{
    private readonly MediaPresentVm _vm;
    private readonly IDeclaratives _declaratives;
    private readonly IViewModelResolver _viewModelResolver;
    private readonly ObservableCollection<MPButtonContext> _buttons;
    private object? _rightViewPanel;
    private FileInfoPanelVm? _panelVm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalPresentController(MediaPresentVm vm, LocalDir? workDir, IDeclaratives declaratives, IViewModelResolver viewModelResolver)
    {
        _vm = vm;
        _declaratives = declaratives;
        _viewModelResolver = viewModelResolver;

        // try open
        _buttons = new ObservableCollection<MPButtonContext> 
        {
            new MPButtonContext
            {
                KeyCombo = "Ctrl+S",
                Name = "Save",
                Command = new Cmd(ActionSave),
            },
            new MPButtonContext
            {
                KeyCombo = "Ctrl+Shift+S",
                Name = "Save&Meta",
                Command = new Cmd(ActionSaveAndMeta),
            },
            new MPButtonContext
            {
                KeyCombo = "I",
                Name = "File info",
                Command = new Cmd(ActionFileInfo),
            },
        };
        TopButtons = new(_buttons);

        if (workDir != null)
            Title = $"Local {workDir.DirPath}";
        else
            Title = "Local file";
    }

    public ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; private set; }
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

    public async Task ActionSave()
    {
        using var load = _vm.LoadingGlobal();

        _vm.View.Pause();
        var currentFile = _vm.CurrentFile;
        var mediaPlayer = _vm.View.MediaBase;
        var res = await _declaratives.SaveLocalFiles(_vm, [currentFile], addTags:null, broker:null, unlocker:_vm);
        if (res.IsFault)
        {
            await _vm.HandleError(res);
            return;
        }
    }

    private async Task ActionSaveAndMeta()
    {
        using var loading = _vm.LoadingGlobal();
        _vm.View.Pause();

        var currentFile = _vm.CurrentFile;
        var mediaPlayer = _vm.View.MediaBase;
        var res = await _declaratives.SaveLocalFilesWithPopup(_vm, [currentFile], null, unlocker: _vm);
        if (res.IsFault)
        {
            await _vm.HandleError(res);
            return;
        }
    }

    private void ActionFileInfo()
    {
        ShowHideFileInfo();
    }

    public void ShowHideFileInfo()
    {
        bool show = RightViewPanel == null;
        if (show)
        {
            if (_panelVm == null)
            {
                _panelVm = _viewModelResolver.Resolve(new FileInfoPanelVm.Key
                {
                    File = _vm.CurrentFile,
                });
            }
            else
            {
                _panelVm.File = _vm.CurrentFile;
            }

            var meta = _vm.View.GetMeta();
            _panelVm.ClearAndSetMeta(meta);
            RightViewPanel = _panelVm.View;
        }
        else
        {
            RightViewPanel = null;
        }
    }
    
    public void OnConnected()
    {
        _vm.OnFileLoading += _vm_OnFileLoading;
    }

    public void OnDisconnected()
    {
        _vm.OnFileLoading -= _vm_OnFileLoading;
    }

    public void OnMetaReceived(object? sender, FileMetaData[] e)
    {
        if (_panelVm == null)
            return;

        _panelVm.UseMeta(e);
    }

    private void _vm_OnFileLoading(object? sender, ISourceFile e)
    {
        if (_panelVm == null)
            return;

        _panelVm.ClearAndSetMeta(null);
    }
}