using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Skia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.MediaPlayers;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using SkiaSharp;

namespace BlindCatAvalonia.SDcontrols;

public class ImagePreview : SKBitmapControl, IVirtualGridRecycle
{
    private object? _source;
    private CancellationTokenSource _cancellationTokenSource = new();
    private ICrypto _crypto = null!;
    private IFFMpegService _ffmpeg = null!;
    private object? _succesedLoadedSource;
    private AppResponse? _error;

    static ImagePreview()
    {
        var m = new StyledPropertyMetadata<double>();
        OpacityProperty.OverrideMetadata<ImagePreview>(m);

    }

    public ImagePreview()
    {
        OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
        _crypto = this.DI<ICrypto>();
        _ffmpeg = this.DI<IFFMpegService>();
        //UpdateImg(Source);
    }

    //public override Size DefaultSize => new Size(250, 250);
    public bool IsVirtualAttach { get; set; }
    public bool IsVirtualDeattach { get; set; }

    #region bindable props
    // source
    public static readonly DirectProperty<ImagePreview, object?> SourceProperty = AvaloniaProperty.RegisterDirect<ImagePreview, object?>(
        nameof(Source),
        (self) => self._source,
        (self, nev) =>
        {
            if (nev == null)
                return;

            self._source = nev;
            self.UpdateImg(nev);

            //if (self.IsLoaded)
            //{
            //    self.UpdateImg(nev);
            //}
            //else
            //{

            //}
        }
    );
    public object? Source
    {
        get => GetValue(SourceProperty);
        set => SetAndRaise(SourceProperty, ref _source, value);
    }

    // is loading
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<ImagePreview, bool>(
        nameof(IsLoading),
        defaultBindingMode: Avalonia.Data.BindingMode.OneWay
    );
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
    #endregion bindable props

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (!IsVirtualAttach)
        {
            //OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
            //_crypto = this.DI<ICrypto>();
            //_ffmpeg = this.DI<IFFMpegService>();
            //UpdateImg(Source);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();

        if (!IsVirtualDeattach)
        {
            base.OnUnloaded(e);
        }
    }

