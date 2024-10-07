using System.ComponentModel.DataAnnotations;

namespace BlindCatData.Models;

public class ContentStorageDb
{
    [Key]
    public required Guid Guid { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// </summary>
    public required string MediaFormat { get; set; }

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
    /// Дата первой индексации
    /// </summary>
    public string? DateIndex { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// -
    /// Дата последней индексации
    /// </summary>
    public string? DateLastIndex { get; set; }

    /// <summary>
    /// Должно быть зашифровано
    /// -
    /// Способ которым был зашифрован оригинальный файл
    /// </summary>
    public string? EncryptionType { get; set; }
}