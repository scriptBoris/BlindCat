using SkiaSharp;
using System.Runtime.InteropServices;

namespace BlindCatMaui.Core;

public class SKBitmapHndl : SKBitmap
{
    private readonly GCHandle _handle;

    public SKBitmapHndl(SKImageInfo info, GCHandle handle) : base(info)
    {
        _handle = handle;
    }

    protected override void DisposeNative()
    {
        base.DisposeNative();
        _handle.Free();
    }
}