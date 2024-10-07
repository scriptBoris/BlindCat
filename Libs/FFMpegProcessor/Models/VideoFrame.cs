using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FFMpegProcessor.Models;

public class VideoFrame : IDisposable
{
    public VideoFrame(int w, int h)
    {
        if (w <= 0 || h <= 0) throw new InvalidDataException("Video frame dimensions have to be bigger than 0 pixels!");

        Width = w;
        Height = h;

        int size = Width * Height * 3;
        RawData = new byte[size];
    }

    /// <summary>
    /// Raw video data in RGB24 pixel format
    /// </summary>
    public byte[] RawData { get; set; }

    /// <summary>
    /// Video width in pixels
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Video height in pixels
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// RawData length
    /// </summary>
    public int RawDataLength { get; private set; }

    /// <summary>
    /// Signaling end of video
    /// </summary>
    public bool IsLastFrame { get; set; }

    public void Dispose()
    {
        RawData = null!;
        GC.SuppressFinalize(this);
    }
}