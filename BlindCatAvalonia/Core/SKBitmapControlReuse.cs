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
    private DrawOperation? _oldDrawOP;
    private BitmapPool? _bitmapPool;
    private HardbassBuffer? _buffer;
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
        _bitmapPool = new(source.IntFrameSize, this);
        _buffer = new HardbassBuffer(source, this);
        Dispatcher.UIThread.Post(() =>
        {
            _reuseContext = source;
            InvalidateMeasure();
        });
    }

    public void Draw(IFrameData data)
    {
        _buffer.Add(data);
        _currentFrameData = data;
        InvalidateVisual();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_reuseContext != null)
        {
            _reuseContext.Dispose();
            _reuseContext = null;
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

    public override void Render(DrawingContext context)
    {
        var sw = Stopwatch.StartNew();
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
            var frameData = _buffer.FetchActualAndLock();
            if (frameData == null)
                return;

            var drawBmp = _bitmapPool.Resolve();
            drawBmp.IsRendering = true;
            drawBmp.Draw(frameData);

            var rect = new Rect(0, 0, bounds.Width, bounds.Height);
            var op = new DrawOperation(this, rect, drawBmp, frameData);
            context.Custom(op);

            _oldFrameData = frameData;
            _oldDrawOP = op;
            _currentFrameData = frameData;
        }
        sw.StopAndCout("Render");
    }

    public class DrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmapControlReuse _host;
        private readonly Rect _bounds;
        private readonly ReusableBitmap _bmp;
        private readonly IFrameData _data;

        public DrawOperation(SKBitmapControlReuse host, Rect bounds, ReusableBitmap bmp, IFrameData fdata)
        {
            _host = host;
            _bounds = bounds;
            _bmp = bmp;
            _data = fdata;
        }

        public Rect Bounds => _bounds;
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
            _bmp.IsRendering = false;
            _host._buffer.Free(_data);
            GC.SuppressFinalize(this);
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
        private readonly List<IFrameData> _list = [];
        private readonly IReusableContext _reusableContext;
        private readonly SKBitmapControlReuse _host;

        public HardbassBuffer(IReusableContext reusableContext, SKBitmapControlReuse host)
        {
            _reusableContext = reusableContext;
            _host = host;
        }

        public void Add(IFrameData frame)
        {
            lock (_host._lockObject)
            {
                _list.Add(frame);
            }
        }

        /// <summary>
        /// Dequeue and lock frame
        /// </summary>
        public IFrameData? FetchActualAndLock()
        {
            IFrameData? frameData;
            int skip = 0;
            lock (_host._lockObject)
            {
                switch (_list.Count)
                {
                    case 0:
                        frameData = null;
                        return null;
                    default:
                        frameData = _list.Last();
                        frameData.IsLocked = true;
                        break;
                }

                for (int i = _list.Count - 1; i >= 0; i--)
                {
                    var item = _list[i];
                    if (item.IsLocked)
                    {
                        continue;
                    }
                    else
                    {
                        _list.Remove(item);
                        _reusableContext.Recycle(item);
                        skip++;
                    }
                }
                Debug.WriteLine($"Skiped frames: {skip}");
            }

            return frameData;
        }

        public void Free(IFrameData data)
        {
            lock (_host._lockObject)
            {
                data.IsLocked = false;
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