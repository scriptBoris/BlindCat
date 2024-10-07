using FFMpegProcessor.Models;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FFMpegProcessor;

public class RawVideoReader : IDisposable
{
    private readonly Stream? _inputStream;
    private readonly string? _inputFile;
    private readonly FileCENC? _inputCENC;
    private Process? ffmpegProcess;
    private readonly string _ffmpeg;
    private bool openedForReading;
    private bool isDisposed;
    private Stream? ffmpegIn;
    private Stream? ffmpegOut;
    private CancellationTokenSource? cancelation;

    private int _bufferSize;

    public RawVideoReader(string fileInput, string ffmpegExecutable = "ffmpeg")
    {
        _inputFile = fileInput;
        _ffmpeg = ffmpegExecutable;
    }

    public RawVideoReader(Stream input, string ffmpegExecutable = "ffmpeg")
    {
        _inputStream = input;
        _ffmpeg = ffmpegExecutable;
    }

    public RawVideoReader(FileCENC input, string ffmpegExecutable = "ffmpeg")
    {
        _inputCENC = input;
        _ffmpeg = ffmpegExecutable;
    }

    public int FrameSize { get; private set; }
    public Stream? Output { get; private set; }
    public byte[]? AlreadyFrame { get; private set; }

    /// <summary>
    /// Load the video for reading frames and seeks to given offset in seconds.
    /// </summary>
    /// <param name="offsetSeconds">Offset in seconds to which to seek to</param>
    public async Task Load(double offsetSeconds, int width, int height, CancellationToken cancel)
    {
        if (openedForReading) 
            throw new InvalidOperationException("Video is already loaded!");

        FrameSize = width * height * 4;
        string offsetString = "";
        if (offsetSeconds > 0)
        {
            var timeSpan = TimeSpan.FromSeconds(offsetSeconds);
            string val = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)timeSpan.TotalHours,
                timeSpan.Minutes,
                timeSpan.Seconds);
            offsetString = "-ss " + val;
        }

        if (_inputStream != null)
        {
            // we will be reading video in RGB24 format
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out ffmpegIn, out ffmpegOut,
            [
                //"-flags low_delay",
                //"-analyzeduration 5000000000",
                //"-probesize 32",
                "-i pipe:0",
                offsetString,
                //"-pix_fmt rgb24",
                "-pix_fmt rgba",
                "-f rawvideo",
                "-",
            ]);

            RunFeedMachine(ffmpegProcess, _inputStream);
        }
        else if (_inputFile != null)
        {
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out _, out ffmpegOut,
            [
                //"-v debug",
                //"-flags low_delay",
                //"-analyzeduration 5000000000",
                $"{offsetString}",
                $"-i \"{_inputFile}\"",
                //"-pix_fmt rgb24",
                "-pix_fmt rgba",
                "-f rawvideo",
                "-",
            ]);
        }
        else if (_inputCENC != null)
        {
            FFmpegWrapper.Open2(_ffmpeg, out ffmpegProcess, out _, out ffmpegOut,
            [
                $"-decryption_key {_inputCENC.Key}",
                $"{offsetString}",
                $"-i \"{_inputCENC.FilePath}\"",
                //"-pix_fmt rgb24",
                "-pix_fmt rgba",
                "-f rawvideo",
                "-",
            ]);
        }
        else
        {
            throw new InvalidDataException();
        }

        try
        {
            // Ожидание первого кадра
            AlreadyFrame = new byte[FrameSize];
            int totalReadBytes = 0;
            while (totalReadBytes < FrameSize)
            {
                if (isDisposed)
                    return;

                //int readBytes = videoReader.Read(frame, totalReadBytes, size - totalReadBytes);
                int readBytes = await ffmpegOut.ReadAsync(AlreadyFrame, totalReadBytes, FrameSize - totalReadBytes, cancel);
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
        catch when( cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            return;
        }

        Output = ffmpegOut;
        openedForReading = ffmpegOut != null;
    }

    private void RunFeedMachine(Process process, Stream dataInput)
    {
        cancelation = new();
        try
        {
            Task.Run(() =>
            {
                if (isDisposed)
                    return;

                dataInput.Position = 0;
                ffmpegIn = process.StandardInput.BaseStream;
                byte[] buffer = new byte[8192];
                int bytesRead;

                while (true)
                {
                    if (isDisposed)
                        return;

                    if (!dataInput.CanRead)
                        break;

                    bytesRead = dataInput.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        if (isDisposed)
                            return;

                        ffmpegIn.Write(buffer, 0, bytesRead);
                    }
                    else
                    {
                        break;
                    }
                }
                ffmpegIn.Close();
            }, cancelation.Token);
        }
        catch when (cancelation.IsCancellationRequested) 
        {
            // Проглатываем
        }
        catch (System.IO.IOException)
        {
            // Проглатываем
        }
        catch (Exception)
        {
            // Не проглатываем
            throw;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        cancelation?.Cancel();
        ffmpegIn?.Dispose();
        ffmpegOut?.Dispose();

        if (ffmpegProcess != null)
        {
            ffmpegProcess.Close();
            ffmpegProcess.Dispose();
            ffmpegProcess = null;
        }
    }
}