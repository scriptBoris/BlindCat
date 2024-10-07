using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.ViewModels;
using System.Diagnostics;

namespace BlindCatCore.Services;

public interface IDeclaratives
{
    /// <summary>
    /// Декларативный подход: <br/>
    /// - проверяет выбранный storage <br/>
    /// - если выбран то проверяет открытый он или нет <br/>
    /// - если не выбран то отображает диалог выбора <br/>
    /// - если нет ни одного storage, то предложит создать storage <br/>
    /// - если storage закрыт, то запрашивает пароль, проверяет пароль, запоминает выбранный storage (опционально)
    /// </summary>
    Task<AppResponse<StorageDir>> DeclarativeSelectStorage(BaseVm vm, bool noRemember = false, bool autoInit = false);

    /// <summary>
    /// Декларативный подход: <br/>
    /// - если хранилище закрыто (нет контроллера), то  <br/>
    /// - запрашивает пароль через диалоговое окно <br/>
    /// - проверяет пароль на валидность <br/>
    /// - создает контроллер и помещает в него валидный введенный пароль <br/>
    /// - возвращает пароль
    /// </summary>
    Task<AppResponse<string>> TryOpenStorage(StorageDir storage);

    /// <summary>
    /// Декларативный подход: <br/>
    /// - отображается диалог выбора storage (если он не выбран) <br/>
    /// - запрашивается пароль для storage (если storage закрыт) <br/>
    /// - отображается popup для meta <br/>
    /// - запись в storage <br/>
    /// - если указан mediaBase, то освобождает ресурсы для текущего плеера (видео) <br/>
    /// - удаляет оригинальные файлы
    /// </summary>
    Task<AppResponse> SaveLocalFilesWithPopup(BaseVm vm, ISourceFile[] localFiles, IProgressBroker<ISourceFile>? progressBroker, IFileUnlocker? unlocker);

    /// <summary>
    /// Декларативный подход: 
    /// - отображается диалог выбора storage (если он не выбран) <br/>
    /// - запрашивается пароль для storage (если storage закрыт) <br/>
    /// - запись в storage <br/>
    /// - если указан mediaBase, то освобождает ресурсы для текущего плеера (видео) <br/>
    /// - удаляет оригинальные файлы
    /// </summary>
    Task<AppResponse> SaveLocalFiles(BaseVm vm, ISourceFile[] localFiles, string[]? addTags, IProgressBroker<ISourceFile>? broker, IFileUnlocker? unlocker);
    
    Task<AppResponse> DeclarativeDeleteFile(BaseVm vm, ISourceFile file);
}

public class Declaratives : IDeclaratives
{
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;

    public Declaratives(IStorageService storageService, IViewPlatforms viewPlatforms)
    {
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
    }

    public async Task<AppResponse<StorageDir>> DeclarativeSelectStorage(BaseVm _vm, bool noRemember = false, bool autoInit = false)
    {
        var storage = _storageService.CurrentStorage;
        if (storage == null)
        {
            if (_storageService.Storages.Count == 0)
            {
                bool create = await _vm.ShowMessage("No storages",
                    "There is no repository to store this file. Create?",
                    "Create",
                    "Cancel");
                if (!create)
                    return AppResponse.Canceled;

                var vm = await _vm.GoTo(new StorageCreateVm.Key { UseAutoInit = true });
                storage = await vm.GetResult();
                if (storage == null)
                    return AppResponse.Canceled;
            }

            if (_storageService.Storages.Count == 1)
            {
                storage = _storageService.Storages[0];
            }
            else
            {
                var items = _storageService.Storages.Select(x => x.Name).ToArray();
                int? selectedId = await _vm.ShowDialogSheet("Select storage", "Cancel", items);
                if (selectedId == null)
                    return AppResponse.Canceled;

                storage = _storageService.Storages[selectedId.Value];
            }
        }

        string password;

        // try pwrd
        if (storage.IsClose)
        {
            using var load = _vm.Loading();
            var res = await TryOpenStorage(storage);
            if (res.IsFault)
                return res.AsError;

            password = storage.Password!;
        }
        else
        {
            password = storage.Password!;
        }

        // auto init
        if (autoInit && !storage.Controller!.IsInitialized)
        {
            var initRes = await _storageService.InitStorage(storage, password, CancellationToken.None);
            if (initRes.IsFault)
                return initRes.AsError;
        }

        if (!noRemember)
            _storageService.CurrentStorage = storage;

        return AppResponse.Result(storage);
    }

    public async Task<AppResponse<string>> TryOpenStorage(StorageDir storage)
    {
        if (storage.Controller != null)
            return AppResponse.Result(storage.Password!);

        var password = await _viewPlatforms.ShowDialogPromtPassword(
            "Password",
            $"The Storage \"{storage.Name}\" is secure, enter password",
            "OK",
            "Cancel",
            placeholder:"Enter password",
            hostView: null);
        if (password == null)
            return AppResponse.Canceled;

        bool correct = await _storageService.CheckPasswordCorrect(storage, password);
        if (!correct)
            return AppResponse.Error("Incorrect password");

        storage.Controller = new StorageDirController
        {
            Password = password,
        };
        _storageService.CurrentStorage = storage;

        return AppResponse.Result(password);
    }

