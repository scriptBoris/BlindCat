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
using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using BlindCatAvalonia.Models;
using BlindCatAvalonia.Tools;

namespace BlindCatAvalonia.Services;

public class StorageService : IStorageService
{
    private readonly ICrypto _crypto;
    private readonly IDataBaseService _dataBaseService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IFFMpegService _fFMpegService;
    private readonly IConfig _config;
    private readonly ObservableCollection<StorageDir> _storages = new();

    public StorageService(
        ICrypto crypto,
        IDataBaseService dataBaseService,
        IViewPlatforms viewPlatforms,
        IFFMpegService fFMpegService,
        IConfig config)
    {
        _crypto = crypto;
        _dataBaseService = dataBaseService;
        _viewPlatforms = viewPlatforms;
        _fFMpegService = fFMpegService;
        _config = config;
        Storages = new(_storages);
    }

    public string AppDir => Environment.CurrentDirectory;
    public ReadOnlyObservableCollection<StorageDir> Storages { get; }
    public StorageDir? CurrentStorage { get; set; }

    public async Task<StorageDir[]> GetStorages()
    {
        var storages = _config.ReadJSON("storages", new StorageDir[] { });

        _storages.Clear();
        foreach (var storage in storages)
        {
            _storages.Add(storage);
        }

        return _storages.ToArray();
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
        _config.WriteJSON("storages", _storages);
        await _config.Save();

        return AppResponse.OK;
    }

    public async Task<AppResponse> Import(string name, string directoryPath)
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

        var enc = await _dataBaseService.CheckMetaKeyExist(neww.PathIndex, "EncryptedDB");
        if (enc.IsFault)
            return enc.AsError;

        // если данные не зашифрованные, шифруем
        if (enc.Result == "0")
        {
            string? password = await _viewPlatforms.ShowDialogPromtPassword("Inport",
                "This storage DB file contains unprotected raw data",
                "Inport & encrypt",
                "cancel",
                placeholder: "Required",
                null);
            if (password == null)
                return AppResponse.Canceled;

            var encRes = await _dataBaseService.Encrypt(neww.PathIndex, password);
            if (encRes.IsFault)
                return encRes.AsError;
        }

        _storages.Add(neww);
        _config.WriteJSON("storages", _storages);
        await _config.Save();


