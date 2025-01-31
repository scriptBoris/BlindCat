using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Internal;
using FFMpegDll.Models;

namespace FFMpegDll;

public sealed unsafe class VideoFileDecoder : IVideoDecoder, IDisposable
{
    private readonly AVCodecContext* _pCodecContext;
    private readonly AVFormatContext* _pFormatContext;
    private readonly AVFrame* _pFrame;
    private readonly AVPacket* _pPacket;
    private readonly AVFrame* _receivedFrame;
    private readonly int _streamIndex;
    private readonly VideoFrameConverter? _converter;
    private readonly object _locker = new();
    private bool _disposed;

    //public VideoFileDecoder(string filePath, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
    public VideoFileDecoder(string filePath, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, FileCencArgs? args = null)
    {
        _pFormatContext = ffmpeg.avformat_alloc_context();
        _receivedFrame = ffmpeg.av_frame_alloc();
        var pFormatContext = _pFormatContext;
        ffmpeg.avformat_open_input(&pFormatContext, filePath, null, null).ThrowExceptionIfError();
        ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();
        AVCodec* codec = null;
        _streamIndex = ffmpeg
            .av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0)
            .ThrowExceptionIfError();
        _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

        if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, HWDeviceType, null, null, 0)
                .ThrowExceptionIfError();
        }

        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamIndex]->codecpar)
            .ThrowExceptionIfError();
        ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();

        CodecName = ffmpeg.avcodec_get_name(codec->id);
        FrameSize = new Size(_pCodecContext->width, _pCodecContext->height);


        if (_pCodecContext->pix_fmt != AVPixelFormat.AV_PIX_FMT_RGBA)
        {
            var sourceSize = FrameSize;
            var sourcePixelFormat = HWDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
                ? PixelFormat
                : HWDeviceType.GetHWPixelFormat();
            var destinationSize = sourceSize;
            var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
            _converter = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);
        }

        //PixelFormat = _pCodecContext->pix_fmt;
        PixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
        _pPacket = ffmpeg.av_packet_alloc();
        _pFrame = ffmpeg.av_frame_alloc();
    }

    public string CodecName { get; }
    public Size FrameSize { get; }
    public AVPixelFormat PixelFormat { get; }

    public void SeekTo(TimeSpan time)
    {
        //double startTime = 10.0; // время в секундах
        double startTime = time.TotalSeconds;
        long timestamp = (long)(startTime * ffmpeg.AV_TIME_BASE);
        ffmpeg.av_seek_frame(_pFormatContext, -1, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD);
    }

    public FrameDecodeResult TryDecodeNextFrame()
    {
        lock (_locker)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

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
                            //frame = *_pFrame;
                            // frame = ResolveFrame(*_pFrame);
                            // endOfVideo = true;
                            return new FrameDecodeResult
                            {
                                FrameBitmapRGBA8888 = null,
                                IsSuccessed = false,
                                IsEndOfStream = true,
                            };
                        }

                        error.ThrowExceptionIfError();
                    } while (_pPacket->stream_index != _streamIndex);

                    ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            AVFrame* tframe;
            if (_pCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0).ThrowExceptionIfError();
                tframe = _receivedFrame;
            }
            else
            {
                tframe = _pFrame;
            }

            byte* frame_data = ResolveFrame(tframe);
            return new FrameDecodeResult
            {
                FrameBitmapRGBA8888 = frame_data,
                IsSuccessed = true,
                IsEndOfStream = false,
            };
        }
    }

    public async Task<VideoMetadata> LoadMetadataAsync(CancellationToken cancel)
    {
        var meta = new VideoMetadata();
        return meta;
    }

    private AVFrame ResolveFrame(AVFrame frame)
    {
        if (_converter != null)
        {
            return _converter.Convert(frame);
        }
        else
        {
            return frame;
        }
    }

    private byte* ResolveFrame(AVFrame* frame)
    {
        if (_converter != null)
        {
            return _converter.Convert(frame);
        }
        else
        {
            return frame->data[0];
        }
    }
    
    public IReadOnlyDictionary<string, string> GetContextInfo()
    {
        AVDictionaryEntry* tag = null;
        var result = new Dictionary<string, string>();

        while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
        {
            var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
            var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
            result.Add(key, value);
        }

        return result;
    }

    public void Dispose()
    {
        lock (_locker)
        {
            if (_disposed)
            {
                Debugger.Break();
                throw new ObjectDisposedException(GetType().FullName);
            }

            _disposed = true;
            var pFrame = _pFrame;
            ffmpeg.av_frame_free(&pFrame);

            var pPacket = _pPacket;
            ffmpeg.av_packet_free(&pPacket);

            var pCodecContext = _pCodecContext;
            var pFormatContext = _pFormatContext;

            ffmpeg.avcodec_free_context(&pCodecContext);
            ffmpeg.avformat_close_input(&pFormatContext);
        }
    }
}