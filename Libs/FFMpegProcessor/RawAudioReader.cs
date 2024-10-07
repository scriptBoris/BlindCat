using FFMpegProcessor.Models;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FFMpegProcessor;

public class RawAudioReader : IDisposable
{
    private readonly Stream? _inputStream;
    private readonly string? _inputFile;
    private readonly FileCENC? _inputCenc;
    private Process? ffmpegProcess;
    private readonly string _ffmpeg;
    private CancellationTokenSource? _cancelation;
    private Stream? ffmpegIn;
    private Stream? ffmpegOut;
    private bool _isDisposed;

    public RawAudioReader(string fileInput, string ffmpegExecutable = "ffmpeg")
    {
        _inputFile = fileInput;
        _ffmpeg = ffmpegExecutable;
    }

    public RawAudioReader(Stream input, string ffmpegExecutable = "ffmpeg")
    {
        _inputStream = input;
        _ffmpeg = ffmpegExecutable;
    }

    public RawAudioReader(FileCENC input, string ffmpegExecutable = "ffmpeg")
    {
        _inputCenc = input;
        _ffmpeg = ffmpegExecutable;
    }

    public Stream? Output { get; private set; }
    public byte[] AlreadyFrame { get; private set; } = [];

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cancelation?.Cancel();

        if (ffmpegProcess != null)
        {
            ffmpegIn?.Dispose();
            ffmpegOut?.Dispose();
            ffmpegProcess.Close();
            ffmpegProcess.Dispose();
            ffmpegProcess = null;
        }
    }

    public async Task<Stream?> Load(TimeSpan offset = default, int sampleRate = 44100, int ac = 2, int bitDepth = 16, CancellationToken cancel = default)
    {
        const bool showOutput = false;
        string offsetString = "";
        if (offset.TotalMilliseconds > 0)
        {
            string val = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)offset.TotalHours,
                offset.Minutes,
                offset.Seconds);
            offsetString = "-ss " + val;
        }

        if (_inputStream != null)
        {
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out ffmpegIn, out ffmpegOut,
            [
                "-i pipe:0",
                $"{offsetString}",
                "-f s16le",
                "-c:a pcm_s16le",
                "-ar 44100",
                "-ac 2",
                "-"
            ]);

            RunStreamInputToProcess(ffmpegProcess, _inputStream);
        }
        else if (_inputFile != null)
        {
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out _, out ffmpegOut,
            [
                $"-i \"{_inputFile}\"",
                $"{offsetString}",
                $"-f s{bitDepth}le",
                "-c:a pcm_s16le",
                $"-ar {sampleRate}",
                $"-ac {ac}",
                "-"
            ]);
        }
        else if (_inputCenc != null)
        {
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out _, out ffmpegOut,
            [
                $"-decryption_key {_inputCenc.Key}",
                $"-i \"{_inputCenc.FilePath}\"",
                $"{offsetString}",
                $"-f s{bitDepth}le",
                "-c:a pcm_s16le",
                "-ar 44100",
                "-ac 2",
                "-"
            ]);
        }
        else
        {
            throw new InvalidDataException();
        }

        try
        {
            int frameSize = sampleRate * ac;
            // Ожидание первого кадра
            AlreadyFrame = new byte[frameSize];
            int totalReadBytes = 0;
            while (totalReadBytes < frameSize)
            {
                if (_isDisposed)
                    return null;

                int readBytes = await ffmpegOut.ReadAsync(AlreadyFrame, totalReadBytes, frameSize - totalReadBytes, cancel);
                if (readBytes <= 0)
                {
                    // todo бывает такое? видео с 1 кадром??? О_о
                    //if (totalReadBytes == 0)
                    //isLastFrame = true;

                    break;
                }

                totalReadBytes += readBytes;
            }
        }
        catch when (cancel.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        Output = ffmpegOut;
        return Output;
    }

    private void RunStreamInputToProcess(Process process, Stream dataInput)
    {
        _cancelation = new();
        try
        {
            Task.Run(() =>
            {
                dataInput.Position = 0;
                ffmpegIn = process.StandardInput.BaseStream;
                byte[] buffer = new byte[8192];
                int bytesRead;

                while (true)
                {
                    if (_isDisposed)
                        return;

                    bytesRead = dataInput.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        if (_isDisposed)
                            return;

                        ffmpegIn.Write(buffer, 0, bytesRead);
                    }
                    else
                    {
                        break;
                    }
                }
                ffmpegIn.Close();
            }, _cancelation.Token);
        }
        catch (System.IO.IOException)
        {
            // Проглатываем
        }
        catch (Exception)
        {
            // Проглатываем
        }
    }
}