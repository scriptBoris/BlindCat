using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Diagnostics.CodeAnalysis;
using FFMpegProcessor.Models;

namespace FFMpegProcessor;

public class VideoReader2 : IDisposable
{
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private readonly Stream? _inputDataStream;
    private readonly string? _inputFileSource;
    private readonly FileCENC? _inputFileCenc;
    private readonly object _locker = new object();

    private Process? process;
    private Stream? ffinput;
    private Stream? ffoutput;
    private bool isEndFrame;
    private bool hasReadyFrame;
    private TaskCompletionSource hasNewFrameToken = new();
    private VideoFrame? frame;
    private bool isDisposed;

    /// <summary>
    /// Used for reading metadata and frames from video files.
    /// </summary>
    /// <param name="streamSource">Stream file</param>
    /// <param name="ffmpegExecutable">Name or path to the ffmpeg executable</param>
    /// <param name="ffprobeExecutable">Name or path to the ffprobe executable</param>
    public VideoReader2(Stream streamSource, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        _inputDataStream = streamSource;
        _inputFileSource = null;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    /// <summary>
    /// Used for reading metadata and frames from video files.
    /// </summary>
    /// <param name="filename">Video file path</param>
    /// <param name="ffmpegExecutable">Name or path to the ffmpeg executable</param>
    /// <param name="ffprobeExecutable">Name or path to the ffprobe executable</param>
    public VideoReader2(string fileSourcePath, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        if (!File.Exists(fileSourcePath)) throw new FileNotFoundException($"File '{fileSourcePath}' not found!");

        _inputDataStream = null;
        _inputFileSource = fileSourcePath;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    /// <summary>
    /// Used for reading metadata and frames from video files.
    /// </summary>
    /// <param name="filename">Video file path</param>
    /// <param name="ffmpegExecutable">Name or path to the ffmpeg executable</param>
    /// <param name="ffprobeExecutable">Name or path to the ffprobe executable</param>
    public VideoReader2(FileCENC fileCENC, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        if (!File.Exists(fileCENC.FilePath)) throw new FileNotFoundException($"File '{fileCENC.FilePath}' not found!");

        _inputFileCenc = fileCENC;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    #region props
    /// <summary>
    /// Current frame position within the loaded video file
    /// </summary>
    public long CurrentFrameOffset { get; set; }

    /// <summary>
    /// True if metadata loaded successfully
    /// </summary>
    [MemberNotNull(nameof(Metadata))]
    public bool LoadedMetadata { get; private set; }

    /// <summary>
    /// Video metadata
    /// </summary>
    public VideoMetadata? Metadata { get; private set; }

    /// <summary>
    /// Can fetch frames
    /// </summary>
    public bool OpenedForReading { get; private set; }
    #endregion props;

    /// <summary>
    /// Use already metadata info
    /// </summary>
    /// <param name="videoMetadata"></param>
    public void UseMetadata(VideoMetadata videoMetadata)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(VideoReader2));

        LoadedMetadata = true;
        Metadata = videoMetadata;
    }

    /// <summary>
    /// Load video metadata into memory.
    /// </summary>
    public void LoadMetadata(bool ignoreStreamErrors = false)
    {
        LoadMetadataAsync(ignoreStreamErrors).Wait();
    }

    /// <summary>
    /// Load video metadata into memory.
    /// </summary>
    public async Task<VideoMetadata?> LoadMetadataAsync(bool ignoreStreamErrors = false, CancellationToken cancellation = default)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(VideoReader2));

        if (LoadedMetadata)
            throw new InvalidOperationException("Video metadata is already loaded!");

        VideoMetadata? metadata = null;
        if (_inputFileSource != null)
        {
            metadata = await LoadMetadataAsync_InternalFile(ignoreStreamErrors, cancellation);
        }
        else if (_inputDataStream != null)
        {
            metadata = await LoadMetadataAsync_InternalStream(_inputDataStream, ignoreStreamErrors, cancellation);
        }
        else if (_inputFileCenc != null)
        {
            metadata = await LoadMetadataAsync_InternalCENC(_inputFileCenc, ignoreStreamErrors, cancellation);
        }
        else
        {
            throw new NotSupportedException();
        }

