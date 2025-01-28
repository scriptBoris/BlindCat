using System.Drawing;
using System.Runtime.InteropServices;
using System.Timers;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll.Internal;

namespace FFMpegDll;

public unsafe class AudioStreamDecoder : IAudioDecoder
{
    private const int BUFFER_SIZE = 8192;
    private readonly AVFormatContext* _pFormatContext;
    private readonly AVIOContext* _pAvioContext;
    private readonly AVCodecContext* _pCodecContext;
    private readonly AVFrame* _pFrame;
    private readonly AVPacket* _pPacket;
    private readonly SwrContext* _pSwrContext;
    private readonly object _locker = new();
    private readonly int _streamAudioIndex;
    private bool _disposed;

    private void* _convertBuffer;
    private GCHandle _sourceStreamHandle;

    public AudioStreamDecoder(Stream source)
    {
        _sourceStreamHandle = GCHandle.Alloc(new StreamWrapper(source), GCHandleType.Pinned);
        var _sourceStreamPointer = (void*)_sourceStreamHandle.AddrOfPinnedObject();

        nint readCallbackPtr = Marshal.GetFunctionPointerForDelegate(ReadCallback);
        var _callbackRead = new avio_alloc_context_read_packet_func { Pointer = readCallbackPtr };

        nint seekCallbackPtr = Marshal.GetFunctionPointerForDelegate(SeekCallback);
        var _callbackSeek = new avio_alloc_context_seek_func { Pointer = seekCallbackPtr };
        _pFormatContext = ffmpeg.avformat_alloc_context();
        
        // may be it is buffer will FREE as automatically when parent members was FREE?
        var _bufferPointer = (byte*)ffmpeg.av_malloc(BUFFER_SIZE);
        _pAvioContext = ffmpeg.avio_alloc_context(
            buffer: _bufferPointer,
            buffer_size: BUFFER_SIZE,
            write_flag: 0,
            opaque: _sourceStreamPointer,
            read_packet: _callbackRead,
            write_packet: null,
            seek: _callbackSeek
        );

        if (_pAvioContext == null)
            throw new InvalidOperationException("Fail to allocate AVIOContext.");

        _pFormatContext->pb = _pAvioContext;

        // Открытие входного формата
        var pFormatContext2 = _pFormatContext;
        ffmpeg.avformat_open_input(&pFormatContext2, null, null, null).ThrowExceptionIfError();
        ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();

        // Finding best audio stream
        AVCodec* acodec = null;
        _streamAudioIndex = ffmpeg
            .av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &acodec, 0);

