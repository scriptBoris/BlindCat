using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll.Core;
using FFMpegDll.Internal;
using FFMpegDll.Models;

namespace FFMpegDll;

public unsafe class AudioFileDecoder : BaseAudioDecoder, IAudioDecoder
{
    // private const int BUFFER_SIZE = 8192;
    // private readonly AVFormatContext* _pFormatContext;
    // private readonly AVCodecContext* _pCodecContext;
    // private readonly AVFrame* _pFrame;
    // private readonly AVPacket* _pPacket;
    // private readonly int _streamAudioIndex;

    // private AudioFrameConverter? _converter;
    private bool _disposed;

    public AudioFileDecoder(string filePath, FileCencArgs? args = null)
    {
        _pFormatContext = ffmpeg.avformat_alloc_context();
        var pFormatContext = _pFormatContext;
        ffmpeg.avformat_open_input(&pFormatContext, filePath, null, null).ThrowExceptionIfError();
        ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();
        AVCodec* codec = null;
        _streamAudioIndex = ffmpeg
            .av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &codec, 0);

        if (_streamAudioIndex < 0)
        {
            CodecName = "";
            HasAudioData = false;
            return;
        }

        _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamAudioIndex]->codecpar)
            .ThrowExceptionIfError();
        ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();

        CodecName = ffmpeg.avcodec_get_name(codec->id);

        _pPacket = ffmpeg.av_packet_alloc();
        _pFrame = ffmpeg.av_frame_alloc();
        HasAudioData = true;
        
        TryMap();
    }

    public string CodecName { get; }
    public int DataSize => _converter?.DataSize ?? 0;
    public TimeSpan FrameTime { get; private set; }
    public int Channels => _converter?.Channels ?? 0;
    public AVSampleFormat OriginSampleFormat { get; private set; }
    public AVSampleFormat OutputSampleFormat => _converter?.OutputSampleFormat ?? AVSampleFormat.AV_SAMPLE_FMT_NONE;
    public int OutputSampleBits => _converter?.OutputSampleBitsDepth ?? 0;
    public int SamplesPerChannel => _converter?.SrcSamplesPerChannel ?? 0;
    public int SampleRate => _converter?.SrcSampleRate ?? 0;

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

    public void Dispose()
    {
        lock (_locker)
        {
            if (_disposed)
                return;

            _disposed = true;

            var pFrame = _pFrame;
            ffmpeg.av_frame_free(&pFrame);

            var pPacket = _pPacket;
            ffmpeg.av_packet_free(&pPacket);

            var pCodecContext = _pCodecContext;
            ffmpeg.avcodec_free_context(&pCodecContext);

            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);

            if (_converter != null)
                _converter.Dispose();
        }
    }
}