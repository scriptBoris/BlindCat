using BlindCatCore.Core;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatMaui.Core;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace BlindCatMaui.SDControls;

public class SkiaImage : SKCanvasView, IMediaBase, IDisposable
{
    private const double ZOOM_MAX = 5.0;
    private const double ZOOM_MIN = 0.1;

    private CancellationTokenSource _cancellationTokenSource = new();
    private double _zoom = 1.0;
    private bool _isResizeRequired;
    private SKBitmap? _drawableBitmap;
    private SKBitmap? _originalBitmap;
    private double _positionXPercent = 0.5;
    private double _positionYPercent = 0.5;

    public event EventHandler<double>? ZoomChanged;

    #region bindable props
    //// source
    //public static readonly BindableProperty SourceProperty = BindableProperty.Create(
    //    nameof(Source),
    //    typeof(string),
    //    typeof(SkiaImage),
    //    null,
    //    propertyChanged: (b, o, n) =>
    //    {
    //        if (b is SkiaImage self)
    //        {
    //            self._cancellationTokenSource.Cancel();
    //            self._cancellationTokenSource = new();
    //            self.LoadImageFromFile(n as string, self._cancellationTokenSource.Token);
    //        }
    //    }
    //);
    //public string? Source
    //{
    //    get => GetValue(SourceProperty) as string;
    //    set => SetValue(SourceProperty, value);
    //}
    #endregion bindable props

    /// <summary>
    /// Отображаемый битмап
    /// </summary>
    private SKBitmap DrawableBitmap
    {
        get => _drawableBitmap ?? _originalBitmap!;
        set
        {
            if (_drawableBitmap != null && _drawableBitmap != _originalBitmap)
                _drawableBitmap.Dispose();

            _drawableBitmap = value;
        }
    }

    public double PositionXPercent
    {
        get => _positionXPercent;
        private set => _positionXPercent = value.Limitation(-0.245, 1.245);
    }

