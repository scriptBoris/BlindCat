﻿using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class AlbumVm : BaseVm
{
    private readonly List<ISourceFile> _selectedFiles = new();
    private readonly ISourceDir _dir;
    private readonly IDeclaratives _declaratives;
    private readonly IDataBaseService _dataBaseService;
    private readonly ObservableCollection<ISourceFile> _files;

    public class Key
    {
        public Key() { }
        public required string Title { get; set; }
        public required ISourceFile[] Items { get; set; }
        public required ISourceDir Dir { get; set; }
    }
    public AlbumVm(Key key, IDeclaratives declaratives, IDataBaseService dataBaseService)
    {
        _dir = key.Dir;
        _declaratives = declaratives;
        this._dataBaseService = dataBaseService;
        _files = new ObservableCollection<ISourceFile>(key.Items);
        Items = new(_files);
        Title = key.Title;
        CommandOpenItem = new Cmd<ISourceFile>(ActionOpenItem);
        CommandSelectedChanged = new Cmd<ISourceFile>(ActionSelectedChanged);
    }

    public bool ShowSelectionPanel { get; set; }
    public ReadOnlyObservableCollection<ISourceFile> Items { get; }
    public int SelectedFilesCount { get; set; }

    #region commands
    public ICommand CommandOpenItem { get; private set; }
    private Task ActionOpenItem(ISourceFile item)
    {
        return GoTo(new MediaPresentVm.Key
        {
            SourceFile = item,
            SourceDir = _dir,
        });
    }

    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(ISourceFile file)
    {
        if (file.IsSelected)
        {
            SelectedFilesCount++;
            _selectedFiles.Add(file);
        }
        else
        {
            SelectedFilesCount--;
            _selectedFiles.Remove(file);
        }

        ShowSelectionPanel = (SelectedFilesCount > 0);
    }

    public ICommand CommandClearSelection => new Cmd(() =>
    {
        SelectedFilesCount = 0;
        foreach (var item in _selectedFiles)
        {
            item.IsSelected = false;
        }
        _selectedFiles.Clear();
        ShowSelectionPanel = false;
    });

    public ICommand CommandSelectAll => new Cmd(() =>
    {
        var all = _dir.GetAllFiles();
        if (all.Count == 0)
            return;

        _selectedFiles.Clear();
        SelectedFilesCount = all.Count;
        _selectedFiles.AddRange(all);
        foreach (var item in _selectedFiles)
        {
            item.IsSelected = true;
        }
        ShowSelectionPanel = true;
    });

    public ICommand CommandSaveSelectedItems => new Cmd(async () =>
    {
        if (_selectedFiles.Count == 0)
            return;

        using var loading = Loading();
        var files = _selectedFiles.ToArray();
        var broker = new ProgressBroker<ISourceFile>((progress, item) =>
        {
            if (item is LocalFile local)
            {
                InvokeInMainThread(() =>
                {
                    _selectedFiles.Remove(local);
                    _dir.Remove(local);
                    SelectedFilesCount = _selectedFiles.Count;
                    ShowSelectionPanel = _selectedFiles.Count > 0;
                });
            }
        });
        var res = await _declaratives.SaveLocalFilesWithPopup(this, files, broker, null);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        if (files.Length > 5)
            await ShowMessage("Success", $"All of {files.Length} files was move to secure storage", "OK");
    });

    public ICommand CommandAddTags => new Cmd(async () =>
    {
        using var loading = Loading();
        var select = await _declaratives.DeclarativeSelectStorage(this, autoInit: true);
        if (select.IsFault)
        {
            await HandleError(select);
            return;
        }

        var storage = select.Result;
        DirPresentVm.UpdateSelectedFiles(storage, _selectedFiles);

        var selectedFiles = _selectedFiles.ToArray();
        var alreadyTags = await DirPresentVm.FindAlreadyTags(selectedFiles);

        await ShowPopup(new EditTagsVm.Key
        {
            SelectedFiles = selectedFiles,
            StorageDir = storage,
            AlreadyTags = alreadyTags,
        });
    });

    public ICommand CommandRemoveTags => new Cmd(async () =>
    {
        using var loading = Loading();
        var select = await _declaratives.DeclarativeSelectStorage(this, autoInit: true);
        if (select.IsFault)
        {
            await HandleError(select);
            return;
        }

        var storage = select.Result;
        DirPresentVm.UpdateSelectedFiles(storage, _selectedFiles);

        var selectedFiles = _selectedFiles.ToArray();
        var alreadyTags = await DirPresentVm.FindAlreadyTags(selectedFiles);
        await ShowPopup(new RemoveTagsVm.Key
        {
            SelectedFiles = _selectedFiles.ToArray(),
            AlreadyTags = alreadyTags,
            StorageDir = storage,
        });
    });

    public ICommand CommandDeleteAlbum => new Cmd(async () =>
    {
        if (_dir is not StorageAlbum album)
        {
            await ShowError("Not available, only for storage Albums");
            return;
        }

        int? res = await ShowDialogSheet("Deletion", "Cancel", "Delete Album only", "Delete Album and contain Files");
        if (res == null)
            return;

        var storage = (StorageDir)album.SourceDir;
        string pathDb = storage.PathIndex;
        string? password = storage.Password;
        if (password == null)
            return;

        if (res == 0)
        {
            using var loading = Loading();
            foreach (StorageFile file in Items)
            {
                file.ParentAlbumGuid = null;
                await _dataBaseService.UpdateContent(pathDb, password, file);
            }
            await _dataBaseService.DeleteAlbum(pathDb, password, album);

            storage.Controller.MakeAlbumDeleted(album, Items, false);
            await Task.Delay(1200);
            await Close();
        }
        else if (res == 1)
        {
            // todo реализовать удаление файлов и альбома
        }
    });
    #endregion commands
}