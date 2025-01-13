using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatData.Models;

public class AlbumStorageDb
{
    [Key]
    public required Guid Guid { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Должно быть зашифровано + тэги должны быть разделены запятой
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// </summary>
    public string? Artist { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// -
    /// Дата создания
    /// </summary>
    public string? DateCreated { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// -
    /// Дата последней индексации
    /// </summary>
    public string? DateModified { get; set; }

    /// <summary>
    /// Элемент-обложка альбома
    /// </summary>
    public Guid? CoverGuid { get; set; }
}
