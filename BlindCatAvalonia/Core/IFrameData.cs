using Avalonia;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Core;

public interface IFrameData : IDisposable
{
    /// <summary>
    /// bitmap who is used it pixmap
    /// </summary>
    object? Parent { get; set; }
    
    bool IsDisposed { get; }
    
    /// <summary>
    /// Only for debug
    /// </summary>
    string DebugName { get; }

    /// <summary>
    /// Is this frame rendered?
    /// </summary>
    bool IsLocked { get; set; }

    /// <summary>
    /// Is this frame inside a bitmap?
    /// </summary>
    bool IsUsedInBitmap { get; set; }

    DateTime DecodedAt { get; }

    int Width { get; }
    int BytesPerPixel { get; }
    int Height { get; }
    nint Pointer { get; }
    PixelFormat PixelFormat { get; }
}