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

namespace BlindCatAvalonia.Core;

public class VideoEngine : IDisposable
{
    private readonly int _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly VideoMetadata _meta;
    //private readonly ConcurrentQueue<Bitmap> _bitmapsBuffer = new();
    private readonly ConcurrentQueue<FrameData> _frameBuffer = new();
    private readonly Size? _resize;
    private System.Timers.Timer timer;
    private RawVideoReader videoReader;
    private Thread engine;
    private bool isDisposed;
    private int currentFrameNumber;
    private bool isEngineRunning;
    private bool isEndVideo;

    //private readonly List<Bitmap> _usedBitmaps = new();
    //private Bitmap? bmpBusy;
    //private Bitmap? bmpFree;
    //private object locker = new();

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? VideoPlayingToEnd;
    public event EventHandler<FrameData>? MayFetchFrame2;

    public VideoEngine(object play,
        TimeSpan startFrom,
        VideoMetadata meta,
        string pathToFFmpegExe)
    {
        if (meta.AvgFramerate == 0)
            throw new InvalidOperationException("Invalid video meta data");

        _meta = meta;
        switch (play)
        {
            case string filePath:
                videoReader = new RawVideoReader(filePath, pathToFFmpegExe);
                break;
            case Stream stream:
                videoReader = new RawVideoReader(stream, pathToFFmpegExe);
                break;
            case FileCENC fileCENC:
                videoReader = new RawVideoReader(fileCENC, pathToFFmpegExe);
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

    public Task Init(CancellationToken cancel)
    {
        int width = _meta.Width;
        int height = _meta.Height;
        return videoReader.Load(Position.TotalSeconds, width, height, cancel);
    }

    public void Run()
    {
        if (videoReader.AlreadyFrame != null)
        {
            int width = _meta.Width;
            int height = _meta.Height;
            byte[] frame = videoReader.AlreadyFrame;
            //Bitmap bitmap = AutoMakeBitmap(frame, width, height);
            var f = MakeFrame(frame);
            _frameBuffer.Enqueue(f);
            //_usedBitmaps.Add(bitmap);
            //bmpFree = bitmap;
        }

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

    DateTime lastST;

    private void Engine()
    {
        int width = _meta.Width;
        int height = _meta.Height;
        int size = videoReader.FrameSize;
        bool isLastFrame = false;

        while (true)
        {
            if (isDisposed)
                break;

            if (_frameBuffer.Count > 0)
            {
                Thread.Sleep(3);
                continue;
            }

            var sw = Stopwatch.StartNew();
            byte[] frame = new byte[size];
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
            sw.Stop();
            Debug.WriteLine($"Lat: {sw.ElapsedMilliseconds}ms");

            if (isDisposed)
                break;

            //var bitmap = AutoMakeBitmap(frame, width, height);
            var f = MakeFrame(frame);
            _frameBuffer.Enqueue(f);

            if (isLastFrame)
            {
                isEndVideo = true;
                return;
            }

            var d = DateTime.Now - lastST;
            framesCounter++;
            //Debug.WriteLine($"Frame! {framesCounter} ({sw.ElapsedMilliseconds}ms) ({d.TotalMilliseconds}ms)");
            lastST = DateTime.Now;
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
    int framesCounter = 0;

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

    //private Bitmap AutoMakeBitmap(byte[] frame, int width, int height)
    //{
    //    Bitmap bitmap;
    //    switch (videoReader.PixFormat)
    //    {
    //        case CryMediaAPI.PixFormats.RGB24:
    //            bitmap = CreateBitmapFromRGB24(frame, width, height);
    //            break;
    //        case CryMediaAPI.PixFormats.Yuv420p:
    //            bitmap = CreateBitmapFromYUV420P(frame, width, height);
    //            break;
    //        default:
    //            throw new NotImplementedException();
    //    }

    //    //if (_resize != null)
    //    //{
    //    //    using var bitmap1 = SkiaUtils.CreateBitmapFromRGB24(frame, width, height);
    //    //    bitmap = SkiaUtils.ResizeBitmap(bitmap1, _resize.Value.Width, _resize.Value.Height);
    //    //}
    //    //else
    //    //{
    //    //    bitmap = SkiaUtils.CreateBitmapFromRGB24(frame, width, height);
    //    //}
    //    return bitmap;
    //}

    //public static Bitmap CreateBitmapFromRGB24(byte[] rgb24Data, int width, int height)
    //{
    //    Bitmap bitmap;

    //    byte[] rgbData = new byte[width * height * 3];

    //    int numThreads = Math.Min(6, Environment.ProcessorCount);
    //    int chunkSize = (height + numThreads - 1) / numThreads;

    //    Parallel.For(0, numThreads, threadIndex =>
    //    {
    //        int startY = threadIndex * chunkSize;
    //        int endY = Math.Min(startY + chunkSize, height);

    //        for (int y = startY; y < endY; y++)
    //        {
    //            int rowStart = y * width * 3;
    //            int rowEnd = rowStart + width * 3;
    //            int rgbaStart = y * width * 3;

    //            for (int i = rowStart, j = rgbaStart; i < rowEnd; i += 3, j += 3)
    //            {
    //                rgbData[j] = rgb24Data[i];         // Красный
    //                rgbData[j + 1] = rgb24Data[i + 1]; // Зеленый
    //                rgbData[j + 2] = rgb24Data[i + 2]; // Синий
    //            }
    //        }
    //    });

    //    var handle = GCHandle.Alloc(rgbData, GCHandleType.Pinned);
    //    nint pointer = handle.AddrOfPinnedObject();

    //    var pix = Avalonia.Platform.PixelFormats.Rgb24;
    //    var asize = new Avalonia.PixelSize(width, height);
    //    var scale = new Avalonia.Vector(96, 96);
    //    var stride = width * 3;
    //    bitmap = new BBitmap(pix, Avalonia.Platform.AlphaFormat.Premul, pointer, asize, scale, stride, handle);

    //    return bitmap;
    //}

    //public static Bitmap CreateBitmapFromYUV420P(byte[] yuv420Data, int width, int height)
    //{
    //    Bitmap bitmap;

    //    byte[] rgbData = new byte[width * height * 3];

    //    int ySize = width * height;
    //    int uvWidth = width / 2;
    //    int uvHeight = height / 2;
    //    int uvSize = uvWidth * uvHeight;

    //    int numThreads = Math.Min(12, Environment.ProcessorCount);
    //    int chunkSize = (height + numThreads - 1) / numThreads;

    //    Parallel.For(0, numThreads, threadIndex =>
    //    {
    //        int startY = threadIndex * chunkSize;
    //        int endY = Math.Min(startY + chunkSize, height);

    //        for (int y = startY; y < endY; y++)
    //        {
    //            for (int x = 0; x < width; x++)
    //            {
    //                int yIndex = y * width + x;
    //                int uvIndex = (y / 2) * uvWidth + (x / 2);

    //                byte Y = yuv420Data[yIndex];
    //                byte U = yuv420Data[ySize + uvIndex];
    //                byte V = yuv420Data[ySize + uvSize + uvIndex];

    //                // Преобразование YUV в RGB
    //                int C = Y - 16;
    //                int D = U - 128;
    //                int E = V - 128;

    //                int R = (298 * C + 409 * E + 128) >> 8;
    //                int G = (298 * C - 100 * D - 208 * E + 128) >> 8;
    //                int B = (298 * C + 516 * D + 128) >> 8;

    //                R = Math.Clamp(R, 0, 255);
    //                G = Math.Clamp(G, 0, 255);
    //                B = Math.Clamp(B, 0, 255);

    //                int rgbIndex = y * width * 3 + x * 3;
    //                rgbData[rgbIndex] = (byte)R;
    //                rgbData[rgbIndex + 1] = (byte)G;
    //                rgbData[rgbIndex + 2] = (byte)B;
    //            }
    //        }
    //    });

    //    var handle = GCHandle.Alloc(rgbData, GCHandleType.Pinned);
    //    nint pointer = handle.AddrOfPinnedObject();

    //    var pix = Avalonia.Platform.PixelFormats.Rgb24;
    //    var asize = new Avalonia.PixelSize(width, height);
    //    var scale = new Avalonia.Vector(96, 96);
    //    var stride = width * 3;

    //    var sw = Stopwatch.StartNew();
    //    bitmap = new BBitmap(pix, Avalonia.Platform.AlphaFormat.Premul, pointer, asize, scale, stride, handle);
    //    sw.Stop();
    //    Debug.WriteLine($"-> ({sw.ElapsedMilliseconds}ms)");

    //    return bitmap;
    //}
}