using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class StoragePresentVm : BaseVm
{
    private readonly List<IStorageElement> _selectedFiles = new();
    private CancellationTokenSource _popCancel = new();
    private string _searchText = "";
    private readonly StorageDir _storage;
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IDeclaratives _declaratives;
    private readonly IDataBaseService _dataBaseService;
    private readonly string _password;
    private CancellationTokenSource _searchCancel = new();
    private readonly SuggestionController _suggestionController;
    private ObservableCollection<IStorageElement>? _files;

    private SortingStorageItems _selectedSortingItem = SortingStorageItems.ByDateIndex;

    public class Key
    {
        public required StorageDir StorageCell { get; set; }
        public required string Password { get; set; }
    }
    public StoragePresentVm(Key key, IStorageService storageService, IViewPlatforms viewPlatforms, IDeclaratives declaratives,
        IDataBaseService dataBaseService)
    {
        _storage = key.StorageCell;
        _password = key.Password;
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        _declaratives = declaratives;
        _dataBaseService = dataBaseService;
        _suggestionController = new(viewPlatforms);
        Files = new([]);
        CommandOpenItem = new Cmd<IStorageElement>(ActionOpenItem);
        CommandSelectedChanged = new Cmd<IStorageElement>(ActionSelectedChanged);
        CommandSelectionSpan = new Cmd<IStorageElement>(ActionSelectionSpan);
        CommandExploreItem = new Cmd<ISourceFile>(ActionExploreItem);
        CommandDeleteItems = new Cmd<IStorageElement>(ActionDeleteItems);
        CommandMoveToNewAlbum = new Cmd<ISourceFile>(ActionMoveToNewAlbum);
        CommandMoveToAlbum = new Cmd<ISourceFile>(ActionMoveToAlbum);
        StorageName = _storage.Name;

        _storage.ElementAdded += _storage_ElementAdded;
        _storage.ElementRemoved += _storage_ElementRemoved;
    }

    #region props
    [DependsOn(nameof(ShowSelectionPanel), nameof(ShowSearchPanel))]
    public bool ShowCustomNavBar => ShowSelectionPanel || ShowSearchPanel;

    public int SelectedFilesCount { get; private set; }
    public bool ShowSelectionPanel { get; private set; }
    public string StorageName { get; }

    public bool ShowSearchPanel { get; private set; }
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            Search(value);
        }
    }

    public bool IsSearching { get; private set; }
    public ReadOnlyObservableCollection<IStorageElement> Files { get; private set; }

    [OnChangedMethod(nameof(OnSorting))]
    public SortingStorageItems SelectedSortingItem { get; set; } = SortingStorageItems.ByDateIndex;
    #endregion props

    #region commands
    public ICommand CommandSearchMode => new Cmd(() =>
    {
        ShowSearchPanel = true;
    });

    public ICommand CommandEditStorage => new Cmd(async () =>
    {
        var vm = await GoTo(new StorageEditVm.Key { Storage = _storage });
        var res = await vm.GetResult();
        if (res == null)
            return;

        if (res.IsDeleted)
        {
            await Close();
        }
    });

    public ICommand CommandCloseCustomNavBar => new Cmd(() =>
    {
        if (ShowSelectionPanel)
        {
            CommandClearSelection.Execute(null);
            return;
        }

        if (ShowSearchPanel)
        {
            ShowSearchPanel = false;
            _searchText = "";
            SetFiles(_storage.Controller!.StorageFiles);
        }
    });

    public ICommand CommandOpenItem { get; set; }
    private void ActionOpenItem(IStorageElement element)
    {
        switch (element)
        {
            case ISourceFile file:
                GoTo(new MediaPresentVm.Key
                {
                    SourceDir = _storage,
                    SourceFile = file
                });
                break;
            case StorageAlbum album:
                var items = new List<ISourceFile>();
                var allContentFiles = _storage
                    .Controller?
                    .StorageContentFiles;

                if (allContentFiles != null)
                {
                    foreach (StorageFile item in allContentFiles)
                    {
                        if (item.ParentAlbumGuid == album.Guid)
                            items.Add(item);
                    }

                    items = items.OrderBy(item => ((StorageFile)item).DateCreated).ToList();
                    album.InitializedContents = items;
                }

                GoTo(new StorageAlbumVm.Key
                {
                    Storage = _storage,
                    Album = album,
                    //Dir = album,
                    //Items = items.ToArray(),
                    //Title = album.Name,
                });
                break;
            default:
                break;
        }
    }

    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(IStorageElement file)
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
    private void ActionSelectionSpan(IStorageElement file)
    {
        if (!file.IsSelected)
        {
            file.IsSelected = true;
            _selectedFiles.Add(file);
        }

        int min = Files.Count;
        int max = -1;
        foreach (var item in _selectedFiles)
        {
            int index = Files.IndexOf(item);
            if (index < min)
                min = index;

            if (index > max) 
                max = index;
        }

        for (int i = min; i <= max; i++)
        {
            var item = Files[i];
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
        _selectedFiles.Clear();
        SelectedFilesCount = Files.Count;
        _selectedFiles.AddRange(Files);
        foreach (var item in _selectedFiles)
        {
            item.IsSelected = true;
        }
        ShowSelectionPanel = true;
    });

    public ICommand CommandEditTags => new Cmd(async () =>
    {
        if (SelectedFilesCount == 0)
            return;

        var arr = _selectedFiles
            .Where(x => x is ISourceFile)
            .Cast<ISourceFile>()
            .ToArray();
        var tags = await DirPresentVm.FindAlreadyTags(arr);
        var popup = await ShowPopup(new EditTagsVm.Key
        {
            SelectedFiles = arr,
            StorageDir = _storage,
            AlreadyTags = tags,
        });
        var res = await popup.GetResult();
        if (res)
        {
            CommandClearSelection.Execute(null);
        }
    });

    public ICommand CommandExportDbAsEncrypt => new Cmd(async () =>
    {
        var dir = await _viewPlatforms.SelectDirectory(View);
        if (dir == null)
            return;

        using var busy = Loading("export", "Export storage data base file", null);
        var res = await _storageService.ExportStorageDb(_storage, dir, _storage.Password);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        await ShowMessage("Success", "Database decrypted", "OK");
    });

    public ICommand CommandExploreItem { get; private set; }
    private void ActionExploreItem(ISourceFile file)
    {
        var res = _viewPlatforms.ShowFileOnExplorer(file.FilePath);
        if (res.IsFault)
        {
            HandleError(res);
        }
    }

    public ICommand CommandDeleteItems { get; private set; }
    private async Task ActionDeleteItems(IStorageElement element)
    {
        if (_storage.Controller == null)
        {
            await ShowError("Storage is closed");
            return;
        }

        bool sure = false;
        if (_selectedFiles.Count == 1)
        {
            sure = await ShowMessage("Deletion file",
                $"You are sure to delete {element.Name}",
                "DELETE",
                "Cancel");
        }
        else
        {
            sure = await ShowMessage("Deletion files",
                $"You are sure to delete {_selectedFiles.Count} files from current storage?",
                "DELETE",
                "Cancel");
        }

        if (!sure)
            return;

        using var busy = Loading();

        if (!element.IsSelected)
            _selectedFiles.Add(element);
        
        foreach (var item in _selectedFiles)
        {
            if (item is StorageAlbum album)
            {
                await _storage.Controller.DeleteAlbum(
                    album, 
                    album.Contents, 
                    _storageService, 
                    _dataBaseService, 
                    false);
            }
            else if (item is StorageFile filef)
            {
                await _storageService.DeleteFile(filef);
                _storage.Controller.OnFileDeleted(filef, false);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        _selectedFiles.Clear();
        SelectedFilesCount = 0;
        ShowSelectionPanel = false;
        
        SetFiles(Files);
    }

    public ICommand CommandMoveToNewAlbum { get; private set; }
    private async Task ActionMoveToNewAlbum(ISourceFile sourceFile)
    {
        var items = _selectedFiles
            .Where(x => x is StorageFile)
            .Cast<StorageFile>()
            .ToArray();
        await GoTo(new AlbumCreateVm.Key
        {
            Files = items,
            StorageDir = _storage,
        });
    }

    public ICommand CommandMoveToAlbum { get; private set; }
    private async Task ActionMoveToAlbum(ISourceFile sourceFile)
    {
        var selected = _selectedFiles.Where(x => x is StorageFile)
            .Cast<StorageFile>()
            .ToArray();

        var vm = await ShowPopup(new SelectStorageAlbumVm.Key
        {
            StorageDir = _storage,
        });

        var res = await vm.GetResult();
        if (res == null)
            return;

        using var loading = Loading();
        var album = res;

        foreach (var item in selected)
        {
            item.ParentAlbumGuid = album.Guid;
            await _dataBaseService.UpdateContent(_storage.PathIndex, _storage.Password, item);
        }
        _storage.Controller.MakeAlbumMove(album, selected);
    }
    #endregion commands

    public override async void OnConnectToNavigation()
    {
        base.OnConnectToNavigation();
        using var load = LoadingGlobal();
        var res = await _storageService.InitStorage(_storage, _password, _popCancel.Token);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        SetFiles(_storage.Controller!.StorageFiles);
    }

    public override void OnDisconnectedFromNavigation()
    {
        base.OnDisconnectedFromNavigation();
        if (Files is IDisposable dfiles)
            dfiles.Dispose();

        _storage.ElementRemoved -= _storage_ElementRemoved;
        _storage.ElementAdded -= _storage_ElementAdded;
    }

    public override void OnKeyComboListener(KeyPressedArgs args)
    {
        base.OnKeyComboListener(args);
        switch (args.Key)
        {
            case "Esc":
                CommandClearSelection.Execute(null);
                break;
            case "Ctrl+A":
                CommandSelectAll.Execute(null);
                break;
            default:
                break;
        }
    }

    private void OnSorting()
    {
        //IEnumerable<StorageFile> files;
        //switch (SelectedSortingItem)
        //{
        //    case SortingStorageItems.ByDateIndex:
        //        files = Files.OrderByDescending(x => x.DateInitIndex);
        //        break;
        //    case SortingStorageItems.Random:
        //        var rand = new Random();
        //        files = Files.OrderByDescending(x => rand.Next());
        //        break;
        //    default:
        //        throw new NotImplementedException();
        //}

        //var obs = new ObservableCollection<StorageFile>(files);
        SetFiles(Files);
    }

    private void SetFiles(IEnumerable<IStorageElement> files)
    {
        IEnumerable<IStorageElement> sorted;
        switch (SelectedSortingItem)
        {
            case SortingStorageItems.ByDateIndex:
                sorted = files.OrderByDescending(x => x.DateCreated);
                break;
            case SortingStorageItems.Random:
                var rand = new Random();
                sorted = files.OrderByDescending(x => rand.Next());
                break;
            default:
                throw new NotImplementedException();
        }

        _files = new(sorted);
        if (_files.Count > 1000)
        {
            Parallel.ForEach(_files, (x) =>
            {
                x.ListContext = _files;
            });
        }
        else
        {
            foreach (var item in _files)
                item.ListContext = _files;
        }
        Files = new(_files);
    }

    private async void Search(string value)
    {
        if (_storage.IsClose)
            return;

        IsSearching = true;
        string? success = await _suggestionController.Output(value);
        if (success == null)
            return;

        if (success != "")
        {
            _searchCancel.Cancel();
            _searchCancel = new();
            var res = await _storage.Controller.Search(success, _searchCancel.Token);
            if (res == null)
                return;

            SetFiles(res);
        }
        else
        {
            SetFiles(_storage.Controller!.StorageFiles);
        }
        IsSearching = false;
    }

    private void _storage_ElementAdded(object? sender, object e)
    {
        if (_files == null || _files.Count == 0)
            return;

        switch (e)
        {
            case IStorageElement element:
                var match = _files.FirstOrDefault(x => x.Guid == element.Guid);
                if (match != null)
                    return;

                _files.Insert(0, element);
                break;
            default:
                break;
        }
    }

    private void _storage_ElementRemoved(object? sender, object e)
    {
        if (_files == null || _files.Count == 0) 
            return;

        switch (e)
        {
            case IStorageElement element:
                var match = _files.FirstOrDefault(x => x.Guid == element.Guid);
                if (match != null)
                {
                    _files.Remove(match);

                    if (_selectedFiles.Remove(match))
                    {
                        SelectedFilesCount--;
                        if (SelectedFilesCount == 0)
                            ShowSelectionPanel = false;
                    }
                }
                break;
            default:
                break;
        }
    }
}