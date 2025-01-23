using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using PropertyChanged;
using ValidatorSam;

namespace BlindCatCore.ViewModels;

public class StorageAlbumVm : BaseVm
{
    private readonly IDataBaseService _dataBaseService;
    private readonly StorageDir _storage;
    private readonly StorageAlbum _album;
    private readonly List<StorageAlbumItem> _selectedItems = new();
    private readonly List<StorageAlbumItem> _initializedFiles;

    private ObservableCollection<StorageAlbumItem>? _items;
    private SortingAlbumItems selectedSortingItem;

    public class Key
    {
        public Key() { }
        public required StorageAlbum Album { get; set; }
        public required StorageDir Storage { get; set; }
    }
    public StorageAlbumVm(Key key, IDataBaseService dataBaseService)
    {
        _album = key.Album;
        _storage = key.Storage;
        _dataBaseService = dataBaseService;

        var allContentFiles = _storage
            .Controller?
            .StorageContentFiles;

        if (allContentFiles != null)
        {
            var items = new List<StorageAlbumItem>();
            foreach (StorageFile item in allContentFiles)
            {
                if (item.ParentAlbumGuid == _album.Guid)
                    items.Add(new StorageAlbumItem
                    {
                        StorageFile = item,
                        IsCover = item.Guid == _album.CoverGuid,
                    });
            }

            _initializedFiles = items;
        }
        else
        {
            _initializedFiles = [];
        }

        MakeItems(SelectedSortingItem);
        AlbumName = _album.Name;
        StorageName = _storage.Name;
        CommandOpenItem = new Cmd<StorageAlbumItem>(ActionOpenItem);
        CommandSelectedChanged = new Cmd<StorageAlbumItem>(ActionSelectedChanged);
        CommandRemoveFromAlbum = new Cmd<StorageAlbumItem>(ActionRemoveFromAlbum);
        CommandSelectionSpan = new Cmd<StorageAlbumItem>(ActionSelectionSpan);
        CommandSetCover = new Cmd<StorageAlbumItem>(ActionSetCover);
    }

    [DependsOn(nameof(ShowSelectionPanel))]
    public bool ShowCustomNavBar => ShowSelectionPanel;
    public bool ShowSelectionPanel { get; set; }
    public bool ShowEditPanel { get; set; }
    public ReadOnlyObservableCollection<StorageAlbumItem> Items { get; private set; }
    public int SelectedFilesCount { get; set; }

    public string AlbumName { get; private set; }
    public string StorageName { get; private set; }

    public FormEdit? Form { get; private set; }
    public SortingAlbumItems SelectedSortingItem
    {
        get => selectedSortingItem;
        set
        {
            selectedSortingItem = value;
            MakeItems(value);
        }
    }

    #region commands
    public ICommand CommandOpenItem { get; private set; }
    private Task ActionOpenItem(StorageAlbumItem item)
    {
        return GoTo(new MediaPresentVm.Key
        {
            SourceFile = item.StorageFile,
            SourceDir = _album,
        });
    }

    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(StorageAlbumItem item)
    {
        if (item.StorageFile.IsSelected)
        {
            SelectedFilesCount++;
            _selectedItems.Add(item);
        }
        else
        {
            SelectedFilesCount--;
            _selectedItems.Remove(item);
        }

        ShowSelectionPanel = (SelectedFilesCount > 0);
    }

    public ICommand CommandSelectionSpan { get; init; }
    private void ActionSelectionSpan(StorageAlbumItem file)
    {
        if (!file.StorageFile.IsSelected)
        {
            file.StorageFile.IsSelected = true;
            _selectedItems.Add(file);
        }

        int min = Items.Count;
        int max = -1;
        foreach (var item in _selectedItems)
        {
            int index = Items.IndexOf(item);
            if (index < min)
                min = index;

            if (index > max)
                max = index;
        }

        for (int i = min; i <= max; i++)
        {
            var item = Items[i];
            if (!item.StorageFile.IsSelected)
            {
                item.StorageFile.IsSelected = true;
                _selectedItems.Add(item);
            }
        }

        SelectedFilesCount = 1 + max - min;
        ShowSelectionPanel = (SelectedFilesCount > 0);
    }

    public ICommand CommandClearSelection => new Cmd(() =>
    {
        SelectedFilesCount = 0;
        foreach (var item in _selectedItems)
        {
            item.StorageFile.IsSelected = false;
        }
        _selectedItems.Clear();
        ShowSelectionPanel = false;
    });

    public ICommand CommandSelectAll => new Cmd(() =>
    {
        _selectedItems.Clear();
        _selectedItems.AddRange(_initializedFiles);
        SelectedFilesCount = _selectedItems.Count;

        foreach (var item in _selectedItems)
        {
            item.StorageFile.IsSelected = true;
        }
        ShowSelectionPanel = true;
    });

