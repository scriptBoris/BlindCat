using FFMpegProcessor.Models;
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
using Avalonia.Threading;
using System.Runtime.CompilerServices;
using Avalonia;
using System.Drawing;
using SkiaSharp;
using System.Collections;
using System.Linq;

namespace BlindCatAvalonia.Core;

public class VideoEngine : IDisposable
{
    private readonly int _totalFrames;
    private readonly TimeSpan _startingTime;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly VideoMetadata _meta;
    private readonly ConcurrentQueue<IFrameData> _frameBuffer = new();
    private readonly IntSize? _resize;
    private System.Timers.Timer _timerFramerate;
    private VideoFileDecoder _videoDecoder;
    private Thread _engineThread;
    private bool _isDisposed;
    private bool _isEngineRunning;
    private bool _isEndVideo;
    private int _framesCounter = 0;
    private int _dropFramesCount;

    private readonly object _locker = new();
    private readonly object _timerLocker = new();
    private readonly ConcurrentBag<FrameDataNative> _recyrclePool = [];
    private readonly List<FrameDataNative> _totalPool = [];

    public event EventHandler<IFrameData>? MayFetchFrame;

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
                _videoDecoder = new VideoFileDecoder(filePath, FFmpeg.AutoGen.Abstractions.AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
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
        _pauseForFrameRate = TimeSpan.FromSeconds(1.0 / meta.AvgFramerate);
        _totalFrames = meta.PredictedFrameCount;

        _startingTime = startFrom;
        _engineThread = new(Engine);
        _engineThread.Name = "Engine (ffmpeg frame reader)";
        _timerFramerate = new();
        _timerFramerate.Elapsed += OnTimerFramerate;
        _timerFramerate.Enabled = true;
        _timerFramerate.AutoReset = true;
        _timerFramerate.Interval = _pauseForFrameRate.TotalMilliseconds;

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

    public AVHWDeviceType HWDevice { get; private set; }
    public bool CanSeeking => true;

    public Task Init(CancellationToken cancel)
    {
        if (_startingTime.Ticks > 0)
            return SeekTo(_startingTime, cancel);

        //int width = _meta.Width;
        //int height = _meta.Height;
        //videoReader.TryDecodeNextFrame(out var frame);
        //return videoReader.Load(Position.TotalSeconds, width, height, cancel);

        bool successFrame = _videoDecoder.TryDecodeNextFrame(out var ff_frame);
        if (successFrame)
        {
            var frameData = FetchOrMakeFrame(ff_frame);
            if (frameData != null)
            {
                frameData.Id = ++_framesCounter;
                _frameBuffer.Enqueue(frameData);
            }
        }

        return Task.CompletedTask;
    }

    public Task SeekTo(TimeSpan position, CancellationToken cancel)
    {
        _videoDecoder.SeekTo(position);
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
            if (_isDisposed)
                return;

            if (_frameBuffer.TryDequeue(out var frame))
            {
                MayFetchFrame?.Invoke(this, frame);
            }
            else if (_isEndVideo)
            {
                _timerFramerate.Stop();
            }
            else
            {
                //Debug.WriteLine("No match drawing Vframe");
            }
        }
    }

