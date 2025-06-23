using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models.Media;
using BlindCatCore.Services;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Models;
using SkiaSharp;

namespace BlindCatAvalonia.Services;

public class FFMpegServiceDesktop : IFFMpegService
{
    private const AVPixelFormat PIX_FMT = AVPixelFormat.AV_PIX_FMT_RGBA;

    public Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(Stream stream, MediaFormats format, Size size,
        TimeSpan byTime,
        EncryptionArgs encryptionArgs,
        CancellationToken cancel)
    {
        FFMpegDll.Init.InitializeFFMpeg();
        using var decoder = new VideoStreamDecoder(stream, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, PIX_FMT);
        decoder.SeekTo(byTime);

        var decRes = decoder.TryDecodeNextFrame();
        if (!decRes.IsSuccessed)
        {
            var error = AppResponse.Error("Fail to decode frame", 2311138);
            return Task.FromResult<AppResponse<DecodeResult>>(error);
        }

        var bmp = MakeBitmap(decRes, decoder.FrameSize, size);
        var formatf = ParseFormat(decoder.CodecName);
        var res = AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
        return Task.FromResult(res);
    }

    public Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(string path, MediaFormats format, Size size,
        TimeSpan byTime,
        FileCENC? encodingData,
        EncryptionArgs encryptionArgs, CancellationToken cancel)
    {
        FFMpegDll.Init.InitializeFFMpeg();
        using var decoder = new VideoFileDecoder(path, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, PIX_FMT);

        if (decoder.PixelFormat == AVPixelFormat.AV_PIX_FMT_NONE)
        {
            var error = AppResponse.Error("Audio file", IFFMpegService.OnlyAudioFile);
            return Task.FromResult<AppResponse<DecodeResult>>(error);
        }

        decoder.SeekTo(byTime);

        var decRes = decoder.TryDecodeNextFrame();
        if (!decRes.IsSuccessed)
        {
            var error = AppResponse.Error("Fail to decode frame", 2311138);
            return Task.FromResult<AppResponse<DecodeResult>>(error);
        }
        
        var bmp = MakeBitmap(decRes, decoder.FrameSize, size);
        var formatf = ParseFormat(decoder.CodecName);
        var res = AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
        return Task.FromResult(res);
    }

    private unsafe SKBitmap MakeBitmap(FrameDecodeResult decodeRes, Size dataImageSize, Size targetSize)
    {
        // Создаем новый SKBitmap с указанным размером
        var bitmap = new SKBitmap(targetSize.Width, targetSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        nint data = (nint)decodeRes.FrameBitmapRGBA8888;

        // Если размер исходных данных не совпадает с целевым размером, нужна масштабизация
        if (dataImageSize != targetSize)
        {
            // Создаем временный SKBitmap для исходных данных
            using var sourceBitmap = new SKBitmap(dataImageSize.Width, dataImageSize.Height, SKColorType.Rgba8888,
                SKAlphaType.Premul);

            void* source = (void*)data;
            void* dest = (void*)sourceBitmap.GetPixels();
            long ln = sourceBitmap.ByteCount;
            Buffer.MemoryCopy(source, dest, ln, ln);

            // Создаем холст для целевого bitmap
            using var canvas = new SKCanvas(bitmap);

            // Создаем матрицу трансформации для масштабирования
            float sx = (float)targetSize.Width / dataImageSize.Width;
            float sy = (float)targetSize.Height / dataImageSize.Height;
            var matrix = SKMatrix.CreateScale(sx, sy);

            // Рисуем исходный битмап с масштабированием
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(sourceBitmap, 0, 0);
        }
        else
        {
            // Если размеры совпадают, просто копируем данные
            void* source = (void*)data;
            void* dest = (void*)bitmap.GetPixels();
            long ln = bitmap.ByteCount;
            Buffer.MemoryCopy(source, dest, ln, ln);
        }

        return bitmap;
    }

    private MediaFormats ParseFormat(string data)
    {
        switch (data)
        {
            case "h264":
            case "h265":
                return MediaFormats.Mp4;
            case "vp8":
            case "vp9":
                return MediaFormats.Webm;
            case "av1":
                return MediaFormats.Webm;
        }

        throw new NotImplementedException();
    }

    public Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult)
    {
        throw new NotImplementedException();
    }

    public object ResizeBitmap(object bitmap, Size size)
    {
        var original = (SKBitmap)bitmap;
        var skImageInfo = new SKImageInfo(size.Width, size.Height);
        var resized = original.Resize(skImageInfo, SKFilterQuality.Medium);
        return resized;
    }

    public Task<AppResponse<DecodeResult>> CreateAndSaveThumbnail(string originFilePath, string pathThumbnail,
        MediaFormats mediaFormat, EncryptionArgs enc,
        CancellationToken none)
    {
        throw new NotImplementedException();
    }
}