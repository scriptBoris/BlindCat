namespace FFMpegDll.Core;

public interface IAudioOutput : IDisposable
{
    void Play();
    void Pause();
    void Stop();
}