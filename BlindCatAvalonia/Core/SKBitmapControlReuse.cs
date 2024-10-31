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
    private BitmapPool? _bitmapPool;
    private FramePool? _framePool;
    private DispatcherTimer? _drawTimer;

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
        _bitmapPool = new BitmapPool(source.IntFrameSize);
        _framePool = new FramePool(source, this);
        Dispatcher.UIThread.Post(() =>
        {
            _reuseContext = source;
            InvalidateMeasure();
        });
    }

    public void PushFrame(IFrameData data)
    {
        _framePool.Add(data);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);

        _drawTimer = new DispatcherTimer(DispatcherPriority.Background);
        _drawTimer.Interval = TimeSpan.FromMilliseconds(5);
        _drawTimer.Tick += (s, e) =>
        {
            InvalidateVisual();
        };
        _drawTimer.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_reuseContext != null)
        {
            _reuseContext.Dispose();
            _reuseContext = null;

            _drawTimer?.Stop();
            _drawTimer = null;
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
        if (_reuseContext == null || _reuseContext.IsDisposed || _bitmapPool == null || _framePool == null)
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
            bool useStatic = false;
            var frameData = _framePool.FetchCarefulAndLock();
            if (frameData == null)
            {
                frameData = _currentFrameData;
                useStatic = _currentFrameData != null;
            }

            if (frameData == null)
            {
                _currentFrameData = null;
                return;
            }

            if (_framePool.Count > 20)
                Debugger.Break(); // wtf?

            if (useStatic)
            {
                if (_opPipeline.TryLast(out var op))
                {
                    if (op._data == frameData)
                        context.Custom(op);
                    else
                        throw new InvalidOperationException();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                var drawBmp = _bitmapPool.Resolve();
                drawBmp.IsRendering = true;
                drawBmp.Draw(frameData);
                var rect = new Rect(0, 0, bounds.Width, bounds.Height);
                var op = new DrawOperation(this, rect, drawBmp, frameData);
                op.DrawId = ++drawCount;
                context.Custom(op);
                _opPipeline.Enqueue(op);
            }

            _currentFrameData = frameData;
        }

        var span = DateTime.Now - lastDraw;
        var fps = 1000.0 / span.TotalMilliseconds;
        Debug.WriteLine($"FPS: {fps}");
        lastDraw = DateTime.Now;
    }

    private int drawCount = 0;
    private readonly ConcurrentQueueExt<DrawOperation> _opPipeline = new();

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

    public class DrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmapControlReuse _host;
        private readonly Rect _bounds;
        private readonly ReusableBitmap _bmp;
        public readonly IFrameData _data;
        private bool _isFree;

        public DrawOperation(SKBitmapControlReuse host, Rect bounds, ReusableBitmap bmp, IFrameData fdata)
        {
            _host = host;
            _bounds = bounds;
            _bmp = bmp;
            _data = fdata;
        }

        public int DrawId { get; set; }
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
            //if (IsDisposed)
            //    return;

            //IsDisposed = true;

            _host.TryFree(this);
            GC.SuppressFinalize(this);
        }

        public void Free()
        {
            if (_isFree) 
                return;

            _isFree = true;
            _host._framePool.Free(_data);
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

    public class FramePool
    {
        private readonly List<IFrameData> _listDraw = [];
        private readonly List<IFrameData> _listReady = [];
        private readonly IReusableContext _reusableContext;
        private readonly SKBitmapControlReuse _host;
        private readonly object _lock = new object();

        public FramePool(IReusableContext reusableContext, SKBitmapControlReuse host)
        {
            _reusableContext = reusableContext;
            _host = host;
        }

        public int Count => _listReady.Count;

        public void Add(IFrameData frame)
        {
            lock (_lock)
            {
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