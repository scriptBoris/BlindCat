using System.Timers;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatMauiMobile.Core;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Core;
using FFMpegDll.Models;
using PointF = System.Drawing.PointF;

namespace BlindCatMauiMobile.Controls;

public partial class VideoPlayer : IMediaBase, IDisposable
{
    private const AVHWDeviceType HWACC = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
    private const AVPixelFormat PIXFMT = AVPixelFormat.AV_PIX_FMT_RGBA;
    private const string IMG_STATE_PLAY = "ic_play_circle.png";
    private const string IMG_STATE_PAUSE = "ic_pause_circle.png";
    
    private readonly TapGestureRecognizer _tapGestureRecognizer;
    private readonly IAudioContext _audioService;
    
    private VideoEngine? _videoAtom;
    private AudioEngine? _audioAtom;
    private IReusableContext? _context;
    private PlayerState _state = PlayerState.Stopped;
    private bool _isInteractiveEnabled;
    private System.Timers.Timer _timerProgress;
    private System.Timers.Timer? _timerInteractionTimeout;
    private int _interactionMilisecondsRemaining;
    
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<string?>? ErrorReceived;
    public event EventHandler<FileMetaData[]?>? MetaReceived;

    public VideoPlayer()
    {
        InitializeComponent();

        _audioService = App.Current.Handler.MauiContext.Services.GetRequiredService<IAudioContext>();
        
        _tapGestureRecognizer = new TapGestureRecognizer();
        _tapGestureRecognizer.Tapped += OnTapped;
        this.GestureRecognizers.Add(_tapGestureRecognizer);
        
        _timerProgress = new System.Timers.Timer(100);
        _timerProgress.Elapsed += TimerProgressOnElapsed;
        _timerProgress.AutoReset = true;
    }

    public double Zoom { get; set; } = 1.0;
    public double PositionXPercent { get; private set; } = 0.5;
    public double PositionYPercent { get; private set; } = 0.5;
    public PointF PositionOffset { get; set; }

    public bool IsInteractiveEnabled
    {
        get => _isInteractiveEnabled;
        set
        {
            bool old = _isInteractiveEnabled;
            _isInteractiveEnabled = value;
            _imageCenterPlayState.IsVisible = value;
            _sliderProgress.IsVisible = value;

            if (value == old)
                return;

            if (_isInteractiveEnabled)
            {
                _interactionMilisecondsRemaining = 3000;
                _timerInteractionTimeout = new(50);
                _timerInteractionTimeout.AutoReset = true;
                _timerInteractionTimeout.Elapsed += TimerInteractionTimeoutOnElapsed;
                _timerInteractionTimeout.Start();
            }
            else
            {
                if (_timerInteractionTimeout != null)
                {
                    _timerInteractionTimeout.Stop();
                    _timerInteractionTimeout.Elapsed -= TimerInteractionTimeoutOnElapsed;
                    _timerInteractionTimeout.Dispose();
                    _timerInteractionTimeout = null;
                }
            }
        }
    }

    public void InvalidateSurface()
    {
        if (this is IView v)
            v.InvalidateArrange();
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        throw new NotImplementedException();
    }