    public ICommand CommandShowEditPanel => new Cmd(() =>
    {
        ShowEditPanel = !ShowEditPanel;
        if (ShowEditPanel)
        {
            Form = new FormEdit(_album);
        }
    });

    public ICommand CommandCloseCustomNavBar => new Cmd(() =>
    {
        foreach (var selectedFile in _selectedItems)
            selectedFile.StorageFile.IsSelected = false;
        _selectedItems.Clear();
        SelectedFilesCount = 0;
        ShowSelectionPanel = false;
    });

    public ICommand CommandRemoveFromAlbum { get; private set; }
    private async Task ActionRemoveFromAlbum(StorageAlbumItem item)
    {
        var storage = _storage;
        string pathDb = storage.PathIndex;
        string? password = storage.Password;
        if (password == null)
            return;

        if (!item.StorageFile.IsSelected)
            _selectedItems.Add(item);

        using var loading = Loading();
        var files = _selectedItems.Select(x => x.StorageFile);
        bool remakeCover = false;

        for (int i = _selectedItems.Count - 1; i >= 0; i--)
        {
            var selectedItem = _selectedItems[i];
            selectedItem.StorageFile.ParentAlbumGuid = null;
            await _dataBaseService.UpdateContent(pathDb, password, selectedItem.StorageFile);
            
            _initializedFiles.Remove(selectedItem);
            _selectedItems.Remove(selectedItem);

            if (_album.CoverGuid == selectedItem.StorageFile.Guid)
                remakeCover = true;
        }

        storage.Controller?.MakeAlbumRemove(_album, files);
        MakeItems(SelectedSortingItem);
        
        if (remakeCover)
        {
            _album.FilePreview = Items.FirstOrDefault()?.StorageFile.FilePreview;
        }

        SelectedFilesCount = 0;
        ShowSelectionPanel = false;
    }

    public ICommand CommandDeleteAlbum => new Cmd(async () =>
    {
        int? res = await ShowDialogSheet("Deletion", "Cancel", "Delete Album only", "Delete Album and contain Files");
        if (res == null)
            return;

        var storage = _storage;
        string pathDb = storage.PathIndex;
        string? password = storage.Password;
        if (password == null)
            return;

        using var loading = Loading();
        if (res == 0)
        {
            var files = Items.Select(x => x.StorageFile);
            foreach (var file in files)
            {
                file.ParentAlbumGuid = null;
                await _dataBaseService.UpdateContent(pathDb, password, file);
            }
            await _dataBaseService.DeleteAlbum(pathDb, password, _album);

            storage.Controller?.MakeAlbumDeleted(_album, files, false);
            await Task.Delay(1200);
            await Close();
        }
        else if (res == 1)
        {
            await _dataBaseService.DeleteAlbum(pathDb, password, _album);
            foreach (var item in _initializedFiles)
                await _dataBaseService.DeleteContent(pathDb, password, item.StorageFile.Guid);

            var files = _initializedFiles.Select(x => x.StorageFile);
            storage.Controller?.MakeAlbumDeleted(_album, files, true);
            await Close();
        }
    });

    public ICommand CommandSetCover { get; init; }
    private async Task ActionSetCover(StorageAlbumItem item)
    {
        var storage = _storage;
        string pathDb = storage.PathIndex;
        string? password = storage.Password;
        if (password == null)
            return;

        using var loading = Loading();
        foreach (var itemF in _initializedFiles)
        {
            if (itemF == item) 
                itemF.IsCover = true;
            else
                itemF.IsCover = false;
        }
        _album.CoverGuid = item.StorageFile.Guid;
        _album.FilePreview = item.StorageFile.FilePreview;
        await _dataBaseService.UpdateAlbum(pathDb, password, _album);
    }
    #endregion commands

    [MemberNotNull(nameof(Items))]
    private void MakeItems(SortingAlbumItems sorting)
    {
        ObservableCollection<StorageAlbumItem> items;
        switch (sorting)
        {
            case SortingAlbumItems.ByName:
                items = _initializedFiles
                    .OrderBy(item => item.StorageFile.Name)
                    .ToObs();
                break;
            case SortingAlbumItems.ByDateCreated:
                items = _initializedFiles
                    .OrderBy(item => item.StorageFile.DateCreated)
                    .ToObs();
                break;
            default:
                throw new NotImplementedException();
        }

        _items = items;
        Items = new(items);
    }

    public class FormEdit
    {
        private readonly StorageAlbum _album;

        public FormEdit(StorageAlbum album)
        {
            _album = album;
        }

        public Validator<string> Name => Validator<string>.Build()
            .UsingValue(_album.Name)
            .UsingRequired();

        public Validator<string> Description => Validator<string>.Build()
            .UsingValue(_album.Description ?? "");

        public Validator<DateTime?> DateCreated => Validator<DateTime?>.Build()
            .UsingValue(_album.DateCreated)
            .UsingRequired();
    }
}