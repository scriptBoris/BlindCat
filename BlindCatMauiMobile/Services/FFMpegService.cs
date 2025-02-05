using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models.Media;
using BlindCatCore.Services;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Models;
using SkiaSharp;
using IntSize = System.Drawing.Size;

namespace BlindCatMauiMobile.Services;

public class FFMpegService : IFFMpegService
{
    private const AVPixelFormat PIX_FMT = AVPixelFormat.AV_PIX_FMT_ARGB;
    private readonly ICrypto _crypto;

    public FFMpegService(ICrypto crypto)
    {
        _crypto = crypto;
    }
    
    public async Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, MediaFormats? format, Size? size, 
        TimeSpan byTime, 
        EncryptionArgs encryptionArgs,
        CancellationToken cancel)
    {
        FFMpegDll.Init.InitializeFFMpeg();
        using var decoder = new VideoStreamDecoder(stream, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, PIX_FMT);
        var data = await decoder.LoadMetadataAsync(cancel);
        decoder.SeekTo(byTime);

        var decRes = decoder.TryDecodeNextFrame(); 
        if (decRes.IsSuccessed)
            return AppResponse.Error("Fail to decode frame", 2311138);

        var bmp = MakeBitmap(decRes, decoder.FrameSize);
        var formatf = ParseFormat(decoder.CodecName);
        return AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
    }

    public async Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, 
        TimeSpan byTime, 
        FileCENC? encodingData,
        EncryptionArgs encryptionArgs, CancellationToken cancel)
    {
        FFMpegDll.Init.InitializeFFMpeg();
        using var decoder = new VideoFileDecoder(path, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, PIX_FMT);
        var data = await decoder.LoadMetadataAsync(cancel);
        decoder.SeekTo(byTime);
        
        var decRes = decoder.TryDecodeNextFrame();
        if (decRes.IsSuccessed)
            return AppResponse.Error("Fail to decode frame", 2311138);

        var bmp = MakeBitmap(decRes, decoder.FrameSize);
        var formatf = ParseFormat(decoder.CodecName);
        return AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = formatf,
        });
    }

    private unsafe SKBitmap MakeBitmap(FrameDecodeResult decodeRes, IntSize frameSize)
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

    public Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(Stream stream, MediaFormats? format, IntSize? size, TimeSpan byTime, EncryptionArgs encryptionArgs,
        CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<DecodeResult>> GetThumbnailFromVideo(string? path, MediaFormats? format, IntSize? size, TimeSpan byTime, FileCENC? encodingData,
        EncryptionArgs encryptionArgs, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult)
    {
        throw new NotImplementedException();
    }

    public object ResizeBitmap(object bitmap, IntSize size)
    {
        throw new NotImplementedException();
    }

    public async Task<AppResponse<DecodeResult>> CreateAndSaveThumbnail(string originFilePath, 
        string pathThumbnail, 
        MediaFormats format, 
        EncryptionArgs enc,
        CancellationToken cancel)
    {
        var size = new System.Drawing.Size(250, 250); 
        var offset = TimeSpan.FromMilliseconds(5);
        AppResponse<DecodeResult> thumbnailRes;
        if (enc.EncryptionMethod == EncryptionMethods.None)
        {
            if (format.IsVideo())
            {
                thumbnailRes = await GetThumbnailFromVideo(
                    originFilePath, 
                    format, 
                    size,
                    offset,
                    null,
                    enc,
                    cancel
                );
            }
            else
            {
                thumbnailRes = await MakeMini(cancel);
            }
        }
        else
        {
            if (format.IsVideo())
            {
                thumbnailRes = await GetThumbnailFromVideo(
                    originFilePath, 
                    format, 
                    size,
                    offset,
                    null,
                    enc,
                    cancel
                );
            }
            else
            {
                // todo make mini picture
                thumbnailRes = await MakeMini(cancel);
            }
        }
        
        if (thumbnailRes.IsFault)
            return thumbnailRes;

        var bmp = (SKBitmap)thumbnailRes.Result.Bitmap;
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var mem = new MemoryStream();
        data.SaveTo(mem);
        mem.Position = 0;

        if (enc.Password != null)
        {
            await _crypto.EncryptFile(mem, pathThumbnail, enc.Password);
        }
        else
        {
            throw new NotImplementedException();
        }
        
        return AppResponse.Result(new DecodeResult
        {
            Bitmap = bmp,
            EncodedFormat = MediaFormats.Jpeg,
        });
    }
    
    private static Task<AppResponse<DecodeResult>> MakeMini(CancellationToken cancel)
    {
        // todo make mini picture
        throw new NotImplementedException();
    }
    
    private static MediaFormats ParseFormat(string decoderCodecName)
    {
        throw new NotImplementedException();
    }
}