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
    IntSize IntFrameSize { get; }
    Size FrameSize => new(IntFrameSize.Width, IntFrameSize.Height);
    bool IsDisposed { get; }

    // get recycle elements
    IFrameData? GetFrame();
    ReusableBitmap ResolveBitmap();

    // free recycle elements
    void RecycleFrame(IFrameData data);
    void RecycleBitmap(ReusableBitmap bitmap);
}

public class SKBitmapControlReuse : SKBitmapControl
{
    private readonly ConcurrentQueueExt<DrawOperation> _opPipeline = new();
    private System.Drawing.PointF _offset;
    private double? _forceScale;
    private IFrameData? _currentFrameData;
    private DispatcherTimer? _drawTimer;
    private int _drawOPCount = 0;

    #region props
    protected IReusableContext? ReuseContext { get; set; }

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
        ReuseContext?.Dispose();
        ReuseContext = source;
        Dispatcher.UIThread.Post(() =>
        {
            InvalidateMeasure();
        });
    }

    protected virtual void DestroyReuseContext()
    {

    }

    public void OnFrameReady()
    {
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (ReuseContext == null)
        {
            return DefaultSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return availableSize;
        }

        return Stretch.CalculateSize(availableSize, ReuseContext.FrameSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (ReuseContext is null || ReuseContext.IsDisposed)
        {
            return finalSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return finalSize;
        }

        var sourceSize = ReuseContext.FrameSize;
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    private DateTime lastDraw;
    public override void Render(DrawingContext context)
    {
        if (ReuseContext == null || ReuseContext.IsDisposed)
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Transparent), null, Bounds, 0);
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = ReuseContext.FrameSize;
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

        var bounds = new Rect(0, 0, ReuseContext.IntFrameSize.Width, ReuseContext.IntFrameSize.Height);
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
            var frameData = ReuseContext.GetFrame();
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

            if (useStatic)
            {
                if (_opPipeline.TryLast(out var op))
                {
                    context.Custom(op);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                var drawBmp = ReuseContext.ResolveBitmap();
                drawBmp.IsRendering = true;
                drawBmp.Draw(frameData);
                var rect = new Rect(0, 0, bounds.Width, bounds.Height);
                var op = new DrawOperation(this, rect, drawBmp, frameData, ReuseContext);
                op.DrawId = ++_drawOPCount;
                context.Custom(op);
                _opPipeline.Enqueue(op);

                // DEBUG
                var latency = DateTime.Now - frameData.DecodedAt;
                if (latency.TotalMilliseconds > 100)
                {
                }
                Debug.WriteLine($"Draw delay: {latency.TotalMilliseconds}ms");
            }

            _currentFrameData = frameData;
        }

        // FPS monitoring
        var span = DateTime.Now - lastDraw;
        var fps = 1000.0 / span.TotalMilliseconds;
        Debug.WriteLine($"FPS: {fps}");
        lastDraw = DateTime.Now;
    }

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
        private readonly IFrameData _data;
        private readonly IReusableContext _reusableContext;
        private bool _isFree;

        public DrawOperation(SKBitmapControlReuse host, Rect bounds, ReusableBitmap bmp, IFrameData fdata, IReusableContext reusableContext)
        {
            _host = host;
            _bounds = bounds;
            _bmp = bmp;
            _data = fdata;
            _reusableContext = reusableContext;
        }

        public int DrawId { get; set; }
        public Rect Bounds => _bounds;
        public bool HitTest(Point p) => _bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var abmp = _bmp;
            var src = new Rect(0, 0, abmp.Size.Width, abmp.Size.Height);
            var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);

            //// DEBUG
            //var latency = DateTime.Now - _data.DecodedAt;
            //Debug.WriteLine($"Draw delay: {latency.TotalMilliseconds}ms");
            //if (latency.TotalMilliseconds > 100)
            //{
            //}

            context.DrawBitmap(abmp, src, dest);
        }

        public void Dispose()
        {
            _host.TryFree(this);
            GC.SuppressFinalize(this);
        }

        public void Free()
        {
            if (_isFree)
                return;

            _isFree = true;
            _reusableContext.RecycleFrame(_data);
            _reusableContext.RecycleBitmap(_bmp);
        }
    }
}