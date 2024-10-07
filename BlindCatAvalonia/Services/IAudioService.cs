using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Services;

public interface IAudioService
{
    IAudioPlay InitAudioOutput(Stream audioData, int sampleRate, int bitDepth, int audioChannels);
}

public interface IAudioPlay : IDisposable
{
    void Play();
    void Pause();
    void Stop();
}