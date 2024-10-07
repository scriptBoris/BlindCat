using BlindCatCore.Core;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace BlindCatCore.Models;

public class StorageDir : BaseNotify, ISourceDir
{
    public event EventHandler<ISourceFile>? FileDeleted;
    public event EventHandler<ISourceFile>? FileDeleting;

    /// <summary>
    /// Идентификатор
    /// </summary>
    public Guid Guid { get; set; }

    /// <summary>
    /// Название хранилища
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Путь к папке хранилища
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Путь к базе денных, где индексированы файлы хранилища
    /// </summary>
    [JsonIgnore]
    public string PathIndex => System.IO.Path.Combine(Path, "index");

    /// <summary>
    /// Возможный пароль
    /// </summary>
    [JsonIgnore]
    public string? Password => Controller?.Password;

    /// <summary>
    /// Выделенный контроллер хранилища
    /// </summary>
    [JsonIgnore]
    public StorageDirController? Controller { get; set; }

    /// <summary>
    /// Можно ли читать данное хранилище?
    /// </summary>
    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(Controller))]
    public bool IsOpen => Controller != null;

    /// <summary>
    /// Закрыто ли хранилище?
    /// </summary>
    [JsonIgnore]
    [MemberNotNullWhen(false, nameof(Controller))]
    public bool IsClose => Controller == null;


    [MemberNotNullWhen(true, nameof(Controller))]
    public bool CheckOpen()
    {
        return IsOpen;
    }

    public IList<ISourceFile> GetAllFiles()
    {
        if (Controller == null)
            return new List<ISourceFile>();

        return (IList<ISourceFile>)Controller.StorageFiles;
    }

    public ISourceFile? GetNext(ISourceFile by)
    {
        return Controller?.GetNext((StorageFile)by);
    }

    public ISourceFile? GetPrevious(ISourceFile by)
    {
        return Controller?.GetPrevious((StorageFile)by);
    }

    public void Remove(ISourceFile file)
    {
        throw new NotSupportedException();
    }
}