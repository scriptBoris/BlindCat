using CryMediaAPI.Video;
using CryMediaAPI.Video.Models;
using BlindCatMaui.Services;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using Size = System.Drawing.Size;
using BlindCatCore.Services;

namespace BlindCatMaui.Core;

public class VideoEngine : IDisposable
{
    private readonly int _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly VideoMetadata _meta;
    private readonly IDispatcher _dispatcher;
    private readonly ConcurrentQueue<SKBitmap> _bitmapsBuffer = new();
    private readonly Size? _resize;
    private System.Timers.Timer timer;
    private RawVideoReader videoReader;
    private Thread engine;
    private bool isDisposed;
    private int currentFrameNumber;
    private bool isEngineRunning;

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? VideoPlayingToEnd;
    public event EventHandler<SKBitmap>? MayFetchFrame;

    public VideoEngine(object play,
        TimeSpan startFrom,
        VideoMetadata meta,
        IFFMpegService ffmpeg,
        IDispatcher dispatcher)
    {
        _meta = meta;
        _dispatcher = dispatcher;
        switch (play)
        {
            case string filePath:
                videoReader = new RawVideoReader(filePath, ffmpeg.PathToFFmpegExe);
                break;
            case Stream stream:
                videoReader = new RawVideoReader(stream, ffmpeg.PathToFFmpegExe);
                break;
            case DoubleStream doubleStream:
                videoReader = new RawVideoReader(doubleStream.Video, ffmpeg.PathToFFmpegExe);
                break;
            default:
                throw new NotImplementedException();
        };

        _duration = TimeSpan.FromSeconds(meta.Duration);
        _pauseForFrameRate = TimeSpan.FromSeconds(1 / meta.AvgFramerate);
        _totalFrames = meta.PredictedFrameCount;

        if (startFrom.TotalSeconds > 0)
        {
            double seek = startFrom.TotalSeconds;
            currentFrameNumber = (int)((seek * meta.PredictedFrameCount) / meta.Duration);
        }

        Position = startFrom;
        engine = new(Engine);
        timer = new();
        timer.Elapsed += OnTimer;
        timer.Enabled = true;
        timer.AutoReset = true;
        timer.Interval = _pauseForFrameRate.TotalMilliseconds;

        try
        {
            var split = _meta.SampleAspectRatio?.Split(':');
            if (split != null)
            {
                float ratioW = float.Parse(split[0]);
                float ratioH = float.Parse(split[1]);
                float coofV = ratioH / ratioW;

                int neww = (int)_meta.Width;
                int newh = (int)((float)_meta.Height * coofV);
                _resize = new Size(neww, newh);
            }
        }
        catch (Exception)
        {
        }
    }

    public TimeSpan Position { get; private set; }

    public void Init()
    {
        videoReader.Load(Position.TotalSeconds);
    }
    
    public void Run()
    {
        if (!isEngineRunning)
        {
            isEngineRunning = true;
            engine.Start();
        }
        timer.Start();
    }

    public void Pause()
    {
        timer.Stop();
    }

    private void OnTimer(object? sender, ElapsedEventArgs e)
    {
        if (_bitmapsBuffer.TryDequeue(out var bitmap))
        {
            currentFrameNumber++;
            MayFetchFrame?.Invoke(this, bitmap);

            if (currentFrameNumber >= _totalFrames)
            {
                Position = _duration;
                PlayingProgressChanged?.Invoke(this, 1);
                VideoPlayingToEnd?.Invoke(this, null!);
            }
            else
            {
                double progress = (double)currentFrameNumber / (double)_totalFrames;
                Position = _duration * progress;
                PlayingProgressChanged?.Invoke(this, progress);
            }
        }
    }

    private void Engine()
    {
        //using var frame = new VideoFrame(_meta.Width, _meta.Height);
        int width = _meta.Width;
        int height = _meta.Height;
        int size = width * height * 3;
        bool isLastFrame = false;
        byte[] frame = new byte[size];
        var frameDurationMs = 1000 / _meta.AvgFramerate;
        long startTime = Stopwatch.GetTimestamp();

        while (true)
        {
            if (isDisposed)
                break;

            if (_bitmapsBuffer.Count >= 15)
            {
                Thread.Sleep(3);
                continue;
            }

            //videoReader.NextFrame(frame);
            int totalReadBytes = 0;
            while (totalReadBytes < size)
            {
                int readBytes = videoReader.Output?.Read(frame, totalReadBytes, size - totalReadBytes) ?? 0;
                if (readBytes <= 0)
                {
                    if (totalReadBytes == 0)
                        isLastFrame = true;

                    break;
                }

                totalReadBytes += readBytes;
            }

            videoReader.Output?.Flush();

            if (isDisposed)
                break;

            SKBitmap bitmap;

            if (_resize != null)
            {
                using var bitmap1 = FFMpegService.CreateBitmapFromRGB24(frame, width, height);
                bitmap = SkiaExt.ResizeBitmap(bitmap1, _resize.Value.Width, _resize.Value.Height);
            }
            else
            {
                bitmap = FFMpegService.CreateBitmapFromRGB24(frame, width, height);
            }

            _bitmapsBuffer.Enqueue(bitmap);

            if (isLastFrame)
                break;

            //Debug.WriteLine($"{currentTimePosition.TotalSeconds} " +
            //    $"of {duration.TotalSeconds} | " +
            //    $"percent: {prog}; " +
            //    $"stream_pos: {strSource.Position}; " +
            //    $"frame: {currentFrameNumber} " +
            //    $"of {totalFrames}");
        }

        videoReader.Dispose();
        videoReader = null!;

        for (int i = _bitmapsBuffer.Count - 1; i >= 0; i--)
            if (_bitmapsBuffer.TryDequeue(out var bitmap))
                bitmap.Dispose();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        if (!isEngineRunning)
        {
            videoReader.Dispose();
            videoReader = null!;
        }
        engine = null!;
    }
}