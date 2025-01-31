namespace FFMpegDll.Models;

public class VideoMetadata
{
    public double AvgFramerate { get; set; }
    public string? SampleAspectRatio { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    /// <summary>
    /// Видео-стримы 
    /// </summary>
    public VideoStreamMetadata[] Streams { get; set; }

    /// <summary>
    /// Длительность в секундах
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Битрейт видео в секунду
    /// </summary>
    public int BitRate { get; set; }

    /// <summary>
    /// Краткое название кодека
    /// </summary>
    public string Codec { get; set; }

    /// <summary>
    /// Подробное название кодека
    /// </summary>
    public string CodecLongName { get; set; }

    /// <summary>
    /// Формат пикселей
    /// </summary>
    public string PixelFormat { get; set; }

    public VideoStreamMetadata GetFirstVideoStream()
    {
        return Streams.FirstOrDefault();
    }
}

public class VideoStreamMetadata
{
    public string DisplayAspectRatio { get; set; }
    public string ColorSpace { get; set; }
    public string ColorRange { get; set; }
    public string ColorPrimaries { get; set; }
    public string ColorTransfer { get; set; }
    public string Profile { get; set; }
    public string StartTime { get; set; }
}