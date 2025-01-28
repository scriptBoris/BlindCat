using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll.Internal;

namespace FFMpegDll;

public sealed unsafe class VideoStreamDecoder : IVideoDecoder
{
    private const int BUFFER_SIZE = 8192;
    private readonly AVFormatContext* _pFormatContext;
    private readonly AVIOContext* _pAvioContext;
    private readonly AVCodecContext* _pCodecContext;
    private readonly AVFrame* _pFrame;
    private readonly AVFrame* _receivedFrame;
    private readonly AVPacket* _pPacket;
    private readonly VideoFrameConverter? _converter;
    private readonly object _locker = new();
    private readonly int _streamVideoIndex;
    private readonly AVRational _stream_time_base;
    
    private bool _disposed;
    private GCHandle _sourceStreamHandle;

    public VideoStreamDecoder(Stream source, AVHWDeviceType acceleration)
    {
        _sourceStreamHandle = GCHandle.Alloc(new StreamWrapper(source), GCHandleType.Pinned);

        var _sourceStreamPointer = (void*)_sourceStreamHandle.AddrOfPinnedObject();

        nint readCallbackPtr = Marshal.GetFunctionPointerForDelegate(ReadCallback);
        var _callbackRead = new avio_alloc_context_read_packet_func { Pointer = readCallbackPtr };

        nint seekCallbackPtr = Marshal.GetFunctionPointerForDelegate(SeekCallback);
        var _callbackSeek = new avio_alloc_context_seek_func { Pointer = seekCallbackPtr };

        // may be it is buffer will FREE as automatically when parent members was FREE?
        var _bufferPointer = (byte*)ffmpeg.av_malloc(BUFFER_SIZE);
        _pFormatContext = ffmpeg.avformat_alloc_context();
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
            throw new InvalidOperationException("Не удалось создать AVIOContext.");

        _pFormatContext->pb = _pAvioContext;

        // Открытие входного формата
        var pFormatContext2 = _pFormatContext;
        ffmpeg.avformat_open_input(&pFormatContext2, null, null, null).ThrowExceptionIfError();
        
        ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();

        // Finding best video stream
        AVCodec* codec = null;
        _streamVideoIndex = ffmpeg
            .av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0)
            .ThrowExceptionIfError();
        
        _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamVideoIndex]->codecpar)
            .ThrowExceptionIfError();

        if (acceleration != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, acceleration, null, null, 0)
                .ThrowExceptionIfError();
        }

        ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();

        CodecName = ffmpeg.avcodec_get_name(codec->id);
        FrameSize = new Size(_pCodecContext->width, _pCodecContext->height);

        if (_pCodecContext->pix_fmt != AVPixelFormat.AV_PIX_FMT_RGBA)
        {
            _converter = new VideoFrameConverter(
                new Size(_pCodecContext->width, _pCodecContext->height),
                _pCodecContext->pix_fmt,
                new Size(_pCodecContext->width, _pCodecContext->height),
                AVPixelFormat.AV_PIX_FMT_RGBA);
        }

        PixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;

        _pFrame = ffmpeg.av_frame_alloc();
        _receivedFrame = ffmpeg.av_frame_alloc();
        _pPacket = ffmpeg.av_packet_alloc();
        _stream_time_base = _pFormatContext->streams[_streamVideoIndex]->time_base;
    }

    public string CodecName { get; }
    public Size FrameSize { get; }
    public AVPixelFormat PixelFormat { get; }
    public TimeSpan FrameTime { get; private set; }

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

    public bool TryDecodeNextFrame(out AVFrame frame, out bool endOfVideo)
    {
        lock (_locker)
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_frame_unref(_receivedFrame);
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
                            frame = *_pFrame;
                            endOfVideo = true;
                            return false;
                        }
                    
                        error.ThrowExceptionIfError();
                    } 
                    while (_pPacket->stream_index != _streamVideoIndex);
                    
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

            if (_pCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0).ThrowExceptionIfError();
                frame = ResolveFrame(*_receivedFrame);
            }
            else
            {
                frame = ResolveFrame(*_pFrame);
            }

            endOfVideo = false;
            return true;
        }
    }

    private AVFrame ResolveFrame(AVFrame frame)
    {
        if (frame.pts != ffmpeg.AV_NOPTS_VALUE)
        {
            double timeInSeconds = frame.pts * ffmpeg.av_q2d(_stream_time_base);
            FrameTime = TimeSpan.FromSeconds(timeInSeconds);
        }

        if (_converter != null)
        {
            return _converter.Convert(frame);
        }
        else
        {
            return frame;
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

            var receivedFrame = _receivedFrame;
            ffmpeg.av_frame_free(&receivedFrame);

            var pPacket = _pPacket;
            ffmpeg.av_packet_free(&pPacket);

            var pCodecContext = _pCodecContext;
            ffmpeg.avcodec_free_context(&pCodecContext);

            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);

            if (_converter != null)
            {
                _converter.Dispose();
            }

            if (_sourceStreamHandle.IsAllocated)
                _sourceStreamHandle.Free();
        }
    }
}