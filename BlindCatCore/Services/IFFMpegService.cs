using System.Drawing;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models.Media;

namespace BlindCatCore.Services;

public interface IFFMpegService
{
    public static int OnlyAudioFile => 6478253;

    /// <summary>
    /// Получает изображение Thumbnail у видео стрима. 
    /// </summary>
    Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(Stream stream, MediaFormats format, Size size,
        TimeSpan byTime,
        EncryptionArgs encryptionArgs,
        CancellationToken cancel);
    
    /// <summary>
    /// Получает изображение Thumbnail у видео файла. 
    /// </summary>
    Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(string path, MediaFormats format, Size size,
        TimeSpan byTime,
        FileCENC? encodingData,
        EncryptionArgs encryptionArgs,
        CancellationToken cancel);

    /// <summary>
    /// Исправляет заголовки Moov для webm и mp4, если они находились в конце файла,
    /// перемещая их в начало, для быстрого стриминга видео
    /// </summary>
    Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult);

    /// <summary>
    /// Изменяет размер битмапы
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="size"></param>
    object ResizeBitmap(object bitmap, Size size);

    /// <summary>
    /// Генерирует и сохраняет файл превью
    /// </summary>
    /// <param name="originFilePath">Оригинальный файл</param>
    /// <param name="pathThumbnail">Путь по которому будет сохранен файл</param>
    /// <param name="mediaFormat"></param>
    /// <param name="enc"></param>
    /// <param name="none"></param>
    Task<AppResponse<DecodeResult>> CreateAndSaveThumbnail(string originFilePath,
        string pathThumbnail, MediaFormats mediaFormat, EncryptionArgs enc, CancellationToken none);
}

public class DecodeResult : IDisposable
{
    public required object Bitmap { get; set; }
    public required MediaFormats EncodedFormat { get; set; }
    public void Dispose()
    {
        if (Bitmap is IDisposable dis)
            dis.Dispose();
    }
}

public class MoovFixResult
{
    public bool UseStream { get; set; }
    public bool UseOriginalFile { get; set; }
}

public struct EncryptionArgs
{
    public EncryptionMethods EncryptionMethod { get; set; }
    
    /// <summary>
    /// Идентификатор хранилища
    /// </summary>
    public Guid Storageid { get; set; }
    
    /// <summary>
    /// Размер файла до шифрования
    /// </summary>
    public long? OriginFileSize { get; set; }
    
    public string? Password { get; set; }
}