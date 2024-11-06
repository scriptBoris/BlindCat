using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

namespace BlindCatAvalonia.Core;

public class ReusableBitmap : WriteableBitmap
{
    private IFrameData? _pixmap;

    public ReusableBitmap(PixelSize size, Vector dpi, PixelFormat? format = null, AlphaFormat? alphaFormat = null)
        : base(size, dpi, format, alphaFormat)
    {
        using (var vok = this.Lock())
        {
            var p = vok.GetType().GetField("_bitmap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(vok);
            var sk = p as SKBitmap;
            SkiaBitmapDetected = sk ?? throw new InvalidOperationException("Fail to find _bitmap (native SK bitmap)");
        }
    }

    public ReusableBitmap(PixelFormat format, AlphaFormat alphaFormat, nint data, PixelSize size, Vector dpi, int stride)
        : base(format, alphaFormat, data, size, dpi, stride)
    {
        using (var vok = this.Lock())
        {
            var p = vok.GetType().GetField("_bitmap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(vok);
            var sk = p as SKBitmap;
            SkiaBitmapDetected = sk ?? throw new InvalidOperationException("Fail to find _bitmap (native SK bitmap)");
        }
    }

    public required string DebugName { get; set; }
    public SKBitmap SkiaBitmapDetected { get; set; }
    public bool IsRendering { get; set; }

    public void Draw(IFrameData data)
    {
        if (_pixmap == data)
        {
            return;
        }

        if (_pixmap != null)
        {
            _pixmap.Parent = null;
        }

        _pixmap = data;
        data.Parent = this;
        var info = new SKImageInfo(data.Width, data.Height, SKColorType.Rgba8888);
        SkiaBitmapDetected.InstallPixels(info, data.Pointer);
        //SkiaBitmapDetected.SetPixels(data.Pointer);

        //var rect = new PixelRect(0,0, data.Width, data.Height);
        //int size = data.Width * data.Height * data.BytesPerPixel;
        //int stride = data.Width * data.BytesPerPixel;
        //this.CopyPixels(rect, data.Pointer, size, stride);
    }

    public override void Dispose()
    {
        Debug.WriteLine($"ReusableBitmap trying disposing ({DebugName})");
        base.Dispose();
        if (_pixmap != null && !_pixmap.IsDisposed)
        {
            _pixmap.Dispose();
        }
        Debug.WriteLine($"ReusableBitmap is disposed ({DebugName})");
    }
}