using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using FFMpegProcessor.Models;

namespace FFMpegProcessor;

public class AudioReader : IDisposable
{
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private readonly string? _filePath;
    private readonly FileCENC? _encryptedFile;
    private readonly Stream? _inputDataStream;
    private int loadedBitDepth = 16;
    private Process? ffmpegprocess;

    /// <summary>
    /// Used for reading metadata and frames from audio files.
    /// </summary>
    /// <param name="filePath">Audio file path</param>
    /// <param name="ffmpegExecutable">Name or path to the ffmpeg executable</param>
    /// <param name="ffprobeExecutable">Name or path to the ffprobe executable</param>
    public AudioReader(string filePath, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File '{filePath}' not found!");

        _filePath = filePath;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    public AudioReader(FileCENC f, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        _encryptedFile = f;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    /// <summary>
    /// Used for reading metadata and frames from audio files.
    /// </summary>
    /// <param name="inputStream">Audio input stream</param>
    /// <param name="ffmpegExecutable">Name or path to the ffmpeg executable</param>
    /// <param name="ffprobeExecutable">Name or path to the ffprobe executable</param>
    public AudioReader(Stream inputStream, string ffmpegExecutable = "ffmpeg", string ffprobeExecutable = "ffprobe")
    {
        _inputDataStream = inputStream;
        _ffmpeg = ffmpegExecutable;
        _ffprobe = ffprobeExecutable;
    }

    public bool OpenedForReading { get; private set; }

    /// <summary>
    /// Current sample position within the loaded audio file
    /// </summary>
    public long CurrentSampleOffset { get; private set; }

    /// <summary>
    /// True if metadata loaded successfully
    /// </summary>
    public bool MetadataLoaded { get; private set; }

    /// <summary>
    /// Audio metadata
    /// </summary>
    public AudioMetadata? Metadata { get; private set; }

    /// <summary>
    /// Output data stream
    /// </summary>
    public Stream? DataStream { get; private set; }

    /// <summary>
    /// Load audio metadata into memory.
    /// </summary>
    public void LoadMetadata(bool ignoreStreamErrors = false) => LoadMetadataAsync(ignoreStreamErrors).Wait();

    /// <summary>
    /// Load audio metadata into memory.
    /// </summary>
    public async Task<AudioMetadata?> LoadMetadataAsync(bool ignoreStreamErrors = false, CancellationToken cancellation = default)
    {
        if (MetadataLoaded)
            throw new InvalidOperationException("Video metadata is already loaded!");

        AudioMetadata? audioMetadata = null;
        if (_filePath != null)
        {
            audioMetadata = await LoadMetadataAsync_file(_filePath, ignoreStreamErrors, cancellation);
        }
        else if (_inputDataStream != null)
        {
            audioMetadata = await LoadMetadataAsync_stream(_inputDataStream, ignoreStreamErrors, cancellation);
        }
        else if (_encryptedFile != null)
        {
            audioMetadata = await LoadMetadataAsync_fileCenc(_encryptedFile, ignoreStreamErrors, cancellation);
        }
        else
        {
            throw new InvalidOperationException();
        }

        if (audioMetadata != null)
        {
            MetadataLoaded = true;
            Metadata = audioMetadata;
        }

        return audioMetadata;
    }

    private async Task<AudioMetadata?> LoadMetadataAsync_file(string file, bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (_, output) = FFmpegWrapper.Open(_ffprobe,
        [
            $"-v quiet",
            $"-i \"{file}\"",
            $"-print_format json=c=1",
            $"-show_format",
            $"-show_streams",
        ], out var proc, false);
        return await MakeMeta(output, proc, ignoreStreamErrors, cancel);
    }

    private async Task<AudioMetadata?> LoadMetadataAsync_stream(Stream inputDataStream, bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (input, output) = FFmpegWrapper.Open(_ffprobe,
        [
            //"-v debug",
            "-v quiet",
            "-i pipe:0",
            "-print_format json=c=1",
            "-show_format",
            "-show_streams",
        ], out var proc, false);

        // input
        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = inputDataStream.Read(buffer, 0, buffer.Length)) > 0)
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
            proc.StandardInput.Close();
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
            inputDataStream.Position = 0;
        }

