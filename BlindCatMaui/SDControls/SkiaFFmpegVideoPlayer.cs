using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using CryMediaAPI.Audio;
using CryMediaAPI.Audio.Models;
using CryMediaAPI.Video;
using CryMediaAPI.Video.Models;
using BlindCatMaui.Core;
using BlindCatMaui.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace BlindCatMaui.SDControls;

public class SkiaFFmpegVideoPlayer : SKCanvasView, IMediaBase, IMediaPlayer, IDisposable
{
    private SKBitmap? _currentFrame;
    private VideoEngine? videoEngine;
    private AudioEngine? audioEngine;
    private VideoMetadata? cachedVideoMeta;
    private AudioMetadata? cachedAudioMeta;
    private object? currentSource;

    private CancellationTokenSource cancellationTokenSource = new();
    private MediaPlayerStates _state = MediaPlayerStates.None;

    /// <summary>
    /// Видео доигралось до своего конца
    /// </summary>
    public event EventHandler? VideoPlayingToEnd;

    public SkiaFFmpegVideoPlayer()
    {
        VideoPlayingToEnd += SkiaFFmpegVideoPlayer_VideoPlayingToEnd;
    }

    private SKBitmap? CurrentFrame
    {
        get => _currentFrame;
        set
        {
            _currentFrame?.Dispose();
            _currentFrame = value;
            InvalidateSurface();
        }
    }

    private void SkiaFFmpegVideoPlayer_VideoPlayingToEnd(object? sender, EventArgs e)
    {
        if (currentSource == null)
            return;

        UseEngine(currentSource, TimeSpan.Zero, true).Forget();
    }

    private async Task UseEngine(object source, TimeSpan startFrom, bool autoStart)
    {
        var ff = this.DiFetch<IFFMpegService>();
        var disp = this.Dispatcher;

        // old dispose
        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }

        if (videoEngine != null)
        {
            Desubscribe(videoEngine);
            videoEngine.Dispose();
            videoEngine = null;
        }

        // new instances
        if (cachedAudioMeta != null && cachedAudioMeta.Channels > 0)
        {
            audioEngine = new AudioEngine(source, startFrom, cachedAudioMeta!, ff, disp);
        }

        videoEngine = new VideoEngine(source, startFrom, cachedVideoMeta!, ff, disp);
        videoEngine.PlayingProgressChanged += PlayingProgressChanged;
        videoEngine.VideoPlayingToEnd += VideoPlayingToEnd;
        videoEngine.MayFetchFrame += FetchBitmap;

        // init
        if (audioEngine != null)
        {
            audioEngine.Init();
        }
        
        videoEngine.Init();