    public double PositionYPercent
    {
        get => _positionYPercent;
        private set => _positionYPercent = value.Limitation(-0.245, 1.245);
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            double old = _zoom;
            _zoom = value.Limitation(ZOOM_MIN, ZOOM_MAX);
            if (_zoom == old)
                return;

            if (_originalBitmap == null)
                return;

            ZoomChanged?.Invoke(this, _zoom);
            OnPropertyChanged(nameof(Zoom));
            InvalidateSurface();
        }
    }

    private float ScaleDen => (float)DeviceDisplay.Current.MainDisplayInfo.Density;
    private double ViewPortWidth => Width;
    private double ViewPortHeight => Height;
    private Size ViewPortSize => new(ViewPortWidth, ViewPortHeight);

    private async Task LoadImageFromFile(string src, CancellationToken cancel)
    {
        var resBitmap = await TaskExt.Run(() =>
        {
            using var str = File.OpenRead(src);
            return SKBitmap.Decode(str);
        }, cancel);

        if (cancel.IsCancellationRequested)
            return;

        _originalBitmap = resBitmap;

        // auto aspect fit size
        if (ViewPortWidth > 0 && ViewPortHeight > 0)
        {
            double newZoom = TryAutoZoom(_originalBitmap, ViewPortSize);
            RatSetZoom(newZoom);
        }
        else
        {
            _isResizeRequired = true;
        }

        InvalidateSurface();
    }

    private async Task LoadImageFromWeb(Uri uri, CancellationToken cancel)
    {
        var http = this.DiFetch<IHttpLauncher>();
        using var res = await http.GetStream(uri.OriginalString, cancel);

        var resBitmap = await TaskExt.Run(() =>
        {
            return SKBitmap.Decode(res.Result);
        }, cancel);

        if (cancel.IsCancellationRequested)
            return;

        _originalBitmap = resBitmap;

        // auto aspect fit size
        if (ViewPortWidth > 0 && ViewPortHeight > 0)
        {
            double newZoom = TryAutoZoom(_originalBitmap, ViewPortSize);
            RatSetZoom(newZoom);
        }
        else
        {
            _isResizeRequired = true;
        }

        InvalidateSurface();
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var res = base.MeasureOverride(widthConstraint, heightConstraint);
        if (_isResizeRequired && _originalBitmap != null)
        {
            double newZoom = TryAutoZoom(_originalBitmap, new Size(widthConstraint, heightConstraint));
            RatSetZoom(newZoom);
            _isResizeRequired = false;
        }
        return res;
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        e.Surface
            .Canvas
            .Clear();

        if (_originalBitmap == null)
            return;

        var position = new Point(PositionXPercent, PositionYPercent);
        var rectViewPort = new Rect(0, 0, ViewPortWidth, ViewPortHeight);
        var result = ZoomAndClipBitmap(_originalBitmap, position, Zoom, rectViewPort);
        float drawX = (float)result.pos.X;
        float drawY = (float)result.pos.Y;
        DrawableBitmap = result.bitmap;

        Debug.WriteLine($"bitmap H:{DrawableBitmap.Height} W:{DrawableBitmap.Width};\nPosX:{PositionXPercent} PosY:{PositionYPercent}");

        e.Surface
           .Canvas
           .DrawBitmap(DrawableBitmap, drawX, drawY, null);
    }

    public void SetPercentPosition(double percentX, double percentY)
    {
        PositionXPercent = percentX;
        PositionYPercent = percentY;
        InvalidateSurface();
    }

    public void Reset()
    {
        _cancellationTokenSource.Cancel();

        RatSetZoom(1.0);
        PositionXPercent = 0.5;
        PositionYPercent = 0.5;

        _originalBitmap?.Dispose();
        _originalBitmap = null;

        DrawableBitmap?.Dispose();
        DrawableBitmap = null!;

        InvalidateSurface();
    }

    public void Dispose()
    {
        _drawableBitmap?.Dispose();
        _originalBitmap?.Dispose();
        _drawableBitmap = null;
        _originalBitmap = null;
    }

    private static SKBitmap ResizeImage(SKBitmap originalImage, float scaleFactor)
    {
        // Определяем новые размеры для увеличенного изображения
        int newWidth = (int)(originalImage.Width * scaleFactor);
        int newHeight = (int)(originalImage.Height * scaleFactor);

        // Создаем новый битмап для увеличенного изображения
        var resizedImage = new SKBitmap(newWidth, newHeight);

        // Создаем канву для рисования на новом битмапе
        using var canvas = new SKCanvas(resizedImage);

        // Рисуем оригинальное изображение на канве с увеличенными размерами
        canvas.DrawBitmap(originalImage, SKRect.Create(newWidth, newHeight), paint: null);

        return resizedImage;
    }

    public static (SKBitmap bitmap, Point pos) ZoomAndClipBitmap(SKBitmap originBitmap, Point percentPos, double zoom, Rect rectViewPort)
    {
        var zoomedBitmapSize = new Size(originBitmap.Width * zoom, originBitmap.Height * zoom);
        var startPoint = GetStartXY(percentPos, zoomedBitmapSize, rectViewPort.Size);
        var rectBitmap = new Rect(startPoint, zoomedBitmapSize);
        var intersect = Rect.Intersect(rectBitmap, rectViewPort);

        double x = (rectBitmap.X < 0) ? Math.Abs(rectBitmap.X) : 0;
        double y = (rectBitmap.Y < 0) ? Math.Abs(rectBitmap.Y) : 0;
        int newWidth = (int)(intersect.Width);
        int newHeight = (int)(intersect.Height);

        var croppedBitmap = new SKBitmap(newWidth, newHeight);
        using var canvas = new SKCanvas(croppedBitmap);
        canvas.Clear(SKColors.Transparent);

        var src = new Rect(x, y, newWidth, newHeight).Shrink(zoom);
        var dest = new Rect(0, 0, newWidth, newHeight);

        canvas.DrawBitmap(
            bitmap: originBitmap,
            source: src.ToSKRect(),
            dest: dest.ToSKRect()
        );

        if (!rectViewPort.Contains(rectBitmap))
        {
            double drawX = startPoint.X;
            double drawY = startPoint.Y;

            if (rectBitmap.X < 0)
                drawX = 0;

            if (rectBitmap.Y < 0)
                drawY = 0;

            startPoint = new Point(drawX, drawY);
        }

        return (croppedBitmap, startPoint);
    }

    private static SKBitmap ClipBitmap(SKBitmap bitmap, Rect rectBitmap, Rect rectViewPort)
    {
        var intersect = Rect.Intersect(rectBitmap, rectViewPort);
        double x = (rectBitmap.X < 0) ? Math.Abs(rectBitmap.X) : 0;
        double y = (rectBitmap.Y < 0) ? Math.Abs(rectBitmap.Y) : 0;
        int newWidth = (int)(intersect.Width);
        int newHeight = (int)(intersect.Height);
        var croppedBitmap = new SKBitmap(newWidth, newHeight);
        using var canvas = new SKCanvas(croppedBitmap);
        canvas.Clear(SKColors.Transparent);

        var src = new Rect(x, y, newWidth, newHeight);
        var dest = new Rect(0, 0, newWidth, newHeight);

        canvas.DrawBitmap(
            bitmap: bitmap,
            source: src.ToSKRect(),
            dest: dest.ToSKRect()
        );

        return croppedBitmap;
    }

    private static Point GetStartXY(Point percentPos, Size bitmapSize, Size viewPortSize)
    {
        double percentX = percentPos.X;
        double percentY = percentPos.Y;
        double x = viewPortSize.Width * percentX;
        double y = viewPortSize.Height * percentY;
        double offsetX = bitmapSize.Width / 2;
        double offsetY = bitmapSize.Height / 2;
        double resultX = x - offsetX;
        double resultY = y - offsetY;

        return new Point
        {
            X = resultX,
            Y = resultY,
        };
    }

    private static double TryAutoZoom(SKBitmap bitmap, Size viewPortSize)
    {
        // Размеры изображения
        double imageWidth = bitmap.Width;
        double imageHeight = bitmap.Height;

        // Размеры viewport
        double viewPortWidth = viewPortSize.Width;
        double viewPortHeight = viewPortSize.Height;

        // Вычисляем масштаб по ширине и высоте
        double widthScale = viewPortWidth / imageWidth;
        double heightScale = viewPortHeight / imageHeight;

        // Возвращаем минимальный масштаб для AspectFit
        double relativeFactor = Math.Min(widthScale, heightScale);
        double res = relativeFactor.Limitation(ZOOM_MIN, ZOOM_MAX);
        if (res > 1)
            return 1;

        return res;
    }

    private void RatSetZoom(double zoom)
    {
        _zoom = zoom;
        OnPropertyChanged(nameof(Zoom));
        ZoomChanged?.Invoke(this, zoom);
    }

    public Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        if (filePath == "<NULL>") 
        {
            return Task.CompletedTask;
        }
        else if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            return LoadImageFromWeb(uri, cancel);
        }
        else
        {
            return LoadImageFromFile(filePath, cancel);
        }
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public async Task SetSourceStorage(StorageFile file, CancellationToken cancel)
    {
        using var decode = await this.DiFetch<ICrypto>().DecryptFile(file.FilePath, file.Storage.Password, cancel);
        if (decode.IsCanceled)
            return;

        if (decode.IsFault)
        {
            Debugger.Break();
            return;
        }

        var resBitmap = await TaskExt.Run(() =>
        {
            return SKBitmap.Decode(decode.Result);
        }, cancel);

        if (cancel.IsCancellationRequested)
            return;

        if (resBitmap == null)
        {
            Debug.WriteLine($"Fail decode binary for \"{file.Name}\" [SKBitmap.Decode]");
            return;
        }

        _originalBitmap = resBitmap;

        // auto aspect fit size
        if (ViewPortWidth > 0 && ViewPortHeight > 0)
        {
            double newZoom = TryAutoZoom(_originalBitmap, ViewPortSize);
            RatSetZoom(newZoom);
        }
        else
        {
            _isResizeRequired = true;
        }

        InvalidateSurface();
    }
}