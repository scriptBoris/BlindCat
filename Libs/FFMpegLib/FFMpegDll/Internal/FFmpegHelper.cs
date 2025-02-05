using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FFMpegDll.Models;

namespace FFMpegDll.Internal;

public static unsafe class FFmpegHelper
{
    public static unsafe string av_strerror(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
        return message;
    }

    public static int ThrowExceptionIfError(this int error)
    {
        if (error < 0) throw new ApplicationException(av_strerror(error));
        return error;
    }

    public static AVPixelFormat GetHWPixelFormat(this AVHWDeviceType hWDevice)
    {
        return hWDevice switch
        {
            AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
            AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
            AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
            _ => AVPixelFormat.AV_PIX_FMT_NONE
        };
    }
    
    public static VideoMetadata LoadVideoMetadata(
        AVFormatContext* _pFormatContext, 
        AVCodecContext* _pCodecContext,
        int _streamVideoIndex,
        Size FrameSize)
    {
        double durationSeconds = 0;
        long duration = _pFormatContext->duration;
        if (duration > 0)
            durationSeconds = duration / (double)ffmpeg.AV_TIME_BASE;
        
        string pix_format = _pCodecContext->pix_fmt.ToString();
        var stream = _pFormatContext->streams[_streamVideoIndex];

        var avgf = stream->avg_frame_rate;
        double avg_fps = -1;
        if (avgf.num > 0 && avgf.den > 0)
            avg_fps = avgf.num / avgf.den;

        string codecShort = ffmpeg.avcodec_get_name(stream->codecpar->codec_id);
        var codecLongPtr = ffmpeg.avcodec_descriptor_get(stream->codecpar->codec_id)->long_name;
        string? codecLong = Marshal.PtrToStringUTF8((nint)codecLongPtr);

        var aspect = stream->sample_aspect_ratio;
        string? aspectStr = null;
        if (aspect.num > 0 && aspect.den > 0)
            aspectStr = $"{aspect.num}:{aspect.den}";

        int videoStreamCount = 0;

        for (int i = 0; i < _pFormatContext->nb_streams; i++)
        {
            if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                videoStreamCount++;
        }
        
        var streams = new VideoStreamMetadata[videoStreamCount];
        int streamIndex = 0;
        for (int i = 0; i < _pFormatContext->nb_streams; i++)
        {
            if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                var str = _pFormatContext->streams[i];
                streams[streamIndex] = new VideoStreamMetadata
                {
                    // todo Сделать мапинг данных для видео стрима
                    // StartTime = str->start_time,
                };
                streamIndex++;
            }
        }
        
        var meta = new VideoMetadata
        {
            Codec = codecShort,
            CodecLongName = codecLong ?? "N/A",
            Height =  FrameSize.Height,
            Width = FrameSize.Width,
            Duration = durationSeconds,
            PixelFormat = pix_format,
            AvgFramerate = avg_fps,
            BitRate = (int)_pFormatContext->bit_rate,
            SampleAspectRatio = aspectStr,
            Streams = streams,
        };
        return meta;
    }
    
    public static AudioMetadata LoadAudioMetadata(
        AVFormatContext* _pFormatContext, 
        AVCodecContext* _pCodecContext,
        int _streamAudioIndex)
    {
        double durationSeconds = 0;
        long duration = _pFormatContext->duration;
        if (duration > 0)
            durationSeconds = duration / (double)ffmpeg.AV_TIME_BASE;
        
        int audioStreamCount = 0;
        for (int i = 0; i < _pFormatContext->nb_streams; i++)
        {
            if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                audioStreamCount++;
        }
        
        var streams = new AudioStreamMetadata[audioStreamCount];
        int streamIndex = 0;
        for (int i = 0; i < _pFormatContext->nb_streams; i++)
        {
            if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                var str = _pFormatContext->streams[i];
                
                string codecShort = ffmpeg.avcodec_get_name(str->codecpar->codec_id);
                var codecLongPtr = ffmpeg.avcodec_descriptor_get(str->codecpar->codec_id)->long_name;
                string? codecLong = Marshal.PtrToStringUTF8((nint)codecLongPtr);
                
                streams[streamIndex] = new AudioStreamMetadata
                {
                    CodecName = codecShort,
                    CodecLongName = codecLong ?? "N/A",
                    SampleRate = str->codecpar->sample_rate,
                    Channels = str->codecpar->ch_layout.nb_channels,
                    ChannelLayout = "N/A",
                    BitRate = (int)str->codecpar->bit_rate,
                };
                streamIndex++;
            }
        }

        int sampleRate = 0;
        if (_streamAudioIndex >= 0)
        {
            var param = _pFormatContext->streams[_streamAudioIndex]->codecpar;
            sampleRate = param->sample_rate;
        }
        
        var meta = new AudioMetadata
        {
            Duration = durationSeconds,
            SampleRate = sampleRate,
            PredictedSampleCount = (long)(durationSeconds * sampleRate),
            Streams = streams,
        };
        return meta;
    }
}