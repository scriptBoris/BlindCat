namespace BlindCatCore.Models;

public interface ISourceFile
{
    /// <summary>
    /// Id локального файла, в разрезе директории, в 
    /// которой он был проиндексирован
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Путь к файлу (оригинал)
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Описание файла (если оно есть)
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Название файла с его расширением
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Путь к файлу превью (изображение или сылка на онлайн изображение)
    /// </summary>
    public string? FilePreview { get; }

    /// <summary>
    /// Расширение файла, например: ".png" или ".jpg" (в нижнем регистре!)
    /// </summary>
    public string FileExtension { get; }

    /// <summary>
    /// Временный файл, для предварительного накопления 
    /// мета-информациеи перед шифрованием в storage
    /// </summary>
    public StorageFile? TempStorageFile { get; set; }

    /// <summary>
    /// Директория в которой находится данный файл
    /// </summary>
    public ISourceDir SourceDir { get; }

    /// <summary>
    /// Выбран ли данный файл пользователем, например для 
    /// шифрования
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Является ли данный файл видео файлом, например mp4, mov, avi...
    /// </summary>
    public bool IsVideo { get; }
}