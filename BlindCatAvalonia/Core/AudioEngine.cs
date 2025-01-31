using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BlindCatAvalonia.Services;
using BlindCatCore.Models;
using BlindCatCore.Models.Media;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Models;

namespace BlindCatAvalonia.Core;

public class AudioEngine : IDisposable
{
    private readonly IAudioService _audioService;
    private readonly long _totalFrames;
    private readonly TimeSpan _pauseForFrameRate;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _startFrom;
    private readonly AudioMetadata _meta;
    private readonly IAudioDecoder _audioReader;
    private readonly ChainStream _stream;
    private Thread? _engineThread;
    private bool _isEngineRunning;
    private bool _isPlaying;
    private bool isDisposed;

    private IAudioPlay? audioOut;

    public event EventHandler<double>? PlayingProgressChanged;
    public event EventHandler? PlayingToEnd;

    public AudioEngine(object play,
        TimeSpan startFrom,
        AudioMetadata meta,
        IAudioService audio)
    {
        _meta = meta;
        _startFrom = startFrom;
        _audioService = audio;

        FFMpegDll.Init.InitializeFFMpeg();
        
        switch (play)
        {
            case string filePath:
                _audioReader = new AudioFileDecoder(filePath);
                break;
            case Stream stream:
                _audioReader = new AudioStreamDecoder(stream);
                break;
            case FileCENC fileCENC:
                throw new NotImplementedException();
                break;
            default:
                throw new NotImplementedException();
        };

        _duration = TimeSpan.FromSeconds(meta.Duration);
        _pauseForFrameRate = TimeSpan.FromSeconds(1 / meta.SampleRate);
        _totalFrames = meta.PredictedSampleCount;
        _stream = new ChainStream();
    }

    public Task Init(CancellationToken cancel)
    {
        int sampleRate = _audioReader.SampleRate;
        int channels = _audioReader.Channels;
        int bitDepth = _audioReader.OutputSampleBits;
        
        _audioReader.TryDecodeNextSample(out var sample);
        _stream.Push(sample);
        
        audioOut = _audioService.InitAudioOutput(_stream, sampleRate, bitDepth, channels);
        _engineThread = new Thread(Engine);
        return Task.CompletedTask;
    }

    private void Engine()
    {
        while (true)
        {
            if (isDisposed)
                return;
        
            if (!_isPlaying || !_stream.CanPush)
            {
                Thread.Sleep(2);
                continue;
            }
        
            bool success = _audioReader.TryDecodeNextSample(out var frameSamples);
            if (!success)
            {
                Thread.Sleep(2);   
                continue;
            }
        
            if (isDisposed)
                return;
        
            _stream.Push(frameSamples);
        }
    }

    public void Run()
    {
        _isPlaying = true;
        audioOut?.Play();

        if (!_isEngineRunning)
        {
            _isEngineRunning = true;
            _engineThread?.Start();
        }
    }

    public void Pause()
    {
        _isPlaying = false;
        audioOut?.Pause();
    }

    public void SeekTo(TimeSpan to)
    {
        _audioReader.SeekTo(to);
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

    private class ChainStream : Stream
    {
        private const int blockCount = 7;
        private readonly ConcurrentBag<Block> _freeBlocks = new();
        private readonly ConcurrentQueue<Block> _pipeLine = new();
        
        public ChainStream()
        {
            for (int i = 0; i < blockCount; i++)
            {
                var block = new Block();
                _freeBlocks.Add(block);
            }
        }

        public override bool CanRead { get; } = true;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = false;
        public override long Position { get; set; }
        public override long Length => throw new NotSupportedException();
        public bool CanPush => _freeBlocks.Count > 0;
        
        public void Push(Span<byte> data)
        {
            if (_freeBlocks.TryTake(out var freeBlock))
            {
                freeBlock.Set(data);
                _pipeLine.Enqueue(freeBlock);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            var currentBlock = Fetch();
            if (currentBlock == null)
                return 0;
            
            int reads = 0;
            int i = 0;

            while (reads < count)
            {
                if (currentBlock.TryRead(out byte b))
                {
                    buffer[i] = b;
                    reads++;
                }
                else
                {
                    _freeBlocks.Add(currentBlock);
                    _pipeLine.TryDequeue(out _);
                    currentBlock = Fetch();
                    if (currentBlock == null)
                        return reads;
                    
                    continue;
                }

                i++;
            }
            
            return reads;
        }

        private Block? Fetch()
        {
            if (_pipeLine.TryPeek(out var block))
                return block;
            
            return null;
        }
        
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private class Block
    {
        public const int blockSize = 16384;
        private readonly byte[] _data;

        public Block()
        {
            _data = new byte[blockSize];
        }
        
        public int Position { get; private set; }
        public int Length { get; private set; }

        public void Set(Span<byte> data)
        {
            int i = 0;
            foreach (var item in data)
            {
                _data[i] = item;
                i++;
            }
            Position = 0;
            Length = i;
        }

        public bool TryRead(out byte b)
        {
            if (Position < Length)
            {
                b = _data[Position];
                Position++;
                return true;
            }

            b = 0;
            return false;
        }
    }
}