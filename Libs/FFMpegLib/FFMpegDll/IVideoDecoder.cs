using FFMpegDll.Models;
using IntSize = System.Drawing.Size;

namespace FFMpegDll;

public interface IVideoDecoder : IDisposable
{
    double AvgFramerate { get; }
    IntSize FrameSize { get; }
    TimeSpan Duration { get; }
    
    void SeekTo(TimeSpan position);
    FrameDecodeResult TryDecodeNextFrame();
    Task<VideoMetadata> LoadMetadataAsync(CancellationToken cancel);
}