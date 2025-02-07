using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Core;
using FFMpegDll.Models;

namespace BlindCatAvalonia.Core;

public class VideoEngine : IDisposable
{
    private readonly TimeSpan _startingTime;
    private readonly VideoMetadata _meta;
    private System.Timers.Timer _timerFramerate;
    private IVideoDecoder _videoDecoder;
    private Thread _engineThread;
    private bool _isDisposed;
    private bool _isEngineRunning;
    private bool _isEndVideo;

    private readonly object _locker = new();
    private readonly object _timerLocker = new();
    
    private IReusableContext? _context;
    
    /// <summary>
    /// Тик таймера по которому можно отрисовывать следующий фрейм
    /// </summary>
    public event EventHandler? FrameReady;
    public event EventHandler? EndOfVideo;

    public VideoEngine(object play,
        TimeSpan startFrom,
        VideoMetadata meta)
    {
        if (meta.AvgFramerate <= 0.001)
            throw new InvalidOperationException("Invalid video meta data");

        FFMpegDll.Init.InitializeFFMpeg();

        var hwacc = FFmpeg.AutoGen.Abstractions.AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
        var pixfmt = FFmpeg.AutoGen.Abstractions.AVPixelFormat.AV_PIX_FMT_RGBA;
        _meta = meta;
        switch (play)
        {
            case string filePath:
                _videoDecoder = new FFMpegDll.VideoFileDecoder(filePath, hwacc, pixfmt);
                break;
            case Stream stream:
                _videoDecoder = new FFMpegDll.VideoStreamDecoder(stream, hwacc, pixfmt);
                break;
            //case FileCENC fileCENC:
            //    videoReader = new RawVideoReader(fileCENC, pathToFFmpegExe);
            //    break;
            default:
                throw new NotImplementedException();
        };

        var interval = TimeSpan.FromSeconds(1.0 / meta.AvgFramerate);

        HWDevice = hwacc;
        _startingTime = startFrom;
        _engineThread = new(Engine);
        _engineThread.Name = "Engine (ffmpeg frame reader)";
        _timerFramerate = new();
        _timerFramerate.Elapsed += OnTimerFramerate;
        _timerFramerate.Enabled = true;
        _timerFramerate.AutoReset = true;
        _timerFramerate.Interval = interval.TotalMilliseconds;
    }

    public AVHWDeviceType HWDevice { get; private set; }
    public bool CanSeeking => true;

    public Task Init(CancellationToken cancel)
    {
        if (_startingTime.Ticks > 0)
            return SeekTo(_startingTime, cancel);

        var decodeRes = _videoDecoder.TryDecodeNextFrame();
        if (decodeRes.IsSuccessed)
        {
            // todo сделать вставку первого фрейма в конвеер?
        }

        return Task.CompletedTask;
    }

    public Task SeekTo(TimeSpan position, CancellationToken cancel)
    {
        _videoDecoder.SeekTo(position);
        _isEndVideo = false;
        return Task.CompletedTask;
    }

    public void Run()
    {
        if (!_isEngineRunning)
        {
            _isEngineRunning = true;
            _engineThread.Start();
        }
        _timerFramerate.Start();
    }

    public void Pause()
    {
        _timerFramerate.Stop();
    }

    private void OnTimerFramerate(object? sender, ElapsedEventArgs e)
    {
        lock (_timerLocker)
        {
            if (_isDisposed || !_timerFramerate.Enabled)
                return;
            
            if (_isEndVideo)
            {
                _timerFramerate.Stop();
                Dispatcher.UIThread.Post(() =>
                {
                    EndOfVideo?.Invoke(this, EventArgs.Empty);
                });
            }
            else
            {
                FrameReady?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    public void SetupContext(IReusableContext reuseContext)
    {
        _context = reuseContext;
    }

    private void Engine()
    {
        while (!_isDisposed)
        {
            int queueCount = _context?.QueuedFrames ?? 0;
            if (queueCount >= 2 || _isEndVideo)
            {
                Thread.Sleep(1);
                continue;
            }

            var decodeResult = _videoDecoder.TryDecodeNextFrame();
            if (_isDisposed)
                break;

            if (decodeResult.IsEndOfStream)
            {
                _isEndVideo = true;
                continue;
            }
            
            if (!decodeResult.IsSuccessed)
            {
                continue;
            }
            
            _context?.PushFrame(decodeResult.FrameBitmapRGBA8888);
        }

        if (_isDisposed)
        {
            TryDisposeEngine(true);
        }
    }

    private void TryDisposeEngine(bool force = false)
    {
        bool canDispose;

        if (force)
            canDispose = true;
        else
            canDispose = !_isEngineRunning;

        if (!canDispose)
            return;

        _videoDecoder.Dispose();
        _videoDecoder = null!;
        Debug.WriteLine("_videoDecoder is disposed");
    }

    public void Dispose()
    {
        lock (_locker)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _timerFramerate.Stop();
            _timerFramerate.Elapsed -= OnTimerFramerate;
            _timerFramerate.Dispose();
            _timerFramerate = null!;
            Debug.WriteLine("_timerFramerate is disposed");

            TryDisposeEngine();

            _engineThread = null!;
            GC.SuppressFinalize(this);
            Debug.WriteLine("Video engine is fully disposed");
        }
    }
}