        if (metadata != null)
        {
            if (metadata.CheckEmpty())
                return null;

            LoadedMetadata = true;
            Metadata = metadata;
        }

        return metadata;
    }

    private async Task<VideoMetadata?> LoadMetadataAsync_InternalFile(bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (_, output) = FFmpegWrapper.Open(_ffprobe,
        [
            //"-v verbose",
            $"-i \"{_inputFileSource}\"",
            "-print_format json=c=1",
            "-show_format",
            "-show_streams",
        ]
        , out var analyzeProcess, false);

        return await MakeMeta(analyzeProcess, output, ignoreStreamErrors, cancel);
    }

    private async Task<VideoMetadata?> LoadMetadataAsync_InternalStream(Stream stream, bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (input, output) = FFmpegWrapper.Open(_ffprobe,
        [
            //"-v debug",
            //"-v verbose",
            "-i pipe:0",
            "-print_format json=c=1",
            "-show_format",
            "-show_streams",
        ], out var analyzeProcess, false);

        // input
        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
            {
                if (input.CanWrite)
                {
                    await input.WriteAsync(buffer, 0, bytesRead, cancel);
                }
                else
                {
                    break;
                }
            }
            analyzeProcess.StandardInput.Close();
        }
        catch when (cancel.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
        }
        finally
        {
            stream.Position = 0;
        }

        return await MakeMeta(analyzeProcess, output, ignoreStreamErrors, cancel);
    }

    private async Task<VideoMetadata?> LoadMetadataAsync_InternalCENC(FileCENC fileCenc, bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (_, output) = FFmpegWrapper.Open(_ffprobe,
        [
            //"-v debug",
            $"-i \"{fileCenc.FilePath}\"",
            $"-decryption_key {fileCenc.Key}",
            "-print_format json=c=1",
            "-show_format",
            "-show_streams",
            //"-report"
        ]
        , out var analyzeProcess, false);

        return await MakeMeta(analyzeProcess, output, ignoreStreamErrors, cancel);
    }

    private async Task<VideoMetadata?> MakeMeta(Process analyzeProcess, Stream output, bool ignoreStreamErrors, CancellationToken cancel)
    {
        try
        {
            var metadata = await JsonSerializer
                .DeserializeAsync(output, SourceGenerationContext.Default.VideoMetadata, cancellationToken: cancel);

            if (cancel.IsCancellationRequested)
                return null;

            if (metadata == null)
                return null;

            try
            {
                var videoStream = metadata.GetFirstVideoStream();
                if (videoStream != null)
                {
                    metadata.Width = videoStream.Width ?? -1;
                    metadata.Height = videoStream.Height ?? -1;
                    metadata.PixelFormat = videoStream.PixFmt;
                    metadata.Codec = videoStream.CodecName;
                    metadata.CodecLongName = videoStream.CodecLongName;

                    metadata.BitRate = videoStream.BitRate == null ? -1 :
                        int.Parse(videoStream.BitRate);

                    metadata.BitDepth = videoStream.BitsPerRawSample == null ?
                        TryParseBitDepth(videoStream.PixFmt) :
                        int.Parse(videoStream.BitsPerRawSample);

                    metadata.Duration = videoStream.Duration == null ?
                        double.Parse(metadata.Format.Duration ?? "0", CultureInfo.InvariantCulture) :
                        double.Parse(videoStream.Duration, CultureInfo.InvariantCulture);

                    metadata.SampleAspectRatio = videoStream.SampleAspectRatio;

                    metadata.AvgFramerateText = videoStream.AvgFrameRate;
                    metadata.AvgFramerate = videoStream.AvgFrameRateNumber;

                    if (metadata.AvgFramerate > 120/*videoStream.RFrameRateNumber*/)
                    {
                        metadata.AvgFramerate = videoStream.RFrameRateNumber;
                    }

                    metadata.PredictedFrameCount = (int)(metadata.AvgFramerate * metadata.Duration);
                    TryFixAutoRotateWidthHeight(metadata, videoStream);
                }
            }
            catch (Exception ex)
            {
                // failed to interpret video stream settings
                if (!ignoreStreamErrors) 
                    throw new InvalidDataException("Failed to parse video stream data! " + ex.Message);
            }
            return metadata;
        }
        catch when (cancel.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to interpret ffprobe video metadata output! " + ex.Message);
        }
        finally
        {
            output.Dispose();
            analyzeProcess.Dispose();
        }
    }

    /// <summary>
    /// Load the video and prepare it for reading frames.
    /// </summary>
    public void Load() => Load(0);

    /// <summary>
    /// Load the video for reading frames and seeks to given offset in seconds.
    /// </summary>
    /// <param name="offsetSeconds">Offset in seconds to which to seek to</param>
    public void Load(double offsetSeconds)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(VideoReader2));

        if (OpenedForReading)
            throw new InvalidOperationException("Video is already loaded!");

        if (!LoadedMetadata)
            throw new InvalidOperationException("Please load the video metadata first!");

        if (Metadata.Width == 0 || Metadata.Height == 0)
            throw new InvalidDataException("Loaded metadata contains errors!");

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

        // stream
        if (_inputDataStream != null)
        {
            ffoutput = LoadAsStream(_inputDataStream, offsetString);
        }
        // file
        else if (_inputFileSource != null)
        {
            ffoutput = LoadAsFile(_inputFileSource, offsetString);
        }
        // encrypted file (CENC)
        else if (_inputFileCenc != null)
        {
            ffoutput = LoadAsCENC(_inputFileCenc, offsetString);
        }
        else
        {
            throw new NotSupportedException();
        }

        //int w = Metadata.Width;
        //int h = Metadata.Height;
        //int size = w * h * 3;

        //frame = new(w, h);
        OpenedForReading = true;
        //var threadEngine = new Thread(() =>
        //{
        //    ReadEngine(ffstream, frame.RawData);
        //});
        //threadEngine.Start();
    }

    private Stream LoadAsStream(Stream _inputDataStream, string offsetString)
    {
        FFmpegWrapper.Open2(_ffmpeg, out process, out ffinput, out ffoutput,
        [
            "-flags low_delay",
            "-analyzeduration 5000000000",
            "-probesize 32",
            "-i pipe:0",
            offsetString,
            "-pix_fmt rgb24",
            "-f rawvideo",
            "-"
        ]);

        _inputDataStream.Position = 0;

        // todo может быть использовать Task?
        var insertThread = new Thread(() =>
        {
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                while (!isDisposed)
                {
                    if (!_inputDataStream.CanRead || hasReadyFrame)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    bytesRead = _inputDataStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    if (!ffinput.CanWrite)
                        break;

                    ffinput.Write(buffer, 0, bytesRead);
                }

                ffinput.Close();
            }
            catch (IOException)
            {
            }
            finally
            {
                _inputDataStream.Position = 0;
                ffinput.Close();
            }

        });
        insertThread.Start();

        return ffoutput;
    }

    private Stream LoadAsFile(string filePath, string offsetString)
    {
        FFmpegWrapper.Open2(_ffmpeg, out process, out _, out ffoutput,
        [
            //"-flags low_delay",
            //"-analyzeduration 5000000000",
            //"-probesize 32",
            $"{offsetString}",
            $"-i \"{filePath}\"",
            $"-pix_fmt rgb24",
            $"-f rawvideo",
            $"-"
        ]);

        return ffoutput;
    }

    private Stream LoadAsCENC(FileCENC file, string offsetString)
    {
        FFmpegWrapper.Open2(_ffmpeg, out process, out _, out ffoutput,
        [
            $"-decryption_key {file.Key}",
            $"{offsetString}",
            $"-i \"{file.FilePath}\"",
            $"-pix_fmt rgb24",
            $"-f rawvideo",
            $"-"
        ]);

        return ffoutput;
    }

    private void ReadEngine(Stream ffmpegstream, byte[] buffer)
    {
        while (!isDisposed)
        {
            lock (_locker)
            {
                if (hasReadyFrame)
                    continue;
            }

            int frameReadBytes = 0;
            while (frameReadBytes < buffer.Length)
            {
                int readBytes = ffmpegstream.Read(buffer, frameReadBytes, buffer.Length - frameReadBytes);
                if (readBytes <= 0)
                {
                    if (frameReadBytes == 0)
                    {
                        isEndFrame = true;
                        return;
                    }
                    else
                    {
                        break;
                    }
                }

                frameReadBytes += readBytes;
            }

            lock (_locker)
            {
                hasReadyFrame = true;
                hasNewFrameToken.TrySetResult();
            }
            ffmpegstream.Flush();
        }
    }

    /// <summary>
    /// Loads the next video frame into memory and returns it. This allocates a new frame.
    /// Returns 'null' when there is no next frame.
    /// </summary>
    public async Task<VideoFrame?> FetchFrame(CancellationToken cancel)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(VideoReader2));

        if (!OpenedForReading || ffoutput == null)
            throw new InvalidOperationException("Please load the video first");

        if (frame == null)
        {
            int w = Metadata!.Width;
            int h = Metadata!.Height;
            int size = w * h * 3;
            frame = new(w, h);
        }

        var buffer = frame.RawData;
        int frameReadBytes = 0;
        while (frameReadBytes < buffer.Length)
        {
            int readBytes = await ffoutput.ReadAsync(buffer, frameReadBytes, buffer.Length - frameReadBytes, cancel);
            if (readBytes <= 0)
            {
                if (frameReadBytes == 0)
                {
                    isEndFrame = true;
                    frame.IsLastFrame = true;
                    break;
                }
                else
                {
                    break;
                }
            }

            frameReadBytes += readBytes;
        }
        ffoutput.Dispose();
        return frame;

        //bool ready;
        //lock (_locker)
        //{
        //    ready = hasReadyFrame;
        //    if (!ready)
        //    {
        //        hasNewFrameToken = new();
        //    }
        //}

        //if (!ready)
        //{
        //    try
        //    {
        //        using var registration = cancel.Register(() => hasNewFrameToken.TrySetCanceled(cancel));
        //        await hasNewFrameToken.Task;
        //    }
        //    catch when (cancel.IsCancellationRequested)
        //    {
        //        return null;
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }
        //}

        //if (isEndFrame)
        //    frame!.IsLastFrame = true;

        //return frame;
    }

    #region support
    private static readonly Regex bitRateSimpleRgx = new Regex(@"\D(\d+?)[bl]e", RegexOptions.Compiled);
    private int TryParseBitDepth(string? pix_fmt)
    {
        if (pix_fmt == null)
            return -1;

        var match = bitRateSimpleRgx.Match(pix_fmt);
        if (match.Success) return int.Parse(match.Groups[1].Value);

        return -1;
    }

    private void TryFixAutoRotateWidthHeight(VideoMetadata metadata, MediaStream videoStream)
    {
        if (videoStream.SideDataList == null)
            return;

        var m = videoStream.SideDataList.FirstOrDefault(x => x.SideDataType == "Display Matrix");
        if (m == null)
            return;

        if (m.Rotation == 0 || m.Rotation == 270)
            return;

        int w = metadata.Width;
        int h = metadata.Height;
        switch (m.Rotation)
        {
            case 0:
            case 270:
                return;
            case -90:
            case 90:
                metadata.Width = h;
                metadata.Height = w;
                return;
            default:
                break;
        }
    }
    #endregion support

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        ffinput?.Dispose();
        ffoutput?.Dispose();
        process?.Dispose();
    }
}
