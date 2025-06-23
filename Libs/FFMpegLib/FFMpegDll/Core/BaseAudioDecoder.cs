using FFmpeg.AutoGen.Abstractions;
using FFMpegDll.Internal;
using FFMpegDll.Models;
using System.Runtime.InteropServices;

namespace FFMpegDll.Core;

public abstract unsafe class BaseAudioDecoder
{
    private const int BUFFER_SIZE = 8192;

    protected readonly object _locker = new();
    protected int _streamAudioIndex;
    protected AVFormatContext* _pFormatContext;
    protected AVCodecContext* _pCodecContext;
    protected AVFrame* _pFrame;
    protected AVPacket* _pPacket;
    protected AudioFrameConverter? _converter;

    public TimeSpan Duration { get; protected set; }
    public bool IsEnoughData { get; protected set; }
    public bool HasAudioData { get; protected set; }
    public long PredictedSampleCount { get; protected set; }

    protected bool TryMap(AVFrame* frame = null)
    {
        if (IsEnoughData)
            return true;
        
        // stream
        var stream = _pFormatContext->streams[_streamAudioIndex];
        var param = stream->codecpar;
        double stream_duration = stream->duration * ffmpeg.av_q2d(stream->time_base);
        var stream_meta = new AudioMeta
        {
            Channels = param->ch_layout.nb_channels,
            SamplesPerChannel = _pCodecContext->frame_size,
            SampleRate = param->sample_rate,
            Duration = stream_duration,
            ChLayout = &param->ch_layout,
            OriginSampleFormat = (AVSampleFormat)param->format,
            FrameSize = param->frame_size,
        };

        // codec
        var codec_meta = new AudioMeta
        {
            Channels = _pCodecContext->ch_layout.nb_channels,
            SamplesPerChannel = _pCodecContext->frame_size,
            SampleRate = _pCodecContext->sample_rate,
            Duration = stream_duration,
            ChLayout = &_pCodecContext->ch_layout,
            OriginSampleFormat = _pCodecContext->sample_fmt,
            FrameSize = _pCodecContext->frame_size,
        };

        // frame
        AudioMeta frame_meta;
        if (frame != null)
        {
            frame_meta = new AudioMeta
            {
                Channels = frame->ch_layout.nb_channels,
                SamplesPerChannel = frame->nb_samples,
                SampleRate = frame->sample_rate,
                Duration = stream_duration,
                ChLayout = &frame->ch_layout,
                OriginSampleFormat = (AVSampleFormat)frame->format,
                FrameSize = _pCodecContext->frame_size,
            };
        }
        else
        {
            frame_meta = new AudioMeta
            {
                ChLayout = null,
                FrameSize = 0,
                Duration = 0,
                Channels = 0,
                SampleRate = 0,
                OriginSampleFormat = 0,
                SamplesPerChannel = 0,
            };
        }
        
        var mer = Merge(stream_meta, codec_meta, frame_meta);
        if (CheckEnoughtData(mer))
        {
            Map(mer);
            return true;
        }

        return false;
    }

    private bool CheckEnoughtData(AudioMeta meta)
    {
        if (meta.SampleRate <= 0)
            return false;

        if (meta.SamplesPerChannel <= 0)
            return false;

        if (meta.Channels <= 0)
            return false;

        return true;
    }

    private AudioMeta Merge(AudioMeta v1, AudioMeta v2, AudioMeta v3)
    {
        int channels = ResolveProp(v1.Channels, v2.Channels, v3.Channels);
        int samplesInChannel = ResolveProp(v1.SamplesPerChannel, v2.SamplesPerChannel, v3.SamplesPerChannel);
        
        int format_int = ResolveProp(
            (int)v1.OriginSampleFormat,
            (int)v2.OriginSampleFormat,
            (int)v3.OriginSampleFormat);
        var format = (AVSampleFormat)format_int;
        
        AVChannelLayout* layout;
        if (v1.Channels > 0)
            layout = v1.ChLayout;
        else if (v2.Channels > 0)
            layout = v2.ChLayout;
        else
            layout = v3.ChLayout;

        int frameSize = ResolveProp(v1.FrameSize, v2.FrameSize, v3.FrameSize);
        if (frameSize == 0)
        {
            byte deepth = format.GetDeepth();
            frameSize =  samplesInChannel * channels * deepth;
        }

        int sampleRate = ResolveProp(v1.SampleRate, v2.SampleRate, v3.SampleRate);
        if (sampleRate < 1000)
            sampleRate = 44100;

        var res = new AudioMeta
        {
            Duration = ResolveProp(v1.Duration, v2.Duration, v3.Duration),
            SamplesPerChannel = samplesInChannel,
            OriginSampleFormat = format,
            SampleRate = sampleRate,
            FrameSize = frameSize,
            Channels = channels,
            ChLayout = layout,
        };
        
        return res;
    }

