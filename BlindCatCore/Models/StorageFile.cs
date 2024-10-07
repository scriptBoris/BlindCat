using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.ViewModels;
using PropertyChanged;

namespace BlindCatCore.Models;

public class StorageFile : BaseNotify, ISourceFile
{
    public Guid Guid { get; set; }

    [DoNotNotify]
    public int Id { get; set; } = -1;

    /// <summary>
    /// Название файла, которое было указано пользователем или 
    /// название файла которое было при сохранении в storage
    /// </summary>
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public string? Artist { get; set; }
    public required string FilePath { get; set; }

    [DependsOn(nameof(Name))]
    public string FileName => $"{Name}.{CachedMediaFormat.ToString().ToLower()}";
    public string FileExtension => $".{CachedMediaFormat.ToString().ToLower()}";
    public string? FilePreview { get; set; }
    public EncryptionMethods EncryptionMethod { get; set; }

    /// <summary>
    /// Указывает на то, что данный файл еще не в хранилище, не индексирован,
    /// но уже имеет метаданные для запиши в storage
    /// </summary>
    public bool IsTemp { get; set; }
    public MediaFormats CachedMediaFormat { get; set; } = MediaFormats.Unknown;

    [Obsolete("Use CachedMediaFormat")]
    public MediaHandlerTypes CachedHandlerType { get; set; } = MediaHandlerTypes.Undefined;

    /// <summary>
    /// Хранилище, в котором находится данный файл
    /// </summary>
    public required StorageDir Storage { get; set; }

    public StorageFile? TempStorageFile { get => this; set => throw new NotSupportedException(); }
    public bool IsSelected { get; set; }
    public ISourceDir SourceDir => Storage;
    public bool IsVideo => CachedMediaFormat.IsVideo();

    /// <summary>
    /// Дата первой индексации
    /// </summary>
    public DateTime? DateInitIndex { get; set; }

    /// <summary>
    /// Дата последней индексации
    /// </summary>
    public DateTime? DateLastIndex { get; set; }

    /// <summary>
    /// Коллекция в которой находится данный элемент.
    /// Нужен для того, чтобы при поиске при прокручивании Влева или Вправо
    /// элементы менялись в рамках результата поиска
    /// </summary>
    [DoNotNotify]
    public IList<StorageFile>? ListContext { get; set; }

    /// <summary>
    /// Указывает на то, что данный файл зашифрован и имеет запись в БД
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Есть ли у данного файла запись в БД
    /// </summary>
    public bool IsNoDBRow { get; set; }

    /// <summary>
    /// Есть ли у данного элемента зашифрованный файл
    /// </summary>
    public bool IsNoFile { get; set; }

    public bool HasError => IsNoFile || IsNoDBRow;
}