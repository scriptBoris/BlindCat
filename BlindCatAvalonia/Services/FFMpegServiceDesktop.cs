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
    
    public Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, MediaFormats? format, Size? size, 
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

        var bmp = MakeBitmap(decRes, decoder.FrameSize);
        var formatf = ParseFormat(decoder.CodecName);
        var res = AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
        return Task.FromResult(res);
    }

    public Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, 
        TimeSpan byTime, 
        FileCENC? encodingData,
        EncryptionArgs encryptionArgs, CancellationToken cancel)
    {
        FFMpegDll.Init.InitializeFFMpeg();
        using var decoder = new VideoFileDecoder(path, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, PIX_FMT);
        decoder.SeekTo(byTime);
        
        var decRes = decoder.TryDecodeNextFrame();
        if (!decRes.IsSuccessed)
        {
            var error = AppResponse.Error("Fail to decode frame", 2311138);
            return Task.FromResult<AppResponse<DecodeResult>>(error);
        }

        var bmp = MakeBitmap(decRes, decoder.FrameSize);
        var formatf = ParseFormat(decoder.CodecName);
        var res = AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
        return Task.FromResult(res);
    }

    private unsafe SKBitmap MakeBitmap(FrameDecodeResult decodeRes, Size frameSize)
    {
        nint pointerFFMpegBitmap = (IntPtr)decodeRes.FrameBitmapRGBA8888;
        int bytePerPixel = 24;
        int width = frameSize.Width;
        int height = frameSize.Height;
        
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888);
        bmp.InstallPixels(info, pointerFFMpegBitmap);

        return bmp;
    }

    private MediaFormats ParseFormat(string data)
    {
        throw new NotImplementedException();
    }
    
    public Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult)
    {
        throw new NotImplementedException();
    }

    public object ResizeBitmap(object bitmap, Size size)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<DecodeResult>> CreateAndSaveThumbnail(string originFilePath, string pathThumbnail, MediaFormats mediaFormat, EncryptionArgs enc,
        CancellationToken none)
    {
        throw new NotImplementedException();
    }
}