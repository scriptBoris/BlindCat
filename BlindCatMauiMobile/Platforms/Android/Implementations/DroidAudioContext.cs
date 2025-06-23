using Android.Media;
using FFMpegDll.Core;

namespace BlindCatMauiMobile.Implementations;

public class DroidAudioContext : IAudioContext
{
    public IAudioOutput? InitAudioOutput(System.IO.Stream stream, int sampleRate, int bitDepth, int channels)
    {
        // Настраиваем параметры AudioTrack
        var channelConfig = channels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;

        var audioFormat = bitDepth == 16 ? Encoding.Pcm16bit : Encoding.Pcm8bit;

        // Вычисляем минимальный размер буфера
        int minBufferSize = AudioTrack.GetMinBufferSize(
            sampleRate,
            channelConfig,
            audioFormat);

        if (minBufferSize < 0)
            return null; // Неподдерживаемая конфигурация

        try
        {
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)
                .SetContentType(AudioContentType.Music)
                .Build();

            var audioFormatt = new AudioFormat.Builder()
                .SetSampleRate(sampleRate)
                .SetChannelMask(channelConfig)
                .SetEncoding(audioFormat)
                .Build();

            var audioTrack = new AudioTrack.Builder()
                .SetAudioAttributes(audioAttributes)
                .SetAudioFormat(audioFormatt)
                .SetBufferSizeInBytes(minBufferSize)
                .SetTransferMode(AudioTrackMode.Stream)
                .Build();
            
            // var audioTrack = new AudioTrack(
            //     // Используем поток MUSIC для воспроизведения
            //     Stream.Music,
            //     sampleRate,
            //     channelConfig,
            //     audioFormat,
            //     minBufferSize,
            //     AudioTrackMode.Stream);

            return new AndroidAudioOutput(audioTrack, stream, minBufferSize);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public class AndroidAudioOutput : IAudioOutput
    {
        private readonly AudioTrack _audioTrack;
        private readonly System.IO.Stream _stream;
        private readonly byte[] _buffer;
        private readonly Task _playbackTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isPlaying;

        public AndroidAudioOutput(AudioTrack audioTrack, System.IO.Stream stream, int bufferSize)
        {
            _audioTrack = audioTrack;
            _stream = stream;
            _buffer = new byte[bufferSize];
            _cancellationTokenSource = new CancellationTokenSource();
            _playbackTask = Task.Run(PlaybackLoop, _cancellationTokenSource.Token);
        }

        private async Task PlaybackLoop()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (!_isPlaying)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                        continue;
                    }

                    int bytesRead = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                    if (bytesRead == 0)
                    {
                        // Достигнут конец потока
                        Stop();
                        continue;
                    }

                    _audioTrack.Write(_buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение при отмене
            }
        }

        public void Play()
        {
            if (!_isPlaying)
            {
                _isPlaying = true;
                _audioTrack.Play();
            }
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _audioTrack.Pause();
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            _audioTrack.Stop();
            _stream.Position = 0; // Перематываем на начало
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _playbackTask.Wait(); // Ждем завершения потока воспроизведения
            _audioTrack.Stop();
            _audioTrack.Release();
            _audioTrack.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}