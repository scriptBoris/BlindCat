using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatMaui.Core;
using BlindCatMaui.Services;
using BlindCatMaui.Views;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace BlindCatMaui.SDControls;

public class ImagePreview : SKCanvasView, IDisposable
{
    private CancellationTokenSource _cancellationTokenSource = new();
    private SKBitmap? _currentImageBmp;
    private SKBitmap? _originImageData;
    private Size? _aspectCache;
    private MediaFormats _fileExt = MediaFormats.Unknown;

    #region bindable props
    // source
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(ImagePreview),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is ImagePreview self)
            {
                self.TryCancelPrevTask();
                self.LoadLocal(n as string, self._cancellationTokenSource.Token);
            }
        }
    );
    public string? Source
    {
        get => GetValue(SourceProperty) as string;
        set => SetValue(SourceProperty, value);
    }

    // source storage
    public static readonly BindableProperty SourceStorageProperty = BindableProperty.Create(
        nameof(SourceStorage),
        typeof(StorageFile),
        typeof(ImagePreview),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is ImagePreview self)
            {
                self.TryCancelPrevTask();
                self.LoadLocalStorageFile((StorageFile)n, self._cancellationTokenSource.Token);
            }
        }
    );
    public StorageFile? SourceStorage
    {
        get => GetValue(SourceStorageProperty) as StorageFile;
        set => SetValue(SourceStorageProperty, value);
    }

    // webp
    public static readonly BindableProperty WebpProperty = BindableProperty.Create(
        nameof(Webp),
        typeof(string),
        typeof(ImagePreview),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is ImagePreview self)
            {
                self.TryCancelPrevTask();
                self.LoadWeb(n as string, self._cancellationTokenSource.Token);
            }
        }
    );
    public string? Webp
    {
        get => GetValue(WebpProperty) as string;
        set => SetValue(WebpProperty, value);
    }

    // layout controller
    public static readonly BindableProperty LayoutControllerProperty = BindableProperty.Create(
        nameof(LayoutController),
        typeof(View),
        typeof(ImagePreview),
        null,
        propertyChanged: (b, o, n) =>
        {
            //if (b is ImagePreview self)
            //{
            //    self.TryCancelPrevTask();
            //    self.LoadLocal(n as string, self._cancellationTokenSource.Token);
            //}
        }
    );
    public View? LayoutController
    {
        get => GetValue(LayoutControllerProperty) as View;
        set => SetValue(LayoutControllerProperty, value);
    }
    #endregion bindable props

    private IFFMpegService _fFMpegService => this.DiFetch<IFFMpegService>();

    /// <summary>
    /// Оригинальная картинка
    /// </summary>
    private SKBitmap? OriginImageData
    {
        get => _originImageData;
        set
        {
            if (_originImageData == value)
                return;

            _originImageData?.Dispose();
            _originImageData = value;
        }
    }

    /// <summary>
    /// Картинка которая отображается сейчас
    /// </summary>
    private SKBitmap? ImageData
    {
        get => _currentImageBmp;
        set
        {
            if (_currentImageBmp == value)
                return;

            _currentImageBmp?.Dispose();
            _currentImageBmp = value;
        }
    }

    private async void LoadWeb(string? url, CancellationToken cancel)
    {
        byte[]? webcache = await WebCacheGet(url);
        if (webcache != null)
        {
            OriginImageData = SKBitmap.Decode(webcache);
            InvalidateSurface();
            return;
        }

        var launcher = this.DiFetch<IHttpLauncher>();

        if (url == null || launcher == null)
        {
            ErrorImage("No URL or HttpLauncher");
            return;
        }

        var arrRes = await launcher.GetBin(url, null);
        if (arrRes.IsFault)
        {
            ErrorImage(arrRes.Description);
            return;
        }

        WebCacheWrite(url, arrRes.Result);

        if (cancel.IsCancellationRequested)
            return;

        try
        {
            await Task.Run(() =>
            {
                OriginImageData = SKBitmap.Decode(arrRes.Result);
            });
            if (cancel.IsCancellationRequested)
                return;

            InvalidateSurface();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ErrorImage(ex.Message);
        }
    }

    private async void LoadLocal(string? path, CancellationToken cancel)
    {
        using var decRes = await _fFMpegService.DecodePicture(path, cancel);
        if (decRes.IsCanceled)
        {
            return;
        }

        if (decRes.IsFault)
        {
            Debug.WriteLine(decRes.Exception);
            ErrorImage(decRes.Description);
            return;
        }

        _fileExt = decRes.Result.EncodedFormat;
        OriginImageData = MakeAspectFill((SKBitmap)decRes.Result.Bitmap, new Size(400, 400));
        ImageData = null;
        _aspectCache = default;
        InvalidateSurface();
    }

    private async void LoadLocalStorageFile(StorageFile? secFile, CancellationToken cancel)
    {
        _fileExt = MediaFormats.Unknown;
        OriginImageData = null;
        ImageData = null;
        _aspectCache = default;
        InvalidateSurface();

        if (secFile == null)
            return;

        string dirThumbnails = Path.Combine(secFile.Storage.Path, "tmls");
        if (!Directory.Exists(dirThumbnails))
            Directory.CreateDirectory(dirThumbnails);

        string pathThumbnail = Path.Combine(dirThumbnails, secFile.Guid.ToString());

        MediaFormats format;
        SKBitmap resultBitmap;

        // use cache
        if (File.Exists(pathThumbnail))
        {
            using var crypto = await this.DiFetch<ICrypto>().DecryptFile(pathThumbnail, secFile.Storage.Password, cancel);
            if (crypto.IsCanceled)
                return;

            if (crypto.IsFault)
            {
                Debug.WriteLine(crypto.MessageForLog);
                ErrorImage(crypto.Description);
                return;
            }

            format = secFile.CachedMediaFormat;
            resultBitmap = await TaskExt.Run(() =>
            {
                return SKBitmap.Decode(crypto.Result);
            }, cancel);

            if (cancel.IsCancellationRequested)
                return;
        }
        // create new
        else
        {
            var cryptoService = this.DiFetch<ICrypto>();
            using var crypto = await cryptoService.DecryptFile(secFile.FilePath, secFile.Storage.Password, cancel);
            if (crypto.IsCanceled)
                return;

            if (crypto.IsFault)
            {
                Debug.WriteLine(crypto.MessageForLog);
                ErrorImage(crypto.Description);
                return;
            }

            var decodeStream = crypto.Result;
            var decRes = await _fFMpegService.DecodePicture(decodeStream, cancel);
            if (decRes.IsCanceled)
                return;

            if (decRes.IsFault)
            {
                Debug.WriteLine(decRes.MessageForLog);
                ErrorImage(decRes.Description);
                return;
            }

            format = decRes.Result.EncodedFormat;
            resultBitmap = (SKBitmap)decRes.Result.Bitmap;

            SaveCache(pathThumbnail, resultBitmap, cryptoService, secFile.Storage.Password);
        }

        _fileExt = format;
        secFile.CachedHandlerType = BlindCatCore.ViewModels.MediaPresentVm.ResolveHandler(_fileExt);
        secFile.CachedMediaFormat = _fileExt;
        OriginImageData = resultBitmap;
        ImageData = null;
        _aspectCache = default;
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        if (OriginImageData == null)
        {
            e.Surface.Canvas.Clear();
            return;
        }

        var bitmap = ImageData ?? OriginImageData;
        var aspectCache = new Size(Width, Height);

        // Если есть RAM кэш
        if (aspectCache == _aspectCache && bitmap != null)
        {
            e.Surface.Canvas.DrawBitmap(bitmap, 0, 0, null);
        }
        // Если нет RAM кэша, то рисуем превью из оригинала
        else
        {
            bitmap = MakeAspectFill(OriginImageData, aspectCache);
            var args = new SKDrawTextArgs
            {
                Text = "no_text",
                TextSize = 20,
                TextColor = SKColors.White,
                OutlineColor = SKColors.Black,
                OutlineWidth = 5,
                HorizontalAlignment = TextAlignment.End,
                VerticalAlignment = TextAlignment.End,
            };

            if (_fileExt.IsVideo())
            {
                args.Text = "video";
                SkiaExt.DrawTextWithOutline(bitmap, args);
            }
            else if (_fileExt == MediaFormats.Gif)
            {
                args.Text = "gif";
                SkiaExt.DrawTextWithOutline(bitmap, args);
            }

            e.Surface.Canvas.DrawBitmap(bitmap, 0, 0, null);

            ImageData = bitmap;
            _aspectCache = aspectCache;
        }
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        double w = widthConstraint;
        double h = heightConstraint;

        bool isWidthInfinity = double.IsInfinity(widthConstraint);
        bool isHeightInfinity = double.IsInfinity(heightConstraint);
        if (isWidthInfinity && isHeightInfinity)
        {
            w = 200;
            h = 200;
        }
        else if (isWidthInfinity)
        {
            w = h;
        }
        else if (isHeightInfinity)
        {
            h = w;
        }
        else
        {
            double value = Math.Min(w, h);
            w = value;
            h = value;
        }

        if (LayoutController != null)
        {
            double parentW = LayoutController.Width;
            double parentH = LayoutController.Height;
            int cols = DirPresentView.MakeGridItemsLayout(parentW, parentH);
            w = parentW / cols;
            h = w;
        }


        DesiredSize = new Size(w, h);
        return DesiredSize;
    }

    private void TryCancelPrevTask()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new();
    }

    private void ErrorImage(string error)
    {
        _fileExt = MediaFormats.Unknown;
        var bmp = new SKBitmap(300, 300);
        using var canv = new SKCanvas(bmp);
        canv.DrawColor(SKColors.Black);
        SkiaExt.DrawTextWithOutline(bmp, new SKDrawTextArgs
        {
            Text = error,
            VerticalAlignment = TextAlignment.Center,
            HorizontalAlignment = TextAlignment.Center,
            TextColor = SKColors.Red,
            OutlineColor = SKColors.White,
            OutlineWidth = 3,
            TextSize = 14,
        });
        OriginImageData = bmp;
        ImageData = null;
        _aspectCache = default;
        InvalidateSurface();

        //ImageData = null;
        //InvalidateSurface();
    }

    public void Dispose()
    {
        _currentImageBmp?.Dispose();
        _originImageData?.Dispose();
        _currentImageBmp = null;
        _originImageData = null;
    }

    private static async void SaveCache(string pathThumbnail, SKBitmap originalBitmap, ICrypto cryptoService, string password)
    {
        using var resizedBitmap = MakeAspectFill(originalBitmap, new Size(400, 400));
        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var mem = new MemoryStream();
        data.SaveTo(mem);
        mem.Position = 0;

        await cryptoService.EncryptFile(mem, pathThumbnail, password);
    }

    private static async void WebCacheWrite(string url, byte[] array)
    {
        string dat = Sha256Utils.CalculateSHA256(url);
        string path1 = Path.Combine(FileSystem.Current.AppDataDirectory, "webCache");
        if (!Directory.Exists(path1))
            Directory.CreateDirectory(path1);

        string path2 = Path.Combine(FileSystem.Current.AppDataDirectory, "webCache", dat);
        if (File.Exists(path2))
            File.Delete(path2);

        await File.WriteAllBytesAsync(path2, array);
    }

    private static async Task<byte[]?> WebCacheGet(string? url)
    {
        if (url == null)
            return null;

        string dat = Sha256Utils.CalculateSHA256(url);
        string path = Path.Combine(FileSystem.Current.AppDataDirectory, "webCache", dat);
        if (!File.Exists(path))
            return null;

        byte[] data = await File.ReadAllBytesAsync(path);
        return data;
    }

    /// <summary>
    /// Заполнение без пустот, без расстягивания
    /// </summary>
    private static SKBitmap MakeAspectFill(SKBitmap imageData, Size viewPortSize)
    {
        // Вычисляем соотношение сторон изображения и viewport
        float sourceAspect = (float)imageData.Width / imageData.Height;
        float targetAspect = (float)viewPortSize.Width / (float)viewPortSize.Height;

        // Определяем размеры обрезанного изображения
        int cropWidth, cropHeight;
        if (sourceAspect > targetAspect)
        {
            // Изображение шире чем viewport, обрезаем по ширине
            cropHeight = imageData.Height;
            cropWidth = (int)(cropHeight * targetAspect);
        }
        else
        {
            // Изображение выше чем viewport, обрезаем по высоте
            cropWidth = imageData.Width;
            cropHeight = (int)(cropWidth / targetAspect);
        }

        // Вычисляем координаты для центрирования обрезки
        int cropX = (imageData.Width - cropWidth) / 2;
        int cropY = (imageData.Height - cropHeight) / 2;

        // Создаем bitmap для обрезанного изображения
        SKBitmap croppedBitmap = new SKBitmap(cropWidth, cropHeight);
        using (SKCanvas canvas = new SKCanvas(croppedBitmap))
        {
            SKRect srcRect = new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            SKRect destRect = new SKRect(0, 0, cropWidth, cropHeight);
            canvas.DrawBitmap(imageData, srcRect, destRect);
        }

        // Изменяем размер обрезанного изображения до размеров viewport
        SKBitmap resizedBitmap = new SKBitmap((int)viewPortSize.Width, (int)viewPortSize.Height);
        using (SKCanvas canvas = new SKCanvas(resizedBitmap))
        {
            canvas.DrawBitmap(croppedBitmap, new SKRect(0, 0, (float)viewPortSize.Width, (float)viewPortSize.Height));
        }

        return resizedBitmap;
    }
}