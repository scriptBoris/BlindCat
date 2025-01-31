namespace FFMpegDll.Models;

public unsafe struct FrameDecodeResult
{
    public byte* FrameBitmapRGBA8888 { get; set; }
    public bool IsSuccessed { get; set; }
    public bool IsEndOfStream { get; set; }
}