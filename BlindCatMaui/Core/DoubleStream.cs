namespace BlindCatMaui.Core;

public class DoubleStream : IDisposable
{
    public DoubleStream(Stream video, Stream audio)
    {
        Video = video;
        Audio = audio;
    }

    public Stream Video { get; private set; }
    public Stream Audio { get; private set; }

    public void Dispose()
    {
        Video.Dispose();
        Audio.Dispose();
    }
}