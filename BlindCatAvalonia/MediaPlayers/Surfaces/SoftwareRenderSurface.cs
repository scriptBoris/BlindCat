using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using BlindCatAvalonia.Core;
using BlindCatCore.Core;
using FFMpegDll.Core;
using IntSize = System.Drawing.Size;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public class SoftwareRenderSurface : Control, IVideoSurface
{
    private IReusableContext? _reuseContext;
    private ConcurrentStack<IReusableBitmap> _stack = new();
    private IReusableBitmap? _lastFrame;
    
    public Matrix Matrix { get; set; }

    public void SetupSource(IReusableContext source)
    {
        _reuseContext = source;
    }

    public void OnFrameReady()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var frame = _reuseContext?.GetFrame();
            if (frame != null)
            {
                _stack.Push(frame);
                InvalidateVisual();
            }
        }, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reuseContext = null;
    }

    public override void Render(DrawingContext context)
    {
        var reuseContext = _reuseContext;
        if (reuseContext == null)
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Transparent), null, Bounds, 0);
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var matrix = Matrix;
        var bounds = new Rect(0, 0, reuseContext.FrameSize.Width, reuseContext.FrameSize.Height);

        using (context.PushClip(viewPort))
        using (context.PushTransform(matrix))
        {
            if (_stack.TryPop(out var bmp))
            {
                // Освобождаем все неактуальные кадры
                while (_stack.TryPop(out var unactual))
                    Reuse((Bitmap)unactual);

                // Запоминаем последний успешный кадр
                _lastFrame = bmp; 
            }

            if (_lastFrame != null)
            {
                var rect = new Rect(0, 0, bounds.Width, bounds.Height);
                var op = new DrawOperation((Bitmap)_lastFrame, rect, Reuse);
                context.Custom(op);
            }
        }
    }

    private void Reuse(Bitmap bmp)
    {
        _reuseContext?.RecycleFrame((IReusableBitmap)bmp);
    }

    private class DrawOperation : ICustomDrawOperation
    {
        private readonly IDeferredDisposing? _lat;
        private readonly Bitmap _bmp;
        private readonly Rect _bounds;
        private readonly Action<Bitmap> _disposeCallback;

        public DrawOperation(Bitmap bitmap, Rect bounds, Action<Bitmap> disposeCallback)
        {
            _bmp = bitmap;
            _bounds = bounds;
            _disposeCallback = disposeCallback;
            if (_bmp is IDeferredDisposing latency)
                _lat = latency;
        }

        public Rect Bounds => _bounds;
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var src = new Rect(0, 0, _bmp.Size.Width, _bmp.Size.Height);
            var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);
            context.DrawBitmap(_bmp, src, dest);
        }

        public void Dispose()
        {
            _disposeCallback(_bmp);
            if (_lat?.IsReadyDispose ?? false)
                _lat.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    public class BitmapPool : IReusableContext
    {
        private readonly ConcurrentQueueExt<IReusableBitmap> _opPipeline = new();
        private readonly List<IReusableBitmap> _recyclePool = [];
        private readonly List<IReusableBitmap> _allPool = [];
        private readonly PixelSize _pixSize;
        private readonly Vector _vector;
        private readonly PixelFormat _pix;
        private readonly object _lock = new();
        private bool _disposed;
        private int _countFrames;

        public BitmapPool(IntSize size)
        {
            _pixSize = new PixelSize(size.Width, size.Height);
            _vector = new Vector(96, 96);
            _pix = PixelFormat.Rgba8888;
            
            for (int i = 0; i < 3; i++)
            {
                _countFrames++;
                var bmp = new ReusableBitmap(_pixSize, _vector, _pix, AlphaFormat.Opaque, "#" + _countFrames);
                _recyclePool.Add(bmp);
                _allPool.Add(bmp);
                Console.WriteLine($"Generated new BITMAP #{_countFrames}");
            }                    

            FrameSize = size;
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
                    bmp = new ReusableBitmap(_pixSize,
                        _vector,
                        _pix,
                        AlphaFormat.Opaque,
                        $"#{_countFrames}");
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
                var bmp = (ReusableBitmap)_allPool[i];
                if (bmp.IsRendering)
                    bmp.IsReadyDispose = true;
                else
                    bmp.Dispose();
                _allPool.RemoveAt(i);
            }

            GC.SuppressFinalize(this);
        }
    }
}