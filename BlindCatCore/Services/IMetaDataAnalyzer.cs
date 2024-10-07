using BlindCatCore.Core;
using BlindCatCore.Enums;

namespace BlindCatCore.Services;

public interface IMetaDataAnalyzer
{
    Task<AppResponse<MediaFormats>> GetFormat(Stream stream, CancellationToken cancellation);
}
