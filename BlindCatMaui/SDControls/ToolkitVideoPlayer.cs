using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using BlindCatMaui.Core;
using System.Diagnostics;

namespace BlindCatMaui.SDControls;

public class ToolkitVideoPlayer : MediaElement, IMediaBase, IMediaPlayer, IMediaElement
{
    private double viewPortWidth;
    private double viewPortHeight;
    private TaskCompletionSource<bool>? load;

    public new event EventHandler<MediaPlayerStates>? StateChanged;
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<double>? PlayingProgressChanged;

    public ToolkitVideoPlayer()
    {
        base.StateChanged += ToolkitVideoPlayer_StateChanged;
        this.PositionChanged += ToolkitVideoPlayer_PositionChanged;
        base.MediaOpened += ToolkitVideoPlayer_MediaOpened;
        base.MediaFailed += ToolkitVideoPlayer_MediaFailed;
    }

    public double Zoom
    {
        get => this.Scale;
        set
        {
            this.Scale = value.Limitation(0.1, 5);
            ZoomChanged?.Invoke(this, this.Scale);
        }
    }

    public double PositionXPercent { get; private set; } = 0.5;
    public double PositionYPercent { get; private set; } = 0.5;

    public MediaPlayerStates State => CurrentState switch
    {
        MediaElementState.Playing => MediaPlayerStates.Playing,
        MediaElementState.Paused => MediaPlayerStates.Pause,
        _ => MediaPlayerStates.Pause,
    };

    public TimeSpan PlayingPosition => Position;

    public void InvalidateSurface()
    {
    }

    private void ToolkitVideoPlayer_PositionChanged(object? sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
    {
        var res = e.Position / Duration;
        PlayingProgressChanged?.Invoke(this, res);
    }

    private void ToolkitVideoPlayer_MediaOpened(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(this, MediaPlayerStates.ReadyToPlaying);
        load?.TrySetResult(true);
    }

    private void ToolkitVideoPlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        load?.TrySetResult(false);
    }

    private void ToolkitVideoPlayer_StateChanged(object? sender, CommunityToolkit.Maui.Core.Primitives.MediaStateChangedEventArgs e)
    {
        switch (e.NewState)
        {
            case MediaElementState.None:
                break;
            case MediaElementState.Opening:
                break;
            case MediaElementState.Buffering:
                break;
            case MediaElementState.Playing:
                StateChanged?.Invoke(this, MediaPlayerStates.Playing);
                break;
            case MediaElementState.Paused:
                StateChanged?.Invoke(this, MediaPlayerStates.Pause);
                break;
            case MediaElementState.Stopped:
                break;
            case MediaElementState.Failed:
                break;
            default:
                break;
        }
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        PositionXPercent = imagePosPercentX;
        PositionYPercent = imagePosPercentY;
        UpdatePos(viewPortWidth, viewPortHeight, Width, Height);
    }

    public async Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        load?.TrySetResult(false);
        load = new();
        Source = filePath;

        await load.AwaitWithCancelation(cancel);
        load = null;
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task SeekTo(double progress, CancellationToken cancellation)
    {
        var tp = Duration * progress;
        return SeekTo(tp, cancellation);
    }
    
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        viewPortWidth = widthConstraint;
        viewPortHeight = heightConstraint;

        var res = base.MeasureOverride(widthConstraint, heightConstraint);
        if (!res.IsZero)
        {
            UpdatePos(widthConstraint, heightConstraint, res.Width, res.Height);
        }
        return res;
    }

    private void UpdatePos(double vpWidth, double vpHeight, double imgWidth, double imgHeight)
    {
        double centerX = vpWidth * PositionXPercent - (imgWidth / 2);
        double centerY = vpHeight * PositionYPercent - (imgHeight / 2);
        TranslationX = centerX;
        TranslationY = centerY;
    }

    public void Reset()
    {
        var old = Source;

        Stop();
        Source = null;
        PositionXPercent = 0.5;
        PositionYPercent = 0.5;
        Zoom = 1.0;

        if (old is IDisposable dis)
            dis.Dispose();
    }

    public async Task SetSourceStorage(StorageFile file, CancellationToken cancel)
    {
        var decode = await this.DiFetch<ICrypto>().DecryptFile(file.FilePath, file.Storage.Password, cancel);
        if (decode.IsCanceled)
            return;

        if (decode.IsFault)
        {
            Debugger.Break();
            return;
        }

        load?.TrySetResult(false);
        load = new();
        Source = new SourceStreamWrapper(decode.Result, file.CachedMediaFormat);

        await load.AwaitWithCancelation(cancel);
        //using (cancel.Register(() =>
        //{
        //    load.TrySetCanceled();
        //}))
        //{
        //    await load.Task;
        //}
        load = null;
    }
}

public class SourceStreamWrapper : MediaSource, IDisposable
{
    public SourceStreamWrapper(Stream stream, MediaFormats mediaFormat)
    {
        Stream = stream;
        MediaFormat = mediaFormat;
    }

    public Stream Stream { get; init; }
    public MediaFormats MediaFormat { get; init; }

    public void Dispose()
    {
        Stream.Dispose();
    }
}