using BlindCatCore.Core;
using BlindCatCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Services;

public interface IDataBaseService
{
    Task<AppResponse> AddContent(string dataBasePath, string? password, StorageFile contentStorageDb, Func<Task<AppResponse>> body);
    Task<AppResponse> UpdateContent(string dataBasePath, string password, StorageFile contentStorageDb);
    Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid);
    Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid, Func<Task<AppResponse>> body);
    Task<bool> CheckPasswordValid(string pathIndex, string? password);
    Task<AppResponse> CreateIndexStorage(StorageDir cell, string? password);
    Task ForceDropConnect(string pathIndex);
    Task<AppResponse<StorageFile[]>> GetFiles(string pathIndex, string? password, CancellationToken cancel);
    Task<AppResponse<StorageAlbum[]>> GetAlbums(string pathIndex, string? password, CancellationToken cancel);
    Task<AppResponse<string>> CheckMetaKeyExist(string pathIndex, string keyName);

    Task<AppResponse> Encrypt(string dataBasePath, string password);
    Task<AppResponse> Decrypt(string dataBasePath, string password);
    Task<AppResponse> CreateAlbum(string dataBasePath, string password, StorageAlbum album);
    Task<AppResponse> DeleteAlbum(string pathDb, string password, StorageAlbum album);
}