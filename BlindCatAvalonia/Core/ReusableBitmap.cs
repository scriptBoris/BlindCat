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

namespace BlindCatAvalonia.Core;

public class ReusableBitmap : WriteableBitmap
{
    //public ReusableBitmap(PixelSize size, Vector dpi, PixelFormat? format = null, AlphaFormat? alphaFormat = null)
    //    : base(size, dpi, format, alphaFormat)
    //{
    //}

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

    public SKBitmap SkiaBitmapDetected { get; set; }

    public void Reuse(IFrameData data)
    {
        SkiaBitmapDetected.SetPixels(data.Pointer);
    }

    public void ReusePixels(nint pixmap)
    {
        SkiaBitmapDetected.SetPixels(pixmap);
    }

    public void Next(IFrameData value)
    {
    }
}