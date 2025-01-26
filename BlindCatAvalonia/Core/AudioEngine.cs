using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlindCatAvalonia.Services;
using FFMpegProcessor;
using FFMpegProcessor.Models;

namespace BlindCatAvalonia.Core;

public class AudioEngine : IDisposable
{
    private readonly IAudioService _audioService;
    private readonly long _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _startFrom;
    private readonly AudioMetadata _meta;
    private readonly RawAudioReader _audioReader;
    private bool isDisposed;

    private IAudioPlay? audioOut;

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? PlayingToEnd;

    public AudioEngine(object play,
        TimeSpan startFrom,
        AudioMetadata meta,
        IAudioService audio,
        string pathToFFmpegExe)
    {
        _meta = meta;
        _startFrom = startFrom;
        _audioService = audio;

        switch (play)
        {
            case string filePath:
                _audioReader = new RawAudioReader(filePath, pathToFFmpegExe);
                break;
            case Stream stream:
                _audioReader = new RawAudioReader(stream, pathToFFmpegExe);
                break;
            case FileCENC fileCENC:
                _audioReader = new RawAudioReader(fileCENC, pathToFFmpegExe);
                break;
            default:
                throw new NotImplementedException();
        };

        _duration = TimeSpan.FromSeconds(meta.Duration);
        _pauseForFrameRate = TimeSpan.FromSeconds(1 / meta.SampleRate);
        _totalFrames = meta.PredictedSampleCount;
    }

    public async Task Init(CancellationToken cancel)
    {
        int sampleRate = _meta.SampleRate;
        int channels = _meta.Channels;
        var audioDataStream = await _audioReader.Load(_startFrom, sampleRate, channels, cancel:cancel);
        if (audioDataStream != null)
        {
            audioOut = _audioService.InitAudioOutput(audioDataStream, sampleRate, 16, channels);
        }
    }

    public void Run()
    {
        audioOut?.Play();
    }

    public void Pause()
    {
        audioOut?.Pause();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        _audioReader.Dispose();
        audioOut?.Stop();
        audioOut?.Dispose();
        audioOut = null;
        GC.SuppressFinalize(this);
    }
}