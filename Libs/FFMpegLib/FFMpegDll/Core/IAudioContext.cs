namespace FFMpegDll.Core;

public interface IAudioContext
{
    IAudioOutput? InitAudioOutput(Stream stream, int sampleRate, int bitDepth, int channels);
}