﻿using FFMpegProcessor.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Platform;
using System.Diagnostics;
using FFmpegDll;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using FFMpegDll;
using FFmpeg.AutoGen.Abstractions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using IntSize = System.Drawing.Size;

namespace BlindCatAvalonia.Core;

public class VideoEngine : IDisposable
{
    private readonly int _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly VideoMetadata _meta;
    private readonly ConcurrentQueue<IFrameData> _frameBuffer = new();
    private readonly IntSize? _resize;
    private System.Timers.Timer timer;
    private VideoFileDecoder videoReader;
    private Thread engine;
    private bool isDisposed;
    private int currentFrameNumber;
    private bool isEngineRunning;
    private bool isEndVideo;
    private int framesCounter = 0;
    private readonly object _locker = new();
    private readonly object _timerLocker = new();
    private readonly ConcurrentBag<FrameDataNative> _recyrclePool = [];
    private readonly List<FrameDataNative> _totalPool = [];

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? VideoPlayingToEnd;
    public event EventHandler<IFrameData>? MayFetchFrame2;

    public VideoEngine(object play,
        TimeSpan startFrom,
        VideoMetadata meta,
        string pathToFFmpegExe)
    {
        if (meta.AvgFramerate == 0)
            throw new InvalidOperationException("Invalid video meta data");

        FFMpegDll.Init.InitializeFFMpeg();

        _meta = meta;
        switch (play)
        {
            case string filePath:
                videoReader = new VideoFileDecoder(filePath, FFmpeg.AutoGen.Abstractions.AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
                break;
            //case Stream stream:
            //    videoReader = new RawVideoReader(stream, pathToFFmpegExe);
            //    break;
            //case FileCENC fileCENC:
            //    videoReader = new RawVideoReader(fileCENC, pathToFFmpegExe);
            //    break;
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
        engine.Name = "Engine (ffmpeg frame reader)";
        timer = new();
        timer.Elapsed += OnTimer;
        timer.Enabled = true;
        timer.AutoReset = true;
        timer.Interval = _pauseForFrameRate.TotalMilliseconds;

        // make frames 1, 2
        var f1 = MakeHard(_meta, "1");
        var f2 = MakeHard(_meta, "2");
        _recyrclePool.Add(f1);
        _recyrclePool.Add(f2);

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
                _resize = new IntSize(neww, newh);
            }
        }
        catch (Exception)
        {
        }
    }

    public TimeSpan Position { get; private set; }
    public AVHWDeviceType HWDevice { get; private set; }
    public bool CanSeeking => true;

    public Task Init(CancellationToken cancel)
    {
        if (Position.Ticks > 0)
            return SeekTo(Position, cancel);
        //int width = _meta.Width;
        //int height = _meta.Height;

        //videoReader.TryDecodeNextFrame(out var frame);
        //return videoReader.Load(Position.TotalSeconds, width, height, cancel);
        return Task.CompletedTask;
    }

    public Task SeekTo(TimeSpan position, CancellationToken cancel)
    {
        Position = position;
        videoReader.SeekTo(Position);
        return Task.CompletedTask;
    }

    public void Run()
    {
        //if (videoReader.AlreadyFrame != null)
        //{
        //    int width = _meta.Width;
        //    int height = _meta.Height;
        //    byte[] frame = videoReader.AlreadyFrame;
        //    //Bitmap bitmap = AutoMakeBitmap(frame, width, height);
        //    var f = MakeFrame(frame);
        //    _frameBuffer.Enqueue(f);
        //    //_usedBitmaps.Add(bitmap);
        //    //bmpFree = bitmap;
        //}

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
        lock (_timerLocker)
        {
            if (_frameBuffer.TryDequeue(out var frame))
            {
                //Debug.WriteLine($"On frame time (frames: {framesCounter})");
                currentFrameNumber++;
                MayFetchFrame2?.Invoke(this, frame);

                if (currentFrameNumber >= _totalFrames)
                {
                    Position = _duration;
                    PlayingProgressChanged?.Invoke(this, 1);
                }
                else
                {
                    double progress = (double)currentFrameNumber / (double)_totalFrames;
                    Position = _duration * progress;
                    PlayingProgressChanged?.Invoke(this, progress);
                }
            }
            else if (isEndVideo)
            {
                timer.Stop();
                VideoPlayingToEnd?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                //Debug.WriteLine("No match drawing Vframe");
            }
        }
    }

