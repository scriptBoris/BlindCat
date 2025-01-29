using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using BlindCatAvalonia.Services;
using SDL2;

namespace BlindCatAvalonia.Linux.Implementations;

internal class LinuxAudio : IAudioService
{
    public IAudioPlay InitAudioOutput(Stream audioDataStream, int sampleRate, int bitDepth, int audioChannels)
    {
        return new AudioPlayer(audioDataStream, sampleRate, bitDepth, audioChannels);
    }
    
    public unsafe class AudioPlayer : IDisposable, IAudioPlay
    {
        private bool disposed = false;
        private SDL.SDL_AudioSpec desired, obtained;
        private uint deviceId;
        private GCHandle gcHandle;
        private readonly Stream audioStream;
        private readonly int bitDepth;
        private readonly int bytesPerSample;
        private byte[] transferBuffer;

        public AudioPlayer(Stream dataInput, int sampleRate = 44100, int bitDepth = 16, int channels = 2)
        {
            if (dataInput == null)
                throw new ArgumentNullException(nameof(dataInput));
            
            if (!dataInput.CanRead)
                throw new ArgumentException("Stream must be readable", nameof(dataInput));

            this.audioStream = dataInput;
            this.bitDepth = bitDepth;
            this.bytesPerSample = (bitDepth / 8) * channels;

            if (SDL.SDL_Init(SDL.SDL_INIT_AUDIO) < 0)
            {
                throw new Exception($"SDL init failed: {SDL.SDL_GetError()}");
            }

            // Определяем формат аудио на основе битовой глубины
            ushort audioFormat;
            switch (bitDepth)
            {
                case 8:
                    audioFormat = SDL.AUDIO_U8;
                    break;
                case 16:
                    audioFormat = SDL.AUDIO_S16;
                    break;
                case 32:
                    audioFormat = SDL.AUDIO_S32;
                    break;
                default:
                    throw new ArgumentException($"Unsupported bit depth: {bitDepth}");
            }

            desired = new SDL.SDL_AudioSpec
            {
                freq = sampleRate,
                format = audioFormat,
                channels = (byte)channels,
                samples = 4096,
                callback = AudioCallback
            };

            gcHandle = GCHandle.Alloc(desired.callback);
            deviceId = INTERNAL_SDL_OpenAudioDevice(null, 0, ref desired, out obtained, 0);
            if (deviceId == 0)
            {
                throw new Exception($"Failed to open audio device: {SDL.SDL_GetError()}");
            }

            // Буфер для передачи данных
            transferBuffer = new byte[obtained.samples * bytesPerSample];

            // Начинаем воспроизведение
            SDL.SDL_PauseAudioDevice(deviceId, 0);
        }

        private void AudioCallback(IntPtr userdata, IntPtr stream, int len)
        {
            int bytesRead = audioStream.Read(transferBuffer, 0, len);
            
            if (bytesRead > 0)
            {
                // Если прочитали данные - копируем их в выходной поток
                Marshal.Copy(transferBuffer, 0, stream, bytesRead);
                
                // Если прочитали меньше чем требуется - заполняем остаток тишиной
                if (bytesRead < len)
                {
                    // Заполняем оставшееся пространство нулями (тишина)
                    for (int i = bytesRead; i < len; i++)
                    {
                        Marshal.WriteByte(stream + i, 0);
                    }
                }
            }
            else
            {
                // Если данных нет - заполняем буфер тишиной
                for (int i = 0; i < len; i++)
                {
                    Marshal.WriteByte(stream + i, 0);
                }
            }
        }

        public void Stop()
        {
            SDL.SDL_PauseAudioDevice(deviceId, 1);
        }
        
        public void Pause()
        {
            SDL.SDL_PauseAudioDevice(deviceId, 1);
        }

        public void Play()
        {
            SDL.SDL_PauseAudioDevice(deviceId, 0);
        }

        // Use it dllImport because SDL.SDL_OpenAudioDevice not supported to choose default output device
        [DllImport("SDL2", EntryPoint = "SDL_OpenAudioDevice", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe uint INTERNAL_SDL_OpenAudioDevice(
            byte* device,
            int iscapture,
            ref SDL.SDL_AudioSpec desired,
            out SDL.SDL_AudioSpec obtained,
            int allowed_changes);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Stop();
                    SDL.SDL_CloseAudioDevice(deviceId);
                    SDL.SDL_Quit();
                    audioStream?.Dispose();
                }

                if (gcHandle.IsAllocated)
                    gcHandle.Free();

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AudioPlayer()
        {
            Dispose(false);
        }
    }
}