    private void Engine()
    {
        while (true)
        {
            if (_frameBuffer.Count >= 2)
            {
                Thread.Sleep(1);
                continue;
            }

            if (_isDisposed)
                break;

            var sw = Stopwatch.StartNew();
            var dec = Stopwatch.StartNew();
            bool successFrame = _videoDecoder.TryDecodeNextFrame(out var ff_frame);
            dec.StopAndCout("TryDecodeNextFrame");

            if (_isDisposed)
                break;

            if (!successFrame)
            {
                _isEndVideo = true;
                return;
            }

            if (_isDisposed)
                break;

            var frameData = FetchOrMakeFrame(ff_frame);
            sw.StopAndCout("Engine cycle");

            if (frameData == null)
            {
                _dropFramesCount++;
                Debug.WriteLine($"Frame drops: {_dropFramesCount}");
                continue;
            }

            frameData.Id = ++_framesCounter;
            _frameBuffer.Enqueue(frameData);
            //Debug.WriteLine($"Frame! {framesCounter} ({sw.ElapsedMilliseconds}ms) ({d.TotalMilliseconds}ms)");
        }

        if (_isDisposed)
        {
            TryDisposeEngine(true);
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
        Debug.WriteLine("Starting FetchOrMakeFrame");
        const int MAX_FRAMES = 6;
        FrameDataNative? free;
        if (!_recyrclePool.TryTake(out free))
        {
            if (_totalPool.Count > MAX_FRAMES)
                return null;

            free = MakeHard(_meta, $"{_totalPool.Count + 1}");
        }

        nint pointerFFMpegBitmap = (IntPtr)ffframe.data[0];
        //uint length = (uint)(free.Width * free.Height * free.BytesPerPixel);
        int length = free.Width * free.Height * free.BytesPerPixel;
        Debug.WriteLine($"Copy {pointerFFMpegBitmap} => {free.Pointer} ({length} length)");

        CopyPixelsCore(pointerFFMpegBitmap, free);

        free.DecodedAt = DateTime.Now;
        Debug.WriteLine("Finished FetchOrMakeFrame");
        return free;
    }

    private unsafe FrameDataNative MakeHard(VideoMetadata meta, string dbgname)
    {
        Debug.WriteLine($"Trying allocate new frame [{dbgname}]");
        const sbyte bytesPerPix = 4;
        nint pointer = Marshal.AllocHGlobal(meta.Width * meta.Height * bytesPerPix);

        var free = new FrameDataNative
        {
            Height = meta.Height,
            Width = meta.Width,
            PixelFormat = PixelFormat.Rgba8888,
            BytesPerPixel = bytesPerPix,
            Buffer = pointer,
            DebugName = dbgname,
        };

        bool isAligned = (pointer.ToInt64() % 16) == 0;
        if (!isAligned)
        {
            Debug.WriteLine("Buffer pointer is not aligned as expected");
        }

        _totalPool.Add(free);

        Debug.WriteLine($"Successed allocated new frame [{dbgname}]");
        return free;
    }

    private static unsafe void CopyPixelsCore(nint source, IFrameData dstData)
    {
        CopyPixels(source, dstData.Pointer, dstData.Width, dstData.Height, dstData.BytesPerPixel);
    }

    private static unsafe void CopyPixels(nint source, nint destination, int width, int height, sbyte bytePerPixel)
    {
        //int stride = width * bytePerPixel;
        //for (var y = 0; y < height; y++)
        //{
        //    int offset = stride * y;
        //    var srcAddress = source + offset;
        //    var dstAddress = destination + offset;
        //    Unsafe.CopyBlock(dstAddress.ToPointer(), srcAddress.ToPointer(), (uint)stride);
        //}

        int stride = width * bytePerPixel;
        int totalBytes = stride * height;

        Unsafe.CopyBlockUnaligned(destination.ToPointer(), source.ToPointer(), (uint)totalBytes);
    }

    public void Recycle(IFrameData data)
    {
        var frame = (FrameDataNative)data;
        if (!_isDisposed)
        {
            _recyrclePool.Add(frame);
            data.IsLocked = false;
        }
        else
        {
            frame.Dispose();
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

        int countDisp = 0;
        int total = _totalPool.Count;
        Debug.WriteLine($"_totalPool starting disposing ({total} total)");
        foreach (var frame in _totalPool)
        {
            if (frame.CanDisposed)
            {
                Debug.WriteLine($"_totalPool trying disposing #{countDisp + 1} frame");
                frame.Dispose();
                countDisp++;
                Debug.WriteLine($"_totalPool success disposed #{countDisp} frame");
            }
            else
            {
                Debugger.Break();
            }
        }
        Debug.WriteLine($"_totalPool is {countDisp} frames disposed by {total}");

        _videoDecoder.Dispose();
        _videoDecoder = null!;
        Debug.WriteLine("_videoDecoder is disposed");

        _totalPool.Clear();
        _recyrclePool.Clear();
    }

    public void Dispose()
    {
        lock (_locker)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _frameBuffer.Clear();
            _timerFramerate.Stop();
            _timerFramerate.Dispose();
            _timerFramerate = null!;
            Debug.WriteLine("_timerFramerate is disposed");

            TryDisposeEngine();

            _engineThread = null!;
            GC.SuppressFinalize(this);
            Debug.WriteLine("Video engine is fully disposed");
        }
    }

    [DebuggerDisplay("Id = {Id}")]
    private unsafe class FrameDataNative : IFrameData
    {
        private readonly object _locker = new();
        private bool _isDisposed;
        private nint _buffer;
        private bool _isLocked;

        public DateTime DecodedAt { get; set; }
        public bool IsDisposed => _isDisposed;
        public bool CanDisposed => !IsLocked;

        public bool IsLocked
        {
            get
            {
                lock (_locker)
                {
                    return _isLocked;
                }
            }
            set
            {
                lock (_locker)
                {
                    _isLocked = value;
                }
            }
        }

        public required sbyte BytesPerPixel { get; set; }
        public required int Width { get; set; }
        public required int Height { get; set; }
        public nint Pointer => _buffer;
        public required PixelFormat PixelFormat { get; set; }

        /// <summary>
        /// Pointer to an array of RGBA8888 pixmaps on the heap, but out of the garbage collector's sight
        /// </summary>
        public required nint Buffer
        {
            init => _buffer = value;
        }

        public required string DebugName { get; init; }
        public int Id { get; set; }

        public void CopyTo(nint destination)
        {
            lock (_locker)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                CopyPixels(_buffer, destination, Width, Height, BytesPerPixel);
            }
        }

        public unsafe void Dispose()
        {
            lock (_locker)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);

                _isDisposed = true;
                if (_buffer != nint.Zero)
                {
                    int ln = Width * Height * BytesPerPixel;
                    Debug.WriteLine($"Frame #{Id} trying disposed ({_buffer})");
                    //CheckValidationBuffer(_buffer, Width * Height * BytesPerPixel);
                    Marshal.FreeHGlobal(_buffer);
                    _buffer = nint.Zero;
                    Debug.WriteLine($"Frame #{Id} is disposed ({_buffer})");
                }
                GC.SuppressFinalize(this);
            }
        }
    }
}