        if (autoStart)
        {
            videoEngine.Run();
            audioEngine?.Run();
            State = MediaPlayerStates.Playing;
        }
    }

    private void Desubscribe(VideoEngine videoEngine)
    {
        videoEngine.PlayingProgressChanged -= PlayingProgressChanged;
        videoEngine.VideoPlayingToEnd -= VideoPlayingToEnd;
        videoEngine.MayFetchFrame -= FetchBitmap;
        videoEngine.Dispose();
    }

    private void FetchBitmap(object? invoker, SKBitmap bitmap)
    {
        this.Dispatcher.Dispatch(() =>
        {
            CurrentFrame = bitmap;
        });
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        e.Surface.Canvas.Clear();

        if (CurrentFrame != null)
        {
            var pos = new Point(PositionXPercent, PositionYPercent);
            var vp = new Rect(0, 0, Width, Height);
            var result = SkiaImage.ZoomAndClipBitmap(CurrentFrame, pos, Zoom, vp);
            float drawX = (float)result.pos.X;
            float drawY = (float)result.pos.Y;
            e.Surface.Canvas.DrawBitmap(result.bitmap, drawX, drawY, null);
        }
    }

    #region media player
    public TimeSpan PlayingPosition => videoEngine?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration
    {
        get
        {
            if (cachedVideoMeta == null)
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(cachedVideoMeta.Duration);
        }
    }

    public MediaPlayerStates State
    {
        get => _state;
        private set
        {
            if (_state == value)
                return;

            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public double Zoom { get; set; } = 1.0;
    public double PositionXPercent { get; private set; } = 0.5;
    public double PositionYPercent { get; private set; } = 0.5;

    public event EventHandler<MediaPlayerStates>? StateChanged;
    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler<double>? ZoomChanged;

    public void Pause()
    {
        videoEngine?.Pause();
        audioEngine?.Pause();
        State = MediaPlayerStates.Pause;
    }

    public void Play()
    {
        videoEngine?.Run();
        audioEngine?.Run();
        State = MediaPlayerStates.Playing;
    }

    // STOP (kill source)
    public void Reset()
    {
        if (State == MediaPlayerStates.None) 
            return;

        _currentFrame?.Dispose();
        _currentFrame = null;

        cachedVideoMeta = null;
        cachedAudioMeta = null;

        if (currentSource is IDisposable src)
            src.Dispose();

        if (videoEngine != null)
        {
            Desubscribe(videoEngine);
            videoEngine.Dispose();
            videoEngine = null;
        }

        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }

        State = MediaPlayerStates.None;
        currentSource = null;
    }

    public async Task SeekTo(double progress, CancellationToken cancellation)
    {
        if (cachedVideoMeta == null || currentSource == null)
            return;

        double durationAsSeconds = cachedVideoMeta.Duration * progress;
        bool autoStart = (State == MediaPlayerStates.Playing);

        await UseEngine(currentSource, TimeSpan.FromSeconds(durationAsSeconds), autoStart);
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        PositionXPercent = imagePosPercentX;
        PositionYPercent = imagePosPercentY;
    }

    public async Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new();

        var di = this.DiFetch<IFFMpegService>();
        using var proc = new VideoReader(filePath, di.PathToFFmpegExe, di.PathToFFprobeExe);
        using var proca = new AudioReader(filePath, di.PathToFFmpegExe, di.PathToFFprobeExe);

        var tv = proc.LoadMetadataAsync(cancellation: cancel);
        var ta = proca.LoadMetadataAsync(cancellation: cancel);
        await Task.WhenAll(tv, ta);
        if (cancel.IsCancellationRequested)
            return;

        await MakeMeta(filePath, tv.Result, ta.Result);
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public async Task SetSourceStorage(StorageFile secureFile, CancellationToken cancel)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new();

        var storage = this.DiFetch<IStorageService>();
        var crypt = this.DiFetch<ICrypto>();
        var ff = this.DiFetch<IFFMpegService>();
        var pass = storage.CurrentStorage!.Password!;

        var decryptvideo = await crypt.DecryptFile(secureFile.FilePath, pass, cancel);
        if (decryptvideo.IsFault)
        {
            Debug.WriteLine(decryptvideo.MessageForLog);
            return;
        }

        var decryptaudio = await crypt.DecryptFile(secureFile.FilePath, pass, cancel);
        if (decryptaudio.IsFault)
        {
            Debug.WriteLine(decryptvideo.MessageForLog);
            return;
        }

        var streamvideo = decryptvideo.Result;
        var streamaudio = decryptaudio.Result;
        using var videoProc = new VideoReader(streamvideo, ff.PathToFFmpegExe, ff.PathToFFprobeExe);
        using var audioProc = new AudioReader(streamaudio, ff.PathToFFmpegExe, ff.PathToFFprobeExe);

        streamvideo.Position = 0;
        var mv = await videoProc.LoadMetadataAsync(cancellation: cancel);
        if (cancel.IsCancellationRequested)
            return;

        streamaudio.Position = 0;
        var ma = await audioProc.LoadMetadataAsync(cancellation: cancel);
        if (cancel.IsCancellationRequested)
            return;

        streamvideo.Position = 0;
        streamaudio.Position = 0;
        var doubleStream = new DoubleStream(streamvideo, streamaudio);
        await MakeMeta(doubleStream, mv, ma);
    }
    #endregion media player

    private async Task MakeMeta(object sourceVideo, VideoMetadata vmeta, AudioMetadata ameta)
    {
        cachedAudioMeta = ameta;
        cachedVideoMeta = vmeta;

        currentSource = sourceVideo;
        State = MediaPlayerStates.ReadyToPlaying;
        await UseEngine(sourceVideo, TimeSpan.Zero, true);
    }

    public void Dispose()
    {
        VideoPlayingToEnd -= SkiaFFmpegVideoPlayer_VideoPlayingToEnd;
        Reset();
    }
}