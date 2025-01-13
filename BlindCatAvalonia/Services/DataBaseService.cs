using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatData;
using BlindCatData.Models;
using Microsoft.EntityFrameworkCore;
using BlindCatCore.Extensions;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Threading;

namespace BlindCatAvalonia.Services;

public class DataBaseService : IDataBaseService
{
    private readonly ICrypto _crypto;

    public DataBaseService(ICrypto crypto)
    {
        _crypto = crypto;
    }

    private void Map(StorageFile source, ContentStorageDb destination, string? password)
    {
        // guid
        destination.Guid = source.Guid;

        // name
        destination.Name = source.Name
            .Encrypt(password, _crypto);

        // artist
        destination.Artist = source.Artist
            .Encrypt(password, _crypto);

        // description
        destination.Description = source.Description
            .Encrypt(password, _crypto);

        // tags
        destination.Tags = string.Join(',', source.Tags)
            .Encrypt(password, _crypto);

        // media format
        destination.MediaFormat = source.CachedMediaFormat
            .ToString()
            .Encrypt(password, _crypto);

        // date index
        destination.DateIndex = source.DateCreated
            .EncryptDate(password, _crypto);

        // date last index
        destination.DateLastIndex = source.DateModified
            .EncryptDate(password, _crypto);

        // encryption type
        string? encMethod = source.EncryptionMethod == EncryptionMethods.None ? null : source.EncryptionMethod.ToString();
        destination.EncryptionType = encMethod
            .Encrypt(password, _crypto);

        // parent album guid
        destination.Parent = source.ParentAlbumGuid;
    }

    private void Map(ContentStorageDb source, StorageFile destination, string? password)
    {
        // guid
        destination.Guid = source.Guid;

        // name
        destination.Name = source.Name
            .Decrypt(password, _crypto);

        // artist
        destination.Artist = source.Artist
            .Decrypt(password, _crypto);

        // description
        destination.Description = source.Description
            .Decrypt(password, _crypto);

        // tags
        destination.Tags = source.Tags
            .Decrypt(password, _crypto)?
            .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        // media format
        destination.CachedMediaFormat = source.MediaFormat
            .Decrypt(password, _crypto)
            .TryParseEnum(MediaFormats.Unknown);

        // date index
        destination.DateCreated = source.DateIndex
            .DecryptDate(password, _crypto);

        // date last index
        destination.DateModified = source.DateLastIndex
            .DecryptDate(password, _crypto);

        // encryption type
        destination.EncryptionMethod = source.EncryptionType
            .Decrypt(password, _crypto)
            .TryParseEnum(EncryptionMethods.Unknown);

        // parent album guid
        destination.ParentAlbumGuid = source.Parent;
    }

    private void MapAlbum(AlbumStorageDb source, StorageAlbum destination, string? password)
    {
        // guid
        destination.Guid = source.Guid;

        // name
        destination.Name = source.Name
            .Decrypt(password, _crypto);

        // artist
        destination.Artist = source.Artist
            .Decrypt(password, _crypto);

        // description
        destination.Description = source.Description
            .Decrypt(password, _crypto);

        // tags
        destination.Tags = source.Tags
            .Decrypt(password, _crypto)?
            .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        // date created
        destination.DateCreated = source.DateCreated
            .DecryptDate(password, _crypto);

        // date modified
        destination.DateModified = source.DateModified
            .DecryptDate(password, _crypto);

        // cover guid
        destination.CoverGuid = source.CoverGuid;
    }

    private void MapAlbum(StorageAlbum source, AlbumStorageDb destination, string? password)
    {
        // guid
        destination.Guid = source.Guid;

        // name
        destination.Name = source.Name
            .Encrypt(password, _crypto);

        // artist
        destination.Artist = source.Artist
            .Encrypt(password, _crypto);

        // description
        destination.Description = source.Description
            .Encrypt(password, _crypto);

        // tags
        destination.Tags = string.Join(',', source.Tags)
            .Encrypt(password, _crypto);

        // date created
        destination.DateCreated = source.DateCreated
            .EncryptDate(password, _crypto);

        // date modified
        destination.DateModified = source.DateModified
            .EncryptDate(password, _crypto);

        // cover guid
        destination.CoverGuid = source.CoverGuid;
    }

