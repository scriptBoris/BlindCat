using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.ViewModels;
using CryMediaAPI.Video;
using BlindCatMaui.Core;
using BlindCatMaui.Models;
using QTFastStart;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlindCatCore.Services;

namespace BlindCatMaui.Services;

public class FFMpegService : IFFMpegService
{
    public required string PathToFFmpegExe { get; set; }
    public required string PathToFFprobeExe { get; set; }
    public required string PathToFFplayExe { set; get; }
    
    public async Task<AppResponse<IMediaMeta>> GetMeta(Stream stream, CancellationToken cancel)
    {
        using var video = new VideoReader(stream,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );

        CryMediaAPI.Video.Models.VideoMetadata res;
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

    public async Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, CancellationToken cancel)
    {
        var meta = await GetMeta(stream, cancel);
        if (meta.IsFault)
            return meta.AsError;

        try
        {
            var format = meta.Result.Format;
            if (format == MediaFormats.Mp4 ||
                format == MediaFormats.Mov ||
                format == MediaFormats.Webm)
            {
                var makeThumb = await this.MakeThumbnail(stream, 0.3, meta.Result, cancel);
                if (makeThumb.IsFault)
                    return makeThumb.AsError;

                return AppResponse.Result(new DecodeResult
                {
                    Bitmap = makeThumb.Result,
                    EncodedFormat = format,
                });
            }

            var codec = SKCodec.Create(stream);
            if (codec == null)
                return AppResponse.Error("Fail decode stream [SKCode.Create(stream)]");

            var bmp = await Task.Run(() =>
            {
                var res = SKBitmap.Decode(codec);
                return res;
            }, cancel);

            return AppResponse.Result(new DecodeResult
            {
                Bitmap = bmp,
                EncodedFormat = codec.EncodedFormat switch
                {
                    SKEncodedImageFormat.Jpeg => MediaFormats.Jpeg,
                    SKEncodedImageFormat.Png => MediaFormats.Png,
                    SKEncodedImageFormat.Gif => MediaFormats.Gif,
                    SKEncodedImageFormat.Webp => MediaFormats.Webp,
                    _ => MediaFormats.Unknown,
                },
            });
        }
        catch (TaskCanceledException)
        {
            return AppResponse.Canceled;
        }
        catch (OperationCanceledException)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Не удалось считать файл изображения", 99314, ex);
        }
    }

    public async Task<AppResponse<DecodeResult>> DecodePicture(string? imgPath, CancellationToken cancel)
    {
        if (!File.Exists(imgPath))
            return AppResponse.Error("Файл не найден");

        try
        {
            var format = MediaPresentVm.ResolveFormat(imgPath);
            if (format.IsVideo())
            {
                var makeThumb = await this.MakeThumbnail(imgPath, 0.3, cancel);
                if (makeThumb.IsFault)
                    return makeThumb.AsError;

                return AppResponse.Result(new DecodeResult
                {
                    Bitmap = makeThumb.Result,
                    EncodedFormat = format,
                });
            }

            var bmp = await Task.Run(() =>
            {
                var res = SKBitmap.Decode(imgPath);
                return res;
            }, cancel);

            if (cancel.IsCancellationRequested)
                return AppResponse.Canceled;

            if (bmp == null)
                return AppResponse.Error("Fail decode image");

            var resultFormat = MediaPresentVm.ResolveFormat(imgPath);

            return AppResponse.Result(new DecodeResult
            {
                Bitmap = bmp,
                EncodedFormat = resultFormat,
            });
        }
        catch (TaskCanceledException)
        {
            return AppResponse.Canceled;
        }
        catch (OperationCanceledException)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Не удалось считать файл изображения", 99314, ex);
        }
    }

    public async Task<AppResponse<object>> MakeThumbnail(string filePath, double offsetFrameSec, CancellationToken cancellation)
    {
        using var video = new VideoReader(filePath,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );

        // load meta
        await video.LoadMetadataAsync(cancellation:cancellation);
        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        double loadoff;
        double dur = video.Metadata.Duration;
        // load file
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
        
        await video.LoadAsync(loadoff, cancellation);
        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        using var frame = video.NextFrame();

        var bitmap = await TaskExt.Run(() =>
        {
            return CreateBitmapFromRGB24(frame.RawData, frame.Width, frame.Height);
        }, cancellation);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        return AppResponse.Result<object>(bitmap);
    }

    public async Task<AppResponse<object>> MakeThumbnail(Stream stream, double offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        using var video = new VideoReader(stream,
            ffmpegExecutable: PathToFFmpegExe,
            ffprobeExecutable: PathToFFprobeExe
        );

        // load bin meta
        if (meta is MediaMeta mm)
            video.UseMetadata(mm.FFMpegMeta);
        else
            await video.LoadMetadataAsync(cancellation: cancellation);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        double loadoff;
        double dur = video.Metadata.Duration;
        // load file
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

        // load bin file
        video.Load(loadoff);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        using var frame = video.NextFrame();

        var bitmap = await TaskExt.Run(() =>
        {
            return CreateBitmapFromRGB24(frame.RawData, frame.Width, frame.Height);
        }, cancellation);

        if (cancellation.IsCancellationRequested)
            return AppResponse.Canceled;

        return AppResponse.Result<object>(bitmap);
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

        //// Преобразуем RGB24 в RGBA32
        //for (int i = 0, j = 0; i < rgb24Data.Length; i += 3, j += 4)
        //{
        //    rgbaData[j] = rgb24Data[i];         // Красный
        //    rgbaData[j + 1] = rgb24Data[i + 1]; // Зеленый
        //    rgbaData[j + 2] = rgb24Data[i + 2]; // Синий
        //    rgbaData[j + 3] = 255;              // Альфа (непрозрачный)
        //}

        var handle = GCHandle.Alloc(rgbaData, GCHandleType.Pinned);
        nint pointer = handle.AddrOfPinnedObject();

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap = new SKBitmapHndl(info, handle);
        bitmap.InstallPixels(info, pointer, info.RowBytes);

        return bitmap;
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
        catch(FastStartSetupException)
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
}

//public class DecodeResult : IDisposable
//{
//    public required SKBitmap Bitmap { get; set; }
//    public required MediaFormats EncodedFormat { get; set; }
//    public void Dispose() => Bitmap.Dispose();
//}

//public class MoovFixResult
//{
//    public bool UseStream { get; set; }
//    public bool UseOriginalFile { get; set; }
//}