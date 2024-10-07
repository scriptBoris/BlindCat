using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using BlindCatCore.ViewModels;
using BlindCatCore.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using static BlindCatCore.Services.IViewModelResolver;

#if ANDROID
using Android.Content;
using Android.Provider;
#endif

namespace BlindCatMaui.Services;

public class StorageService : IStorageService
{
    private readonly ICrypto _crypto;
    private readonly IDataBaseService _dataBaseService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IFFMpegService _fFMpegService;
    private readonly ObservableCollection<StorageDir> _storages = new();

    public StorageService(ICrypto crypto, IDataBaseService dataBaseService, IViewPlatforms viewPlatforms, IFFMpegService fFMpegService)
    {
        _crypto = crypto;
        _dataBaseService = dataBaseService;
        _viewPlatforms = viewPlatforms;
        _fFMpegService = fFMpegService;
        Storages = new(_storages);
    }

    public string AppDir => FileSystem.Current.AppDataDirectory;
    public ReadOnlyObservableCollection<StorageDir> Storages { get; }
    public StorageDir? CurrentStorage { get; set; }

    public async Task<StorageDir[]> GetStorages()
    {
        StorageDir[] storages;
        string? json = Preferences.Default.Get<string>("storages", null);
        if (json != null)
        {
            storages = JsonSerializer.Deserialize<StorageDir[]>(json)!;
        }
        else
        {
            storages = [];
        }

        _storages.Clear();
        foreach (var storage in storages)
        {
            _storages.Add(storage);
        }

        return storages;
    }

    public async Task<AppResponse> AddStorage(StorageDir cell, string? password)
    {
        if (!Directory.Exists(cell.Path))
            return AppResponse.Error($"No such directory to path \"{cell.Path}\"");

        const string index = "index";
        string indexPath = Path.Combine(cell.Path, index);
        if (File.Exists(indexPath))
            return AppResponse.Error($"Directory to path \"{cell.Path}\" is already use by other STORAGE");

        var dbRes = await _dataBaseService.CreateIndexStorage(cell, password);
        if (dbRes.IsFault)
            return dbRes.AsError;

        _storages.Add(cell);
        string json = JsonSerializer.Serialize(_storages);
        Preferences.Default.Set("storages", json);
        return AppResponse.OK;
    }

    public AppResponse Import(string name, string directoryPath)
    {
        var conflict = _storages.FirstOrDefault(x => x.Path == name);
        if (conflict != null)
            return AppResponse.Error("This storage is now exist");

        var neww = new StorageDir
        {
            Guid = Guid.NewGuid(),
            Name = name,
            Path = directoryPath,
        };

        _storages.Add(neww);
        string json = JsonSerializer.Serialize(_storages);
        Preferences.Default.Set("storages", json);
        return AppResponse.OK;
    }

    public async Task<AppResponse> DeleteStorage(StorageDir storageCell)
    {
        _storages.Remove(storageCell);
        string json = JsonSerializer.Serialize(_storages);
        Preferences.Default.Set("storages", json);

        if (!Directory.Exists(storageCell.Path))
            return AppResponse.OK;

        string[] files = Directory.GetFiles(storageCell.Path);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        int undeleted = 0;
        await Parallel.ForEachAsync(files, async (x, cancel) =>
        {
            if (!x.Contains("index") && !Guid.TryParse(x, out _))
                return;

            int tryCount = 10;

            while (tryCount > 0)
            {
                try
                {
                    File.Delete(x);
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Problem delete storage element dir: " + ex);
                    await Task.Delay(1000);
                    tryCount--;
                }
            }

            if (tryCount == 0)
                undeleted++;
        });

        if (undeleted > 0)
            return AppResponse.Error("The Storage was deleted, but some files could not be deleted. Please remove them manually");

        return AppResponse.OK;
    }

