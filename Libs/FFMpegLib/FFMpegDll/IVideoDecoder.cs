using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFMpegDll.Models;

namespace FFMpegDll;

public interface IVideoDecoder : IDisposable
{
    void SeekTo(TimeSpan position);
    FrameDecodeResult TryDecodeNextFrame();
    Task<VideoMetadata> LoadMetadataAsync(CancellationToken cancel);
}