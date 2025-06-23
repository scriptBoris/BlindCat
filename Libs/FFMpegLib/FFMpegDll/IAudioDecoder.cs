using FFMpegDll.Models;

namespace FFMpegDll;

public interface IAudioDecoder : IDisposable
{
    /// <summary>
    /// Частота дискретизации (количество сэмплов в секунду)
    /// </summary>
    int SampleRate { get; }
    
    /// <summary>
    /// Количество аудио каналов
    /// </summary>
    int Channels { get; }
    
    /// <summary>
    /// Глубина сэмплов (в битах)
    /// </summary>
    int OutputSampleBits { get; }
    
    /// <summary>
    /// Длительность аудио
    /// </summary>
    TimeSpan Duration { get; }
    
    /// <summary>
    /// Предполагаемое количество сэмплов в источнике
    /// </summary>
    long PredictedSampleCount { get; }
    
    /// <summary>
    /// Хватает ли мета данных для чтения аудио потока. Если нет, то вызовите LoadMetadataAsync 
    /// </summary>
    bool IsEnoughData { get; }

    /// <summary>
    /// Содержит ли источник звуковую дорожку (дорожки)
    /// </summary>
    bool HasAudioData { get; }

    void SeekTo(TimeSpan timePosition);
    FrameAudioDecodeResult TryDecodeNextSample();
    Task<AudioMetadata?> LoadMetadataAsync(CancellationToken cancel, bool readFrame = false);
}