using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatData;
using BlindCatData.Models;
using Microsoft.EntityFrameworkCore;
using BlindCatCore.Extensions;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlindCatMaui.Services;

public class DataBaseService : IDataBaseService
{
    private readonly ICrypto _crypto;

    public DataBaseService(ICrypto crypto)
    {
        _crypto = crypto;
    }

    private void Map(StorageFile source, ContentStorageDb destination, string password)
    {
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

        // date last index
        destination.DateLastIndex = DateTime.Now.Ticks
            .Encrypt(password, _crypto);
    }

    public async Task<AppResponse> AddContent(string dbPath, string password, StorageFile file)
    {
        try
        {
            using var db = new MauiDbContext(dbPath);
            var dbAdd = new ContentStorageDb
            {
                Guid = file.Guid,

                MediaFormat = file
                    .CachedMediaFormat
                    .ToString()
                    .Encrypt(password, _crypto),

                DateIndex = DateTime.Now
                    .Ticks
                    .Encrypt(password, _crypto),
            };

            Map(file, dbAdd, password);

            db.Contents.Add(dbAdd);
            await db.SaveChangesAsync();
            return AppResponse.OK;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail add content to db", 44114, ex);
        }
    }

    public async Task<AppResponse> UpdateContent(string dataBasePath, string password, StorageFile file)
    {
        using var db = new MauiDbContext(dataBasePath);
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

        using var db = new MauiDbContext(dataBasePath);
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

        using var db = new MauiDbContext(dataBasePath);
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
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
                return AppResponse.OK;
            }
            catch (Exception ex)
            {
                return AppResponse.Error("DB error", 671, ex);
            }
        }
    }

    public async Task<bool> CheckPasswordValid(string pathIndex, string? password)
    {
        if (!Path.Exists(pathIndex))
            return true;

        using var db = new MauiDbContext(pathIndex);
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
        using var db = new MauiDbContext(cell.PathIndex);
        var metaKey1 = await db.Meta.FirstOrDefaultAsync();
        if (metaKey1 != null)
            return AppResponse.Error("Index db already has been initialized (Meta.MGE_jtov)");

        string v;
        if (string.IsNullOrEmpty(password))
        {
            v = "?";
        }
        else
        {
            v = _crypto.EncryptString("Data can be decrypted!", password);
        }

        metaKey1 = new MetaDb
        {
            Key = "MGE_jtov",
            Value = v,
        };
        db.Meta.Add(metaKey1);
        await db.SaveChangesAsync();

        return AppResponse.OK;
    }

    public async Task ForceDropConnect(string pathIndex)
    {
        using (var context = new MauiDbContext(pathIndex))
        {
            context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");
        }
        await Task.Delay(500);
    }

    public async Task<AppResponse<StorageFile[]>> GetFiles(string pathIndex, string? password)
    {
        using var db = new MauiDbContext(pathIndex);
        var dbItems = await db.Contents.ToArrayAsync();
        try
        {
            var files = new StorageFile[dbItems.Length];
            for (int i = 0; i < dbItems.Length; i++)
            {
                var dbItem = dbItems[i];

                // tags
                string? tagsRaw = dbItem.Tags.Decrypt(password, _crypto);
                string[] tags = tagsRaw?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

                // media format
                string? mediaFormat = dbItem.MediaFormat.Decrypt(password, _crypto);
                if (!Enum.TryParse<MediaFormats>(mediaFormat, out var format))
                    format = MediaFormats.Unknown;

                // name
                string? name = dbItem.Name.Decrypt(password, _crypto);

                // artist
                string? artist = dbItem.Artist.Decrypt(password, _crypto);

                // description
                string? description = dbItem.Description.Decrypt(password, _crypto);

                // date last index
                long? dateLastIndexTicks = dbItem.DateLastIndex.DecryptInt64(password, _crypto);
                DateTime? dateLastLastIndex = dateLastIndexTicks != null ? DateTime.FromBinary(dateLastIndexTicks.Value) : null;

                // date intex
                long? dateIndexTicks = dbItem.DateIndex.DecryptInt64(password, _crypto);
                DateTime? dateIndex = dateIndexTicks != null ? DateTime.FromBinary(dateIndexTicks.Value) : null;

                string guidFile = dbItem.Guid.ToString();
                var file = new StorageFile
                {
                    FilePath = Path.Combine(pathIndex, guidFile),
                    Guid = dbItem.Guid,
                    Name = name,
                    Artist = artist,
                    Description = description,
                    Tags = tags,
                    CachedMediaFormat = format,
                    CachedHandlerType = BlindCatCore.ViewModels.MediaPresentVm.ResolveHandler(format),
                    DateLastIndex = dateLastLastIndex,
                    DateInitIndex = dateIndex,
                    FilePreview = Path.Combine(pathIndex, "tmls", guidFile),
                    Storage = null,
                };
                files[i] = file;
            }

            return AppResponse.Result(files);
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Error parse data from db", 3571, ex);
        }
    }

    public async Task<AppResponse> CheckMetaKeyExist(string pathIndex, string keyName)
    {
        using var db = MauiDbContext.JustConnect(pathIndex);
        var m =await db.Meta.FirstOrDefaultAsync(x => x.Key == keyName);
        if (m == null)
            return AppResponse.Error($"No match key {keyName}");

        return AppResponse.OK;
    }
}