    private void Map(AudioMeta meta)
    {
        Duration = TimeSpan.FromSeconds(meta.Duration);
        PredictedSampleCount = (long)(meta.Duration * meta.SampleRate);
        IsEnoughData = true;
        HasAudioData = true;

        _converter = new AudioFrameConverter(
            meta.SampleRate,
            meta.SamplesPerChannel,
            meta.OriginSampleFormat,
            AVSampleFormat.AV_SAMPLE_FMT_S16,
            meta.ChLayout
        );
    }

    public FrameAudioDecodeResult TryDecodeNextSample()
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
                            return new FrameAudioDecodeResult
                            {
                                Data = 0,
                                DataLength = 0,
                                IsSuccessed = false,
                                IsEndOfStream = true,
                            };
                        }

                        error.ThrowExceptionIfError();
                    } while (_pPacket->stream_index != _streamAudioIndex);

                    ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();

            if (_converter != null)
            {
                nint data = _converter.ResolveSample(_pFrame, out int dataLength);
                return new FrameAudioDecodeResult
                {
                    Data = data,
                    DataLength = dataLength,
                    IsSuccessed = true,
                    IsEndOfStream = false,
                };
            }
            else
            {
                return new FrameAudioDecodeResult
                {
                    Data = 0,
                    DataLength = 0,
                    IsSuccessed = false,
                    IsRequiredToCreateConverter = true,
                };
            }
        }
    }

    public Task<AudioMetadata?> LoadMetadataAsync(CancellationToken cancel, bool loadFrame)
    {
        lock (_locker)
        {
            AudioMetadata? meta;

            if (loadFrame)
            {
                var decodedFrame = TryDecodeNextSample();
                var ffmpeg_frame = *_pFrame;

                meta = FFmpegHelper.LoadAudioMetadata(ffmpeg_frame, _pFormatContext, _pCodecContext, _streamAudioIndex);
                TryMap(_pFrame);

                // Если требуется пересоздать конвертер, то пересоздаем
                if (meta.IsSuccess && decodedFrame.IsRequiredToCreateConverter)
                {
                    nint data = _converter.ResolveSample(&ffmpeg_frame, out int dataLength);

                    decodedFrame = new FrameAudioDecodeResult
                    {
                        Data = data,
                        DataLength = dataLength,
                        IsSuccessed = true,
                    };
                }

                if (decodedFrame.IsSuccessed)
                {
                    meta.FirstFrame = decodedFrame;
                }
            }
            else
            {
                meta = FFmpegHelper.LoadAudioMetadata(null, _pFormatContext, _pCodecContext, _streamAudioIndex);
                TryMap(_pFrame);
            }

            if (meta.IsSuccess)
            {
                IsEnoughData = true;
                PredictedSampleCount = (long)(Duration.TotalSeconds * meta.SampleRate);
            }

            return Task.FromResult<AudioMetadata?>(meta);
        }
    }

    private static int ResolveProp(int v1, int v2, int v3)
    {
        if (v1 > 0)
            return v1;
        
        if (v2 > 0)
            return v2;
        
        return v3;
    }
    
    private static double ResolveProp(double v1, double v2, double v3)
    {
        if (v1 > 0.0001)
            return v1;
        
        if (v2 > 0.0001)
            return v2;
        
        return v3;
    }
    
    private readonly struct AudioMeta
    {
        public required int Channels { get; init; }
        public required int SamplesPerChannel { get; init; }
        public required int SampleRate { get; init; }
        public required int FrameSize { get; init; }
        public required double Duration { get; init; }
        public required AVChannelLayout* ChLayout { get; init; }
        public required AVSampleFormat OriginSampleFormat { get; init; }
    }
}