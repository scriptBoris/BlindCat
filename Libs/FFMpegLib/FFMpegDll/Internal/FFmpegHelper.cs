using FFmpeg.AutoGen.Abstractions;
using System.Drawing;
using System.Runtime.InteropServices;
using FFMpegDll.Models;

namespace FFMpegDll.Internal;

public static unsafe class FFmpegHelper
{
    public static string av_strerror(int error)
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

    /// <summary>
    /// Глубина звука в битах
    /// </summary>
    /// <param name="format"></param>
    public static byte GetDeepth(this AVSampleFormat format)
    {
        switch (format)
        {
            case AVSampleFormat.AV_SAMPLE_FMT_U8:     return 8;   // Unsigned 8-bit
            case AVSampleFormat.AV_SAMPLE_FMT_S16:    return 16;  // Signed 16-bit
            case AVSampleFormat.AV_SAMPLE_FMT_S32:    return 32;  // Signed 32-bit
            case AVSampleFormat.AV_SAMPLE_FMT_FLT:    return 32;  // Float 32-bit
            case AVSampleFormat.AV_SAMPLE_FMT_DBL:    return 64;  // Double 64-bit

            case AVSampleFormat.AV_SAMPLE_FMT_U8P:    return 8;   // Planar 8-bit
            case AVSampleFormat.AV_SAMPLE_FMT_S16P:   return 16;  // Planar 16-bit
            case AVSampleFormat.AV_SAMPLE_FMT_S32P:   return 32;  // Planar 32-bit
            case AVSampleFormat.AV_SAMPLE_FMT_FLTP:   return 32;  // Planar Float 32-bit
            case AVSampleFormat.AV_SAMPLE_FMT_DBLP:   return 64;  // Planar Double 64-bit

            case AVSampleFormat.AV_SAMPLE_FMT_S64:    return 64;  // Signed 64-bit
            case AVSampleFormat.AV_SAMPLE_FMT_S64P:   return 64;  // Planar 64-bit

            default: 
                return 0;
        }
    }
    
    public static VideoMetadata LoadVideoMetadata(
        AVFormatContext* _pFormatContext, 
        AVCodecContext* _pCodecContext,
        int _streamVideoIndex,
        Size FrameSize)
    {
        string pix_format = _pCodecContext->pix_fmt.ToString();
        var stream = _pFormatContext->streams[_streamVideoIndex];
        
        double durationSeconds = 0;
        long duration = stream->duration;
        if (duration > 0)
            durationSeconds = duration * ffmpeg.av_q2d(stream->time_base);

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
        AVFrame? frame,
        AVFormatContext* _pFormatContext, 
        AVCodecContext* _pCodecContext,
        int _streamAudioIndex)
    {
        int audioStreamCount = 0;
        for (int i = 0; i < _pFormatContext->nb_streams; i++)
        {
            if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                audioStreamCount++;
        }
        
        var streams = new AudioStreamMetadata[audioStreamCount];
        int streamIndex = 0;
        AVStream* stream = null;
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
                
                if (i == _streamAudioIndex)
                    stream = str;
                
                streamIndex++;
            }
        }

        int sampleRate;
        double durationSeconds;
        int channels;
        int samplesPerChannel;
        AVSampleFormat format;

        if (frame != null)
        {
            sampleRate = frame.Value.sample_rate;
            channels = frame.Value.ch_layout.nb_channels;
            format = (AVSampleFormat)frame.Value.format;
            durationSeconds = frame.Value.duration * ffmpeg.av_q2d(frame.Value.time_base);
            samplesPerChannel = frame.Value.nb_samples;
        }
        else if (stream != null)
        {
            sampleRate = stream->codecpar->sample_rate;
            channels = stream->codecpar->ch_layout.nb_channels;
            format = (AVSampleFormat)stream->codecpar->format;
            durationSeconds = stream->duration * ffmpeg.av_q2d(stream->time_base);
            samplesPerChannel = stream->codecpar->frame_size;
        }
        else
        {
            durationSeconds = 0;
            channels = 0;
            sampleRate = 0;
            format = AVSampleFormat.AV_SAMPLE_FMT_NONE;
            samplesPerChannel = 0;
        }

        var meta = new AudioMetadata
        {
            Duration = durationSeconds,
            SampleRate = sampleRate,
            PredictedSampleCount = (long)(durationSeconds * sampleRate),
            Streams = streams,
            Channels = channels,
            SampleFormat = format,
            SamplesPerChannel = samplesPerChannel,
        };
        
        const string NULL_ERR = "null_error";
        
        if (sampleRate < 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = ResolveMessageError(sampleRate) ?? NULL_ERR;
        }
        else if (sampleRate == 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = "SampleRate is zero";
        }
        else if (channels < 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = ResolveMessageError(channels) ?? NULL_ERR;
        }
        else if (channels == 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = "Channels count is zero";
        }
        else if (samplesPerChannel < 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = ResolveMessageError(samplesPerChannel) ?? NULL_ERR;
        }
        else if (samplesPerChannel == 0)
        {
            meta.IsSuccess = false;
            meta.ErrorMessage = "SamplesPerChannel count is zero";
        }
        else
        {
            meta.IsSuccess = true;
        }
        
        return meta;
    }

    private static string? ResolveMessageError(int error)
    {
        byte[] buffer = new byte[1024];
        fixed (byte* ptr = buffer)
        {
            ffmpeg.av_strerror(error, ptr, 1024);
            string? errorMessage = Marshal.PtrToStringAnsi((nint)ptr);
            return errorMessage;
        }
    }
}