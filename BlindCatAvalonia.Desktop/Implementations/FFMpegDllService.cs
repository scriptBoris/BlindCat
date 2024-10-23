using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Desktop.Implementations;

public class FFMpegDllService : IFFMpegService
{
    public string PathToFFmpegExe => throw new NotImplementedException();
    public string PathToFFprobeExe => throw new NotImplementedException();

    public Task<AppResponse<DecodeResult>> DecodePicture(Stream stream, MediaFormats? format, Size? size, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<DecodeResult>> DecodePicture(string? path, MediaFormats? format, Size? size, EncryptionArgs encryption, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<MoovFixResult>> FixMoovMp4(string file, Stream outStreamResult)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<IMediaMeta>> GetMeta(Stream stream, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<object>> MakeThumbnail(string videoFilePath, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<object>> MakeThumbnail(Stream stream, double? offsetFrameSec, IMediaMeta? meta, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<object>> MakeThumbnail(string videoFilePath, double? offsetFrameSec, IMediaMeta? meta, EncryptionArgs encryption, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public object ResizeBitmap(object bitmap, Size size)
    {
        throw new NotImplementedException();
    }

    public Task<AppResponse<DecodeResult>> SaveThumbnail(string filePath, MediaFormats fileformat, string saveDestinationPath, EncryptionArgs encryptionArgs, string? password)
    {
        throw new NotImplementedException();
    }
}
