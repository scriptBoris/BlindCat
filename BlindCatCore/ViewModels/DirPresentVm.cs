using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class DirPresentVm : BaseVm
{
    private readonly string _dirPath;
    private readonly List<LocalFile> _selectedFiles = new();
    private readonly IStorageService _storageService;
    private readonly IDeclaratives _declaratives;
    private readonly LocalFile? _lazyLoadingFile;

    public class Key
    {
        public required string DirectoryPath { get; set; }
        public LocalDir? Directory { get; set; }
        public bool IsDeep { get; set; }
        public LocalFile? LazyLoadingFile { get; set; }
    }

    public DirPresentVm(Key key, IStorageService storageService, IDeclaratives declaratives)
    {
        _dirPath = key.DirectoryPath;
        _storageService = storageService;
        _declaratives = declaratives;
        _lazyLoadingFile = key.LazyLoadingFile;
        IsDeepDir = key.IsDeep;
        Dir = key.Directory ?? new LocalDir
        {
            DirPath = _dirPath,
        };

        CommandOpen = new Cmd<LocalFile>(ActionOpen);
        CommandSelectedChanged = new Cmd<LocalFile>(ActionSelectedChanged);
    }

    public LocalDir Dir { get; }
    public bool IsDeepDir { get; }
    public int SelectedFilesCount { get; private set; }
    public bool ShowSelectionPanel { get; private set; }

    #region commands
    public ICommand CommandOpen { get; init; }
    private void ActionOpen(LocalFile file)
    {
        GoTo(new MediaPresentVm.Key
        {
            SourceDir = Dir,
            Album = Dir.Files,
            SourceFile = file,
        });
    }

    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(LocalFile file)
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

        ShowSelectionPanel = (_selectedFiles.Count > 0);
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
        if (IsDeepDir)
            throw new InvalidOperationException("Невозможно выбрать все элементы, т.к. данные подгружаются в режиме реального времени");

        if (Dir.Files == null)
            return;

        _selectedFiles.Clear();
        SelectedFilesCount = Dir.Files.Count;
        _selectedFiles.AddRange(Dir.Files);
        foreach (var item in _selectedFiles)
        {
            item.IsSelected = true;
        }
        ShowSelectionPanel = true;
    });

    public ICommand CommandSaveSelectedItems => new Cmd(async () =>
    {
        if (_selectedFiles.Count == 0 || IsLoading)
            return;

        using var loading = Loading();
        var files = _selectedFiles.ToArray();
        var savedFiles = new List<ISourceFile>();
        var broker = new ProgressBroker<ISourceFile>((arg, item) =>
        {
            if (arg.IsSuccess)
                savedFiles.Add(item);
        });
        var res = await _declaratives.SaveLocalFilesWithPopup(this, files, broker, null);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        // todo сделать удаление группой?
        for (int i = savedFiles.Count - 1; i >= 0; i--)
        {
            var local = (LocalFile)savedFiles[i];
            savedFiles.RemoveAt(i);
            InvokeInMainThread(() =>
            {
                Dir.Remove(local);
            });
        }
    });

    public ICommand CommandDeleteSelectedItems => new Cmd(async () =>
    {
        if (_selectedFiles.Count == 0)
            return;

        bool res = await ShowMessage("Deletion",
            $"You are sure to delete {_selectedFiles.Count} files?",
            "Delete",
            "Cancel");
        if (!res)
            return;

        for (int i = _selectedFiles.Count - 1; i >= 0; i--)
        {
            try
            {
                var f = _selectedFiles[i];
                if (!File.Exists(f.FilePath))
                    return;

                string fname = Path.GetFileName(f.FilePath);
                string check = Path.Combine(_dirPath, fname);

                if (!File.Exists(check))
                    return;

                File.Delete(check);
                Dir.Remove(f);
            }
            catch (Exception ex)
            {
                string err = $"Fail to delete file\n\n" + ex.ToString();
                await ShowError(err);
            }
        }
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
        UpdateSelectedFiles(storage, _selectedFiles);

        var selectedFiles = _selectedFiles.ToArray();
        var alreadyTags = await FindAlreadyTags(selectedFiles);

        var popup = await ShowPopup(new EditTagsVm.Key
        {
            SelectedFiles = selectedFiles,
            StorageDir = storage,
            AlreadyTags = alreadyTags,
        });

        if (await popup.GetResult())
        {
            CommandClearSelection.Execute(null);
        }
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
        UpdateSelectedFiles(storage, _selectedFiles);

        var selectedFiles = _selectedFiles.ToArray();
        var alreadyTags = await FindAlreadyTags(selectedFiles);
        await ShowPopup(new RemoveTagsVm.Key
        {
            SelectedFiles = _selectedFiles.ToArray(),
            AlreadyTags = alreadyTags,
            StorageDir = storage,
        });
    });
    #endregion commands

    private void Dir_FileDeleted(object? sender, ISourceFile e)
    {
        if (SelectedFilesCount <= 0)
            return;

        bool del = _selectedFiles.Remove((LocalFile)e);
        if (!del)
            return;

        SelectedFilesCount--;
        ShowSelectionPanel = (_selectedFiles.Count > 0);
    }

    public override async void OnConnectToNavigation()
    {
        base.OnConnectToNavigation();
        Dir.FileDeleted += Dir_FileDeleted;

        if (Dir.Files != null)
            return;

        using var busy = Loading();
        if (IsDeepDir)
        {
            await GetFilesAsDeep(_dirPath);
        }
        else
        {
            var filesLoad = await GetFilesAsync(_dirPath, Dir);
            if (filesLoad.IsFault)
            {
                await HandleError(filesLoad);
                return;
            }

            var files = filesLoad.Result;
            if (_lazyLoadingFile != null)
            {
                var match = files.FirstOrDefault(x => x.FileName == _lazyLoadingFile.FileName);
                if (match != null)
                {
                    int index = Array.IndexOf(files, match);
                    if (index == -1)
                        throw new InvalidOperationException();

                    files[index] = _lazyLoadingFile;
                }
            }

            InvokeInMainThread(() =>
            {
                Dir.FilesSource = new(files);
            });
        }
    }

    public override void OnDisconnectedFromNavigation()
    {
        base.OnDisconnectedFromNavigation();
        Dir.FileDeleted -= Dir_FileDeleted;
    }

    public override void OnKeyComboListener(KeyPressedArgs args)
    {
        base.OnKeyComboListener(args);
        switch (args.Key)
        {
            case "Esc":
                CommandClearSelection?.Execute(null);
                break;
            case "Ctrl+A":
                CommandSelectAll?.Execute(null);
                break;
            case "Delete":
                CommandDeleteSelectedItems?.Execute(null);
                break;
            case "Ctrl+S":
                CommandSaveSelectedItems.Execute(null);
                break;
            default:
                break;
        }
    }

    public override async Task<bool> TryClose()
    {
        if (ShowSelectionPanel)
        {
            CommandClearSelection.Execute(null);
            return false;
        }

        return await base.TryClose();
    }

    public static async Task<AppResponse<LocalFile[]>> GetFilesAsync(string dirPath, LocalDir dir)
    {
        string[] files;
        try
        {
            files = await Task.Run(() => Directory.GetFiles(dirPath));
        }
        catch (Exception ex)
        {
            return AppResponse.Error($"Fail get files by path {dirPath}", 111019, ex);
        }

        var medias = new List<LocalFile>();
        foreach (var file in files)
        {
            bool isMedia = MediaPresentVm.ResolveFormat(file) != MediaFormats.Unknown;
            if (!isMedia)
                continue;

            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.System))
                continue;

            medias.Add(new LocalFile
            {
                Id = medias.Count,
                FilePath = file,
                Dir = dir,
            });
        }
        var array = medias.ToArray();
        return AppResponse.Result(array);
    }

    public static LocalFile[] GetFiles(string dirPath, LocalDir dir)
    {
        var files = Directory.GetFiles(dirPath);
        var medias = new List<LocalFile>();
        foreach (var file in files)
        {
            bool isMedia = MediaPresentVm.ResolveFormat(file) != MediaFormats.Unknown;
            if (!isMedia)
                continue;

            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.System))
                continue;

            medias.Add(new LocalFile
            {
                Id = medias.Count,
                FilePath = file,
                Dir = dir,
            });
        }
        return medias.ToArray();
    }

    public static void UpdateSelectedFiles(StorageDir storage, IEnumerable<ISourceFile> _selectedFiles)
    {
        foreach (var item in _selectedFiles)
        {
            if (item.TempStorageFile != null)
                continue;

            item.TempStorageFile = new StorageFile
            {
                IsTemp = true,
                FilePath = item.FilePath,
                FilePreview = "<NULL>",
                Storage = storage,
            };
        }
    }

    private async Task GetFilesAsDeep(string dirPath)
    {
        throw new NotImplementedException();
        // todo нужно ли это вообще?
        //Dir.FilesSource = new();
        //var files = Directory.GetFiles(dirPath);
        //foreach (string file in files)
        //{
        //    bool isMedia = MediaPresentVm.ResolveFormat(file) != MediaFormats.Unknown;
        //    if (!isMedia)
        //        continue;

        //    var attributes = File.GetAttributes(file);
        //    if (attributes.HasFlag(FileAttributes.System))
        //        continue;

        //    Dir.Files.Add(new LocalFile
        //    {
        //        Id = _id++,
        //        FilePath = file,
        //        Dir = Dir,
        //    });

        //    await Task.Delay(50);
        //}

        //var dirs = Directory.GetDirectories(dirPath);
        //foreach (string subDir in dirs)
        //{
        //    await GetFilesAsDeep(subDir);
        //}
    }

    public static async Task<TagCount[]> FindAlreadyTags(ISourceFile[] selectedFiles)
    {
        var vars = selectedFiles.Select(x => x.TempStorageFile).ToArray();
        return await FindAlreadyTags(vars);
    }

    public static async Task<TagCount[]> FindAlreadyTags(StorageFile[] selectedFiles)
    {
        var list = new List<TagCount>();
        await Task.Run(() =>
        {
            foreach (var item in selectedFiles)
            {
                foreach (string tag in item.Tags)
                {
                    var m = list.FirstOrDefault(x => string.Equals(x.TagName, tag, StringComparison.OrdinalIgnoreCase));
                    if (m != null)
                    {
                        m.Count++;
                    }
                    else
                    {
                        list.Add(new TagCount
                        {
                            TagName = tag,
                            Count = 1,
                        });
                    }
                }
            }
        });

        return list.ToArray();
    }
}