    public async Task<AppResponse> SaveLocalFilesWithPopup(BaseVm _vm, ISourceFile[] localFiles, IProgressBroker<ISourceFile>? progressBroker, IFileUnlocker? unlocker)
    {
        var storage = _storageService.CurrentStorage;
        if (storage == null)
        {
            if (_storageService.Storages.Count == 0)
            {
                bool create = await _vm.ShowMessage("No storages",
                    "There is no repository to store this file. Create?",
                    "Create",
                    "Cancel");
                if (!create)
                    return AppResponse.Canceled;

                var vm = await _vm.GoTo(new StorageCreateVm.Key { UseAutoInit = true });
                storage = await vm.GetResult();
                if (storage == null)
                    return AppResponse.Canceled;
            }

            if (_storageService.Storages.Count == 1)
            {
                storage = _storageService.Storages[0];
            }
            else
            {
                var items = _storageService.Storages.Select(x => x.Name).ToArray();
                int? selectedId = await _vm.ShowDialogSheet("Select storage", "Cancel", items);
                if (selectedId == null)
                    return AppResponse.Canceled;

                storage = _storageService.Storages[selectedId.Value];
            }
        }

        string password;

        // try pwrd
        if (storage.IsClose)
        {
            using var load = _vm.Loading();
            var res = await TryOpenStorage(storage);
            if (res.IsFault)
                return res.AsError;

            password = storage.Password!;
        }
        else
        {
            password = storage.Password!;
        }

        _storageService.CurrentStorage = storage;

        // try init
        if (!storage.Controller!.IsInitialized)
        {
            using var load = _vm.Loading("Initializing storage");
            var initRes = await _storageService.InitStorage(storage, password, CancellationToken.None);
            if (initRes.IsFault)
                return initRes.AsError;
        }

        for (int i = 0; i < localFiles.Length; i++)
        {
            var loc = localFiles[i];
            loc.TempStorageFile ??= new StorageFile
            {
                IsTemp = true,
                FilePath = loc.FilePath,
                FilePreview = null,
                Storage = storage,
            };
        }

        var popup = await _vm.ShowPopup(new BlindCatCore.PopupViewModels.SaveFilesVm.Key
        {
            StorageDir = storage,
            SaveFiles = localFiles,
            ProgressBroker = progressBroker,
            Unlocker = unlocker,
        });
        bool isOk = await popup.GetResult();
        if (!isOk)
            return AppResponse.Canceled;

        return AppResponse.OK;
    }

    public async Task<AppResponse> SaveLocalFiles(BaseVm _vm, ISourceFile[] localFiles, string[]? addTags, IProgressBroker<ISourceFile>? broker, IFileUnlocker? unlocker)
    {
        var storageRes = await DeclarativeSelectStorage(_vm, autoInit: true);
        if (storageRes.IsFault)
            return storageRes.AsError;

        var storage = storageRes.Result;
        string password = storage.Password!;

        for (int i = 0; i < localFiles.Length; i++)
        {
            var loc = localFiles[i];
            loc.TempStorageFile ??= new StorageFile
            {
                IsTemp = true,
                FilePath = loc.FilePath,
                Storage = storage,
                Name = Path.GetFileNameWithoutExtension(loc.FilePath),
            };

            if (addTags != null)
                loc.TempStorageFile.Tags = TagsController.Merge(loc.TempStorageFile.Tags, addTags, []);

            // todo Сделать отображение файлов, которые не удалось записать в storage
            var saveRes = await _storageService.SaveStorageFile(storage, loc, password, unlocker);
            if (saveRes.IsCanceled)
            {
                throw new InvalidOperationException("This operation should not be canceled");
            }

            if (saveRes.IsSuccess)
            {
                broker?.OnItemCompleted(loc);
            }
            else
            {
                broker?.OnItemFailed(saveRes, loc);
            }
        }

        return AppResponse.OK;
    }

    public async Task<AppResponse> DeclarativeDeleteFile(BaseVm vm, ISourceFile file)
    {
        if (file.TempStorageFile!.Storage?.Controller == null)
        {
            return AppResponse.Error("Storage is closed");
        }

        bool del = await vm.ShowMessage("Deletion file",
            $"You are sure to delete {file.TempStorageFile!.Name}",
            "DELETE",
            "Cancel");
        if (!del)
            return AppResponse.Canceled;

        if (file is not StorageFile f)
            return AppResponse.Error($"parameter {nameof(file)} is not StorageFile");

        using var busy = vm.Loading("deletetion", "File deletion in progress...", null);
        var delRes = await _storageService.DeleteFile(file);
        if (delRes.IsFault)
            return delRes;

        file.TempStorageFile.Storage.Controller.DeleteFile(f);
        return AppResponse.OK;
    }
}