        return await MakeMeta(output, proc, ignoreStreamErrors, cancel);
    }

    private async Task<AudioMetadata?> LoadMetadataAsync_fileCenc(FileCENC file, bool ignoreStreamErrors, CancellationToken cancel)
    {
        var (_, output) = FFmpegWrapper.Open(_ffprobe,
        [
            "-v quiet",
            $"-i \"{file.FilePath}\"",
            $"-decryption_key {file.Key}",
            "-print_format json=c=1",
            "-show_format",
            "-show_streams",
        ], out var proc);

        return await MakeMeta(output, proc, ignoreStreamErrors, cancel);
    }

    private async Task<AudioMetadata?> MakeMeta(Stream r, Process proc, bool ignoreStreamErrors, CancellationToken cancel)
    {
        try
        {
            var metadata = await JsonSerializer.DeserializeAsync(r, SourceGenerationContext.Default.AudioMetadata, cancel);
            if (metadata == null)
                return null;

            try
            {
                var audioStream = metadata.GetFirstAudioStream();
                if (audioStream != null)
                {
                    metadata.Channels = audioStream.Channels ?? -1;
                    metadata.Codec = audioStream.CodecName;
                    metadata.CodecLongName = audioStream.CodecLongName;
                    metadata.SampleFormat = audioStream.SampleFmt;

                    metadata.SampleRate = audioStream.SampleRateNumber;

                    metadata.Duration = audioStream.Duration == null ?
                        double.Parse(metadata.Format.Duration ?? "-1", CultureInfo.InvariantCulture) :
                        double.Parse(audioStream.Duration, CultureInfo.InvariantCulture);

                    metadata.BitRate = audioStream.BitRate == null ? -1 :
                        int.Parse(audioStream.BitRate);

                    metadata.BitDepth = audioStream.BitsPerSample ?? -1;
                    metadata.PredictedSampleCount = (int)Math.Round(metadata.Duration * metadata.SampleRate);

                    if (metadata.BitDepth == 0)
                    {
                        // try to parse it from format
                        if (metadata.SampleFormat.Contains("64")) metadata.BitDepth = 64;
                        else if (metadata.SampleFormat.Contains("32")) metadata.BitDepth = 32;
                        else if (metadata.SampleFormat.Contains("24")) metadata.BitDepth = 24;
                        else if (metadata.SampleFormat.Contains("16")) metadata.BitDepth = 16;
                        else if (metadata.SampleFormat.Contains("8")) metadata.BitDepth = 8;
                    }
                }
            }
            catch (Exception ex)
            {
                // failed to interpret video stream settings
                if (!ignoreStreamErrors) throw new InvalidDataException("Failed to parse audio stream data! " + ex.Message);
            }

            return metadata;
        }
        catch when (cancel.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to interpret ffprobe audio metadata output! " + ex.Message);
        }
        finally
        {
            proc.Dispose();
        }
    }

    public void UseMetadata(AudioMetadata meta)
    {
        MetadataLoaded = true;
        Metadata = meta;
    }

    /// <summary>
    /// Load the audio and prepare it for reading frames.
    /// </summary>
    /// <param name="bitDepth">frame bit rate in which the audio will be processed (16, 24, 32)</param>
    /// <param name="cancel"></param>
    /// <param name="offset"></param>
    public async Task Load(TimeSpan offset = default, int bitDepth = 16, CancellationToken cancel = default)
    {
        if (bitDepth != 16 && bitDepth != 24 && bitDepth != 32)
            throw new InvalidOperationException("Acceptable bit depths are 16, 24 and 32");

        if (OpenedForReading)
            throw new InvalidOperationException("Audio is already loaded!");

        if (!MetadataLoaded)
            throw new InvalidOperationException("Please load the audio metadata first!");

        string offsetString = "";
        if (offset.TotalMilliseconds > 0)
        {
            string val = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)offset.TotalHours,
                offset.Minutes,
                offset.Seconds);
            offsetString = "-ss " + val;
        }

        if (_inputDataStream != null)
        {
            var (input, output) = FFmpegWrapper.Open(_ffmpeg,
            [
                //"-v debug",
                "-i pipe:0",
                offsetString,
                $"-f s{bitDepth}le",
                "-c:a pcm_s16le",
                "-ar 44100",
                "-ac 2",
                "-",
            ]
            , out ffmpegprocess, true);

            ffmpegprocess.StartInfo.RedirectStandardError = true;
            ffmpegprocess.ErrorDataReceived += (o, e) =>
            {
                Debug.WriteLine(e.Data);
            };

            // input
            try
            {
                byte[] buffer = new byte[4096];
                long totalInputs = 0;
                int bytesRead;
                while ((bytesRead = await _inputDataStream.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
                {
                    totalInputs += bytesRead;
                    if (_inputDataStream.CanWrite)
                    {
                        await _inputDataStream.WriteAsync(buffer, 0, bytesRead, cancel);
                    }
                    else
                    {
                        break;
                    }
                }

                input.Close();
            }
            catch when (cancel.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
            }
            finally
            {
                _inputDataStream.Position = 0;
                DataStream = output;
            }
        }
        else if (_filePath != null)
        {
            var (_, output) = FFmpegWrapper.Open(_ffmpeg,
            [
                //"-v debug",
                $"-i \"{_filePath}\"",
                $"{offsetString}",
                $"-f s{bitDepth}le",
                "-ar 44100",
                "-ac 2",
                "-"
            ],
            out ffmpegprocess);
            DataStream = output;
        }
        else
        {
            throw new NotSupportedException();
        }

        loadedBitDepth = bitDepth;
        OpenedForReading = true;
    }

    ///// <summary>
    ///// Loads the next audio frame into memory and returns it. This allocates a new frame.
    ///// Returns 'null' when there is no next frame.
    ///// </summary>
    ///// <returns></returns>
    //public override AudioFrame NextFrame() => NextFrame(1024);

    ///// <summary>
    ///// Loads the next audio frame into memory and returns it. This allocates a new frame.
    ///// Returns 'null' when there is no next frame.
    ///// </summary>
    ///// <param name="samples">Number of samples to read in a frame</param>
    //public AudioFrame NextFrame(int samples)
    //{
    //    var frame = new AudioFrame(Metadata.Channels, samples, loadedBitDepth);
    //    return NextFrame(frame);
    //}

    ///// <summary>
    ///// Loads the next audio frame into memory and returns it. This allocates a new frame.
    ///// Returns 'null' when there is no next frame.
    ///// </summary>
    ///// <param name="frame">Existing frame to be overwritten with new frame data.</param>
    //public override AudioFrame NextFrame(AudioFrame frame)
    //{
    //    if (!OpenedForReading) throw new InvalidOperationException("Please load the audio first!");

    //    var success = frame.Load(DataStream);
    //    if (success) CurrentSampleOffset += frame.LoadedSamples;
    //    return success ? frame : null;
    //}

    /// <summary>
    /// Diposes the DataStream
    /// </summary>
    public void Dispose()
    {
        DataStream?.Dispose();
        ffmpegprocess?.Dispose();
        GC.SuppressFinalize(this);
    }
}