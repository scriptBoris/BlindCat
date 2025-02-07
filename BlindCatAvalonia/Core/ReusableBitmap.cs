using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FFMpegDll.Core;
using Size = System.Drawing.Size;

namespace BlindCatAvalonia.Core;

public class ReusableBitmap : WriteableBitmap, IDeferredDisposing, IReusableBitmap
{
    public ReusableBitmap(PixelSize size, Vector dpi, PixelFormat? format = null, AlphaFormat? alphaFormat = null, string? debugName = null)
        : base(size, dpi, format, alphaFormat)
    {
        DebugName = debugName ?? Guid.NewGuid().ToString();
        FrameSize = new Size(size.Width, size.Height);
    }

    public string DebugName { get; set; }
    public bool IsRendering { get; set; }
    public bool IsReadyDispose { get; set; }
    public Size FrameSize { get; }
    
    public unsafe void Populate(nint bitmapSrc)
    {
        using (var vok = this.Lock())
        {
            void* src = (void*)bitmapSrc;
            void* dst = (void*)vok.Address;
            int size = PixelSize.Width * PixelSize.Height * 4;
            Buffer.MemoryCopy(src, dst, size, size);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        Console.WriteLine($"BITMAP {DebugName} was disposed");
    }
}