        _pCodecContext = ffmpeg.avcodec_alloc_context3(acodec);
        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamAudioIndex]->codecpar)
            .ThrowExceptionIfError();

        ffmpeg.avcodec_open2(_pCodecContext, acodec, null).ThrowExceptionIfError();

        CodecName = ffmpeg.avcodec_get_name(acodec->id);

        _pFrame = ffmpeg.av_frame_alloc();
        _pPacket = ffmpeg.av_packet_alloc();

        var param = _pFormatContext->streams[_streamAudioIndex]->codecpar;
        OriginSampleFormat = (AVSampleFormat)param->format;
        Channels = param->ch_layout.nb_channels;
        SampleRate = param->sample_rate;
        DataSize = ffmpeg.av_samples_get_buffer_size(
            null,
            Channels,
            SamplesPerChannel,
            OriginSampleFormat,
            1
        );
        SamplesPerChannel = _pCodecContext->frame_size > 0 
            ? _pCodecContext->frame_size // Используем frame_size, если он определён
            : 4096;                      // Если frame_size неизвестен, выбираем безопасное значение с запасом
        
        switch (OriginSampleFormat)
        {
            case AVSampleFormat.AV_SAMPLE_FMT_U8:
                OutputSampleBits = 8;
                OutputSampleBytes = 1;
                OutputSampleFormat = OriginSampleFormat;
                break;
            case AVSampleFormat.AV_SAMPLE_FMT_S16:
                OutputSampleBits = 16;
                OutputSampleBytes = 2;
                OutputSampleFormat = OriginSampleFormat;
                break;
            case AVSampleFormat.AV_SAMPLE_FMT_S32:
                OutputSampleBits = 32;
                OutputSampleBytes = 4;
                OutputSampleFormat = OriginSampleFormat;
                break;
            // use converter
            default:
                OutputSampleBits = 16;
                OutputSampleBytes = 2;
                OutputSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;

                SwrContext* swrContext = null;
                int alloc_swr_response = ffmpeg.swr_alloc_set_opts2(
                    &swrContext,
                    &_pCodecContext->ch_layout, // out
                    OutputSampleFormat,         // out
                    SampleRate,                 // out
                    &_pCodecContext->ch_layout, // input
                    OriginSampleFormat,         // input
                    SampleRate,                 // input
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
                    SamplesPerChannel,
                    OutputSampleFormat,
                    1
                );
                
                _convertBuffer = ffmpeg.av_malloc((ulong)bufferSize);
                break;
        }
    }

    public int DataSize { get; private set; }
    public string CodecName { get; }
    public TimeSpan FrameTime { get; private set; }
    public int Channels { get; private set; }
    public AVSampleFormat OriginSampleFormat { get; private set; }
    public AVSampleFormat OutputSampleFormat { get; private set; }
    public int OutputSampleBits { get; private set; }
    public int OutputSampleBytes { get; private set; }
    public int SamplesPerChannel { get; private set; }
    public int SampleRate { get; private set; }

    public static int ReadCallback(void* opaque, byte* buffer, int bufferSize)
    {
        var stream_pointer = (StreamWrapper*)opaque;
        var streamWrapper = *stream_pointer;
        var stream = streamWrapper.ResolveStream();

        try
        {
            var spanBuffer = new Span<byte>(buffer, bufferSize);
            int bytesRead = stream.Read(spanBuffer);

            return bytesRead == 0 ? ffmpeg.AVERROR_EOF : bytesRead;
        }
        catch
        {
            return ffmpeg.AVERROR(5);
        }
    }

    public static long SeekCallback(void* @opaque, long @offset, int @whence)
    {
        var stream_pointer = (StreamWrapper*)opaque;
        var streamWrapper = *stream_pointer;
        var stream = streamWrapper.ResolveStream();
        switch (whence)
        {
            case 0: // Аналог SEEK_SET
                stream.Seek(offset, SeekOrigin.Begin);
                break;
            case 1: // Аналог SEEK_CUR
                stream.Seek(offset, SeekOrigin.Current);
                break;
            case 2: // Аналог SEEK_END
                stream.Seek(offset, SeekOrigin.End);
                break;
            case 0x10000: // AVSEEK_SIZE (не перемещение, а запрос размера)
                return stream.Length;
            default:
                return -1; // Возвращаем ошибку, если значение неизвестно
        }
        
        return stream.Position;
    }

    public void SeekTo(TimeSpan position)
    {
        lock (_locker)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            var timestamp = (long)(position.TotalSeconds * ffmpeg.AV_TIME_BASE);
            ffmpeg.av_seek_frame(_pFormatContext, -1, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD)
                .ThrowExceptionIfError();

            FrameTime = position;
        }
    }

    public bool TryDecodeNextSample(out Span<byte> frameSamples)
    {
        lock (_locker)
        {
            ffmpeg.av_frame_unref(_pFrame);
            int error;

            do
            {
                try
                {
                    do
                    {
                        ffmpeg.av_packet_unref(_pPacket);
                        error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);
                    
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frameSamples = Array.Empty<byte>();
                            return false;
                        }
                    
                        error.ThrowExceptionIfError();
                    } 
                    while (_pPacket->stream_index != _streamAudioIndex);
                    
                    ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } 
            while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            frameSamples = ResolveSample(_pFrame);

            return true;
        }
    }

    private Span<byte> ResolveSample(AVFrame* frame)
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
            
            int convertedBytes = convertedSamples * Channels * OutputSampleBytes;
            return new Span<byte>(pBuffer, convertedBytes);
        }
        // use ready-made data for playback
        else
        {
            var ptr = (IntPtr)frame->data[0];
            var bptr = (byte*)ptr;
            var span = new Span<byte>(bptr, DataSize);
            return span;
        }
    }

    public void Dispose()
    {
        lock (_locker)
        {
            if (_disposed)
                return;

            _disposed = true;
            
            var avioContext = _pAvioContext;
            ffmpeg.avio_context_free(&avioContext);

            var pFrame = _pFrame;
            ffmpeg.av_frame_free(&pFrame);

            var pPacket = _pPacket;
            ffmpeg.av_packet_free(&pPacket);

            var pCodecContext = _pCodecContext;
            ffmpeg.avcodec_free_context(&pCodecContext);

            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
            
            
            if (_pSwrContext != null)
            {
                var pSwrContext = _pSwrContext;
                ffmpeg.swr_free(&pSwrContext);
            }

            if (_convertBuffer != null)
            {
                ffmpeg.av_free(_convertBuffer);
            }

            if (_sourceStreamHandle.IsAllocated)
                _sourceStreamHandle.Free();
        }
    }
}