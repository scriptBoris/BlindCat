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
using IntSize = System.Drawing.Size;
using Avalonia.Threading;

namespace BlindCatAvalonia.Core;

public interface IReusableContext : IDisposable
{
    event EventHandler? ExternalDisposed;
    IntSize IntFrameSize { get; }
    Size FrameSize => new(IntFrameSize.Width, IntFrameSize.Height);
    ReusableBitmap ReusableBitmap { get; }
    IFrameData? GetFrame();

    bool IsDisposed { get; }
    bool DisposeAfterRender { get; set; }
}

public class SKBitmapControlReuse : SKBitmapControl
{
    private System.Drawing.PointF _offset;
    private double? _forceScale;
    private IReusableContext? _reuseContext;

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
    public bool IsRendering { get; private set; }
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
        Dispatcher.UIThread.Post(() =>
        {
            _reuseContext = source;
            InvalidateMeasure();
        });
    }

    public void DrawFrame()
    {

    }

    //private static RenderPair? MakeAvaloniaBitmap(SKBitmap? skia)
    //{
    //    if (skia == null)
    //        return null;

    //    PixelFormat format;

    //    switch (skia.ColorType)
    //    {
    //        case SKColorType.Rgba8888:
    //            format = PixelFormat.Rgba8888;
    //            break;
    //        case SKColorType.Bgra8888:
    //            format = PixelFormat.Bgra8888;
    //            break;
    //        case SKColorType.Gray8:
    //            format = PixelFormats.Gray8;
    //            break;
    //        default:
    //            throw new NotSupportedException();
    //    }

    //    var pix = skia.GetPixels();
    //    var scale = new Vector(96, 96);
    //    var size = new PixelSize(skia.Width, skia.Height);
    //    var stride = skia.Info.Width * skia.BytesPerPixel;
    //    var avaloniaBitmap = new Bitmap(format, AlphaFormat.Premul, pix, size, scale, stride);
    //    return new RenderPair(skia, avaloniaBitmap);
    //}

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
            if (IsRendering)
            {
                _reuseContext.DisposeAfterRender = true;
            }
            else
            {
                _reuseContext.Dispose();
            }
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
        if (_reuseContext == null || _reuseContext.IsDisposed)
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
            var rect = new Rect(0, 0, bounds.Width, bounds.Height);
            var op = new DrawOperation(this, rect, _reuseContext);
            context.Custom(op);
        }
    }

    private class RenderPair : IDisposable
    {
        public RenderPair(Bitmap bitmap)
        {
            ABitmap = bitmap;
        }

        public Bitmap ABitmap { get; private set; }
        public double Width => ABitmap.Size.Width;
        public double Height => ABitmap.Size.Height;
        public bool IsDisposed { get; private set; }
        public bool CanDispose { get; set; }
        public bool AutoDispose { get; set; }
        public Size Size
        {
            get
            {
                if (IsDisposed)
                    return new Size();

                return new Size(Width, Height);
            }
        }

        public PixelSize PixelSize
        {
            get
            {
                if (IsDisposed)
                    return new PixelSize();

                return ABitmap.PixelSize;
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            ABitmap.Dispose();

            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }

    private class DrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmapControlReuse _host;
        private readonly Rect _bounds;
        private readonly IReusableContext _reusableContext;
        private readonly ReusableBitmap _bmp;

        private IFrameData? oldFrame;

        public DrawOperation(SKBitmapControlReuse host, Rect bounds, IReusableContext reusableContext)
        {
            _host = host;
            _reusableContext = reusableContext;
            _bounds = bounds;
            _bmp = reusableContext.ReusableBitmap;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            _host.IsRendering = true;

            if (_reusableContext.IsDisposed)
            {
                FreeOldFrame();
                _host.IsRendering = false;
                return;
            }

            var newFrameData = _reusableContext.GetFrame();
            if (newFrameData != null)
            {
                FreeOldFrame();

                var abmp = _bmp;
                abmp.Reuse(newFrameData);

                var src = new Rect(0, 0, abmp.Size.Width, abmp.Size.Height);
                var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);
                context.DrawBitmap(abmp, src, dest);
                oldFrame = newFrameData;
            }
            else if (oldFrame != null)
            {
                var abmp = _bmp;
                var src = new Rect(0, 0, abmp.Size.Width, abmp.Size.Height);
                var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);
                context.DrawBitmap(abmp, src, dest);
            }
            else
            {
                _host.IsRendering = false;
            }
        }

        public void Dispose()
        {
            if (_reusableContext.DisposeAfterRender)
                _reusableContext.Dispose();

            oldFrame?.Dispose();
            oldFrame = null;
            _host.IsRendering = false;
        }

        private void FreeOldFrame()
        {
            if (oldFrame != null)
            {
                oldFrame.Dispose();
                oldFrame = null;
            }
        }
    }
}