    public async Task<AppResponse> SaveStorageFile(StorageDir storage, StorageFile unsavedFile, string password, IFileUnlocker? unlockFile)
    {
        string filePath = unsavedFile.FilePath;

        // check exists
        if (!File.Exists(filePath))
            return AppResponse.Error($"Failed to save file {filePath}. File is not exists");

        // check file can read
        Exception? exLock = null;
        int tryCount = 5;
        FileStream fileStream = null!;
        while (tryCount > 0)
        {
            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                break;
            }
            catch (Exception ex)
            {
                tryCount--;
                exLock = ex;
                await Task.Delay(500);
            }
        }

        if (tryCount == 0)
            return AppResponse.Error("No access to target file", 52, exLock);

        var handle = MediaPresentVm.ResolveFormat(filePath);
        var guid = Guid.NewGuid();
        string outputPath = Path.Combine(storage.Path, guid.ToString());

        // write real file
        switch (handle)
        {
            case BlindCatCore.Enums.MediaFormats.Mp4:
            case BlindCatCore.Enums.MediaFormats.Mov:
                using (var mem = new MemoryStream())
                {
                    var res = await _fFMpegService.FixMoovMp4(filePath, mem);
                    if (res.IsFault)
                        return res.AsError;

                    if (res.Result.UseStream)
                    {
                        mem.Position = 0;
                        await _crypto.EncryptFile(mem, outputPath, password);
                    }
                    else if (res.Result.UseOriginalFile)
                    {
                        await _crypto.EncryptFile(filePath, outputPath, password);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                break;
            default:
                await _crypto.EncryptFile(filePath, outputPath, password);
                break;
        }

        unsavedFile.FilePath = outputPath;
        unsavedFile.Guid = guid;
        unsavedFile.Storage = storage;
        unsavedFile.CachedMediaFormat = handle;
        unsavedFile.CachedHandlerType = MediaPresentVm.ResolveHandler(handle);
        //unsavedFile.Name = Path.GetFileNameWithoutExtension(filePath);

        // write DB
        var addFile = await _dataBaseService.AddContent(storage.PathIndex, password, unsavedFile);
        if (addFile.IsFault)
        {
            File.Delete(outputPath);
            return addFile.AsError;
        }

        // dispose
        fileStream.Dispose();

        int deleteTryCount = 5;
        Exception? deleteEx = null;
        while (deleteTryCount > 0)
        {
            try
            {
                if (unlockFile != null)
                {
                    await unlockFile.UnlockFile(filePath);
                }
                File.Delete(filePath);
                break;
            }
            catch (Exception ex)
            {
                deleteTryCount--;
                deleteEx = ex;
                await Task.Delay(500);
            }
        }

        if (deleteTryCount == 0)
        {
            // cancel operation
            File.Delete(outputPath);
            await _dataBaseService.DeleteContent(storage.PathIndex, password, guid);
            return AppResponse.Error("Cant delete origin file,", 338, deleteEx);
        }

        unsavedFile.IsIndexed = true;
        unsavedFile.IsTemp = false;
        storage.Controller?.AddFile(unsavedFile);
        return AppResponse.OK;
    }

    public async Task<AppResponse> UpdateStorageFile(StorageDir storage, StorageFile file, string password)
    {
        var dbRes = await _dataBaseService.UpdateContent(storage.PathIndex, password, file);
        if (dbRes.IsFault)
            return dbRes.AsError;

        var ctrl = storage.Controller;
        if (ctrl != null && ctrl.IsInitialized && file.Tags.Length > 0)
        {
            await ctrl.AddTags(file.Tags);
        }

        file.IsIndexed = true;
        return AppResponse.OK;
    }

    public async Task<AppResponse> DeleteFile(ISourceFile file)
    {
        var storage = file.TempStorageFile!.Storage;
        string password = storage.Password!;
        var del = await _dataBaseService.DeleteContent(storage.PathIndex, password, file.TempStorageFile!.Guid, 
            async() =>
            {
                if (Path.Exists(file.FilePreview))
                {
                    File.Delete(file.FilePreview);
                }

                if (Path.Exists(file.FilePath))
                {
                    File.Delete(file.FilePath);
                }

                return AppResponse.OK;
            });

        if (del.IsFault)
            return del.AsError;

        return AppResponse.OK;
    }

    public Task<bool> CheckPasswordCorrect(StorageDir storage, [NotNullWhen(true)] string? password)
    {
        return _dataBaseService.CheckPasswordValid(storage.PathIndex, password);
    }

    public async Task<bool> ExportFiles(string[] files)
    {
        foreach (var file in files)
        {
            byte[] b = await File.ReadAllBytesAsync(file);
            await SavePictureService.SavePicture(b, file);
        }

        return true;
    }

    public async Task<bool> WriteSrc(string galleryName, string fileName, byte[] dat)
    {
        string appDir = FileSystem.Current.AppDataDirectory;
        string gals = Path.Combine(appDir, "gals");
        if (!Directory.Exists(gals))
            Directory.CreateDirectory(gals);

        string galsGal = Path.Combine(gals, galleryName);
        if (!Directory.Exists(galsGal))
            Directory.CreateDirectory(galsGal);

        string galsGalFile = Path.Combine(galsGal, fileName);

        if (File.Exists(galsGalFile))
            File.Delete(galsGalFile);

        using var file = File.Create(galsGalFile);
        await file.WriteAsync(dat, 0, dat.Length);
        return true;
    }

    public async Task<bool> WriteSrc(string galleryName, string fileName, Stream stream, Action<int> callback)
    {
        string appDir = FileSystem.Current.AppDataDirectory;
        string gals = Path.Combine(appDir, "gals");
        if (!Directory.Exists(gals))
            Directory.CreateDirectory(gals);

        string galsGal = Path.Combine(gals, galleryName);
        if (!Directory.Exists(galsGal))
            Directory.CreateDirectory(galsGal);

        string galsGalFile = Path.Combine(galsGal, fileName);

        if (File.Exists(galsGalFile))
            File.Delete(galsGalFile);

        using var file = File.Create(galsGalFile);
        int totalRead = 0;
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }
                else
                {
                    await file.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    callback?.Invoke(totalRead);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
        return true;
    }

    public async Task<AppResponse> InitStorage(StorageDir _storage, string password, CancellationToken cancel)
    {
        var res = await ReadObjects(_storage, cancel);
        if (res.IsFault)
            return res.AsError;

        if (res.Result == null)
            return AppResponse.OK;

        var list = new ObservableCollection<StorageFile>(res.Result);

        _storage.Controller = new StorageDirController
        {
            Password = password,
        };
        await _storage.Controller.InitFiles(res.Result);
        return AppResponse.OK;
    }

    private async Task<AppResponse<StorageFile[]>> ReadObjects(StorageDir _storage, CancellationToken token)
    {
        string[] files = Directory.GetFiles(_storage.Path);

        var realFiles = new List<StorageFile>();
        foreach (string file in files)
        {
            string ext = Path.GetExtension(file);
            if (!string.IsNullOrEmpty(ext))
                continue;

            string name = Path.GetFileName(file);
            if (!Guid.TryParse(name, out var id))
                continue;

            string? previewPath = Path.Combine(_storage.Path, "tmls", name);
            if (!File.Exists(previewPath))
                previewPath = null;

            var n = new StorageFile
            {
                Guid = id,
                FilePath = file,
                Storage = _storage,
                IsIndexed = false,
                FilePreview = previewPath,
            };
            realFiles.Add(n);
        };

        var listDb = await _dataBaseService.GetFiles(_storage.PathIndex, _storage.Password);

        if (token.IsCancellationRequested)
            return AppResponse.Canceled;

        if (listDb.IsFault)
            return listDb.AsError;

        var a = realFiles.Select(x => x.Guid);
        var b = listDb.Result.Select(x => x.Guid);
        var expecpts = await FindMissingFilesAsync(a, b);

        if (token.IsCancellationRequested)
            return AppResponse.Canceled;

        var resultItems = new ConcurrentBag<StorageFile>();

        // Беспорядок в БД:
        // Запись в БД есть, а файла нету
        if (expecpts.a.Length > 0)
        {
            foreach (var guid in expecpts.a)
            {
                var dbRow = listDb.Result.First(x => x.Guid == guid);
                dbRow.IsNoFile = true;
                dbRow.Storage = _storage;
                resultItems.Add(dbRow);
            }
        }

        // Беспорядок в files:
        // Файл есть, а записи в БД нету
        if (expecpts.b.Length > 0)
        {
            foreach (var guid in expecpts.b)
            {
                var file = realFiles.First(x => x.Guid == guid);
                file.IsNoDBRow = true;
                file.Name = "<NO_DB_ROW>";
                resultItems.Add(file);
            }
        }

        // сопоставляем
        await Parallel.ForEachAsync(listDb.Result, async (dbItem, cancel) =>
        {
            var matchFile = realFiles.FirstOrDefault(x => x.Guid == dbItem.Guid);
            if (matchFile != null)
            {
                matchFile.IsIndexed = true;
                matchFile.Name = dbItem.Name;
                matchFile.Description = dbItem.Description;
                matchFile.Tags = dbItem.Tags;
                matchFile.Artist = dbItem.Artist;
                matchFile.CachedMediaFormat = dbItem.CachedMediaFormat;
                matchFile.DateLastIndex = dbItem.DateLastIndex;
                matchFile.DateInitIndex = dbItem.DateInitIndex;
                matchFile.FilePreview = dbItem.FilePreview;
                resultItems.Add(matchFile);
            }
        });

        var resultFiles = resultItems.Order(new Sorting()).ToArray();

        if (token.IsCancellationRequested)
            return AppResponse.Canceled;

        return AppResponse.Result(resultFiles);
    }

    public async Task<bool> CheckStorageDir(string indexFilePath)
    {
        var res = await _dataBaseService.CheckMetaKeyExist(indexFilePath, "MGE_jtov");
        if (res.IsSuccess)
            return true;

        return false;
    }


    public static async Task<(Guid[] a, Guid[] b)> FindMissingFilesAsync(IEnumerable<Guid> a, IEnumerable<Guid> b)
    {
        return await Task.Run(() =>
        {
            var filesInFolder1 = new HashSet<Guid>(a);
            var filesInFolder2 = new HashSet<Guid>(b);

            var missingInFolder1 = filesInFolder2.Except(filesInFolder1).ToArray();
            var missingInFolder2 = filesInFolder1.Except(filesInFolder2).ToArray();
            return (missingInFolder1, missingInFolder2);
        });
    }

    public static class SavePictureService
    {
        public static async Task<bool> SavePicture(byte[] arr, string imageName)
        {
#if ANDROID
            var contentValues = new ContentValues();
            contentValues.Put(MediaStore.IMediaColumns.DisplayName, imageName);
            contentValues.Put(MediaStore.Files.IFileColumns.MimeType, "image/png");
            contentValues.Put(MediaStore.IMediaColumns.RelativePath, "Pictures/relativePath");
            try
            {
                var context = Android.App.Application.Context;
                using var uri = context.ContentResolver!.Insert(MediaStore.Images.Media.ExternalContentUri, contentValues);
                using var output = context.ContentResolver.OpenOutputStream(uri)!;
                await output.WriteAsync(arr, 0, arr.Length);
                output.Flush();
                output.Close();
            }
            catch (System.Exception ex)
            {
                Console.Write(ex.ToString());
                return false;
            }
            contentValues.Put(MediaStore.IMediaColumns.IsPending, 1);
            return true;
#endif
            return false;
        }
    }

    private class Sorting : IComparer<StorageFile>
    {
        public int Compare(StorageFile a, StorageFile b)
        {
            if (a.HasError || b.HasError)
            {
                if (a.HasError && !b.HasError)
                    return -1;
                else if (!a.HasError && b.HasError)
                    return 1;
                else
                    return 0;
            }

            if (a.DateInitIndex < b.DateInitIndex)
                return 1;
            else if (a.DateInitIndex > b.DateInitIndex)
                return -1;
            else
                return 0;
        }
    }
}
