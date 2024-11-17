using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;

namespace BlindCatAvalonia.Core;

public class ReusableBitmap : WriteableBitmap, IDeferredDisposing
{
    public ReusableBitmap(PixelSize size, Vector dpi, PixelFormat? format = null, AlphaFormat? alphaFormat = null, string? debugName = null)
        : base(size, dpi, format, alphaFormat)
    {
        DebugName = debugName ?? Guid.NewGuid().ToString();
    }

    public string DebugName { get; set; }
    public bool IsRendering { get; set; }
    public bool IsReadyDispose { get; set; }
}