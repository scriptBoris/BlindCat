using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using BlindCatAvalonia.Core;
using BlindCatCore.Core;
using IntSize = System.Drawing.Size;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public interface IReusableContext : IDisposable
{
    IntSize IntFrameSize { get; }
    PixelSize PixelSize => new(IntFrameSize.Width, IntFrameSize.Height);
    Size FrameSize => new(IntFrameSize.Width, IntFrameSize.Height);
    bool IsDisposed { get; }

    IFrameData? GetFrame();
    void RecycleFrame(IFrameData data);
}

public class SoftwareRenderSurface : Control, IVideoSurface
{
    private IReusableContext? _reuseContext;
    private BitmapPool? _bitmapPool;

    public Matrix Matrix { get; set; }

    public void SetupSource(IReusableContext source)
    {
        _reuseContext = source;
        _bitmapPool = new BitmapPool(source.IntFrameSize);
    }

    public void OnFrameReady()
    {
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bitmapPool?.Dispose();
        _bitmapPool = null;
    }

    public override void Render(DrawingContext context)
    {
        var reuseContext = _reuseContext;
        var bitmapPool = _bitmapPool;
        if (reuseContext == null || reuseContext.IsDisposed || bitmapPool == null)
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Transparent), null, Bounds, 0);
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var matrix = Matrix;
        var bounds = new Rect(0, 0, reuseContext.IntFrameSize.Width, reuseContext.IntFrameSize.Height);

        using (context.PushClip(viewPort))
        using (context.PushTransform(matrix))
        {
            var frameData = reuseContext.GetFrame();
            if (frameData == null)
                return;
            
            var bmp = bitmapPool.Resolve();
            var bitmapSize = reuseContext.PixelSize;
            using (var vok = bmp.Lock())
            {
                frameData.CopyTo(vok.Address);
                reuseContext.RecycleFrame(frameData);
            }

            var rect = new Rect(0, 0, bounds.Width, bounds.Height);
            var op = new DrawOperation(bmp, rect, () => bitmapPool.PushPipeline());
            context.Custom(op);
        }
    }

    private class DrawOperation : ICustomDrawOperation
    {
        private readonly IDeferredDisposing? _lat;
        private readonly Bitmap _bmp;
        private readonly Rect _bounds;
        private readonly Action _disposeCallback;

        public DrawOperation(Bitmap bitmap, Rect bounds, Action disposeCallback)
        {
            _bmp = bitmap;
            _bounds = bounds;
            _disposeCallback = disposeCallback;
            if (_bmp is IDeferredDisposing latency) _lat = latency;
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
            _disposeCallback();
            if (_lat?.IsReadyDispose ?? false) _lat.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private class BitmapPool : IDisposable
    {
        private readonly ConcurrentQueueExt<ReusableBitmap> _opPipeline = new();
        private readonly List<ReusableBitmap> _pool = [];
        private readonly PixelSize _pixSize;
        private readonly Vector _vector;
        private bool _disposed = false;

        public BitmapPool(IntSize size)
        {
            _pixSize = new PixelSize(size.Width, size.Height);
            _vector = new Vector(96, 96);
            var bmp0 = new ReusableBitmap(_pixSize, _vector, PixelFormat.Rgba8888, AlphaFormat.Opaque, "#1");
            var bmp1 = new ReusableBitmap(_pixSize, _vector, PixelFormat.Rgba8888, AlphaFormat.Opaque, "#2");
            _pool.Add(bmp0);
            _pool.Add(bmp1);
        }

        public void Dispose()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _disposed = true;
            for (int i = _pool.Count - 1; i >= 0; i--)
            {
                var bmp = _pool[i];
                if (bmp.IsRendering)
                    bmp.IsReadyDispose = true;
                else
                    bmp.Dispose();
                _pool.RemoveAt(i);
            }
            GC.SuppressFinalize(this);
        }

        public ReusableBitmap Resolve()
        {
            ReusableBitmap? match;
            match = _pool.FirstOrDefault(x => !x.IsRendering);

            if (match == null)
            {
                match = new ReusableBitmap(_pixSize, _vector, PixelFormat.Rgba8888, AlphaFormat.Opaque)
                {
                    DebugName = $"#{_pool.Count + 1}",
                };
                _pool.Add(match);
            }

            match.IsRendering = true;
            _opPipeline.Enqueue(match);
            return match;
        }

        public void PushPipeline()
        {
            lock (_opPipeline.Locker)
            {
                if (_opPipeline.Count == 1)
                {
                    // static image
                }
                else
                {
                    if (_opPipeline.TryDequeue(out var bmp))
                    {
                        bmp.IsRendering = false;
                        //if (bmp == current)
                        //{
                        //    bmp.Free();
                        //}
                        //else
                        //{
                        //    // something went wrong?
                        //    Debugger.Break();
                        //}
                    }
                }
            }
        }

        public void Free(ReusableBitmap bitmap)
        {
            bitmap.IsRendering = false;

            if (_disposed)
            {
                bitmap.Dispose();
            }
        }
    }
}