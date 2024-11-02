using BlindCatCore.Core;
using BlindCatCore.Extensions;
using BlindCatCore.ExternalApi;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class HomeVm : BaseVm
{
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IAppEnv _appEnv;
    private readonly IStorageService _storageService;
    private readonly IDeclaratives _declaratives;
    private readonly IPluginHost _pluginHost;

    public class Key
    {
    }

    public HomeVm(Key key,
        IViewPlatforms viewPlatforms,
        IAppEnv appEnv,
        IStorageService storageService,
        IDeclaratives declaratives,
        IPluginHost pluginHost)
    {
        _viewPlatforms = viewPlatforms;
        _appEnv = appEnv;
        _storageService = storageService;
        _declaratives = declaratives;
        _pluginHost = pluginHost;
        CommandSelectItem = new Cmd<HomeItem>(ActionSelectItem);
        CommandOpenStorage = new Cmd<StorageDir>(ActionOpenStorage);
        CommandOpenPlugin = new Cmd<IPlugin>(ActionOpenPlugin);

        Items =
        [
            new HomeItem
            {
                Name = "Open deep directory",
                Key = new DirPresentVm.Key { DirectoryPath = "NO_RESOLVED" },
                Preaction = async (x) =>
                {
                    string? dir = await _viewPlatforms.SelectDirectory(View);
                    if (dir == null)
                        return false;

                    var key = (DirPresentVm.Key)x.Key;
                    key.DirectoryPath = dir;
                    key.IsDeep = true;
                    return true;
                },
            },
            new HomeItem
            {
                Name = "Open directory",
                Key = new DirPresentVm.Key { DirectoryPath = "NO_RESOLVED" },
                Preaction = async (x) =>
                {
                    string? dir = await _viewPlatforms.SelectDirectory(View);
                    if (dir == null)
                        return false;

                    var key = (DirPresentVm.Key)x.Key;
                    key.DirectoryPath = dir;
                    return true;
                },
            },
            new HomeItem
            {
                Name = "Open file",
                Key = new MediaPresentVm.Key { SourceFile = null! },
                Preaction = async (x) =>
                {
                    var res = await _viewPlatforms.SelectMediaFile(View);
                    if (res == null)
                        return false;

                    res.Stream.Dispose();
                    var key = (MediaPresentVm.Key)x.Key;
                    key.SourceFile = new LocalFile
                    {
                        Id = 0,
                        FilePath = res.Path,
                    };
                    return true;
                }
            },
        ];
    }

    public HomeItem[] Items { get; set; }
    public ReadOnlyObservableCollection<StorageDir> Storages => _storageService.Storages;
    public IPlugin[] Plugins { get; private set; } = [];

    #region commands
    public ICommand CommandSelectItem { get; private set; }
    private async void ActionSelectItem(HomeItem x)
    {
        if (x.Preaction != null)
        {
            bool canRunning = await x.Preaction.Invoke(x);
            if (!canRunning)
                return;
        }

        await GoTo(x.Key);
    }

    public ICommand CommandOpenStorage { get; private set; }
    private async void ActionOpenStorage(StorageDir storageDir)
    {
        string? pass = storageDir.Password;
        if (storageDir.IsClose)
        {
            using var loading = LoadingGlobal();
            var res = await _declaratives.TryOpenStorage(storageDir);
            if (res.IsFault)
            {
                await HandleError(res);
                return;
            }
            pass = storageDir.Password;
        }

        await GoTo(new StoragePresentVm.Key
        {
            StorageCell = storageDir,
            Password = pass,
        });
    }

    public ICommand CommandImportStorage => new Cmd(async () =>
    {
        string? storageDir = await _viewPlatforms.SelectDirectory(View);
        if (storageDir == null)
            return;

        var files = Directory.GetFiles(storageDir);
        var index = files.FirstOrDefault(x => Path.GetFileName(x) == "index");
        if (index == null)
        {
            await ShowError("Selected directory is not storage object");
            return;
        }

        using var busy = LoadingGlobal();
        if (!await _storageService.CheckStorageDir(index))
        {
            await ShowError("Selected directory is not storage object");
            return;
        }

        string? name = await ShowDialogPromt("Input name", "Typing name for new storage");
        if (name == null)
            return;

        var importRes = await _storageService.Import(name, storageDir);
        if (importRes.IsSuccess)
            await ShowMessage("Success", "Storage was successful imported", "OK");
        else
            await HandleError(importRes);
    });

    public ICommand CommandAddStorage => new Cmd(() =>
    {
        GoTo(new StorageCreateVm.Key { });
    });

    public ICommand CommandOpenPlugin { get; private init; }
    private async Task ActionOpenPlugin(IPlugin plugin)
    {
        var cancel = new CancellationTokenSource();
        using var busy = LoadingGlobal("plugin", $"Plugin \"{plugin.Name}\" is running...", cancel);
        IBlindCatApi? api = null;
        try
        {
            api = _pluginHost.MakePublicApi(plugin, this, cancel.Token);
            api.BusyContext = busy;
            await plugin.OnActivated(api, cancel.Token);
        }
        catch (Exception ex)
        {
            await ShowError($"Error into \"{plugin.Name}\" plugin:\n{ex}");
        }
        finally
        {
            api?.Dispose();
        }
    }
    #endregion commands

    public override async void OnConnectToNavigation()
    {
        using var loading = LoadingGlobal();
        string? filePath = _appEnv.AppLaunchedArgs;
        if (!string.IsNullOrEmpty(filePath))
        {
            string dir = Path.GetDirectoryName(filePath)!;
            var locdic = new LocalDir
            {
                DirPath = dir,
            };

            var locfile = new LocalFile
            {
                Id = -1,
                FilePath = filePath,
            };

            await GoTo(new DirPresentVm.Key 
            { 
                DirectoryPath = dir, 
                Directory = locdic, 
                LazyLoadingFile = locfile,
            }, false);

            await GoTo(new MediaPresentVm.Key
            {
                SourceDir = locdic,
                Album = locdic.Files,
                SourceFile = locfile,
            }, false);
        }

        await _storageService.GetStorages();

        var erros = new List<(string dll, Exception ex)>();
        var plugins = new List<IPlugin>();

        await Task.Run(async () =>
        {
            string? pathRuntime = Environment.ProcessPath;
            if (string.IsNullOrEmpty(pathRuntime))
                return;

            string? pathDir = Path.GetDirectoryName(pathRuntime);
            if (string.IsNullOrEmpty(pathDir))
                return;

            string pluginDir = Path.Combine(pathDir, "plugins");
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            var ipluginType = typeof(IPlugin);
            var files = Directory.GetFiles(pluginDir, "*.dll");
            foreach (var file in files)
            {
                try
                {
                    var ass = Assembly.LoadFrom(file);
                    var types = ass.GetTypes();
                    foreach (var type in types)
                    {
                        if (ipluginType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            var plugin = (IPlugin)Activator.CreateInstance(type)!;
                            await plugin.EntryPoint();
                            plugins.Add(plugin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    erros.Add((file, ex));
                }
            }
        });

        Plugins = plugins.ToArray();

        if (erros.Count > 0)
        {
            await Task.Delay(1000);
            foreach (var item in erros)
            {
                await ShowError($"Next plugin could not be loaded:\n" +
                    $" - {Path.GetFileName(item.dll)}\n\n" +
                    $"Reason: {item.ex}");
            }
        }
    }
}

public class HomeItem
{
    public required string Name { get; set; }
    public required object Key { get; set; }
    public Func<HomeItem, Task<bool>>? Preaction { get; set; }
}