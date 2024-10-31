using Avalonia.Controls.Skia;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using Avalonia.Threading;
using System.Threading.Tasks;
using IntSize = System.Drawing.Size;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using BlindCatCore.Core;

namespace BlindCatAvalonia.Core;

public interface IReusableContext : IDisposable
{
    event EventHandler? ExternalDisposed;
    IntSize IntFrameSize { get; }
    Size FrameSize => new(IntFrameSize.Width, IntFrameSize.Height);
    IFrameData? GetFrame();

    bool IsDisposed { get; }
    bool DisposeAfterRender { get; set; }
    void Recycle(IFrameData data);
}

public class SKBitmapControlReuse : SKBitmapControl
{
    private System.Drawing.PointF _offset;
    private double? _forceScale;
    private IReusableContext? _reuseContext;
    private IFrameData? _currentFrameData;
    private IFrameData? _oldFrameData;
    private BitmapPool? _bitmapPool;
    private HardbassBuffer? _buffer;
    private DispatcherTimer? dis;

    //private BitmapPack? _pack;
    //private ReusableBitmap? _reuseBmp;
    private readonly object _lockObject = new();

    #region props
    public double RenderScale { get; private set; } = -1.0;

    public double? ForceScale
    {
        get => _forceScale;
        set
        {
            _forceScale = value;
            InvalidateMeasure();
        }
    }