        return AppResponse.OK;
    }

    public async Task<AppResponse> DeleteStorage(StorageDir storageCell)
    {
        _storages.Remove(storageCell);
        _config.WriteJSON("storages", _storages);
        await _config.Save();

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

    public async Task<AppResponse> SaveStorageFile(StorageDir storage, ISourceFile _sourceFile, string password, IFileUnlocker? unlockFile)
    {
        StorageFile unsavedFile;
        if (_sourceFile is StorageFile sf)
        {
            throw new NotImplementedException();
            unsavedFile = sf;
            // todo в будущем сделать поддержку сохранять не индексированные файлы
        }
        else if (_sourceFile.TempStorageFile != null)
        {
            unsavedFile = _sourceFile.TempStorageFile;

            if (!unsavedFile.IsTemp || unsavedFile.IsIndexed)
                return AppResponse.Error("File already saved", 777476);
        }
        else
        {
            return AppResponse.Error("File no contains data for saving (TempStorageFile is null)", 777477);
        }

        string originFilePath = unsavedFile.FilePath;

        // check exists
        if (!File.Exists(originFilePath))
            return AppResponse.Error($"Failed to save file {originFilePath}. File is not exists");

        // check if file in storage dir
        if (Path.GetDirectoryName(originFilePath) == storage.Path)
            return AppResponse.Error($"Failed to save file {originFilePath}. File is already saved");

        var mediaFormat = MediaPresentVm.ResolveFormat(originFilePath);
        if (mediaFormat == BlindCatCore.Enums.MediaFormats.Unknown)
            return AppResponse.Error($"Failed to save file {originFilePath}. Unknown media format");

        if (!unsavedFile.IsTemp)
            return AppResponse.Error($"Failed to save file {originFilePath}. File is already saved");

        var guid = Guid.NewGuid();
        string outputPath = Path.Combine(storage.Path, guid.ToString());
        unsavedFile.FilePath = outputPath;
        unsavedFile.Guid = guid;
        unsavedFile.Storage = storage;
        unsavedFile.CachedMediaFormat = mediaFormat;
        unsavedFile.EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.dotnet;
        unsavedFile.DateCreated = DateTime.Now;
        unsavedFile.DateModified = DateTime.Now;

        string? savedPathThumbnail = null;
        string? savedPathEncrypted = null;

        async Task<AppResponse> FileOperation()
        {
            // try save thumbnail
            try
            {
                string dirThumbnails = Path.Combine(unsavedFile.Storage.Path, "tmls");
                if (!Directory.Exists(dirThumbnails))
                    Directory.CreateDirectory(dirThumbnails);

                var pathThumbnail = Path.Combine(dirThumbnails, guid.ToString());
                var enc = new EncryptionArgs { EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.None };
                using var thumbnailRes = await _fFMpegService.SaveThumbnail(originFilePath, mediaFormat, pathThumbnail, enc, password);
                if (thumbnailRes.IsFault)
                    return thumbnailRes.AsError;

                savedPathThumbnail = pathThumbnail;
            }
            catch (Exception ex)
            {
                return AppResponse.Error("Fail generate thumbnail", 851456, ex);
            }

            // write real file
            try
            {
                switch (mediaFormat)
                {
                    case BlindCatCore.Enums.MediaFormats.Mp4:
                    case BlindCatCore.Enums.MediaFormats.Mov:
                        using (var mem = new MemoryStream())
                        {
                            var res = await _fFMpegService.FixMoovMp4(originFilePath, mem);
                            if (res.IsFault)
                                return res.AsError;

                            if (res.Result.UseStream)
                            {
                                mem.Position = 0;
                                await _crypto.EncryptFile(mem, outputPath, password);
                            }
                            else if (res.Result.UseOriginalFile)
                            {
                                await _crypto.EncryptFile(originFilePath, outputPath, password);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                        break;
                    default:
                        await _crypto.EncryptFile(originFilePath, outputPath, password);
                        break;
                }

                savedPathEncrypted = outputPath;
            }
            catch (Exception ex)
            {
                return AppResponse.Error("Fail encrypt file", 421679, ex);
            }

            // try delete origin file
            int deleteTryCount = 5;
            Exception? deleteEx = null;
            while (deleteTryCount > 0)
            {
                try
                {
                    if (unlockFile != null)
                    {
                        await unlockFile.UnlockFile(originFilePath);
                    }
                    File.Delete(originFilePath);
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
                return AppResponse.Error("Cant delete origin file,", 338, deleteEx);

            //unsavedFile.IsIndexed = true;
            //unsavedFile.IsTemp = false;
            //storage.Controller?.AddFile(unsavedFile);
            return AppResponse.OK;
        }

        // write DB
        var addFile = await _dataBaseService.AddContent(storage.PathIndex, password, unsavedFile, FileOperation);
        if (addFile.IsFault)
        {
            if (savedPathThumbnail != null)
                try
                {
                    File.Delete(savedPathThumbnail);
                }
                catch (Exception) { }


            if (savedPathEncrypted != null)
                try
                {
                    File.Delete(savedPathEncrypted);
                }
                catch (Exception) { }

            unsavedFile.Guid = default;
            unsavedFile.FilePath = originFilePath;
            unsavedFile.Storage = null;
            unsavedFile.DateCreated = null;
            unsavedFile.DateModified = null;
            unsavedFile.CachedMediaFormat = BlindCatCore.Enums.MediaFormats.Unknown;
            unsavedFile.EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.Unknown;
            return addFile.AsError;
        }

        unsavedFile.IsIndexed = true;
        unsavedFile.IsTemp = false;
        storage.Controller?.AddFile(unsavedFile);
        _sourceFile.SourceDir?.Remove(_sourceFile);

        var ctrl = storage.Controller;
        if (ctrl != null && ctrl.IsInitialized && unsavedFile.Tags.Length > 0)
        {
            await ctrl.AddTags(unsavedFile.Tags);
        }
        return AppResponse.OK;
        #region old cote
        //// check file can read
        //Exception? exLock = null;
        //int tryCount = 5;
        //FileStream fileStream = null!;
        //while (tryCount > 0)
        //{
        //    try
        //    {
        //        fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        //        break;
        //    }
        //    catch (Exception ex)
        //    {
        //        tryCount--;
        //        exLock = ex;
        //        await Task.Delay(500);
        //    }
        //}

        //if (tryCount == 0)
        //    return AppResponse.Error("No access to target file", 52, exLock);

        //var mediaFormat = MediaPresentVm.ResolveFormat(filePath);
        //var guid = Guid.NewGuid();
        //string outputPath = Path.Combine(storage.Path, guid.ToString());

        //// try save thumbnail
        //try
        //{
        //    string dirThumbnails = Path.Combine(unsavedFile.Storage.Path, "tmls");
        //    if (!Directory.Exists(dirThumbnails))
        //        Directory.CreateDirectory(dirThumbnails);

        //    string pathThumbnail = Path.Combine(dirThumbnails, guid.ToString());

        //    var enc = new EncryptionArgs { EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.None };
        //    using var _ = await _fFMpegService.SaveThumbnail(filePath, mediaFormat, pathThumbnail, enc, password);
        //}
        //catch (Exception)
        //{
        //}

        //// write real file
        //switch (mediaFormat)
        //{
        //    case BlindCatCore.Enums.MediaFormats.Mp4:
        //    case BlindCatCore.Enums.MediaFormats.Mov:
        //        using (var mem = new MemoryStream())
        //        {
        //            var res = await _fFMpegService.FixMoovMp4(filePath, mem);
        //            if (res.IsFault)
        //                return res.AsError;

        //            if (res.Result.UseStream)
        //            {
        //                mem.Position = 0;
        //                await _crypto.EncryptFile(mem, outputPath, password);
        //            }
        //            else if (res.Result.UseOriginalFile)
        //            {
        //                await _crypto.EncryptFile(filePath, outputPath, password);
        //            }
        //            else
        //            {
        //                throw new NotImplementedException();
        //            }
        //        }
        //        break;
        //    default:
        //        await _crypto.EncryptFile(filePath, outputPath, password);
        //        break;
        //}

        //unsavedFile.FilePath = outputPath;
        //unsavedFile.Guid = guid;
        //unsavedFile.Storage = storage;
        //unsavedFile.CachedMediaFormat = mediaFormat;
        //unsavedFile.EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.dotnet;

        //if (unsavedFile.DateInitIndex == null)
        //    unsavedFile.DateInitIndex = DateTime.Now;

        //unsavedFile.DateLastIndex = DateTime.Now;

        //// write DB
        //var addFile = await _dataBaseService.AddContent(storage.PathIndex, password, unsavedFile);
        //if (addFile.IsFault)
        //{
        //    File.Delete(outputPath);
        //    return addFile.AsError;
        //}

        //// dispose
        //fileStream.Dispose();

        //int deleteTryCount = 5;
        //Exception? deleteEx = null;
        //while (deleteTryCount > 0)
        //{
        //    try
        //    {
        //        if (unlockFile != null)
        //        {
        //            await unlockFile.UnlockFile(filePath);
        //        }
        //        File.Delete(filePath);
        //        break;
        //    }
        //    catch (Exception ex)
        //    {
        //        deleteTryCount--;
        //        deleteEx = ex;
        //        await Task.Delay(500);
        //    }
        //}

        //if (deleteTryCount == 0)
        //{
        //    // cancel operation
        //    File.Delete(outputPath);
        //    await _dataBaseService.DeleteContent(storage.PathIndex, password, guid);
        //    return AppResponse.Error("Cant delete origin file,", 338, deleteEx);
        //}

        //unsavedFile.IsIndexed = true;
        //unsavedFile.IsTemp = false;
        //storage.Controller?.AddFile(unsavedFile);
        //return AppResponse.OK;
        #endregion old cote
    }

    public async Task<AppResponse> UpdateStorageFile(StorageDir storage, StorageFile file, string password)
    {
        if (file.DateCreated == null)
            file.DateCreated = DateTime.Now;

        file.DateModified = DateTime.Now;

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
            async () =>
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

    public Task<bool> ExportFiles(string[] files)
    {
        throw new NotImplementedException();
    }

    public Task<bool> WriteSrc(string galleryName, string fileName, byte[] dat)
    {
        throw new NotImplementedException();
    }

    public Task<bool> WriteSrc(string galleryName, string fileName, Stream stream, Action<int> callback)
    {
        throw new NotImplementedException();
    }

    public async Task<AppResponse> InitStorage(StorageDir _storage, string password, CancellationToken cancel)
    {
        var res = await ReadObjects(_dataBaseService, _storage, cancel);
        if (res.IsFault)
            return res.AsError;

        if (res.Result == null)
            return AppResponse.OK;

        _storage.Controller = new StorageDirController
        {
            Storage = _storage,
            Password = password,
        };
        await _storage.Controller.InitFiles(res.Result.HumanItems, res.Result.AllContentFiles);
        return AppResponse.OK;
    }

    private static async Task<AppResponse<ReadResult>> ReadObjects(IDataBaseService _dataBaseService, StorageDir _storage, CancellationToken token)
    {
        string[] files = Directory.GetFiles(_storage.Path);

        var realFiles = new List<StorageFile>();
        await TaskExt.Run(() =>
        {
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
        }, token);

        var unsortedContent = new ConcurrentBag<ISourceFile>();
        var unsortedResultItems = new ConcurrentBag<IStorageElement>();

        // читаем файлы в БД
        var listDb = await _dataBaseService.GetFiles(_storage.PathIndex, _storage.Password, token);
        if (listDb.IsFault)
            return listDb.AsError;

        // читаем альбомы в БД
        var albumsDb = await _dataBaseService.GetAlbums(_storage.PathIndex, _storage.Password, token);
        if (albumsDb.IsFault)
            return albumsDb.AsError;

        var albums = albumsDb.Result;
        foreach (var album in albums)
        {
            album.SourceDir = _storage;
            unsortedResultItems.Add(album);
        }

        var a = realFiles.Select(x => x.Guid);
        var b = listDb.Result.Select(x => x.Guid);
        var expecpts = await FindMissingFilesAsync(a, b);

        if (token.IsCancellationRequested)
            return AppResponse.Canceled;


        // Беспорядок в БД:
        // Запись в БД есть, а файла нету
        if (expecpts.a.Length > 0)
        {
            foreach (var guid in expecpts.a)
            {
                var dbRow = listDb.Result.First(x => x.Guid == guid);
                dbRow.IsErrorNoFile = true;
                dbRow.Storage = _storage;
                dbRow.EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.Unknown;
                
                unsortedContent.Add(dbRow);
                unsortedResultItems.Add(dbRow);
            }
        }

        // Беспорядок в files:
        // Файл есть, а записи в БД нету
        if (expecpts.b.Length > 0)
        {
            foreach (var guid in expecpts.b)
            {
                var file = realFiles.First(x => x.Guid == guid);
                file.IsErrorNoDBRow = true;
                file.Name = "<NO_DB_ROW>";
                file.EncryptionMethod = BlindCatCore.Enums.EncryptionMethods.Unknown;

                unsortedContent.Add(file);
                unsortedResultItems.Add(file);
            }
        }

        // сопоставляем
        await Parallel.ForEachAsync(listDb.Result, (dbItem, cancel) =>
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
                matchFile.DateCreated = dbItem.DateCreated;
                matchFile.DateModified = dbItem.DateModified;
                matchFile.FilePreview = dbItem.FilePreview;
                matchFile.EncryptionMethod = dbItem.EncryptionMethod;
                matchFile.ParentAlbumGuid = dbItem.ParentAlbumGuid;

                if (matchFile.ParentAlbumGuid != null)
                {
                    var parentAlbum = albums.FirstOrDefault(x => x.Guid == matchFile.ParentAlbumGuid);
                    if (parentAlbum != null)
                    {
                        parentAlbum.SafeAdd(matchFile);
                    }
                }
                else
                {
                    unsortedResultItems.Add(matchFile);
                }
                unsortedContent.Add(matchFile);
            }
            return ValueTask.CompletedTask;
        });

        IStorageElement[] sortedResultFiles = null!;
        await TaskExt.Run(() =>
        {
            sortedResultFiles = unsortedResultItems.Order(new Sorting()).ToArray();
        }, token);

        if (token.IsCancellationRequested)
            return AppResponse.Canceled;

        var result = new ReadResult
        {
            AllContentFiles = unsortedContent.ToArray(),
            HumanItems = sortedResultFiles,
        };
        return AppResponse.Result(result);
    }

    public async Task<bool> CheckStorageDir(string indexFilePath)
    {
        var res = await _dataBaseService.CheckMetaKeyExist(indexFilePath, "MGE_jtov");
        if (res.IsSuccess)
            return true;

        return false;
    }

    public async Task<AppResponse> ExportStorageDb(StorageDir storage, string dir, string? password)
    {
        string from = storage.PathIndex;
        string to = Path.Combine(dir, "index");
        await _dataBaseService.ForceDropConnect(from);

        File.Copy(from, to, true);
        await Task.Delay(500);
        return await _dataBaseService.Decrypt(to, password);
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

    private class Sorting : IComparer<IStorageElement>
    {
        public int Compare(IStorageElement a, IStorageElement b)
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

            if (a.DateCreated < b.DateCreated)
                return 1;
            else if (a.DateCreated > b.DateCreated)
                return -1;
            else
                return 0;
        }
    }

    private class ReadResult
    {
        public required IStorageElement[] HumanItems { get; set; }
        public required ISourceFile[] AllContentFiles { get; set; }
    }
}
