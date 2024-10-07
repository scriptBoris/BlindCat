using CryMediaAPI.Audio;
using CryMediaAPI.Audio.Models;
using CSCore.SoundOut;
using CSCore;
using BlindCatMaui.Services;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using BlindCatCore.Services;

namespace BlindCatMaui.Core;

public class AudioEngine : IDisposable
{
    private readonly long _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _startFrom;
    private readonly AudioMetadata _meta;
    private readonly IDispatcher _dispatcher;
    private readonly RawAudioReader audioReader;
    private bool isDisposed;

    private ISoundOut? _soundOut;
    private IWaveSource? _waveSource;

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? PlayingToEnd;

    public AudioEngine(object play,
        TimeSpan startFrom,
        AudioMetadata meta,
        IFFMpegService ffmpeg,
        IDispatcher dispatcher)
    {
        _meta = meta;
        _startFrom = startFrom;
        _dispatcher = dispatcher;
        switch (play)
        {
            case string filePath:
                audioReader = new RawAudioReader(filePath, ffmpeg.PathToFFmpegExe);
                break;
            case Stream stream:
                audioReader = new RawAudioReader(stream, ffmpeg.PathToFFmpegExe);
                break;
            case DoubleStream doubleStream:
                audioReader = new RawAudioReader(doubleStream.Audio, ffmpeg.PathToFFmpegExe);
                break;
            default:
                throw new NotImplementedException();
        };

        _duration = TimeSpan.FromSeconds(meta.Duration);
        _pauseForFrameRate = TimeSpan.FromSeconds(1 / meta.SampleRate);
        _totalFrames = meta.PredictedSampleCount;
    }

    public void Init()
    {
        audioReader.Load(_startFrom);
        var waveFormat = new WaveFormat(44100, 16, 2);
        _waveSource = new WaveSourceStream(audioReader.Output, waveFormat);
        _soundOut = new WasapiOut();
        _soundOut.Initialize(_waveSource);
    }

    public void Run()
    {
        _soundOut!.Play();
    }

    public void Pause()
    {
        _soundOut!.Pause();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        audioReader.Dispose();
        _soundOut?.Stop();
        _soundOut?.Dispose();
        _soundOut = null;
    }

    public class WaveSourceStream : IWaveSource
    {
        private readonly Stream _stream;
        private readonly WaveFormat _waveFormat;

        public WaveSourceStream(Stream stream, WaveFormat waveFormat)
        {
            _stream = stream;
            _waveFormat = waveFormat;
        }

        public WaveFormat WaveFormat => _waveFormat;
        public bool CanSeek => false;
        public long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long Length => throw new NotImplementedException();

        public int Read(byte[] buffer, int offset, int count)
        {
            if (!_stream.CanRead)
                return 0;

            int res = _stream.Read(buffer, offset, count);
            return res;
        }

        public void Dispose()
        {
        }
    }
}