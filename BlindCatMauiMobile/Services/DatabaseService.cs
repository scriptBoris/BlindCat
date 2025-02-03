using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;

namespace BlindCatMauiMobile.Services;

public class DatabaseService : IDataBaseService
{
    public Task<AppResponse> AddContent(string dataBasePath, string? password, StorageFile contentStorageDb, Func<Task<AppResponse>> body)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> UpdateContent(string dataBasePath, string password, StorageFile contentStorageDb)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid, Func<Task<AppResponse>> body)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckPasswordValid(string pathIndex, string? password)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> CreateIndexStorage(StorageDir cell, string? password)
    {
        throw new NotImplementedException();
    }

    public Task ForceDropConnect(string pathIndex)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<StorageFile[]>> GetFiles(string pathIndex, string? password, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<StorageAlbum[]>> GetAlbums(string pathIndex, string? password, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<string>> CheckMetaKeyExist(string pathIndex, string keyName)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> Encrypt(string dataBasePath, string password)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> Decrypt(string dataBasePath, string password)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> CreateAlbum(string dataBasePath, string password, StorageAlbum album)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> DeleteAlbum(string pathDb, string password, StorageAlbum album)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> UpdateAlbum(string pathDb, string password, StorageAlbum album)
    {
        throw new NotImplementedException();
    }
}