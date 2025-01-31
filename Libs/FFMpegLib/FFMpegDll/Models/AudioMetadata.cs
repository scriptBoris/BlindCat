namespace FFMpegDll.Models;

public class AudioMetadata
{
    /// <summary>
    /// Длительность в секундах
    /// </summary>
    public double Duration { get; set; }
    public int SampleRate { get; set; }
    public long PredictedSampleCount { get; set; }
    
    /// <summary>
    /// Аудио-стримы
    /// </summary>
    public AudioStreamMetadata[] Streams { get; set; }
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