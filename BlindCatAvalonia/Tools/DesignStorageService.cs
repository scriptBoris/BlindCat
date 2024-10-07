using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Tools;

public class DesignStorageService : IStorageService
{
    public string AppDir => throw new NotImplementedException();

    public ReadOnlyObservableCollection<StorageDir> Storages { get; } = new(new ObservableCollection<StorageDir>()
    {
        new StorageDir
        {
            Name = "Home storage",
            Path = "C:/data/home",
        },

        new StorageDir
        {
            Name = "Work storage",
            Path = "C:/data/work",
        },
    });

    public StorageDir? CurrentStorage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Task<AppResponse> AddStorage(StorageDir cell, string? password)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckPasswordCorrect(StorageDir storage, [NotNullWhen(true)] string? password)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckStorageDir(string indexFilePath)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> DeleteFile(ISourceFile file)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> DeleteStorage(StorageDir storageCell)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExportFiles(string[] files)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> ExportStorageDb(StorageDir storage, string dir, string? password)
    {
        throw new NotImplementedException();
    }

    public Task<StorageDir[]> GetStorages()
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> Import(string name, string directoryPath)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> InitStorage(StorageDir storage, string password, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> SaveStorageFile(StorageDir storage, ISourceFile unsavedFile, string password, IFileUnlocker? unlockFile)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse> UpdateStorageFile(StorageDir cell, StorageFile storage, string password)
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
}
