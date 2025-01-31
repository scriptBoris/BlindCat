﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.MediaPlayers.Surfaces;
using BlindCatAvalonia.Services;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Models.Media;
using BlindCatCore.Services;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Models;
using SkiaSharp;
using IntSize = System.Drawing.Size;

namespace BlindCatAvalonia.SDcontrols;

public class VideoPlayerSkia : Control, IMediaPlayer
{
    private readonly object _lock = new();
    private readonly IVideoSurface _surface;
    private readonly IFFMpegService _ffmpeg;
    private readonly ICrypto _crypto;
    private readonly IStorageService _storageService;
    private readonly IAudioService _audioService;

    private double _progress;
    private double _progressTick;
    private bool isDisposed;
    private SKBitmap? _currentFrame;
    private VideoEngine? videoEngine;
    private AudioEngine? audioEngine;
    private VideoMetadata? cachedVideoMeta;
    private AudioMetadata? cachedAudioMeta;
    private object? currentSource;
    private System.Timers.Timer? timerProgress;
    private MediaPlayerStates _state = MediaPlayerStates.None;
    private double? _forceScale;
    private System.Drawing.PointF _offset;

    /// <summary>
    /// Видео доигралось до своего конца
    /// (всегда запускается в UI потоке)
    /// </summary>
    public event EventHandler VideoPlayingToEnd;

    public VideoPlayerSkia()
    {
        VideoPlayingToEnd += SkiaFFmpegVideoPlayer_VideoPlayingToEnd;
        
        FFMpegDll.Init.InitializeFFMpeg();

        _ffmpeg = this.DI<IFFMpegService>();
        _crypto = this.DI<ICrypto>();
        _storageService = this.DI<IStorageService>();
        _audioService = this.DI<IAudioService>();
        _surface = new SoftwareRenderSurface();
        //_surface = new OpenGLSurface();
        VisualChildren.Add((Visual)_surface);
    }

    protected IReusableContext? ReuseContext { get; set; }
    public double RenderScale { get; private set; } = -1.0;

    public double? ForceScale
    {
        get => _forceScale;
        set
        {
            _forceScale = value;
            InvalidateMeasure();
        }
    }