    private async void UpdateImg(object? src)
    {
        if (_succesedLoadedSource == src)
        {
            //Debug.WriteLine($"IMAGE PREVIEW:\nNew source img: {src ?? "NULL"} (already)");
            //return;
        }
        else
        {
            //Debug.WriteLine($"IMAGE PREVIEW:\nNew source img: {src ?? "NULL"}");
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();
        var token = _cancellationTokenSource.Token;
        SKBitmap? bmp;
        Bitmap = null;
        Loading(true);

        try
        {
            switch (src)
            {
                case StorageFile storageFile:
                    bmp = await LoadStorageFile(storageFile, token);
                    break;
                case string file:
                    bmp = await LoadLocalFile(file, token);
                    break;
                case StorageAlbum storageAlbum:
                    bmp = await LoadAlbumPreview(storageAlbum, token);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (bmp != null)
            {
                Bitmap = bmp;
                _succesedLoadedSource = src;
            }
            else
            {
                if (!token.IsCancellationRequested)
                {
                }
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
                Loading(false);
        }
    }

    private async Task<SKBitmap?> LoadStorageFile(StorageFile secFile, CancellationToken cancel)
    {
        string? password = secFile.Storage.Password;
        string dirThumbnails = Path.Combine(secFile.Storage.Path, "tmls");
        if (!Directory.Exists(dirThumbnails))
            Directory.CreateDirectory(dirThumbnails);

        var id = secFile.Storage.Guid;
        long? size = secFile.OriginFileSize;
        string pathThumbnail = Path.Combine(dirThumbnails, secFile.Guid.ToString());
        MediaFormats format = secFile.CachedMediaFormat;

        SKBitmap? resultBitmap = null;
        AppResponse? error = null;

        // use cache
        if (File.Exists(pathThumbnail))
        {
            var sw = Stopwatch.StartNew();
            resultBitmap = await Task.Run(() =>
            {
                using var crypto = _crypto.DecryptFileFast(id, pathThumbnail, secFile.Storage.Password, size);
                if (crypto == null)
                {
                    error = AppResponse.Error("Fail decrypt");
                    return null;
                }

                SKBitmap? result = null;
                try
                {
                    result = SKBitmap.Decode(crypto);
                    if (result == null)
                    {
                        error = AppResponse.Error("Fail decode", 912758);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    error = AppResponse.Error("Fail decode", 912759, ex);
                    return null;
                }
            });
            sw.StopAndCout("dencrypted preview img file (stream)");

            if (cancel.IsCancellationRequested)
            {
                resultBitmap?.Dispose();
                return null;
            }
        }
        // create new
        else
        {
            var enc = new EncryptionArgs
            {
                EncryptionMethod = secFile.EncryptionMethod,
                Password = password,
            };
            var decRes = await _ffmpeg.SaveThumbnail(secFile.FilePath, format, pathThumbnail, enc, password);
            
            if (cancel.IsCancellationRequested)
                return null;

            if (decRes.IsFault)
            {
                error = decRes;
            }

            if (decRes.IsSuccess) 
            {
                format = decRes.Result.EncodedFormat;
                resultBitmap = decRes.Result.Bitmap as SKBitmap;
            }
        }

        if (error != null)
            resultBitmap = HandleError(resultBitmap, error);

        secFile.CachedMediaFormat = format;
        return resultBitmap;
    }

    private async Task<SKBitmap?> LoadLocalFile(string filePath, CancellationToken cancel)
    {
        AppResponse? error = null;
        SKBitmap? resultBmp = null;

        var mf = MediaPresentVm.ResolveFormat(filePath);
        if (mf.IsVideo())
        {
            var res = await _ffmpeg.DecodePicture(filePath, mf, new System.Drawing.Size(250, 250), cancel);
            if (res.IsFault)
            {
                error = res;
                return null;
            }

            resultBmp = (SKBitmap)res.Result.Bitmap;
        }
        else
        {
            var res = await TaskExt.Run(() =>
            {
                if (!File.Exists(filePath))
                    return AppResponse.Error("File not exists");

                var bmpRes = LoadThumbnail(filePath, 250, 250);
                if (bmpRes.IsFault)
                    return bmpRes;

                var bmp = bmpRes.Result;
                int h = bmp.Height;
                int w = bmp.Width;
                if (Math.Max(h, w) > 500)
                {
                    bmp = (SKBitmap)_ffmpeg.ResizeBitmap(bmp, new System.Drawing.Size(250, 250));
                }
                return AppResponse.Result(bmp);
            }, cancel);

            if (cancel.IsCancellationRequested)
                return null;

            if (res.IsFault)
                error = res;

            resultBmp = res.Result;
        }

        if (error != null)
        {
            return HandleError(resultBmp, error);
        }
        else
        {
            return resultBmp;
        }
    }

    private async Task<SKBitmap?> LoadAlbumPreview(StorageAlbum album, CancellationToken cancel)
    {
        var storage = album.SourceDir as StorageDir;
        if (storage == null)
            return null;

        string? password = storage.Controller?.Password;
        string? pathThumbnail = album.FilePreview;

        SKBitmap? resultBitmap = null;
        AppResponse? error = null;
        
        var id = album.Guid;

        // use cache
        if (File.Exists(pathThumbnail))
        {
            var sw = Stopwatch.StartNew();
            resultBitmap = await TaskExt.Run(() =>
            {
                using var crypto = _crypto.DecryptFileFast(id, pathThumbnail, password, null);
                if (crypto == null)
                {
                    error = AppResponse.Error("Fail decrypt");
                    return null;
                }
                var res = SKBitmap.Decode(crypto);
                return res;
            }, cancel);
            sw.StopAndCout("dencrypted preview img file (stream)");

            //resultBitmap = SKBitmap.Decode(crypto);

            if (cancel.IsCancellationRequested)
            {
                resultBitmap?.Dispose();
                return null;
            }
        }
        // create new
        else
        {
            // todo сделать создание превью
        }

        if (error != null)
        {
            return HandleError(resultBitmap, error);
        }
        else
        {
            return resultBitmap;
        }
    }

    private AppResponse<SKBitmap> LoadThumbnail(string filePath, int desiredWidth, int desiredHeight)
    {
        try
        {
            using var codec = SKCodec.Create(filePath);
            if (codec == null)
                return AppResponse.Error("Unable to create codec for the image", 3111301);

            var sizeDim = codec.GetScaledDimensions(desiredWidth / (float)codec.Info.Width);
            var scaledInfo = new SKImageInfo(sizeDim.Width, sizeDim.Height);
            var bitmap = new SKBitmap(scaledInfo);
            var result = codec.GetPixels(scaledInfo, bitmap.GetPixels(out _));
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return AppResponse.Error($"Failed to decode image. Codec result: {result}", 3111302);

            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                var m = ImageSkia.MakeMeta(filePath, bitmap, false, true, true);
                bitmap = m.RotatedImage!;
            }

            return AppResponse.Result(bitmap);
        }
        catch (Exception ex)
        {
            return AppResponse.Error("Unexpected error", 3111303, ex);
        }
    }

    private SKBitmap HandleError(SKBitmap? bitmap, AppResponse error)
    {
        string msg = error.Description;
        if (bitmap == null)
        {
            bitmap = new SKBitmap(250, 250);
        }

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = false,
            TextSize = 14,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.Default,
        };

        // Вычисляем размеры текста
        var textWidth = paint.MeasureText(msg);
        var textBounds = new SKRect();

        paint.MeasureText(msg, ref textBounds);

        // Центрируем текст
        float x = bitmap.Width / 2f;
        float y = (bitmap.Height / 2f) - textBounds.MidY;

        // Рисуем текст
        canvas.DrawText(msg, x, y, paint);
        return bitmap;
    }

    private void Loading(bool flag)
    {
        IsLoading = flag;
        if (Parent is ILoadingListener loadingListener)
        {
            loadingListener.LoadingStart(flag);
        }
    }
}