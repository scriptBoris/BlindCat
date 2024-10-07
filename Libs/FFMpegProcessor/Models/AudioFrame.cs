using System;
using System.IO;

namespace FFMpegProcessor.Models;

/// <summary>
/// Audio frame containing multiple audio samples in signed PCM format with given bit depth.
/// </summary>
public class AudioFrame : IDisposable
{
    /// <summary>
    /// Creates an empty audio frame with fixed sample count and given bit depth using signed PCM format.
    /// </summary>
    /// <param name="channels">Number of channels</param>
    /// <param name="sampleCount">Number of samples to store within this frame</param>
    /// <param name="bitDepth">Bits per sample (16, 24 or 32)</param>
    public AudioFrame(int channels, int sampleCount = 1024, int bitDepth = 16)
    {
        if (bitDepth != 16 && bitDepth != 24 && bitDepth != 32) throw new InvalidOperationException("Acceptable bit depths are 16, 24 and 32");
        if (channels <= 0) throw new InvalidDataException("Channel count has to be bigger than 0!");
        if (sampleCount <= 0) throw new InvalidDataException("Sample count has to be bigger than 0!");

        Channels = channels;
        SampleCount = sampleCount;
        BytesPerSample = bitDepth / 8;
        int size = sampleCount * channels * BytesPerSample;

        RawData = new byte[size];
    }

    /// <summary>
    /// Number of channels
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Number of audio samples this frame can contain
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    /// Bit depth (Bytes per sample)
    /// </summary>
    public int BytesPerSample { get; }

    /// <summary>
    /// Number of loaded audio samples when calling Load()
    /// </summary>
    public int LoadedSamples { get; private set; }

    /// <summary>
    /// Raw audio data in signed PCM format
    /// </summary>
    public byte[] RawData { get; private set; }

    /// <summary>
    /// RawData length
    /// </summary>
    public int RawDataLength { get; private set; }

    /// <summary>
    /// Clears the frame buffer
    /// </summary>
    public void Dispose()
    {
        RawData = null!;
        GC.SuppressFinalize(this);
    }
}