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
    bool IsDisposed { get; }
    
    /// <summary>
    /// Only for debug
    /// </summary>
    string DebugName { get; }

    /// <summary>
    /// Is this frame rendered?
    /// </summary>
    bool IsLocked { get; set; }

    DateTime DecodedAt { get; }

    int Width { get; }
    sbyte BytesPerPixel { get; }
    int Height { get; }
    nint Pointer { get; }
    PixelFormat PixelFormat { get; }

    void CopyTo(nint destination);
}