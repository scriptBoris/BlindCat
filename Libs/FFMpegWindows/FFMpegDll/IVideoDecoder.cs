using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegDll;

public interface IVideoDecoder : IDisposable
{
    void SeekTo(TimeSpan position);
    bool TryDecodeNextFrame(out AVFrame ff_frame, out bool endOfVideo);
}