using BlindCatCore.Enums;

namespace BlindCatCore.Core;

public interface IMediaPlayer : IMediaBase
{
    event EventHandler<MediaPlayerStates>? StateChanged;
    event EventHandler<double>? PlayingProgressChanged;

    TimeSpan PlayingPosition { get; }
    TimeSpan Duration { get; }
    MediaPlayerStates State { get; }
    void Play();
    void Pause();
    Task SeekTo(double progress, CancellationToken cancellation);
}