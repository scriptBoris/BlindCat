using System.Runtime.InteropServices;

namespace FFMpegDll.Internal;

public readonly struct StreamWrapper
{
    private readonly GCHandle _handle;

    public StreamWrapper(Stream stream)
    {
        _handle = GCHandle.Alloc(stream, GCHandleType.Normal);
    }

    public Stream ResolveStream()
    {
        var streamW = (Stream)_handle.Target!;
        return streamW;
    }
}