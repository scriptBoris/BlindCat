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
    string DebugName { get; }
    bool IsLocked { get; set; }
    int Width { get; }
    int BytesPerPixel { get; }
    int Height { get; }
    nint Pointer { get; }
    PixelFormat PixelFormat { get; }
}