    public System.Drawing.PointF Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            InvalidateVisual();
        }
    }

    public virtual Size DefaultSize { get; }
    #endregion props

    protected virtual void OnScaleChanged(double scale)
    {
        RenderScale = scale;
    }

    public void ResetOffsetAndScale(bool redraw = true)
    {
        _offset = new System.Drawing.PointF();
        _forceScale = null;
        if (redraw)
            InvalidateVisual();
    }

    public void Source(IReusableContext source)
    {
        //var pix = new PixelSize(source.IntFrameSize.Width, source.IntFrameSize.Height);
        //var vector = new Vector(96, 96);
        //_reuseBmp = new ReusableBitmap(pix, vector, PixelFormat.Rgba8888, AlphaFormat.Opaque);
        //_pack = new(source.IntFrameSize);
        _bitmapPool = new(source.IntFrameSize);
        _buffer = new HardbassBuffer(source, this);
        Dispatcher.UIThread.Post(() =>
        {
            _reuseContext = source;
            InvalidateMeasure();
        });
    }

    public void PushFrame(IFrameData data)
    {
        _buffer.Add(data);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);

        dis = new DispatcherTimer(DispatcherPriority.Render);
        dis.Interval = TimeSpan.FromMilliseconds(10);
        dis.Tick += (s, e) =>
        {
            InvalidateVisual();
        };
        dis.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_reuseContext != null)
        {
            _reuseContext.Dispose();
            _reuseContext = null;

            dis?.Stop();
            dis = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_reuseContext == null)
        {
            return DefaultSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return availableSize;
        }

        return Stretch.CalculateSize(availableSize, _reuseContext.FrameSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_reuseContext is null || _reuseContext.IsDisposed)
        {
            return finalSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return finalSize;
        }

        var sourceSize = _reuseContext.FrameSize;
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    private DateTime lastDraw;
    public override void Render(DrawingContext context)
    {
        if (_reuseContext == null || _reuseContext.IsDisposed || _bitmapPool == null || _buffer == null)
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Transparent), null, Bounds, 0);
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = _reuseContext.FrameSize;
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        if (ForceScale != null)
        {
            double forceScale = ForceScale.Value;
            double x = forceScale / Math.Sqrt(2);
            scale = new Vector(x, x);
        }

        if (RenderScale != scale.Length)
            OnScaleChanged(scale.Length);

        var scaledSize = sourceSize * scale;
        var centerRect = viewPort
            .CenterRect(new Rect(scaledSize));

        var destRect = centerRect
            .Intersect(viewPort);

        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale));

        var bounds = new Rect(0, 0, _reuseContext.IntFrameSize.Width, _reuseContext.IntFrameSize.Height);
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);

        double offsetX = Offset.X;
        double offsetY = Offset.Y;

        double transX = centerRect.X + offsetX;
        double transY = centerRect.Y + offsetY;

        double mod1 = transX * (sourceRect.Width / destRect.Width);
        double mod2 = transY * (sourceRect.Height / destRect.Height);
        var translateMatrix = Matrix.CreateTranslation(mod1, mod2);

        if (destRect == default)
        {
            return;
        }

        using (context.PushClip(viewPort))
        using (context.PushTransform(translateMatrix * scaleMatrix))
        {
            bool preserve = false;
            var frameData = _buffer.FetchCarefulAndLock();
            if (frameData == null)
            {
                frameData = _currentFrameData;
                preserve = _currentFrameData != null;
            }

            if (frameData == null)
                return;

            if (_buffer.Count > 10)
                Debugger.Break(); // wtf?

            bool useStatic = preserve; //&& _oldDrawOP != null && _oldDrawOP.IsPreserve;
            if (useStatic)
            {
                if (_opPipeline.TryLast(out var op))
                {
                    //var staticBmp = op!.Bitmap;
                    //staticBmp.IsRendering = true;
                    //var rect = new Rect(0, 0, bounds.Width, bounds.Height);
                    //var op = new DrawOperation(this, rect, staticBmp, frameData, true);
                    context.Custom(op);
                }
                else
                {

                }
            }
            else
            {
                ReusableBitmap drawBmp;
                drawBmp = _bitmapPool.Resolve();
                drawBmp.IsRendering = true;
                drawBmp.Draw(frameData);
                var rect = new Rect(0, 0, bounds.Width, bounds.Height);
                var op = new DrawOperation(this, rect, drawBmp, frameData, preserve);
                context.Custom(op);
                _opPipeline.Enqueue(op);
            }

            _oldFrameData = frameData;
            _currentFrameData = frameData;
        }

        var span = DateTime.Now - lastDraw;
        var fps = 1000.0 / span.TotalMilliseconds;
        Debug.WriteLine($"FPS: {fps}");
        lastDraw = DateTime.Now;
    }

    private readonly ConcurrentQueueExt<DrawOperation> _opPipeline = new();
    private readonly ConcurrentBag<DrawOperation> _garbageOP = new();

    private void TryFree(DrawOperation current)
    {
        if (_opPipeline.Count == 1)
        {
            // static image
        }
        else
        {
            if (_opPipeline.TryDequeue(out var op))
            {
                op.Free();
            }
        }
    }

    private void ClearGarbagesOP(DrawOperation current)
    {
        while (_garbageOP.TryTake(out var garbageOP))
        {
            garbageOP.Free();
        }
    }

    private void ClaimAsGarbage(DrawOperation drawOperation)
    {
        _garbageOP.Add(drawOperation);
    }

    public class DrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmapControlReuse _host;
        private readonly Rect _bounds;
        private readonly ReusableBitmap _bmp;
        private readonly IFrameData _data;

        public DrawOperation(SKBitmapControlReuse host, Rect bounds, ReusableBitmap bmp, IFrameData fdata, bool preserve)
        {
            _host = host;
            _bounds = bounds;
            _bmp = bmp;
            _data = fdata;
            IsPreserve = preserve;
        }

        public bool IsPreserve { get; private set; }
        public bool IsAlive => !IsDisposed;
        public bool IsDisposed { get; private set; }
        public Rect Bounds => _bounds;

        public ReusableBitmap Bitmap => _bmp;
        public bool HitTest(Point p) => _bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var abmp = _bmp;
            var src = new Rect(0, 0, abmp.Size.Width, abmp.Size.Height);
            var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);

            context.DrawBitmap(abmp, src, dest);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            _host.TryFree(this);

            //if (IsPreserve)
            //{
            //    _host.ClaimAsGarbage(this);
            //}
            //else
            //{
            //    _host.ClearGarbagesOP(this);
            //    _host._buffer.Free(_data);
            //}

            GC.SuppressFinalize(this);
        }

        public void Free()
        {
            _host._buffer.Free(_data);
            _bmp.IsRendering = false;
        }
    }

    public class BitmapPool : IDisposable
    {
        private readonly List<ReusableBitmap> _pool = new();
        private readonly PixelSize pix;
        private readonly Vector vector;

        public BitmapPool(IntSize size)
        {
            pix = new PixelSize(size.Width, size.Height);
            vector = new Vector(96, 96);
            var Bmp1 = new ReusableBitmap(pix, vector, PixelFormat.Rgba8888, AlphaFormat.Opaque)
            {
                DebugName = "#1",
            };
            var Bmp2 = new ReusableBitmap(pix, vector, PixelFormat.Rgba8888, AlphaFormat.Opaque)
            {
                DebugName = "#2",
            };
            _pool.Add(Bmp1);
            _pool.Add(Bmp2);
        }

        public void Dispose()
        {
        }

        public ReusableBitmap Resolve()
        {
            ReusableBitmap? match;
            match = _pool.FirstOrDefault(x => !x.IsRendering);

            if (match == null)
            {
                match = new ReusableBitmap(pix, vector, PixelFormat.Rgba8888, AlphaFormat.Opaque)
                {
                    DebugName = $"#{_pool.Count + 1}",
                };
                _pool.Add(match);
            }

            return match;
        }
    }

    public class HardbassBuffer
    {
        private readonly List<IFrameData> _listDraw = [];
        private readonly List<IFrameData> _listReady = [];
        private readonly IReusableContext _reusableContext;
        private readonly SKBitmapControlReuse _host;
        private readonly object _lock = new object();

        public HardbassBuffer(IReusableContext reusableContext, SKBitmapControlReuse host)
        {
            _reusableContext = reusableContext;
            _host = host;
        }

        public int Count => _listReady.Count;

        public void Add(IFrameData frame)
        {
            lock (_lock)
            {
                // todo костыть 
                //var m = _listReady.FirstOrDefault(x => x == frame);
                //if (m != null)
                //    return;

                _listReady.Add(frame);
            }
        }

        public IFrameData? FetchCarefulAndLock()
        {
            lock (_lock)
            {
                switch (_listReady.Count)
                {
                    case 0:
                        return null;
                    default:
                        var frameData = _listReady.First();
                        frameData.IsLocked = true;
                        _listReady.Remove(frameData);
                        _listDraw.Add(frameData);
                        return frameData;
                }
            }
        }

        ///// <summary>
        ///// Dequeue and lock frame
        ///// </summary>
        //public IFrameData? FetchActualAndLock()
        //{
        //    IFrameData? frameData;
        //    int skip = 0;
        //    lock (_host._lockObject)
        //    {
        //        switch (_readylist.Count)
        //        {
        //            case 0:
        //                frameData = null;
        //                return null;
        //            default:
        //                frameData = _readylist.Last();
        //                frameData.IsLocked = true;
        //                break;
        //        }

        //        for (int i = _readylist.Count - 1; i >= 0; i--)
        //        {
        //            var item = _readylist[i];
        //            if (item.IsLocked)
        //            {
        //                continue;
        //            }
        //            else
        //            {
        //                _readylist.Remove(item);
        //                _reusableContext.Recycle(item);
        //                skip++;
        //            }
        //        }
        //        Debug.WriteLine($"Skiped frames: {skip}");
        //    }

        //    return frameData;
        //}

        public void Free(IFrameData data)
        {
            lock (_lock)
            {
                data.IsLocked = false;
                _listDraw.Remove(data);
                _reusableContext.Recycle(data);
            }
        }
    }
}

public enum BitmapRenderResult
{
    Success,
    AllIsRendering,
    AllIsFree,
}