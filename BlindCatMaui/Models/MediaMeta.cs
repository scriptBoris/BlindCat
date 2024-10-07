using BlindCatCore.Core;
using BlindCatCore.Enums;
using CryMediaAPI.Video.Models;

namespace BlindCatMaui.Models;

public class MediaMeta : IMediaMeta
{
    public MediaFormats Format { get; set; }
    public VideoMetadata FFMpegMeta { get; set; }
}