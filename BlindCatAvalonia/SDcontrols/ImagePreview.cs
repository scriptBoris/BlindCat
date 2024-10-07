using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using BlindCatAvalonia.Core;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Immutable;
using BlindCatAvalonia.MediaPlayers;

namespace BlindCatAvalonia.SDcontrols;

public class ImagePreview : SKBitmapControlExt, IVirtualGridRecycle
{
    private object? _source;
    private CancellationTokenSource _cancellationTokenSource = new();
    private ICrypto _crypto = null!;
    private IFFMpegService _ffmpeg = null!;
    private object? _succesedLoadedSource;

    static ImagePreview()
    {
        var m = new StyledPropertyMetadata<double>();
        OpacityProperty.OverrideMetadata<ImagePreview>(m);
    }

    public override Size DefaultSize => new Size(250, 250);
    public bool IsVirtualAttach { get; set; }
    public bool IsVirtualDeattach { get; set; }

    #region bindable props
    // source
    public static readonly DirectProperty<ImagePreview, object?> SourceProperty = AvaloniaProperty.RegisterDirect<ImagePreview, object?>(
        nameof(Source),
        (self) => self._source,
        (self, nev) =>
        {
            self._source = nev;

            if (self.IsLoaded)
            {
                self.UpdateImg(nev);
            }
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
            OpacityMask = new ImmutableSolidColorBrush(Colors.Gray);
            _crypto = this.DI<ICrypto>();
            _ffmpeg = this.DI<IFFMpegService>();
            UpdateImg(Source);
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
            Debug.WriteLine($"IMAGE PREVIEW:\nNew source img: {src ?? "NULL"} (already)");
            return;
        }
        else
        {
            Debug.WriteLine($"IMAGE PREVIEW:\nNew source img: {src ?? "NULL"}");
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
                default:
                    Bitmap = null;
                    return;
            }

            if (bmp != null)
            {
                Bitmap = bmp;
                _succesedLoadedSource = src;
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
                Loading(false);
        }
    }

    private async Task<SKBitmap?> LoadStorageFile(StorageFile? secFile, CancellationToken cancel)
    {
        if (secFile == null)
            return null;

        string? password = secFile.Storage.Password;
        string dirThumbnails = Path.Combine(secFile.Storage.Path, "tmls");
        if (!Directory.Exists(dirThumbnails))
            Directory.CreateDirectory(dirThumbnails);

        string pathThumbnail = Path.Combine(dirThumbnails, secFile.Guid.ToString());
        MediaFormats format = secFile.CachedMediaFormat;

        SKBitmap resultBitmap;

        // use cache
        if (File.Exists(pathThumbnail))
        {
            var sw = Stopwatch.StartNew();
            using var crypto = await _crypto.DecryptFile(pathThumbnail, secFile.Storage.Password, cancel);
            if (crypto.IsFault)
            {
                SetError(crypto);
                return null;
            }
            sw.StopAndCout("dencrypted preview img file (stream)");

            resultBitmap = SKBitmap.Decode(crypto.Result);
            //resultBitmap = await TaskExt.Run(() =>
            //{
            //    return SKBitmap.Decode(crypto.Result);
            //}, cancel);

            if (cancel.IsCancellationRequested)
                return null;
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
            if (decRes.IsFault)
            {
                SetError(decRes);
                return null;
            }

            if (cancel.IsCancellationRequested)
                return null;

            format = decRes.Result.EncodedFormat;
            resultBitmap = (SKBitmap)decRes.Result.Bitmap;
            //resultBitmap = SaveCacheThumbnail(pathThumbnail, originImage, _crypto, secFile.Storage.Password!);
        }

        secFile.CachedMediaFormat = format;
        return resultBitmap;
    }

    private async Task<SKBitmap?> LoadLocalFile(string filePath, CancellationToken cancel)
    {
        var mf = MediaPresentVm.ResolveFormat(filePath);
        if (mf.IsVideo())
        {
            var res = await _ffmpeg.DecodePicture(filePath, mf, new System.Drawing.Size(250, 250), cancel);
            if (res.IsFault)
            {
                SetError(res);
                return null;
            }

            return (SKBitmap)res.Result.Bitmap;
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
            {
                SetError(res);
                return null;
            }

            return res.Result;
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

    private void SetError(AppResponse err)
    {
        if (err.IsCanceled)
            return;

        IErrorListener? match = null;
        var p = this.Parent;
        while (match == null && p != null)
        {
            if (p is IErrorListener c)
                match = c;
            else
                p = p.Parent;
        }

        match?.SetError(err);
        Debug.WriteLine(err.MessageForLog);
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