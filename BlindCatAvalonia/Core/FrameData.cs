using Avalonia;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Core;

public class FrameData : ILockedFramebuffer
{
    private readonly GCHandle _handle;
    private bool isDisposed;

    public FrameData(byte[] frameRawData)
    {
        _handle = GCHandle.Alloc(frameRawData, GCHandleType.Pinned);
        Pointer = _handle.AddrOfPinnedObject();
    }

    public nint Pointer { get; private set; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public int BytesPerPixel { get; } = 4;
    public required int TotalBytes { get; init; }
    public PixelFormat PixelFormat { get; } = PixelFormats.Rgba8888;

    public nint Address => Pointer;
    public PixelSize Size => new PixelSize(Width, Height);
    public int RowBytes => Width * BytesPerPixel;
    public Vector Dpi => new Vector(96, 96);
    public PixelFormat Format => PixelFormat;

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        _handle.Free();
        GC.SuppressFinalize(this);
    }
}