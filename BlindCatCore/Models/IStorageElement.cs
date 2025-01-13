using BlindCatCore.Enums;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models;

public interface IStorageElement
{
    public Guid Guid { get; }
    public string? Name { get; }
    public string? Description { get; }
    public string[] Tags { get; }
    public string? Artist { get; }

    [DependsOn(nameof(Name))]
    public string FileName => $"{Name}.{CachedMediaFormat.ToString().ToLower()}";
    public string FileExtension => $".{CachedMediaFormat.ToString().ToLower()}";
    public string? FilePreview { get; }
    public EncryptionMethods EncryptionMethod { get; }
    public MediaFormats CachedMediaFormat { get; }

    public bool IsSelected { get; set; }

    /// <summary>
    /// Хранилище, в котором находится данный файл
    /// </summary>
    public ISourceDir SourceDir { get; }
    public bool IsVideo { get; }
    public bool IsAlbum { get; }

    /// <summary>
    /// Сколько еще элементов содержит внутри себя
    /// </summary>
    public int ChildrenCount { get; }

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime? DateCreated { get; set; }

    /// <summary>
    /// Дата модификации
    /// </summary>
    public DateTime? DateModified { get; set; }

    /// <summary>
    /// Коллекция в которой находится данный элемент.
    /// Нужен для того, чтобы при поиске при прокручивании Влева или Вправо
    /// элементы менялись в рамках результата поиска
    /// </summary>
    public IList<IStorageElement>? ListContext { get; set; }

    /// <summary>
    /// Указывает на то, что данный файл зашифрован и имеет запись в БД
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Флаг об ошибке, что у данного файла нет записи в БД
    /// </summary>
    public bool IsErrorNoDBRow { get; set; }

    /// <summary>
    /// Флаг об ошибке, что у у данного элемента нет зашифрованного медиафайла 
    /// </summary>
    public bool IsErrorNoFile { get; set; }

    public bool HasError => IsErrorNoFile || IsErrorNoDBRow;
}
