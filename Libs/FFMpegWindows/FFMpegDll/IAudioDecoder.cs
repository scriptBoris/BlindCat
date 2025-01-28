namespace FFMpegDll;

public interface IAudioDecoder : IDisposable
{
    void SeekTo(TimeSpan timePosition);
    int SampleRate { get; }
    int Channels { get; }
    int OutputSampleBits { get; }
    bool TryDecodeNextSample(out Span<byte> frameSampleData);
}