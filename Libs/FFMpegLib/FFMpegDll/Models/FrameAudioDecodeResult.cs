namespace FFMpegDll.Models;

public struct FrameAudioDecodeResult
{
    public nint Data { get; set; }
    public int DataLength { get; set; }
    public bool IsSuccessed { get; set; }
    public bool IsEndOfStream { get; set; }
    public bool IsRequiredToCreateConverter { get; set; }
}