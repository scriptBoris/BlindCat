using System.Collections.Concurrent;
using FFMpegDll.Core;
using FFMpegDll.Models;

namespace FFMpegDll;

public class AudioEngine : IDisposable
{
    private readonly long _totalFrames;
    private readonly IAudioDecoder _audioDecoder;
    private readonly ChainStream _stream;
    private readonly IAudioContext _audioService;
    private Thread? _engineThread;
    private bool _isEngineRunning;
    private bool _isPlaying;
    private bool _isDisposed;
    private IAudioOutput? _audioOut;

    public event EventHandler? PlayingToEnd;

    public AudioEngine(object play, IAudioContext audio)
    {
        _audioService = audio;

        FFMpegDll.Init.InitializeFFMpeg();
        
        switch (play)
        {
            case string filePath:
                _audioDecoder = new AudioFileDecoder(filePath);
                break;
            case Stream stream:
                _audioDecoder = new AudioStreamDecoder(stream);
                break;
            // case FileCENC fileCENC:
            //     throw new NotImplementedException();
            //     break;
            default:
                throw new NotImplementedException();
        }

        _totalFrames = _audioDecoder.PredictedSampleCount;
        _stream = new ChainStream();
    }

    public TimeSpan Duration => _audioDecoder.Duration;
    public bool HasAudioData => _audioDecoder.HasAudioData;
    public bool IsEnoughData => _audioDecoder.IsEnoughData;

    public async Task<FFMpegResult> Init(CancellationToken cancel)
    {
        FrameAudioDecodeResult frame;
        
        if (IsEnoughData)
        {
            frame = _audioDecoder.TryDecodeNextSample();
        }
        else
        {
            var meta = await _audioDecoder.LoadMetadataAsync(cancel, true);
            if (meta == null || cancel.IsCancellationRequested)
                return FFMpegResult.Cancelled;

            if (!meta.IsSuccess)
                return FFMpegResult.Error(10, meta.ErrorMessage);

            if (meta.FirstFrame == null)
                return FFMpegResult.Error(11, "No frame");
            
            frame = meta.FirstFrame.Value;
        }
        
        int sampleRate = _audioDecoder.SampleRate;
        int channels = _audioDecoder.Channels;
        int bitDepth = _audioDecoder.OutputSampleBits;
        _stream.Push(frame.Data, frame.DataLength);
        _audioOut = _audioService.InitAudioOutput(_stream, sampleRate, bitDepth, channels);
        _engineThread = new Thread(Engine);
        
        return FFMpegResult.Success;
    }

    private void Engine()
    {
        while (!_isDisposed)
        {
            if (!_isPlaying || !_stream.CanPush)
            {
                Thread.Sleep(2);
                continue;
            }
        
            var frame = _audioDecoder.TryDecodeNextSample();
            if (!frame.IsSuccessed)
            {
                Thread.Sleep(2);   
                continue;
            }
        
            _stream.Push(frame.Data, frame.DataLength);
        }
    }

    public void Run()
    {
        _isPlaying = true;
        _audioOut?.Play();

        if (!_isEngineRunning)
        {
            _isEngineRunning = true;
            _engineThread?.Start();
        }
    }

    public void Pause()
    {
        _isPlaying = false;
        _audioOut?.Pause();
    }

    public void SeekTo(TimeSpan to)
    {
        _audioDecoder.SeekTo(to);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _audioDecoder.Dispose();
        _audioOut?.Stop();
        _audioOut?.Dispose();
        _audioOut = null;
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
        
        public void Push(Span<byte> span)
        {
            if (_freeBlocks.TryTake(out var freeBlock))
            {
                freeBlock.Set(span);
                _pipeLine.Enqueue(freeBlock);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        
        public unsafe void Push(nint data, int dataLength)
        {
            var span = new Span<byte>((void*)data, dataLength);
            if (_freeBlocks.TryTake(out var freeBlock))
            {
                freeBlock.Set(span);
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