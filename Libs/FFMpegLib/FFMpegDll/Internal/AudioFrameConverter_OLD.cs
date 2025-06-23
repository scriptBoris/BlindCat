using FFmpeg.AutoGen.Abstractions;

namespace FFMpegDll.Internal;

public unsafe class AudioFrameConverter_OLD : IDisposable
{
    private void* _convertBuffer;
    private SwrContext* _pSwrContext;
    
    public AudioFrameConverter_OLD(
        int sampleRate,
        int samplesPerChannel,
        AVChannelLayout* channelLayout,
        AVSampleFormat originSampleFormat)
    {
        Channels = channelLayout->nb_channels;

        switch (originSampleFormat)
        {
            case AVSampleFormat.AV_SAMPLE_FMT_U8:
                OutputSampleBitsDepth = 8;
                OutputSampleByteDepth = 1;
                OutputSampleFormat = originSampleFormat;
                break;
            case AVSampleFormat.AV_SAMPLE_FMT_S16:
                OutputSampleBitsDepth = 16;
                OutputSampleByteDepth = 2;
                OutputSampleFormat = originSampleFormat;
                break;
            case AVSampleFormat.AV_SAMPLE_FMT_S32:
                OutputSampleBitsDepth = 32;
                OutputSampleByteDepth = 4;
                OutputSampleFormat = originSampleFormat;
                break;
            // use converter
            default:
                OutputSampleBitsDepth = 16;
                OutputSampleByteDepth = 2;
                OutputSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;
        
                SwrContext* swrContext = null;
                int alloc_swr_response = ffmpeg.swr_alloc_set_opts2(
                    &swrContext,
                    channelLayout,              // out
                    OutputSampleFormat,         // out
                    sampleRate,                 // out
                    channelLayout,              // input
                    originSampleFormat,         // input
                    sampleRate,                 // input
                    0, 
                    null
                );
                
                if (alloc_swr_response != 0)
                    throw new InvalidOperationException("Fail allocating swr converter audio context");
                
                int swr_init_response = ffmpeg.swr_init(swrContext);
                if (swr_init_response != 0)
                    throw new InvalidOperationException("Fail allocating swr converter audio context (init)");

                _pSwrContext = swrContext;
                int bufferSize = ffmpeg.av_samples_get_buffer_size(
                    null,
                    Channels,
                    samplesPerChannel,
                    OutputSampleFormat,
                    1
                );
                
                _convertBuffer = ffmpeg.av_malloc((ulong)bufferSize);
                break;
        }
        
        DataSize = ffmpeg.av_samples_get_buffer_size(
            null,
            Channels,
            samplesPerChannel,
            originSampleFormat,
            1
        );
        SamplesPerChannel = samplesPerChannel;
        SampleRate = sampleRate;
    }

    public int DataSize { get; } 
    
    /// <summary>
    /// Формат сэмплов
    /// </summary>
    public AVSampleFormat OutputSampleFormat { get; }
    
    /// <summary>
    /// Глубина в байтах
    /// </summary>
    public byte OutputSampleByteDepth { get; }
    
    /// <summary>
    /// Глубина в битах
    /// </summary>
    public byte OutputSampleBitsDepth { get; }
    public int Channels { get; }
    public int SamplesPerChannel { get; }
    public int SampleRate { get; }

    public nint ResolveSample(AVFrame* frame, out int length)
    {
        // try use converter
        if (_pSwrContext != null)
        {
            byte* pBuffer = (byte*)_convertBuffer;
            
            int convertedSamples = ffmpeg.swr_convert(
                _pSwrContext,
                &pBuffer,                // Буфер для выходных данных
                frame->nb_samples,       // Количество сэмплов для конвертации
                frame->extended_data,    // Входные данные
                frame->nb_samples        // Количество входных сэмплов
            );

            if (convertedSamples < 0)
                throw new Exception("Error of frame audio converting");
            
            int convertedBytes = convertedSamples * Channels * OutputSampleByteDepth;
            length = convertedBytes;
            return (nint)pBuffer;
        }
        // use ready-made data for playback
        else
        {
            length = DataSize;
            return (nint)frame->data[0];
        }
    }

    public void Dispose()
    {
        if (_pSwrContext != null)
        {
            var pSwrContext = _pSwrContext;
            ffmpeg.swr_free(&pSwrContext);
        }

        if (_convertBuffer != null)
        {
            ffmpeg.av_free(_convertBuffer);
        }
    }
}