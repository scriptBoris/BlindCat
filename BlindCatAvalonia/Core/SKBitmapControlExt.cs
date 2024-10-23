using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using HarfBuzzSharp;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace BlindCatAvalonia.Core;

public class SKBitmapControlExt : SKBitmapControl
{
    private RenderPair? _renderPair;
    private System.Drawing.PointF _offset;
    private double? _forceScale;

    public new SKBitmap? Bitmap
    {
        set
        {
            if (IsRendering && _renderPair != null)
            {
                _renderPair.AutoDispose = true;
            }
            else
            {
                _renderPair?.Dispose();
            }

            var oldSize = _renderPair?.Size ?? new Size();
            _renderPair = MakeAvaloniaBitmap(value);
            var newSize = _renderPair?.Size ?? new Size();

            if (oldSize != newSize)
                InvalidateMeasure();

            InvalidateVisual();
        }
    }

    private IFrameData? _oldRawFrame;
    public IFrameData RawFrame
    {
        set
        {
            _oldRawFrame?.Dispose();
            _oldRawFrame = value;
            var oldSize = _renderPair?.PixelSize;

            var stride = value.Width * value.BytesPerPixel;
            var size = new PixelSize(value.Width, value.Height);

            if (_renderPair == null || oldSize != size)
            {
                if (IsRendering && _renderPair != null)
                {
                    _renderPair.AutoDispose = true;
                }
                else
                {
                    _renderPair?.Dispose();
                }

                var pix = value.Pointer;
                var scale = new Vector(96, 96);
                var format = value.PixelFormat;
                var wBitmap = new WriteableBitmap2(format, AlphaFormat.Premul, pix, size, scale, stride);
                _renderPair = new RenderPair(wBitmap);

                using (var vok = wBitmap.Lock())
                {
                    var p = vok.GetType().GetField("_bitmap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(vok);
                    var sk = p as SKBitmap;
                    wBitmap.SkiaBitmapDetected = sk;
                }
            }
            else
            {
                if (_renderPair.ABitmap is WriteableBitmap2 w)
                {
                    w.SkiaBitmapDetected?.SetPixels(value.Pointer);
                }
            }

            if (oldSize != size)
            {
                InvalidateMeasure();
            }

            InvalidateVisual();
        }
    }

    public SKBitmap? UnsafeBitmap
    {
        get
        {
            if (_renderPair == null || _renderPair.IsDisposed)
                return null;

            return _renderPair.SKBitmap;
        }
    }

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

    private static RenderPair? MakeAvaloniaBitmap(SKBitmap? skia)
    {
        if (skia == null)
            return null;

        PixelFormat format;

        switch (skia.ColorType)
        {
            case SKColorType.Rgba8888:
                format = PixelFormat.Rgba8888;
                break;
            case SKColorType.Bgra8888:
                format = PixelFormat.Bgra8888;
                break;
            case SKColorType.Gray8:
                format = PixelFormats.Gray8;
                break;
            default:
                throw new NotSupportedException();
        }

        var pix = skia.GetPixels();
        var scale = new Vector(96, 96);
        var size = new PixelSize(skia.Width, skia.Height);
        var stride = skia.Info.Width * skia.BytesPerPixel;
        var avaloniaBitmap = new Bitmap(format, AlphaFormat.Premul, pix, size, scale, stride);
        return new RenderPair(skia, avaloniaBitmap);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_renderPair != null && !_renderPair.IsDisposed)
        {
            if (IsRendering)
            {
                _renderPair.AutoDispose = true;
            }
            else
            {
                _renderPair.Dispose();
            }
            _renderPair = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var bitmap = _renderPair;
        if (bitmap is null || bitmap.IsDisposed)
        {
            return DefaultSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return availableSize;
        }

        var sourceSize = new Size(bitmap.Width, bitmap.Height);
        return Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bitmap = _renderPair;
        if (bitmap is null || bitmap.IsDisposed)
        {
            return finalSize;
        }

        if (VerticalAlignment == Avalonia.Layout.VerticalAlignment.Stretch)
        {
            return finalSize;
        }

        var sourceSize = new Size(bitmap.Width, bitmap.Height);
        return Stretch.CalculateSize(finalSize, sourceSize);
    }

    public override void Render(DrawingContext context)
    {
        var bitmap = _renderPair;
        if (bitmap is null || bitmap.IsDisposed)
        {
            context.DrawRectangle(new SolidColorBrush(Colors.Transparent), null, Bounds, 0);
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(bitmap.Width, bitmap.Height);
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

        var bounds = new Rect(0, 0, bitmap.Width, bitmap.Height);
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
            context.Custom(new DrawOperation(this, new Rect(0, 0, bounds.Width, bounds.Height), bitmap));
            //context.Custom(new DrawOperation(this, new Rect(0, 0, bounds.Width, bounds.Height), bitmap.SKBitmap));
            //context.DrawImage(bitmap.ABitmap, new Rect(0, 0, bounds.Width, bounds.Height));
        }
    }

    private class RenderPair : IDisposable
    {
        public RenderPair(SKBitmap skBitmap, Bitmap bitmap)
        {
            SKBitmap = skBitmap;
            ABitmap = bitmap;
        }

        public RenderPair(Bitmap bitmap)
        {
            ABitmap = bitmap;
        }

        public SKBitmap? SKBitmap { get; private set; }
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
            SKBitmap?.Dispose();
            ABitmap.Dispose();

            GC.SuppressFinalize(ABitmap);
            if (SKBitmap != null)
                GC.SuppressFinalize(SKBitmap);
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }

    private class DrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmapControlExt _host;
        private readonly RenderPair _renderPair;
        private readonly Rect _bounds;

        public DrawOperation(SKBitmapControlExt host, Rect bounds, RenderPair renderPair)
        {
            _host = host;
            _renderPair = renderPair;
            _bounds = bounds;
        }

        public void Dispose()
        {
            if (_renderPair.AutoDispose)
                _renderPair.Dispose();

            _host.IsRendering = false;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            _host.IsRendering = true;

            if (_renderPair.IsDisposed)
                return;

            var abmp = _renderPair.ABitmap;
            var src = new Rect(0, 0, abmp.Size.Width, abmp.Size.Height);
            var dest = new Rect(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height);
            context.DrawBitmap(abmp, src, dest);
        }
    }

    private class WriteableBitmap2 : WriteableBitmap
    {
        public WriteableBitmap2(PixelSize size, Vector dpi, PixelFormat? format = null, AlphaFormat? alphaFormat = null)
            : base(size, dpi, format, alphaFormat)
        {
        }

        public WriteableBitmap2(PixelFormat format, AlphaFormat alphaFormat, nint data, PixelSize size, Vector dpi, int stride)
            : base(format, alphaFormat, data, size, dpi, stride)
        {
        }

        public SKBitmap? SkiaBitmapDetected { get; set; }
    }
}