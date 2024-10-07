using BlindCatCore.Core;
using BlindCatCore.Models;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace BlindCatCore.Services;

public interface IStorageService
{
    string AppDir { get; }
    ReadOnlyObservableCollection<StorageDir> Storages { get; }
    StorageDir? CurrentStorage { get; set; }

    [Obsolete]
    Task<bool> ExportFiles(string[] files);
    [Obsolete]
    Task<bool> WriteSrc(string galleryName, string fileName, byte[] dat);
    [Obsolete]
    Task<bool> WriteSrc(string galleryName, string fileName, Stream stream, Action<int> callback);

    Task<StorageDir[]> GetStorages();
    Task<AppResponse> AddStorage(StorageDir cell, string? password);
    Task<AppResponse> DeleteStorage(StorageDir storageCell);

    /// <summary>
    /// Атомарная функция
    /// </summary>
    Task<AppResponse> SaveStorageFile(StorageDir storage, ISourceFile unsavedFile, string password, IFileUnlocker? unlockFile);

    /// <summary>
    /// Атомарная функция
    /// </summary>
    Task<AppResponse> UpdateStorageFile(StorageDir cell, StorageFile storage, string password);

    /// <summary>
    /// Проверяет пароль на валидность для указанного хранилища
    /// </summary>
    Task<bool> CheckPasswordCorrect(StorageDir storage, [NotNullWhen(true)] string? password);

    /// <summary>
    /// Сканирует папку директории, сканирует файлы в БД и сопоставляет с файлами в папке
    /// + индексирует тэги
    /// </summary>
    Task<AppResponse> InitStorage(StorageDir storage, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Импортирует уже существующую директорию
    /// </summary>
    /// <param name="name"></param>
    /// <param name="directoryPath"></param>
    Task<AppResponse> Import(string name, string directoryPath);

    /// <summary>
    /// Проверяет указанный файл на то, что он является базой даных для storage
    /// </summary>
    Task<bool> CheckStorageDir(string indexFilePath);

    /// <summary>
    /// Удаляет зашифрованный файл / превью / строку из БД
    /// </summary>
    Task<AppResponse> DeleteFile(ISourceFile file);

    Task<AppResponse> ExportStorageDb(StorageDir storage, string dir, string? password);
}
