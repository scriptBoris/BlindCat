using FFMpegProcessor;
using FFMpegProcessor.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Size = System.Drawing.Size;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Platform;
using System.Diagnostics;
using FFmpegDll;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using FFMpegDll;
using FFmpeg.AutoGen.Abstractions;

namespace BlindCatAvalonia.Core;

public class VideoEngine : IDisposable
{
    private readonly int _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly VideoMetadata _meta;
    private readonly ConcurrentQueue<IFrameData> _frameBuffer = new();
    private readonly Size? _resize;
    private System.Timers.Timer timer;
    private VideoFileDecoder videoReader;
    private Thread engine;
    private bool isDisposed;
    private int currentFrameNumber;
    private bool isEngineRunning;
    private bool isEndVideo;
    private int framesCounter = 0;

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

        FFMpegDll.Init.RegisterFFmpegBinaries();
        DynamicallyLoadedBindings.Initialize();

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
    public AVHWDeviceType HWDevice { get; private set; }

    public Task Init(CancellationToken cancel)
    {
        //int width = _meta.Width;
        //int height = _meta.Height;

        //videoReader.TryDecodeNextFrame(out var frame);
        //return videoReader.Load(Position.TotalSeconds, width, height, cancel);
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
        //Debug.WriteLine($"On frame time (buffer count: {_bitmapsBuffer.Count})");

        if (_frameBuffer.TryDequeue(out var frame))
        {
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

    private void Engine()
    {
        var sw = new Stopwatch();
        var sourceSize = videoReader.FrameSize;
        var sourcePixelFormat = HWDevice == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
            ? videoReader.PixelFormat
            : GetHWPixelFormat(HWDevice);
        var destinationSize = sourceSize;
        var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
        using var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);

        while (true)
        {
            bool successFrame = videoReader.TryDecodeNextFrame(out var frame);

            if (isDisposed)
                break;

            if (!successFrame)
            {
                isEndVideo = true;
                return;
            }

            if (_frameBuffer.Count > 2)
            {
                Thread.Sleep(3);
                continue;
            }

            if (isDisposed)
                break;

            sw.Start();
            if (videoReader.PixelFormat != AVPixelFormat.AV_PIX_FMT_RGBA)
            {
                var convertedFrame = vfc.Convert(frame);
                var fd = MakeFrame(convertedFrame);
                _frameBuffer.Enqueue(fd);
            }
            else
            {
                var fd = MakeFrame(frame);
                _frameBuffer.Enqueue(fd);
            }

            sw.Stop();
            Debug.WriteLine($"Lat: {sw.ElapsedMilliseconds}ms");
            sw.Restart();

            framesCounter++;
            //Debug.WriteLine($"Frame! {framesCounter} ({sw.ElapsedMilliseconds}ms) ({d.TotalMilliseconds}ms)");
        }

        if (isDisposed)
        {
            videoReader.Dispose();
            videoReader = null!;

            for (int i = _frameBuffer.Count - 1; i >= 0; i--)
                if (_frameBuffer.TryDequeue(out var bmp))
                    bmp.Dispose();
        }
    }

    public void Dispose()
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

            for (int i = _frameBuffer.Count - 1; i >= 0; i--)
                if (_frameBuffer.TryDequeue(out var bmp))
                    bmp.Dispose();
        }
        engine = null!;
    }

    private FrameData MakeFrame(byte[] array)
    {
        var f = new FrameData(array)
        {
            Height = _meta.Height,
            Width = _meta.Width,
            TotalBytes = array.Length,
        };
        return f;
    }

    private unsafe FrameDataNative MakeFrame(FFmpeg.AutoGen.Abstractions.AVFrame fframe)
    {
        var ptr = (IntPtr)fframe.data[0];
        var f = new FrameDataNative
        {
            Height = _meta.Height,
            Width = _meta.Width,
            Pointer = ptr,
            PixelFormat = PixelFormat.Rgba8888,
            AVFrame = fframe,
        };
        return f;
    }

    private static AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
    {
        return hWDevice switch
        {
            AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
            AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
            AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
            _ => AVPixelFormat.AV_PIX_FMT_NONE
        };
    }

    private class FrameDataNative : IFrameData
    {
        public int BytesPerPixel => 4;
        public required int Width { get; set; }
        public required int Height { get; set; }
        public required nint Pointer { get; set; }
        public required PixelFormat PixelFormat { get; set; }
        public required AVFrame AVFrame { get; set; }

        public void Dispose()
        {
        }
    }
}