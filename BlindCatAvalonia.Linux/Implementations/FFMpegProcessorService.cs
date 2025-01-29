using Avalonia.Controls.Shapes;
using BlindCatAvalonia.Tools;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using FFMpegProcessor;
using FFMpegProcessor.Models;
using QTFastStart;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Linux.Implementations;

public class FFMpegProcessorService : IFFMpegService
{
    private readonly ICrypto _crypto;

    public FFMpegProcessorService(ICrypto crypto)
    {
        _crypto = crypto;
    }

    public required string PathToFFmpegExe { get; set; } = "ffmpeg";
    public required string PathToFFprobeExe { get; set; } = "ffprobe";

    public Task<AppResponse<IMediaMeta>> GetMeta(Stream stream, CancellationToken cancel)
    {
        using var video = new VideoReader2(stream,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );
        return GetMetaInternal(video, cancel);
    }

    public async Task<AppResponse<IMediaMeta>> GetMeta(string path, EncryptionArgs? enc, CancellationToken cancel)
    {
        VideoReader2? video = null;
        Stream? stream = null;
        try
        {
            var encMethod = enc?.EncryptionMethod ?? EncryptionMethods.None;
            string? password = enc?.Password;

            switch (encMethod)
            {
                case EncryptionMethods.None:
                    video = new VideoReader2(path, PathToFFmpegExe, PathToFFprobeExe);
                    break;
                case EncryptionMethods.dotnet:
                    var id = enc.Value.Storageid;
                    long? size = enc.Value.OriginFileSize;
                    var dec = await _crypto.DecryptFile(id, path, password, size, cancel);
                    if (dec.IsFault)
                        return dec.AsError;

                    stream = dec.Result;
                    video = new VideoReader2(stream, PathToFFmpegExe, PathToFFprobeExe);
                    break;
                case EncryptionMethods.CENC:
                    var fileCENC = new FileCENC
                    {
                        FilePath = path,
                        Key = ToCENCPassword(password),
                        Kid = GetKid(),
                    };
                    video = new VideoReader2(fileCENC, PathToFFmpegExe, PathToFFprobeExe);
                    break;
                default:
                    throw new NotSupportedException();
            }
            return await GetMetaInternal(video, cancel);
        }
        finally
        {
            video?.Dispose();
            stream?.Dispose();
        }
    }

    private async Task<AppResponse<IMediaMeta>> GetMetaInternal(VideoReader2 video, CancellationToken cancel)
    {
        VideoMetadata? res;
        try
        {
            res = await video.LoadMetadataAsync(cancellation: cancel);
            if (cancel.IsCancellationRequested)
                return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail fetch meta", 3419, ex);
        }

        if (res == null)
        {
            return AppResponse.Error("Fail fetch meta", 3420);
        }

        MediaFormats media;
        string? format = res?.Format?.FormatName;
        switch (format)
        {
            case "mov,mp4,m4a,3gp,3g2,mj2":
                media = MediaFormats.Mp4;
                break;
            case "jpeg_pipe":
                media = MediaFormats.Jpeg;
                break;
            case "webp_pipe":
                media = MediaFormats.Webp;
                break;
            case "gif":
                media = MediaFormats.Gif;
                break;
            case "png_pipe":
                media = MediaFormats.Png;
                break;
            case "matroska,webm":
                media = MediaFormats.Webm;
                break;
            case "flv":
                media = MediaFormats.Flv;
                break;
            default:
                media = MediaFormats.Unknown;
                Debugger.Break();
                break;
        }

        return AppResponse.Result<IMediaMeta>(new MediaMeta
        {
            FFMpegMeta = res,
            Format = media,
        });
    }