    public Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        var audio = new AudioEngine(filePath, _audioService);
        var video = new VideoEngine(filePath, HWACC, PIXFMT);
        return SetupEngine(video, audio, cancel);
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public Task SetSourceStorage(StorageFile file, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public void Play()
    {
        if (_state == PlayerState.Playing)
            return;
        
        _state = PlayerState.Playing;
        _imageCenterPlayState.Source = IMG_STATE_PLAY;
        _videoAtom?.Run();
        _audioAtom?.Run();
        _timerProgress.Start();
    }

    public void Pause()
    {
        if (_state == PlayerState.Paused)
            return;
        
        _state = PlayerState.Paused;
        _imageCenterPlayState.Source = IMG_STATE_PAUSE;
        _videoAtom?.Pause();
        _audioAtom?.Pause();
        _timerProgress.Stop();
    }
    
    public void Reset()
    {
        if (_videoAtom != null)
        {
            _videoAtom.Pause();
            _videoAtom.FrameReady -= VideoAtomOnFrameReady;
            _videoAtom.Dispose();
            _videoAtom = null;
        }

        if (_context != null)
        {
            _context.Dispose();
            _context = null;
            _canvas.SetupContext(null);
        }

        if (_audioAtom != null)
        {
            _audioAtom.Pause();
            _audioAtom.Dispose();
            _audioAtom = null;
        }
        
        _timerProgress.Stop();
        _canvas.SetupContext(null);
        _state = PlayerState.Stopped;
    }

    public FileMetaData[]? GetMeta()
    {
        return null;
    }

    public void Dispose()
    {
        Reset();
        
        _timerProgress.Elapsed -= TimerProgressOnElapsed;
        _timerProgress.Dispose();
        
        _tapGestureRecognizer.Tapped -= OnTapped;
    }
    
    private async Task SetupEngine(VideoEngine video, AudioEngine audio, CancellationToken cancel)
    {
        if (_videoAtom != null)
        {
            _videoAtom.FrameReady -= VideoAtomOnFrameReady;
            _videoAtom.Dispose();
            _videoAtom = null;
        }

        if (_context != null)
        {
            _context.Dispose();
            _context = null;
            _canvas.SetupContext(null);
        }

        if (_audioAtom != null)
        {
            _audioAtom.Dispose();
            _audioAtom = null;
        }

        Task? taskVideoInit = null;
        Task<FFMpegResult>? taskAudioInit = null;
        var tasks = new List<Task>(2);
        if (video.Duration.TotalSeconds > 0.0001)
        {
            _context = new SkiaBitmapPool(video.FrameSize);
            _videoAtom = video;
            _videoAtom.FrameReady += VideoAtomOnFrameReady;
            _videoAtom.SetupContext(_context);
            _canvas.SetupContext(_context);
            taskVideoInit = video.Init(cancel); 
            tasks.Add(taskVideoInit);
        }
        else
        {
            video.Dispose();
        }

        if (audio.HasAudioData)
        {
            _audioAtom = audio;
            taskAudioInit = audio.Init(cancel);
            tasks.Add(taskAudioInit);
        }
        else
        {
            audio.Dispose();
        }

        if (_videoAtom == null && _audioAtom == null)
            throw new InvalidOperationException();
        
        await Task.WhenAll(tasks);
        
        // check audio success init
        if (taskAudioInit != null)
        {
            var resInit = taskAudioInit.Result;
            if (!resInit.IsSuccess)
            {
                _audioAtom?.Dispose();
                _audioAtom = null;
            }
        }
        
        if (!cancel.IsCancellationRequested)
            Play();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        _interactionMilisecondsRemaining = 3000;

        if (IsInteractiveEnabled)
        {
            switch (_state)
            {
                case PlayerState.Playing:
                    Pause();
                    break;
                case PlayerState.Paused:
                    Play();
                    break;
                case PlayerState.Stopped:
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
        else
        {
            IsInteractiveEnabled = true;
            switch (_state)
            {
                case PlayerState.Playing:
                    _imageCenterPlayState.Source = IMG_STATE_PLAY;
                    break;
                case PlayerState.Paused:
                    _imageCenterPlayState.Source = IMG_STATE_PAUSE;
                    break;
                default:
                    break;
            }
        }
    }
    
    private void TimerProgressOnElapsed(object? sender, ElapsedEventArgs e)
    {
        double prog = _timerProgress.Interval;
        var max = _videoAtom?.Duration ?? TimeSpan.Zero;
        double step = 0;
        
        if (max.TotalSeconds > 0.0001)
        {
            step = prog / max.TotalMilliseconds;
        }
        
        this.Dispatcher.Dispatch(() =>
        {
            _sliderProgress.Value += step;
        });
    }
    
    private void TimerInteractionTimeoutOnElapsed(object? sender, ElapsedEventArgs e)
    {
        if (sender is System.Timers.Timer timer)
        {
            int old = _interactionMilisecondsRemaining; 
            _interactionMilisecondsRemaining -= (int)timer.Interval;

            if (_interactionMilisecondsRemaining <= 0 && old > 0)
            {
                timer.Stop();
                this.Dispatcher.Dispatch(() =>
                {
                    IsInteractiveEnabled = false;
                });
            }
        }
    }
    
    private void VideoAtomOnFrameReady(object? sender, EventArgs e)
    {
        this.Dispatcher.Dispatch(() =>
        {
            _canvas.InvalidateSurface();
        });
    }

    private enum PlayerState
    {
        Stopped,
        Playing,
        Paused,
    }
}