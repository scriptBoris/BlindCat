using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using ValidatorSam;

namespace BlindCatCore.ViewModels;

public class AlbumVm : BaseVm
{
    private readonly List<ISourceFile> _selectedFiles = new();
    private readonly ISourceDir _dir;
    private readonly IDeclaratives _declaratives;
    private readonly IDataBaseService _dataBaseService;
    private readonly ObservableCollection<ISourceFile> _files;
    private readonly StorageAlbum? _album;

    public class Key
    {
        public Key() { }
        public StorageAlbum? Album { get; set; }
        public required string Title { get; set; }
        public required ISourceFile[] Items { get; set; }
        public required ISourceDir Dir { get; set; }
    }
    public AlbumVm(Key key, IDeclaratives declaratives, IDataBaseService dataBaseService)
    {
        _dir = key.Dir;
        _declaratives = declaratives;
        _dataBaseService = dataBaseService;
        _album = key.Album;
        _files = new ObservableCollection<ISourceFile>(key.Items);
        Items = new(_files);
        Title = key.Title;
        CommandOpenItem = new Cmd<ISourceFile>(ActionOpenItem);
        CommandSelectedChanged = new Cmd<ISourceFile>(ActionSelectedChanged);
        CommandSelectionSpan = new Cmd<ISourceFile>(ActionSelectionSpan);
    }

    [DependsOn(nameof(ShowSelectionPanel))]
    public bool ShowCustomNavBar => ShowSelectionPanel;
    public bool ShowSelectionPanel { get; set; }
    public bool ShowEditPanel { get; set; }
    public ReadOnlyObservableCollection<ISourceFile> Items { get; }
    public int SelectedFilesCount { get; set; }
    public SortingAlbumItems SelectedSortingItem { get; set; }

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

    public ICommand CommandSelectionSpan { get; init; }
    private void ActionSelectionSpan(ISourceFile file)
    {
        if (!file.IsSelected)
        {
            file.IsSelected = true;
            _selectedFiles.Add(file);
        }

        int min = Items.Count;
        int max = -1;
        foreach (var item in _selectedFiles)
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
            if (!item.IsSelected)
            {
                item.IsSelected = true;
                _selectedFiles.Add(item);
            }
        }

        SelectedFilesCount = 1 + max - min;
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

    public ICommand CommandCloseCustomNavBar => new Cmd(() =>
    {
        foreach (var item in _selectedFiles)
        {
            item.IsSelected = false; 
        }
        _selectedFiles.Clear();
        ShowSelectionPanel = false;
        SelectedFilesCount = 0;
    });
    #endregion commands
}