    public async Task<AppResponse> AddContent(string dbPath, string? password, StorageFile file, Func<Task<AppResponse>> body)
    {
        using var db = BlindCatDbContext.JustConnect(dbPath);
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var dbAdd = new ContentStorageDb
            {
                Guid = file.Guid,
                MediaFormat = null,
            };

            Map(file, dbAdd, password);

            await db.Contents.AddAsync(dbAdd);
            await db.SaveChangesAsync();

            var err = await body();
            if (err.IsFault)
            {
                transaction.Rollback();
                return err.AsError;
            }

            transaction.Commit();
            return AppResponse.OK;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail add content to db", 44114, ex);
        }
        //try
        //{
        //    using var db = new MauiDbContext(dbPath);
        //    var dbAdd = new ContentStorageDb
        //    {
        //        Guid = file.Guid,
        //        MediaFormat = null,
        //    };

        //    Map(file, dbAdd, password);

        //    db.Contents.Add(dbAdd);
        //    await db.SaveChangesAsync();
        //    return AppResponse.OK;
        //}
        //catch (Exception ex)
        //{
        //    return AppResponse.Error("Fail add content to db", 44114, ex);
        //}
    }

    public async Task<AppResponse> UpdateContent(string dataBasePath, string password, StorageFile file)
    {
        using var db = new BlindCatDbContext(dataBasePath);
        var match = await db.Contents.FindAsync(file.Guid);
        if (match == null)
        {
            return AppResponse.Error("Fail to update storage file, because db no find the row");
        }

        Map(file, match, password);

        db.Contents.Update(match);

        await db.SaveChangesAsync();
        return AppResponse.OK;
    }

    public async Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid)
    {
        if (!await CheckPasswordValid(dataBasePath, password))
        {
            return AppResponse.Error("Invalid password db");
        }

        using var db = new BlindCatDbContext(dataBasePath);
        var match = await db.Contents.FindAsync(fileGuid);
        if (match == null)
        {
            return AppResponse.Error("Fail to delete storage file, because db no find the row");
        }

        db.Contents.Remove(match);

        await db.SaveChangesAsync();
        return AppResponse.OK;
    }

    public async Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid, Func<Task<AppResponse>> body)
    {
        if (!await CheckPasswordValid(dataBasePath, password))
        {
            return AppResponse.Error("Invalid password db");
        }

        using var db = new BlindCatDbContext(dataBasePath);
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var match = await db.Contents.FindAsync(fileGuid);
            if (match == null)
            {
                return AppResponse.Error("Fail to delete storage file, because db no find the row");
            }

            db.Contents.Remove(match);

            var err = await body();
            if (err.IsFault)
            {
                transaction.Rollback();
                return err.AsError;
            }

            await db.SaveChangesAsync();

            transaction.Commit();
            return AppResponse.OK;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("DB error", 671, ex);
        }
    }

    public async Task<AppResponse> CreateAlbum(string dataBasePath, string password, StorageAlbum album)
    {
        using var db = BlindCatDbContext.JustConnect(dataBasePath);
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var dbAdd = new AlbumStorageDb
            {
                Guid = default,
            };

            MapAlbum(album, dbAdd, password);

            await db.Albums.AddAsync(dbAdd);
            await db.SaveChangesAsync();

            foreach (var item in album.Contents)
            {
                var contentDb = await db.Contents.FindAsync(item);
                if (contentDb == null)
                {
                    transaction.Rollback();
                    return AppResponse.Error("Fail to create new album because some files no contains into Database");
                }

                contentDb.Parent = dbAdd.Guid;
                db.Contents.Update(contentDb);
            }
            await db.SaveChangesAsync();

            transaction.Commit();
            return AppResponse.OK;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail add content to db", 44114, ex);
        }
    }

    public async Task<AppResponse> DeleteAlbum(string dataBasePath, string password, StorageAlbum album)
    {
        using var db = BlindCatDbContext.JustConnect(dataBasePath);
        try
        {
            var match = await db.Albums.FindAsync(album.Guid);
            if (match == null)
                return AppResponse.Error("Fail to delete album entity db because no find");

            db.Albums.Remove(match);
            await db.SaveChangesAsync();
            return AppResponse.OK;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail to delete album entity db", 44114, ex);
        }
    }

    public async Task<bool> CheckPasswordValid(string pathIndex, string? password)
    {
        if (!Path.Exists(pathIndex))
            return true;

        using var db = new BlindCatDbContext(pathIndex);
        var metaKey = await db.Meta.FirstOrDefaultAsync();
        if (metaKey == null)
            return false;

        string rightValue;
        string dec;
        if (string.IsNullOrEmpty(password))
        {
            dec = metaKey.Value;
            rightValue = "?";
        }
        else
        {
            dec = _crypto.DecryptString(metaKey.Value, password);
            rightValue = "Data can be decrypted!";
        }

        bool isCorrect = metaKey.Key == "MGE_jtov" && dec == rightValue;
        if (!isCorrect)
            await Task.Delay(1000);

        return isCorrect;
    }

    public async Task<AppResponse> CreateIndexStorage(StorageDir cell, string? password)
    {
        using var db = new BlindCatDbContext(cell.PathIndex);
        var metaKey1 = await db.Meta.FirstOrDefaultAsync();
        if (metaKey1 != null)
            return AppResponse.Error("Index db already has been initialized (Meta.MGE_jtov)");

        string v;
        string encflag;
        if (string.IsNullOrEmpty(password))
        {
            v = "?";
            encflag = "0";
        }
        else
        {
            v = _crypto.EncryptString("Data can be decrypted!", password);
            encflag = "1";
        }

        metaKey1 = new MetaDb
        {
            Key = "MGE_jtov",
            Value = v,
        };
        db.Meta.Add(metaKey1);


        metaKey1 = new MetaDb
        {
            Key = "EncryptedDB",
            Value = encflag,
        };
        db.Meta.Add(metaKey1);
        await db.SaveChangesAsync();

        return AppResponse.OK;
    }

    public async Task ForceDropConnect(string pathIndex)
    {
        //using (var context = new MauiDbContext(pathIndex))
        //{
        //    context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");
        //}
        SqliteConnection.ClearAllPools();
        await Task.Delay(500);
    }

    public async Task<AppResponse<StorageFile[]>> GetFiles(string pathIndex, string? password, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested)
            return AppResponse.Canceled;

        if (!File.Exists(pathIndex))
            return AppResponse.Error("DB index file is not exists", 40023);

        using var db = new BlindCatDbContext(pathIndex);
        var res = await GetFilesInternal(pathIndex, password, db, cancel);
        if (res.IsFault)
            return res.AsError;

        // AUTO FIX
        bool save = false;
        for (int i = 0; i < res.Result.AppItems.Length; i++)
        {
            var appItem = res.Result.AppItems[i];
            var dbItem = res.Result.DbItems[i];

            if (appItem.DateCreated == null)
            {
                var created = File.GetCreationTime(appItem.FilePath);
                appItem.DateCreated = created;
                appItem.DateModified = created;

                Map(appItem, dbItem, password);
                db.Contents.Update(dbItem);
                save = true;
            }
        }

        if (save)
            await db.SaveChangesAsync();

        return AppResponse.Result(res.Result.AppItems);
    }

    public async Task<AppResponse<StorageAlbum[]>> GetAlbums(string pathIndex, string? password, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested)
            return AppResponse.Canceled;

        if (!File.Exists(pathIndex))
            return AppResponse.Error("DB index file is not exists", 40023);

        using var db = BlindCatDbContext.JustConnect(pathIndex);

        // cote
        string? pathDir = Path.GetDirectoryName(pathIndex);
        if (pathDir == null)
            return AppResponse.Error($"Fail to get directory path by \"{pathDir}\"", 4040001);

        var dbItems = await db.Albums.ToArrayAsync(cancel);
        var files = new StorageAlbum[dbItems.Length];

        try
        {
            await TaskExt.Run(() =>
            {
                for (int i = 0; i < dbItems.Length; i++)
                {
                    var dbItem = dbItems[i];
                    string guidFile = dbItem.Guid.ToString();
                    var appItem = new StorageAlbum
                    {
                        SourceDir = null,
                    };

                    MapAlbum(dbItem, appItem, password);

                    files[i] = appItem;
                }
            }, cancel);

            if (cancel.IsCancellationRequested)
                return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Error parse data from db", 3571, ex);
        }

        //// AUTO FIX
        //bool save = false;
        //for (int i = 0; i < files.Length; i++)
        //{
        //    var appItem = res.Result.AppItems[i];
        //    var dbItem = res.Result.DbItems[i];

        //    if (appItem.DateCreated == null)
        //    {
        //        var created = File.GetCreationTime(appItem.FilePath);
        //        appItem.DateCreated = created;
        //        appItem.DateModified = created;

        //        Map(appItem, dbItem, password);
        //        db.Contents.Update(dbItem);
        //        save = true;
        //    }
        //}

        //if (save)
        //    await db.SaveChangesAsync();

        return AppResponse.Result(files);
    }

    private async Task<AppResponse<ItemsBundle>> GetFilesInternal(string pathIndex, string? password, BlindCatDbContext db, CancellationToken cancel)
    {
        string? pathDir = Path.GetDirectoryName(pathIndex);
        if (pathDir == null)
            return AppResponse.Error($"Fail to get directory path by \"{pathDir}\"", 4040001);

        var dbItems = await db.Contents.ToArrayAsync(cancel);

        try
        {
            var files = new StorageFile[dbItems.Length];
            await TaskExt.Run(() =>
            {
                for (int i = 0; i < dbItems.Length; i++)
                {
                    var dbItem = dbItems[i];
                    string guidFile = dbItem.Guid.ToString();
                    var appItem = new StorageFile
                    {
                        FilePath = Path.Combine(pathDir, guidFile),
                        FilePreview = Path.Combine(pathDir, "tmls", guidFile),
                        Storage = null,
                    };

                    Map(dbItem, appItem, password);

                    files[i] = appItem;
                }
            }, cancel);

            if (cancel.IsCancellationRequested)
                return AppResponse.Canceled;

            return AppResponse.Result(new ItemsBundle
            {
                AppItems = files,
                DbItems = dbItems,
            });
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Error parse data from db", 3571, ex);
        }
    }

    public async Task<AppResponse<string>> CheckMetaKeyExist(string pathIndex, string keyName)
    {
        using var db = BlindCatDbContext.JustConnect(pathIndex);
        var m = await db.Meta.FirstOrDefaultAsync(x => x.Key == keyName);
        if (m == null)
            return AppResponse.Error($"No match key {keyName}");

        return AppResponse.Result(m.Value);
    }

    // todo добавить отмену
    public async Task<AppResponse> Encrypt(string dataBasePath, string password)
    {
        if (!Path.Exists(dataBasePath))
            return AppResponse.Error($"File \"{dataBasePath}\" not exists");

        if (string.IsNullOrWhiteSpace(password))
            return AppResponse.Error("Password can't be empty");

        bool isValid = await CheckPasswordValid(dataBasePath, password);
        if (!isValid)
            return AppResponse.Error("Incorrect password");

        using var db = BlindCatDbContext.JustConnect(dataBasePath);
        var res = await GetFilesInternal(dataBasePath, null, db, CancellationToken.None);
        if (res.IsFault)
            return res.AsError;

        for (int i = 0; i < res.Result.DbItems.Length; i++)
        {
            var appItem = res.Result.AppItems[i];
            var dbItem = res.Result.DbItems[i];
            Map(appItem, dbItem, password);
            db.Contents.Update(dbItem);
        }

        // decrypt flag
        var decKey = await db.Meta.FirstAsync(x => x.Key == "EncryptedDB");
        decKey.Value = "1";
        db.Meta.Update(decKey);

        await db.SaveChangesAsync();
        return AppResponse.OK;
    }

    // todo добавить отмену
    public async Task<AppResponse> Decrypt(string dataBasePath, string password)
    {
        if (!Path.Exists(dataBasePath))
            return AppResponse.Error($"File \"{dataBasePath}\" not exists");

        if (string.IsNullOrWhiteSpace(password))
            return AppResponse.Error("Password can't be empty");

        bool isValid = await CheckPasswordValid(dataBasePath, password);
        if (!isValid)
            return AppResponse.Error("Incorrect password");

        using var db = BlindCatDbContext.JustConnect(dataBasePath);
        var res = await GetFilesInternal(dataBasePath, password, db, CancellationToken.None);
        if (res.IsFault)
            return res.AsError;

        for (int i = 0; i < res.Result.DbItems.Length; i++)
        {
            var appItem = res.Result.AppItems[i];
            var dbItem = res.Result.DbItems[i];
            Map(appItem, dbItem, null);
            db.Contents.Update(dbItem);
        }

        // decrypt flag
        var decKey = await db.Meta.FirstAsync(x => x.Key == "EncryptedDB");
        decKey.Value = "0";
        db.Meta.Update(decKey);

        await db.SaveChangesAsync();
        return AppResponse.OK;
    }

    private class ItemsBundle
    {
        public required ContentStorageDb[] DbItems { get; set; }
        public required StorageFile[] AppItems { get; set; }
    }
}