    public async Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, MediaFormats? _format, Size? size, CancellationToken cancel)
    {
        IMediaMeta? meta = null;
        var format = _format ?? MediaFormats.Unknown;
        if (format == MediaFormats.Unknown)
        {
            var metaRes = await GetMeta(stream, cancel);
            if (metaRes.IsFault)
                return metaRes.AsError;

            format = metaRes.Result.Format;
            meta = metaRes.Result;
        }

        try
        {
            SKBitmap originImage;
            if (format.IsVideo())
            {
                var makeThumb = await this.MakeThumbnail(stream, null, meta, cancel);
                if (makeThumb.IsFault)
                    return makeThumb.AsError;

                originImage = (SKBitmap)makeThumb.Result;
            }
            else
            {
                byte[] bin = stream.ToArray();
                var bmp = SKBitmap.Decode(bin);
                if (cancel.IsCancellationRequested)
                    return AppResponse.Canceled;

                if (bmp == null)
                    return AppResponse.Error("Fail decode stream [SKBitmap.Decode(stream)]");

                originImage = bmp;
            }

            if (size != null)
                originImage = MakeAspectFill(originImage, size.Value);

            return AppResponse.Result(new DecodeResult
            {
                Bitmap = originImage,
                EncodedFormat = format,
            });
        }
        catch when (cancel.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Не удалось считать файл изображения", 99314, ex);
        }
    }

    public async Task<AppResponse<DecodeResult>> DecodePicture(string? imgPath, MediaFormats? _format, Size? size, CancellationToken cancel)
    {
        if (!File.Exists(imgPath))
            return AppResponse.Error("Файл не найден");

        IMediaMeta? meta = null;
        var format = _format ?? MediaFormats.Unknown;

        // try 1
        if (format == MediaFormats.Unknown)
            format = MediaPresentVm.ResolveFormat(imgPath);

        // try 2
        if (format == MediaFormats.Unknown)
        {
            var metaRes = await GetMeta(imgPath, null, cancel);
            if (metaRes.IsFault)
                return metaRes.AsError;

            format = metaRes.Result.Format;
            meta = metaRes.Result;
        }

        try
        {
            SKBitmap originImage;
            if (format.IsVideo())
            {
                var makeThumb = await this.MakeThumbnail(imgPath, null, meta, cancel);
                if (makeThumb.IsFault)
                    return makeThumb.AsError;

                originImage = (SKBitmap)makeThumb.Result;
            }
            else
            {
                var bmp = SKBitmap.Decode(imgPath);
                if (cancel.IsCancellationRequested)
                    return AppResponse.Canceled;

                if (bmp == null)
                    return AppResponse.Error("Fail decode image");

                originImage = bmp;
            }

            if (size != null)
                originImage = MakeAspectFill(originImage, size.Value);

            return AppResponse.Result(new DecodeResult
            {
                Bitmap = originImage,
                EncodedFormat = format,
            });
        }
        catch when (cancel.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Не удалось считать файл изображения", 99314, ex);
        }
    }

    public async Task<AppResponse<DecodeResult>> DecodePicture(string? imgPath, MediaFormats? _format, Size? size, EncryptionArgs enc, CancellationToken cancel)
    {
        if (!File.Exists(imgPath))
            return AppResponse.Error("Файл не найден");

        IMediaMeta? meta = null;
        var format = _format ?? MediaFormats.Unknown;
        
        // try 1
        if (format == MediaFormats.Unknown)
            format = MediaPresentVm.ResolveFormat(imgPath);

        // try 2
        if (format == MediaFormats.Unknown)
        {
            var metaRes = await GetMeta(imgPath, enc, cancel);
            if (metaRes.IsFault)
                return metaRes.AsError;

            format = metaRes.Result.Format;
            meta = metaRes.Result;
        }

        try
        {
            SKBitmap originImage;
            if (format.IsVideo())
            {
                var makeThumb = await this.MakeThumbnail(imgPath, null, meta, cancel);
                if (makeThumb.IsFault)
                    return makeThumb.AsError;

                originImage = (SKBitmap)makeThumb.Result;
            }
            else
            {
                var id = enc.Storageid;
                long? ogsize = enc.OriginFileSize;
                using var dec = await _crypto.DecryptFile(id, imgPath, enc.Password, ogsize, cancel);
                if (dec.IsFault)
                    return dec.AsError;

                var bmp = SKBitmap.Decode(dec.Result);
                if (cancel.IsCancellationRequested)
                    return AppResponse.Canceled;

                if (bmp == null)
                    return AppResponse.Error("Fail decode image");

                originImage = bmp;
            }

            if (size != null)
                originImage = MakeAspectFill(originImage, size.Value);

            return AppResponse.Result(new DecodeResult
            {
                Bitmap = originImage,
                EncodedFormat = format,
            });
        }
        catch when (cancel.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Не удалось считать файл изображения", 99314, ex);
        }
    }

    public async Task<AppResponse<object>> MakeThumbnail(string filePath, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        using var video = new VideoReader2(filePath,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );
        return await MakeThumbnailInternal(video, offsetFrameSec, meta, cancellation);
    }

    public async Task<AppResponse<object>> MakeThumbnail(Stream stream, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        using var video = new VideoReader2(stream,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );
        return await MakeThumbnailInternal(video, offsetFrameSec, meta, cancellation);
    }

    public async Task<AppResponse<object>> MakeThumbnail(string videoFilePath, double? offsetFrameSec, IMediaMeta? meta, EncryptionArgs enc, CancellationToken cancellation)
    {
        VideoReader2? video = null;
        try
        {
            switch (enc.EncryptionMethod)
            {
                case EncryptionMethods.None:
                    video = new VideoReader2(videoFilePath,
                        ffmpegExecutable: PathToFFmpegExe,
                        ffprobeExecutable: PathToFFprobeExe
                    );
                    break;
                case EncryptionMethods.dotnet:
                    throw new InvalidOperationException("Use MakeThumbnail(Stream stream ...)");
                case EncryptionMethods.CENC:
                    var fileCENC = new FileCENC
                    {
                        FilePath = videoFilePath,
                        Key = ToCENCPassword(enc.Password),
                        Kid = GetKid(),
                    };
                    video = new VideoReader2(fileCENC,
                        ffmpegExecutable: PathToFFmpegExe,
                        ffprobeExecutable: PathToFFprobeExe
                    );
                    break;
                default:
                    throw new NotImplementedException();
            }

            return await MakeThumbnailInternal(video, offsetFrameSec, meta, cancellation);
        }
        finally
        {
            video?.Dispose();
        }
    }

    public async Task<AppResponse<object>> MakeThumbnailInternal(VideoReader2 video, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        VideoMetadata? loadedMeta = null;
        
        // load bin meta
        if (meta is MediaMeta vmeta)
        {
            video.UseMetadata(vmeta.FFMpegMeta);
            loadedMeta = vmeta.FFMpegMeta;
        }
        else
        {
            loadedMeta = await video.LoadMetadataAsync(cancellation: cancellation);
        }

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        if (loadedMeta == null)
            return AppResponse.Error("Fail load meta data for thumbnail", 777418);

        double dur = loadedMeta.Duration;
        if (dur == 0)
            return AppResponse.Error("Videofile is contains only audio track", IFFMpegService.OnlyAudioFile);

        double loadoff;
        if (offsetFrameSec != null)
        {
            loadoff = offsetFrameSec.Value;
        }
        else
        {
            if (dur >= 60)
            {
                loadoff = 20.0;
            }
            else if (dur >= 30)
            {
                loadoff = 9.0;
            }
            else if (dur >= 5)
            {
                loadoff = 2.0;
            }
            else if (dur >= 1)
            {
                loadoff = 0.5;
            }
            else
            {
                loadoff = 0.1;
            }
        }

        video.Load(loadoff);
        var frame = await video.FetchFrame(cancellation);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        if (frame == null)
            return AppResponse.Error("Frame is null");

        var bitmap = await TaskExt.Run(() =>
        {
            return CreateBitmapFromRGB24(frame.RawData, frame.Width, frame.Height);
        }, cancellation);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        return AppResponse.Result<object>(bitmap);
    }

    public object ResizeBitmap(object bitmap, Size size)
    {
        return MakeAspectFill((SKBitmap)bitmap, size);
    }

    public async Task<AppResponse<DecodeResult>> SaveThumbnail(string filePath, MediaFormats fileformat, string saveDestinationPath, EncryptionArgs enc, string? password)
    {
        AppResponse<DecodeResult> thumbnailRes;
        if (enc.EncryptionMethod == EncryptionMethods.None)
        {
            thumbnailRes = await DecodePicture(filePath, fileformat, new Size(250, 250), CancellationToken.None);
        }
        else
        {
            thumbnailRes = await DecodePicture(filePath, fileformat, new Size(250, 250), enc, CancellationToken.None);
        }

        if (thumbnailRes.IsSuccess)
        {
            var bmp = (SKBitmap)thumbnailRes.Result.Bitmap;
            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var mem = new MemoryStream();
            data.SaveTo(mem);
            mem.Position = 0;

            if (password != null)
            {
                await _crypto.EncryptFile(mem, saveDestinationPath, password);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        return thumbnailRes;
    }

    public static SKBitmap CreateBitmapFromRGB24(byte[] rgb24Data, int width, int height)
    {
        SKBitmap bitmap;

        byte[] rgbaData = new byte[width * height * 4];

        int numThreads = Environment.ProcessorCount - 2;
        int chunkSize = (height + numThreads - 1) / numThreads;

        Parallel.For(0, numThreads, threadIndex =>
        {
            int startY = threadIndex * chunkSize;
            int endY = Math.Min(startY + chunkSize, height);

            for (int y = startY; y < endY; y++)
            {
                int rowStart = y * width * 3;
                int rowEnd = rowStart + width * 3;

                int rgbaStart = y * width * 4;

                for (int i = rowStart, j = rgbaStart; i < rowEnd; i += 3, j += 4)
                {
                    rgbaData[j] = rgb24Data[i];         // Красный
                    rgbaData[j + 1] = rgb24Data[i + 1]; // Зеленый
                    rgbaData[j + 2] = rgb24Data[i + 2]; // Синий
                    rgbaData[j + 3] = 255;              // Альфа (непрозрачный)
                }
            }
        });

        var handle = GCHandle.Alloc(rgbaData, GCHandleType.Pinned);
        nint pointer = handle.AddrOfPinnedObject();

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap = new Tools.SKBitmapHndl(info, handle);
        bitmap.InstallPixels(info, pointer, info.RowBytes);

        return bitmap;
    }

    public static SKBitmap MakeAspectFill(SKBitmap imageData, Size viewPortSize)
    {
        // Вычисляем соотношение сторон изображения и viewport
        float sourceAspect = (float)imageData.Width / imageData.Height;
        float targetAspect = (float)viewPortSize.Width / (float)viewPortSize.Height;

        // Определяем размеры обрезанного изображения
        int cropWidth, cropHeight;
        if (sourceAspect > targetAspect)
        {
            // Изображение шире чем viewport, обрезаем по ширине
            cropHeight = imageData.Height;
            cropWidth = (int)(cropHeight * targetAspect);
        }
        else
        {
            // Изображение выше чем viewport, обрезаем по высоте
            cropWidth = imageData.Width;
            cropHeight = (int)(cropWidth / targetAspect);
        }

        // Вычисляем координаты для центрирования обрезки
        int cropX = (imageData.Width - cropWidth) / 2;
        int cropY = (imageData.Height - cropHeight) / 2;

        // Создаем bitmap для обрезанного изображения
        SKBitmap croppedBitmap = new SKBitmap(cropWidth, cropHeight);
        using (SKCanvas canvas = new SKCanvas(croppedBitmap))
        {
            SKRect srcRect = new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            SKRect destRect = new SKRect(0, 0, cropWidth, cropHeight);
            canvas.DrawBitmap(imageData, srcRect, destRect);
        }

        // Изменяем размер обрезанного изображения до размеров viewport
        SKBitmap resizedBitmap = new SKBitmap((int)viewPortSize.Width, (int)viewPortSize.Height);
        using (SKCanvas canvas = new SKCanvas(resizedBitmap))
        {
            canvas.DrawBitmap(croppedBitmap, new SKRect(0, 0, (float)viewPortSize.Width, (float)viewPortSize.Height));
        }

        return resizedBitmap;
    }

    public async Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult)
    {
        int tryCount = 5;
        Exception? exception = null;
        while (tryCount > 0)
        {
            try
            {
                using (var checkSteam = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                tryCount--;
                await Task.Delay(200);
            }
        }

        if (tryCount == 0)
            return AppResponse.Error("Fail fix to move MOOV position from end to start", 88196, exception);
        try
        {
            await Task.Run(() =>
            {
                var qt = new Processor();
                qt.Process(file, outStreamResult);
            });
        }
        catch (FastStartSetupException)
        {
            return AppResponse.Result(new MoovFixResult
            {
                UseOriginalFile = true,
            });
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Error", 117312, ex);
        }

        return AppResponse.Result(new MoovFixResult
        {
            UseStream = true,
        });
    }

    public async Task<AppResponse> EncodeVideoTo_Mp4_CENC(string inputFile, string target, string password)
    {
        string key = ToCENCPassword(password);
        string kid = GetKid();

        _ = FFmpegWrapper.Open(PathToFFmpegExe,
        [
            $"-i \"{inputFile}\"",
            "-vcodec libx264",
            "-acodec aac",
            "-encryption_scheme cenc-aes-ctr",
            $"-encryption_key {key}",
            $"-encryption_kid {kid}",
            "-f mp4",
            $"\"{target}\""
        ]
        , out var proc, true);

        await proc.WaitForExitAsync();
        proc.Dispose();
        return AppResponse.OK;
    }

    public async Task<AppResponse> EncodeVideoTo_Mp4_CENC(Stream inputFileStream, string target, string password)
    {
        try
        {
            if (File.Exists(target))
                File.Delete(target);
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail delete exist temp file", 16, ex);
        }

        string key = ToCENCPassword(password);
        string kid = GetKid();

        var (input, _) = FFmpegWrapper.Open(PathToFFmpegExe,
        [
            //"-v debug",
            $"-i pipe:0",
            "-vcodec libx264",
            "-acodec aac",
            "-encryption_scheme cenc-aes-ctr",
            $"-encryption_key {key}",
            $"-encryption_kid {kid}",
            "-f mp4",
            $"\"{target}\"",
            "-report",
        ]
        , out var proc);

        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while (true)
            {
                bytesRead = inputFileStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                if (!input.CanWrite)
                    break;

                await input.WriteAsync(buffer, 0, bytesRead);
            }
            input.Close();
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Fail ffmpeg operation", 214, ex);
        }
        finally
        {
            proc.Dispose();
        }

        //await proc.WaitForExitAsync();
        return AppResponse.OK;
    }

    public string ToCENCPassword(string password)
    {
        byte[] salt = Encoding.UTF8.GetBytes("fffuuuuu");
        int iterations = 10000;
        int keySize = 16;
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(keySize);

        string hexKey = BitConverter.ToString(key).Replace("-", "").ToLower();
        return hexKey;
    }

    public string GetKid()
    {
        return "112233445566778899aabbccddeeff00";
    }

    public class MediaMeta : IMediaMeta
    {
        public MediaFormats Format { get; set; }
        public VideoMetadata FFMpegMeta { get; set; }
    }
}