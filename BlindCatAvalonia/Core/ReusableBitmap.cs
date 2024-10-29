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

        _pixmap = data;
        SkiaBitmapDetected.SetPixels(data.Pointer);
    }
}