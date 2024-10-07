using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Services;

public class MetaDataAnalyzer : IMetaDataAnalyzer
{
    //private readonly IFFMpegService _fFMpegService;

    public MetaDataAnalyzer(/*IFFMpegService fFMpegService*/)
    {
        //_fFMpegService = fFMpegService;
    }

    public async Task<AppResponse<MediaFormats>> GetFormat(Stream stream, CancellationToken cancellation)
    {
        //var res = await _fFMpegService.GetMeta(stream, cancellation);
        //if (res.IsCanceled)
        //    return AppResponse.Canceled;

        //if (res.IsFault)
        //    return res.AsError;

        //return AppResponse.Result(res.Result.Format);
        throw new NotImplementedException();
    }
}