    private int frameId;
    private void Engine()
    {
        while (true)
        {
            if (_frameBuffer.Count >= 3)
            {
                Thread.Sleep(1);
                continue;
            }

            var sw = Stopwatch.StartNew();
            var dec = Stopwatch.StartNew();
            bool successFrame = videoReader.TryDecodeNextFrame(out var ff_frame);
            dec.StopAndCout("TryDecodeNextFrame");

            if (isDisposed)
                break;

            if (!successFrame)
            {
                isEndVideo = true;
                return;
            }

            if (isDisposed)
                break;

            var frameData = FetchOrMakeFrame(ff_frame);
            sw.StopAndCout("Engine cycle");

            if (frameData == null)
                continue;

            frameData.Id = ++frameId;
            _frameBuffer.Enqueue(frameData);

            framesCounter++;
            //Debug.WriteLine($"Frame! {framesCounter} ({sw.ElapsedMilliseconds}ms) ({d.TotalMilliseconds}ms)");
        }

        if (isDisposed)
        {
            videoReader.Dispose();
            videoReader = null!;
        }
    }

    private FrameDataNative FetchOrMakeFrameAbs(AVFrame ffframe)
    {
        FrameDataNative? frame;
        while ((frame = FetchOrMakeFrame(ffframe)) == null)
        {
            Thread.Sleep(3);
        }
        return frame;
    }

    private unsafe FrameDataNative? FetchOrMakeFrame(AVFrame ffframe)
    {
        lock (_locker)
        {
            const int MAX_FRAMES = 10;
            var sw = Stopwatch.StartNew();
            FrameDataNative? free;
            if (!_recyrclePool.TryTake(out free))
            {
                if (_totalPool.Count > MAX_FRAMES)
                    return null;

                free = MakeHard(_meta, $"{_totalPool.Count + 1}");
            }

            nint pointerFFMpegBitmap = (IntPtr)ffframe.data[0];
            int length = free.Width * free.Height * free.BytesPerPixel;
            Buffer.MemoryCopy((void*)pointerFFMpegBitmap, (void*)free.Buffer, length, length);
            sw.StopAndCout("FetchOrMakeFrame");
            return free;
        }
    }

    private FrameDataNative MakeHard(VideoMetadata meta, string dbgname)
    {
        nint pointer = Marshal.AllocHGlobal(meta.Width * meta.Height * 4);
        var free = new FrameDataNative
        {
            Height = meta.Height,
            Width = meta.Width,
            Pointer = pointer,
            PixelFormat = PixelFormat.Rgba8888,
            Buffer = pointer,
            DebugName = dbgname,
        };

        _totalPool.Add(free);

        return free;
    }

    public void Recycle(IFrameData data)
    {
        var frame = (FrameDataNative)data;
        if (!isDisposed)
        {
            _recyrclePool.Add(frame);
        }
        else
        {
            frame.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_locker)
        {
            if (isDisposed)
                return;

            isDisposed = true;

            timer.Stop();
            timer.Dispose();

            if (!isEngineRunning)
            {
                videoReader.Dispose();
                videoReader = null!;
            }

            foreach (var item in _totalPool)
            {
                if (!item.IsLocked)
                    item.Dispose();
            }
            _totalPool.Clear();
            _recyrclePool.Clear();
            _frameBuffer.Clear();
            engine = null!;
            GC.SuppressFinalize(this);
        }
    }

    [DebuggerDisplay("Id = {Id}")]
    private class FrameDataNative : IFrameData
    {
        private bool isDisposed;
        private nint buffer;

        public bool IsLocked { get; set; }
        public int BytesPerPixel => 4;
        public required int Width { get; set; }
        public required int Height { get; set; }
        public required nint Pointer { get; init; }
        public required PixelFormat PixelFormat { get; set; }

        /// <summary>
        /// Pointer to an array of RGBA8888 pixmaps on the heap, but out of the garbage collector's sight
        /// </summary>
        public required nint Buffer
        {
            get => buffer;
            init => buffer = value;
        }

        public required string DebugName { get; init; }
        public int Id { get; set; }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            if (buffer != nint.Zero)
            {
                Marshal.FreeHGlobal(buffer);
                buffer = nint.Zero;
            }
        }
    }
}