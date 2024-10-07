using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class StoragePresentVm : BaseVm
{
    private readonly List<StorageFile> _selectedFiles = new();
    private CancellationTokenSource _popCancel = new();
    private string _searchText = "";
    private readonly StorageDir _storage;
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IDeclaratives _declaratives;
    private readonly string _password;
    private CancellationTokenSource _searchCancel = new();
    private readonly SuggestionController _suggestionController;
    private ObservableCollection<StorageFile>? _files;
    private ReadOnlyObservableCollection<StorageFile> _filesRO;

    private SortingStorageItems _selectedSortingItem = SortingStorageItems.ByDateIndex;

    public class Key
    {
        public required StorageDir StorageCell { get; set; }
        public required string Password { get; set; }
    }
    public StoragePresentVm(Key key, IStorageService storageService, IViewPlatforms viewPlatforms, IDeclaratives declaratives)
    {
        _storage = key.StorageCell;
        _password = key.Password;
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        _declaratives = declaratives;
        _suggestionController = new(viewPlatforms);
        _filesRO = new([]);
        CommandOpenItem = new Cmd<StorageFile>(ActionOpenItem);
        CommandSelectedChanged = new Cmd<StorageFile>(ActionSelectedChanged);
        CommandExploreItem = new Cmd<ISourceFile>(ActionExploreItem);
        CommandDeleteItem = new Cmd<ISourceFile>(ActionDeleteItem);
        StorageName = _storage.Name;

        _storage.FileDeleted += _storage_FileDeleted;
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
    public ReadOnlyObservableCollection<StorageFile> Files => _filesRO;

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
    private void ActionOpenItem(StorageFile file)
    {
        GoTo(new MediaPresentVm.Key
        {
            SourceDir = _storage,
            SourceFile = file
        });
    }

    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(StorageFile file)
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

        var arr = _selectedFiles.ToArray();
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

    public ICommand CommandDeleteItem { get; private set; }
    private async Task ActionDeleteItem(ISourceFile file)
    {
        var res = await _declaratives.DeclarativeDeleteFile(this, file);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        if (file is StorageFile f)
        {
            _selectedFiles.Remove(f);
        }
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

        _storage.FileDeleted -= _storage_FileDeleted;
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

    private void SetFiles(IEnumerable<StorageFile> files)
    {
        IEnumerable<StorageFile> sorted;
        switch (SelectedSortingItem)
        {
            case SortingStorageItems.ByDateIndex:
                sorted = files.OrderByDescending(x => x.DateInitIndex);
                break;
            case SortingStorageItems.Random:
                var rand = new Random();
                sorted = files.OrderByDescending(x => rand.Next());
                break;
            default:
                throw new NotImplementedException();
        }

        _files = new(sorted);
        _filesRO = new(_files);
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
        OnPropertyChanged(nameof(Files));

        //if (files is ObservableCollection<StorageFile> obs)
        //{
        //    _files = obs;
        //    _filesRO = new(obs);
        //    foreach (var item in obs)
        //        item.ListContext = obs;

        //    OnPropertyChanged(nameof(Files));
        //}
        //else if (files is ReadOnlyObservableCollection<StorageFile> ro)
        //{
        //    _filesRO = ro;
        //    OnPropertyChanged(nameof(Files));
        //    _files = null;

        //    if (ro.Count > 1000)
        //    {
        //        Parallel.ForEach(ro, (x) =>
        //        {
        //            x.ListContext = null;
        //        });
        //    }
        //    else
        //    {
        //        foreach (var item in ro)
        //            item.ListContext = null;
        //    }
        //}
        //else
        //{
        //    throw new NotSupportedException();
        //}
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

    private void _storage_FileDeleted(object? sender, ISourceFile e)
    {
        if (e is StorageFile f)
            _files?.Remove(f);
    }
}