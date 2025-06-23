using FFmpeg.AutoGen.Abstractions;

namespace FFMpegDll.Models;

public class AudioMetadata
{
    public bool IsSuccess { get; set; }

    public string ErrorMessage { get; set; } = "OK";

    /// <summary>
    /// Длительность в секундах
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Количество сэмплов за одну секунду
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Предполагаемое количество сэмплов вообще
    /// </summary>
    public long PredictedSampleCount { get; set; }

    /// <summary>
    /// Количество звуковых каналов
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Формат сэмплов
    /// </summary>
    public AVSampleFormat SampleFormat { get; set; }

    /// <summary>
    /// Аудио-стримы
    /// </summary>
    public AudioStreamMetadata[] Streams { get; set; }

    /// <summary>
    /// Прочитанный первый фрейм
    /// </summary>
    public FrameAudioDecodeResult? FirstFrame { get; set; }

    /// <summary>
    /// Количество сеэплов на один канал
    /// </summary>
    public int SamplesPerChannel { get; set; }
}

public class AudioStreamMetadata
{
    public int BitRate { get; set; }
    public int SampleRate { get; set; }
    public string CodecName { get; set; }
    public string CodecLongName { get; set; }
    public string ChannelLayout { get; set; }
    public int Channels { get; set; }
    public object Tags { get; set; }
    public string? Language { get; set; }
}