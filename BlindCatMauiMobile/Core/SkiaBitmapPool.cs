using BlindCatCore.Core;
using FFMpegDll.Core;
using SkiaSharp;
using IntSize = System.Drawing.Size;

namespace BlindCatMauiMobile.Core;

public class SkiaBitmapPool : IReusableContext
{
    private readonly ConcurrentQueueExt<IReusableBitmap> _opPipeline = new();
    private readonly List<IReusableBitmap> _recyclePool = [];
    private readonly List<IReusableBitmap> _allPool = [];
    private readonly SKColorType _pix;
    private readonly object _lock = new();
    private bool _disposed;
    private int _countFrames;

    public SkiaBitmapPool(IntSize size)
    {
        _pix = SKColorType.Rgba8888;
        FrameSize = size;

        for (int i = 0; i < 3; i++)
        {
            _countFrames++;
            var bmp = new SKReusableBitmap(FrameSize.Width, FrameSize.Height, _pix, SKAlphaType.Opaque);
            _recyclePool.Add(bmp);
            _allPool.Add(bmp);
            Console.WriteLine($"Generated new BITMAP #{_countFrames}");
        }
    }

    public IntSize FrameSize { get; }
    public int QueuedFrames => _opPipeline.Count;

    public IReusableBitmap? GetFrame()
    {
        if (_opPipeline.TryDequeue(out var op))
        {
            return op;
        }

        return null;
    }

    public void PushFrame(nint bitmapArray)
    {
        lock (_lock)
        {
            var bmp = _recyclePool.FirstOrDefault();
            if (bmp != null)
            {
                _recyclePool.Remove(bmp);
            }
            else
            {
                _countFrames++;
                bmp = new SKReusableBitmap(FrameSize.Width, FrameSize.Height, _pix, SKAlphaType.Opaque);
                _allPool.Add(bmp);
                Console.WriteLine($"Generated new BITMAP #{_countFrames}");
            }

            bmp.Populate(bitmapArray);
            _opPipeline.Enqueue(bmp);
        }
    }

    public void RecycleFrame(IReusableBitmap data)
    {
        lock (_lock)
        {
            _recyclePool.Add(data);
        }
    }

    public void Dispose()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _disposed = true;
        for (int i = _allPool.Count - 1; i >= 0; i--)
        {
            var bmp = (SKReusableBitmap)_allPool[i];
            bmp.Dispose();
            _allPool.RemoveAt(i);
        }

        GC.SuppressFinalize(this);
    }
}