﻿using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatData.Models;

namespace BlindCatCore.Services;

public interface IDataBaseService
{
    Task<AppResponse> AddContent(string dataBasePath, string password, StorageFile contentStorageDb);
    Task<AppResponse> UpdateContent(string dataBasePath, string password, StorageFile contentStorageDb);
    Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid);
    Task<AppResponse> DeleteContent(string dataBasePath, string password, Guid fileGuid, Func<Task<AppResponse>> body);
    Task<bool> CheckPasswordValid(string pathIndex, string? password);
    Task<AppResponse> CreateIndexStorage(StorageDir cell, string? password);
    Task ForceDropConnect(string pathIndex);
    Task<AppResponse<StorageFile[]>> GetFiles(string pathIndex, string? password);
    Task<AppResponse> CheckMetaKeyExist(string pathIndex, string keyName);
}