    public System.Drawing.PointF Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            InvalidateVisual();
        }
    }

    public Stretch Stretch { get; set; } = Stretch.Uniform;

    public StretchDirection StretchDirection { get; set; } = StretchDirection.Both;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Dispose();
    }

    protected virtual void OnScaleChanged(double scale)
    {
        RenderScale = scale;
        Dispatcher.UIThread.Post(() =>
        {
            ZoomChanged?.Invoke(this, scale);
        });
    }

    private void ResetOffsetAndScale(bool redraw = true)
    {
        _offset = new System.Drawing.PointF();
        _forceScale = null;
        if (redraw)
            InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size result = default;
        try
        {
            if (ReuseContext == null)
            {
                result = new Size(0, 0);
                return result;
            }

            if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
            {
                result = availableSize;
                return availableSize;
            }

            result = Stretch.CalculateSize(availableSize, ReuseContext.FrameSize, StretchDirection);
            return result;
        }
        finally
        {
            var surfaceView = (Control)_surface;
            surfaceView.Measure(result);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size result;
        var sourceSize = ReuseContext?.FrameSize ?? default;
        var sur = (Control)_surface;

        if (ReuseContext is null || ReuseContext.IsDisposed)
        {
            result = finalSize;
        }
        else if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            result = finalSize;
        }
        else
        {
            result = Stretch.CalculateSize(finalSize, sourceSize);
        }

        if (sourceSize.Width > 0 && sourceSize.Height > 0)
        {
            var scale = Stretch.CalculateScaling(finalSize, sourceSize, StretchDirection);
            if (ForceScale != null)
            {
                double forceScale = ForceScale.Value;
                double x = forceScale / Math.Sqrt(2);
                scale = new Vector(x, x);
            }

            if (RenderScale != scale.Length)
                OnScaleChanged(scale.Length);
        }

        var viewPort = new Rect(0, 0, finalSize.Width, finalSize.Height); 
        _surface.Matrix = MakeMatrix(sourceSize, viewPort);
        sur.Arrange(viewPort);
        return result;
    }

    private Matrix MakeMatrix(Size sourceSize, Rect viewPort)
    {
        if (ReuseContext == null || ReuseContext.IsDisposed)
        {
            return default;
        }

        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return default;
        }

        if (VisualRoot == null)
            return default;

        var scale = Stretch.CalculateScaling(viewPort.Size, sourceSize, StretchDirection);
        if (ForceScale != null)
        {
            double forceScale = ForceScale.Value;
            double x = forceScale / Math.Sqrt(2);
            scale = new Vector(x, x);
        }

        if (RenderScale != scale.Length)
            OnScaleChanged(scale.Length);

        var scaledSize = sourceSize * scale;
        var centerRect = viewPort.CenterRect(new Rect(scaledSize));

        var destRect = centerRect.Intersect(viewPort);

        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));

        var bounds = new Rect(0, 0, ReuseContext.IntFrameSize.Width, ReuseContext.IntFrameSize.Height);
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);

        double offsetX = Offset.X;
        double offsetY = Offset.Y;

        double transX = centerRect.X + offsetX;
        double transY = centerRect.Y + offsetY;

        double mod1 = transX * (sourceRect.Width / destRect.Width);
        double mod2 = transY * (sourceRect.Height / destRect.Height);
        var translateMatrix = Matrix.CreateTranslation(mod1, mod2);

        if (destRect == default)
        {
            return default;
        }

        return translateMatrix * scaleMatrix;
    }

    private async void SkiaFFmpegVideoPlayer_VideoPlayingToEnd(object? sender, EventArgs e)
    {
        if (currentSource == null || videoEngine == null)
            throw new InvalidOperationException();

        if (!Dispatcher.UIThread.CheckAccess())
            throw new InvalidOperationException("Required execution on main thread");

        await SeekTo(0, CancellationToken.None);
        Play();
    }

    private async Task UseEngine(object source, TimeSpan startFrom, bool autoStart, CancellationToken cancel)
    {
        object audioSource;
        object videoSource;

        if (source is DoubleStream ds)
        {
            audioSource = ds.Audio;
            videoSource = ds.Video;
        }
        else
        {
            audioSource = source;
            videoSource = source;
        }

        // old dispose
        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }

        if (videoEngine != null)
        {
            videoEngine.FrameReady -= OnFrameReady;
            videoEngine.Dispose();
            videoEngine = null;
        }

        if (timerProgress != null)
        {
            timerProgress.Stop();
            timerProgress.Elapsed -= TimerProgress_Elapsed;
            timerProgress.Dispose();
            timerProgress = null;
        }

        if (ReuseContext != null)
        {
            ReuseContext.Dispose();
            ReuseContext = null;
        }

        // new instances
        if (cachedAudioMeta != null && cachedAudioMeta.Streams.Length > 0)
        {
            audioEngine = new AudioEngine(audioSource, startFrom, cachedAudioMeta!, _audioService);
        }

        if (cachedVideoMeta != null && cachedVideoMeta.Streams.Length > 0)
        {
            videoEngine = new VideoEngine(videoSource, startFrom, cachedVideoMeta!);
            videoEngine.FrameReady += OnFrameReady;
            videoEngine.EndOfVideo += OnEndOfVideo;
        }

        // init
        var inits = new List<Task>();
        if (audioEngine != null)
            inits.Add(audioEngine.Init(cancel));

        if (videoEngine != null)
            inits.Add(videoEngine.Init(cancel));

        if (videoEngine == null || cachedVideoMeta == null)
        {
            SetError("No video data");
            return;
        }

        await Task.WhenAll(inits);

        if (cancel.IsCancellationRequested)
            return;

        double rateMs = cachedVideoMeta.AvgFramerate;
        timerProgress = new System.Timers.Timer(rateMs);
        timerProgress.Elapsed += TimerProgress_Elapsed;
        timerProgress.AutoReset = true;
        CalculateProgress(startFrom);
        _progressTick = rateMs / Duration.TotalMilliseconds;
        ReuseContext = new ReuseContextSkia(new IntSize(cachedVideoMeta.Width, cachedVideoMeta.Height), videoEngine);
        InvalidateMeasure();
        _surface.SetupSource(ReuseContext);

        if (autoStart)
        {
            videoEngine?.Run();
            audioEngine?.Run();
            timerProgress.Start();
            State = MediaPlayerStates.Playing;
        }
    }

    private void CalculateProgress(TimeSpan startFrom)
    {
        _progress = (Duration.TotalSeconds > 0 && startFrom.TotalSeconds > 0) ? (double)(startFrom.TotalSeconds / Duration.TotalSeconds) : 0;
    }

    private void TimerProgress_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        lock (_lock)
        {
            _progress += _progressTick;
            if (_progress >= 1)
            {
                _progress = 1;
                // if (sender is System.Timers.Timer self)
                //     self.Stop();

                // Dispatcher.UIThread.Post(() =>
                // {
                //     VideoPlayingToEnd.Invoke(this, EventArgs.Empty);
                // });
            }

            PlayingProgressChanged?.Invoke(this, _progress);
        }
    }

    private void OnFrameReady(object? invoker, IFrameData frameData)
    {
        var ct = ReuseContext as ReuseContextSkia;
        ct?.Push(frameData);
        _surface.OnFrameReady();
    }

    private void OnEndOfVideo(object? invoker, EventArgs eventArgs)
    {
        VideoPlayingToEnd?.Invoke(this, eventArgs);
    }

    #region media player
    public TimeSpan PlayingPosition => _progress * Duration;
    public TimeSpan Duration
    {
        get
        {
            if (cachedVideoMeta != null && cachedVideoMeta.Streams.Length > 0)
            {
                return TimeSpan.FromSeconds(cachedVideoMeta.Duration);
            }
            else if (cachedAudioMeta != null && cachedAudioMeta.Streams.Length > 0)
            {
                return TimeSpan.FromSeconds(cachedAudioMeta.Duration);
            }
            return TimeSpan.Zero;
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

    public double Zoom
    {
        get => RenderScale;
        set
        {
            if (value <= 0.2)
                value = 0.2;
            else if (value >= 5.0)
                value = 5.0;

            ForceScale = value;
        }
    }
    public double PositionXPercent { get; private set; } = 0.5;
    public double PositionYPercent { get; private set; } = 0.5;
    public System.Drawing.PointF PositionOffset
    {
        get => Offset;
        set => Offset = value;
    }

    public event EventHandler<MediaPlayerStates>? StateChanged;
    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<string?>? ErrorReceived;
    public event EventHandler<FileMetaData[]?>? MetaReceived;

    public void Pause()
    {
        videoEngine?.Pause();
        audioEngine?.Pause();
        timerProgress?.Stop();
        State = MediaPlayerStates.Pause;
    }

    public void Play()
    {
        videoEngine?.Run();
        audioEngine?.Run();
        timerProgress?.Start();
        State = MediaPlayerStates.Playing;
    }

    // STOP (kill source)
    public void Reset()
    {
        try
        {
            if (State == MediaPlayerStates.None)
                return;

            _currentFrame?.Dispose();
            _currentFrame = null;

            cachedVideoMeta = null;
            cachedAudioMeta = null;

            if (currentSource is IDisposable src)
                src.Dispose();

            currentSource = null;

            if (videoEngine != null)
            {
                videoEngine.FrameReady -= OnFrameReady;
                videoEngine.Dispose();
                videoEngine = null;
            }

            if (audioEngine != null)
            {
                audioEngine.Dispose();
                audioEngine = null;
            }

            if (timerProgress != null)
            {
                timerProgress.Stop();
                timerProgress.Elapsed -= TimerProgress_Elapsed;
                timerProgress.Dispose();
                timerProgress = null;
            }

            if (ReuseContext != null)
            {
                ReuseContext.Dispose();
                ReuseContext = null;
            }

            State = MediaPlayerStates.None;
        }
        finally
        {
            ResetOffsetAndScale(false);
        }
    }

    public async Task SeekTo(double seekProgress, CancellationToken cancellation)
    {
        if (cachedVideoMeta == null || currentSource == null || videoEngine == null)
            return;

        double durationAsSeconds = Duration.TotalSeconds * seekProgress;
        var time = TimeSpan.FromSeconds(durationAsSeconds);
        
        CalculateProgress(time);
        await videoEngine.SeekTo(time, cancellation);
        audioEngine?.SeekTo(time);
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        PositionXPercent = imagePosPercentX;
        PositionYPercent = imagePosPercentY;
    }

    public async Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        var metaSize = MakeMeta(filePath, null, null, true, false);
        MetaReceived?.Invoke(this, metaSize);
        
        using var proc = new VideoFileDecoder(filePath, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
        using var proca = new AudioFileDecoder(filePath);

        // using var proc = new VideoReader2(filePath, _ffmpeg.PathToFFmpegExe, _ffmpeg.PathToFFprobeExe);
        // using var proca = new AudioReader(filePath, _ffmpeg.PathToFFmpegExe, _ffmpeg.PathToFFprobeExe);

        var tv = proc.LoadMetadataAsync(cancel);
        var ta = proca.LoadMetadataAsync(cancel);
        await Task.WhenAll(tv, ta);
        if (cancel.IsCancellationRequested)
            return;

        var metaMedia = MakeMeta(filePath, tv.Result, ta.Result, false, true);
        MetaReceived?.Invoke(this, metaMedia);
        await SetupMetaAndPlay(filePath, tv.Result, ta.Result, cancel);
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public async Task SetSourceStorage(StorageFile secureFile, CancellationToken cancel)
    {
        string password = _storageService.CurrentStorage!.Password!;

        IVideoDecoder? videoProc = null;
        IAudioDecoder? audioProc = null;
        var id = secureFile.Storage.Guid;
        long? size = secureFile.OriginFileSize;

        try
        {
            object source;
            if (secureFile.EncryptionMethod == EncryptionMethods.CENC)
            {
                var fileCenc = new FileCencArgs
                {
                    Key = _crypto.ToCENCPassword(secureFile.Storage.Password!),
                    Kid = _crypto.GetKid(),
                };
                videoProc = new VideoFileDecoder(secureFile.FilePath, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, fileCenc);
                audioProc = new AudioFileDecoder(secureFile.FilePath, fileCenc);
                source = fileCenc;
            }
            else if (secureFile.EncryptionMethod == EncryptionMethods.dotnet)
            {
                var decryptvideo = await _crypto.DecryptFile(id, secureFile.FilePath, password, size, cancel);
                if (decryptvideo.IsFault)
                {
                    Debug.WriteLine(decryptvideo.MessageForLog);
                    return;
                }

                var decryptaudio = await _crypto.DecryptFile(id, secureFile.FilePath, password, size, cancel);
                if (decryptaudio.IsFault)
                {
                    Debug.WriteLine(decryptvideo.MessageForLog);
                    return;
                }

                var streamvideo = decryptvideo.Result;
                var streamaudio = decryptaudio.Result;

                videoProc = new VideoStreamDecoder(streamvideo, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
                audioProc = new AudioStreamDecoder(streamaudio);
                source = new DoubleStream(streamvideo, streamaudio);
            }
            else
            {
                throw new NotSupportedException();
            }

            var mv = await videoProc.LoadMetadataAsync(cancel);
            if (cancel.IsCancellationRequested)
                return;

            var ma = await audioProc.LoadMetadataAsync(cancel);
            if (cancel.IsCancellationRequested)
                return;
            
            if (source is DoubleStream dstr)
                dstr.Reset();

            await SetupMetaAndPlay(source, mv, ma, cancel);
        }
        finally
        {
            videoProc?.Dispose();
            audioProc?.Dispose();
        }
    }

    public void InvalidateSurface()
    {
        InvalidateArrange();
    }
    #endregion media player

    private Task SetupMetaAndPlay(object sourceVideo, VideoMetadata vmeta, AudioMetadata? ameta, CancellationToken cancel)
    {
        cachedAudioMeta = ameta;
        cachedVideoMeta = vmeta;

        currentSource = sourceVideo;
        State = MediaPlayerStates.ReadyToPlaying;
        return UseEngine(sourceVideo, TimeSpan.Zero, true, cancel);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        VideoPlayingToEnd -= SkiaFFmpegVideoPlayer_VideoPlayingToEnd;
        Reset();
    }

    public FileMetaData[]? GetMeta()
    {
        return MakeMeta(currentSource, cachedVideoMeta, cachedAudioMeta, true, true);
    }

    public void SetError(string error)
    {
        ErrorReceived?.Invoke(this, error);
    }

    private static FileMetaData[]? MakeMeta(object? currentSource, VideoMetadata? cachedVideoMeta, AudioMetadata? cachedAudioMeta, bool useFileSize, bool useMediaMeta)
    {
        if (currentSource == null)
            return null;

        string filePath;
        switch (currentSource)
        {
            case string file:
                filePath = file;
                break;
            case StorageFile sfile:
                filePath = sfile.FilePath;
                break;
            default:
                throw new NotSupportedException();
        }

        var res = new List<FileMetaData>();
        var metaitems = new ObservableCollection<FileMetaItem>();
        var metaVideoItems = new ObservableCollection<FileMetaItem>();
        res.Add(new FileMetaData
        {
            GroupName = "Meta",
            MetaItems = metaitems,
        });

        if (useFileSize)
        {
            var fileInfo = new FileInfo(filePath);
            metaitems.Insert(0, new FileMetaItem
            {
                Meta = "File size",
                Value = fileInfo.Length.ToString(),
            });
        }

        if (useMediaMeta)
        {
            if (cachedVideoMeta == null && cachedAudioMeta == null)
                return null;

            if (cachedVideoMeta != null)
            {
                metaitems.Add(new FileMetaItem
                {
                    Meta = "Width",
                    Value = cachedVideoMeta.Width.ToString(),
                });

                metaitems.Add(new FileMetaItem
                {
                    Meta = "Height",
                    Value = cachedVideoMeta.Height.ToString(),
                });

                var dur = TimeSpan.FromSeconds(cachedVideoMeta.Duration);
                var durationTxt = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}",
                    (int)dur.TotalHours, // Часы (включая дни)
                    dur.Minutes, // Минуты
                    dur.Seconds, // Секунды
                    dur.Milliseconds);
                metaitems.TryAdd("Duration", durationTxt);
                metaitems.TryAdd("Avg video framerate", cachedVideoMeta.AvgFramerate.ToString());
                metaitems.TryAdd("Avg video bit rate", cachedVideoMeta.BitRate.ToString());
                metaitems.TryAdd("Video codec", cachedVideoMeta.Codec);
                metaitems.TryAdd("Video codec full", cachedVideoMeta.CodecLongName);
                metaitems.TryAdd("Pixel format", cachedVideoMeta.PixelFormat);

                var vid = cachedVideoMeta.GetFirstVideoStream();
                if (vid != null)
                {
                    metaVideoItems.TryAdd("Display aspect ratio", vid.DisplayAspectRatio);
                    metaVideoItems.TryAdd("Color space", vid.ColorSpace);
                    metaVideoItems.TryAdd("Color range", vid.ColorRange);
                    metaVideoItems.TryAdd("Color primaries", vid.ColorPrimaries);
                    metaVideoItems.TryAdd("Color transfer", vid.ColorTransfer);
                    metaVideoItems.TryAdd("Profile", vid.Profile);
                    metaVideoItems.TryAdd("Start time", vid.StartTime);
                }

                if (metaVideoItems.Count > 0)
                {
                    res.Add(new FileMetaData
                    {
                        GroupName = "Video",
                        MetaItems = metaVideoItems,
                    });
                }
            }

            if (cachedAudioMeta != null)
            {
                foreach (var audioStr in cachedAudioMeta.Streams)
                {
                    var ametaItems = new ObservableCollection<FileMetaItem>();

                    ametaItems.TryAdd("Bit rate", audioStr.BitRate.ToString());
                    ametaItems.TryAdd("Sample rate", audioStr.SampleRate.ToString());
                    ametaItems.TryAdd("Codec", audioStr.CodecName);
                    ametaItems.TryAdd("Codec full", audioStr.CodecLongName);
                    ametaItems.TryAdd("Channel layout", audioStr.ChannelLayout);
                    ametaItems.TryAdd("Channels", audioStr.Channels.ToString());
                    ametaItems.TryAdd("Language", audioStr.Language);

                    if (ametaItems.Count > 0)
                        res.Add(new FileMetaData
                        {
                            GroupName = "Audio",
                            MetaItems = ametaItems,
                        });
                }
            }
        }

        return res.ToArray();
    }

    private class DoubleStream(Stream video, Stream audio) : IDisposable
    {
        public Stream Video { get; } = video;
        public Stream Audio { get; } = audio;

        public void Dispose()
        {
            Video.Dispose();
            Audio.Dispose();
        }

        public void Reset()
        {
            Video.Position = 0;
            Audio.Position = 0;
        }
    }

    private class ReuseContextSkia : IReusableContext
    {
        private readonly FramePool _framePool;

        public ReuseContextSkia(IntSize intFrameSize, VideoEngine engine)
        {
            _framePool = new FramePool(engine);
            IntFrameSize = intFrameSize;
        }

        public IntSize IntFrameSize { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Push(IFrameData frame)
        {
            _framePool.Add(frame);
        }

        public IFrameData? GetFrame()
        {
            return _framePool.FetchActualLoseOther2();
        }

        public void RecycleFrame(IFrameData data)
        {
            _framePool.Free(data);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _framePool.Dispose();
        }
    }

    private class FramePool : IDisposable
    {
        private readonly List<IFrameData> _listDraw = [];
        private readonly List<IFrameData> _listReady = [];
        private readonly VideoEngine _videoEngine;
        private readonly object _lock = new();
        private bool _disposed;
        private IFrameData? _current;

        public FramePool(VideoEngine videoEngine)
        {
            _videoEngine = videoEngine;
        }

        public int Count => _listReady.Count;

        public void Add(IFrameData frame)
        {
            lock (_lock)
            {
                _listReady.Add(frame);
            }
        }

        public IFrameData? FetchActualLoseOther2()
        {
            lock (_lock)
            {
                var frame = FetchActualLoseOther();
                if (frame == null)
                {
                    frame = _current;
                }
                else
                {
                    _current = frame;
                }
                return frame;
            }
        }

        public IFrameData? FetchActualLoseOther()
        {
            lock (_lock)
            {
                switch (_listReady.Count)
                {
                    // hard code for improve performance
                    case 0:
                        return null;
                    case 1:
                        var frameData1 = _listReady[0];

                        frameData1.IsLocked = true;
                        _listReady.Remove(frameData1);
                        _listDraw.Add(frameData1);
                        return frameData1;
                    case 2:
                        var f2_0 = _listReady[0];
                        var frameData2 = _listReady[1];

                        _listReady.Remove(f2_0);
                        _videoEngine.Recycle(f2_0);

                        frameData2.IsLocked = true;
                        _listReady.Remove(frameData2);
                        _listDraw.Add(frameData2);
                        return frameData2;
                    case 3:
                        var f3_0 = _listReady[0];
                        var f3_1 = _listReady[1];
                        var frameData3 = _listReady[2];

                        _listReady.Remove(f3_0);
                        _videoEngine.Recycle(f3_0);

                        _listReady.Remove(f3_1);
                        _videoEngine.Recycle(f3_1);

                        frameData3.IsLocked = true;
                        _listReady.Remove(frameData3);
                        _listDraw.Add(frameData3);
                        return frameData3;
                    case 4:
                        var f4_0 = _listReady[0];
                        var f4_1 = _listReady[1];
                        var f4_2 = _listReady[2];
                        var frameData4 = _listReady[3];

                        _listReady.Remove(f4_0);
                        _videoEngine.Recycle(f4_0);

                        _listReady.Remove(f4_1);
                        _videoEngine.Recycle(f4_1);

                        _listReady.Remove(f4_2);
                        _videoEngine.Recycle(f4_2);

                        frameData4.IsLocked = true;
                        _listReady.Remove(frameData4);
                        _listDraw.Add(frameData4);
                        return frameData4;
                    default:
                        var last = _listReady.Last();
                        last.IsLocked = true;
                        _listReady.Remove(last);
                        _listDraw.Add(last);

                        for (int i = _listReady.Count - 1; i >= 0; i--)
                        {
                            var del = _listReady[i];
                            _listReady.Remove(del);
                            _videoEngine.Recycle(del);
                        }

                        return last;
                }
            }
        }

        public IFrameData? FetchCarefulAndLock()
        {
            lock (_lock)
            {
                switch (_listReady.Count)
                {
                    case 0:
                        return null;
                    default:
                        var frameData = _listReady.First();
                        frameData.IsLocked = true;
                        _listReady.Remove(frameData);
                        _listDraw.Add(frameData);
                        return frameData;
                }
            }
        }

        public void Free(IFrameData data)
        {
            lock (_lock)
            {
                if (_listDraw.Remove(data))
                {
                    _videoEngine.Recycle(data);
                }
                else
                {
                    // todo фрейм не находится в коллекции отрисованных, что-то пошло не так?
                    //Debugger.Break();
                    //throw new InvalidOperationException();
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _listDraw.Clear();
                _listReady.Clear();
                GC.SuppressFinalize(this);
            }
        }
    }
}