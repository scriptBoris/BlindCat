using BlindCatCore.Core;
using BlindCatCore.Enums;
using System.Drawing;

namespace BlindCatCore.Services;

public interface IFFMpegService
{
    public static int OnlyAudioFile => 6478253;
    string PathToFFmpegExe { get; }
    string PathToFFprobeExe { get; }

    Task<AppResponse<IMediaMeta>> GetMeta(Stream stream, CancellationToken cancel);

    /// <summary>
    /// Получает изображение у стрима. 
    /// Если это файл изобрежения, то возвращает всю картинку; <br/>
    /// Если это файл видео, то возвращает картинку превью (Thumbnail)
    /// </summary>
    Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, MediaFormats? format, Size? size, CancellationToken cancel);


    /// <summary>
    /// Получает изображение у файла. 
    /// Если это файл изобрежения, то возвращает всю картинку; <br/>
    /// Если это файл видео, то возвращает картинку превью (Thumbnail)
    /// </summary>
    Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, CancellationToken cancel);

    /// <summary>
    /// Получает изображение у файла. 
    /// Если это файл изобрежения, то возвращает всю картинку; <br/>
    /// Если это файл видео, то возвращает картинку превью (Thumbnail)
    /// </summary>
    Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, EncryptionArgs encryption, CancellationToken cancel);

    /// <summary>
    /// Создает превью у видео
    /// </summary>
    /// <param name="videoFilePath">Видео файл</param>
    /// <param name="offsetFrameSec"></param>
    /// <param name="cancellation"></param>
    Task<AppResponse<object>> MakeThumbnail(string videoFilePath, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation);

    /// <summary>
    /// Создает превью для стрима
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="offsetFrameSec"></param>
    /// <param name="meta">Использовать уже прочитанную матаинфу или загружать самостоятельно</param>
    /// <param name="cancellation"></param>
    Task<AppResponse<object>> MakeThumbnail(Stream stream, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation);


    /// <summary>
    /// Создает превью для видео
    /// </summary>
    /// <param name="videoFilePath">Видео файл</param>
    /// <param name="offsetFrameSec"></param>
    /// <param name="meta">Использовать уже прочитанную матаинфу или загружать самостоятельно</param>
    /// <param name="encryption">параметры дешифрования</param>
    /// <param name="cancellation"></param>
    Task<AppResponse<object>> MakeThumbnail(string videoFilePath, double? offsetFrameSec, IMediaMeta? meta, EncryptionArgs encryption, CancellationToken cancellation);

    /// <summary>
    /// Исправляет заголовки Moov для webm и mp4, если они находились в конце файла,
    /// перемещая их в начало, для быстрого стриминга видео
    /// </summary>
    Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult);

    object ResizeBitmap(object bitmap, Size size);

    /// <summary>
    /// Сохраняет превью файла (видео или фото).
    /// </summary>
    /// <param name="filePath">Файл с которого будет генерироваться превью</param>
    /// <param name="fileformat">Формат файла с которого будет генерироваться превью</param>
    /// <param name="saveDestinationPath">Полный путь файла, куда будет сохраняться превью (.jpg 250x250)</param>
    /// <param name="encryptionArgs">Параметры шифрования файла с которого будет генерироваться превью</param>
    /// <param name="password">Пароль по которому будет шифроваться сохраненное превью</param>
    Task<AppResponse<DecodeResult>> SaveThumbnail(string filePath, MediaFormats fileformat, string saveDestinationPath, EncryptionArgs encryptionArgs, string? password);
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
    public string? Password { get; set; }
}