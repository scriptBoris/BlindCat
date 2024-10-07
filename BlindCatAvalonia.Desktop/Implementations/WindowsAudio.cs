using BlindCatAvalonia.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Desktop.Implementations;

internal class WindowsAudio : IAudioService
{
    public IAudioPlay InitAudioOutput(Stream audioDataStream, int sampleRate, int bitDepth, int audioChannels)
    {
        var waveFormat = new WaveFormat(sampleRate, bitDepth, audioChannels);
        var waveProvider = new WaveSourceStream(audioDataStream, waveFormat);
        var mix = new MultiplexingWaveProvider([waveProvider], 2);
        var dev = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
        dev.Init(mix);

        return new NAudioOut
        {
            Device = dev,
        };
    }

    private class NAudioOut : IAudioPlay
    {
        public required IWavePlayer Device { get; set; }

        public void Pause()
        {
            Device.Pause();
        }

        public void Play()
        {
            Device.Play();
        }

        public void Stop()
        {
            Device.Stop();
        }

        public void Dispose()
        {
            Device.Dispose();
        }
    }

    public class WaveSourceStream : WaveStream
    {
        private readonly Stream _stream;
        private readonly WaveFormat _waveFormat;

        public WaveSourceStream(Stream stream, WaveFormat waveFormat)
        {
            _stream = stream;
            _waveFormat = waveFormat;
        }

        public override WaveFormat WaveFormat => _waveFormat;
        public override bool CanSeek => false;
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override long Length => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_stream.CanRead)
                return 0;

            int res = _stream.Read(buffer, offset, count);
            return res;
        }
    }
}