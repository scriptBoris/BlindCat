using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegDll;
//
// public sealed unsafe class VideoFrameConverter : IDisposable
// {
//     private readonly void* _convertedFrameBufferPtr;
//     private readonly Size _destinationSize;
//     private readonly byte_ptr4 _dstData;
//     private readonly int4 _dstLinesize;
//     private readonly SwsContext* _pConvertContext;
//
//     public VideoFrameConverter(Size sourceSize, AVPixelFormat sourcePixelFormat,
//         Size destinationSize, AVPixelFormat destinationPixelFormat)
//     {
//         _destinationSize = destinationSize;
//
//         _pConvertContext = ffmpeg.sws_getContext(sourceSize.Width,
//             sourceSize.Height,
//             sourcePixelFormat,
//             destinationSize.Width,
//             destinationSize.Height,
//             destinationPixelFormat,
//             ffmpeg.SWS_FAST_BILINEAR,
//             null,
//             null,
//             null);
//         if (_pConvertContext == null)
//             throw new ApplicationException("Could not initialize the conversion context.");
//
//         int convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(
//             destinationPixelFormat,
//             destinationSize.Width,
//             destinationSize.Height,
//             1);
//         
//         _convertedFrameBufferPtr = ffmpeg.av_malloc((ulong)convertedFrameBufferSize);
//         _dstData = new byte_ptr4();
//         _dstLinesize = new int4();
//
//         ffmpeg.av_image_fill_arrays(
//             ref _dstData,
//             ref _dstLinesize,
//             (byte*)_convertedFrameBufferPtr,
//             destinationPixelFormat,
//             destinationSize.Width,
//             destinationSize.Height,
//             1);
//     }
//
//     public void Dispose()
//     {
//         ffmpeg.av_free(_convertedFrameBufferPtr);
//         ffmpeg.sws_freeContext(_pConvertContext);
//     }
//
//     public AVFrame Convert(AVFrame sourceFrame)
//     {
//         var sw = Stopwatch.StartNew();
//         ffmpeg.sws_scale(_pConvertContext,
//             sourceFrame.data,
//             sourceFrame.linesize,
//             0,
//             sourceFrame.height,
//             _dstData,
//             _dstLinesize);
//
//         var data = new byte_ptr8();
//         data.UpdateFrom(_dstData);
//         var linesize = new int8();
//         linesize.UpdateFrom(_dstLinesize);
//         sw.Stop();
//
//         if (sw.ElapsedMilliseconds > 1)
//             Debug.WriteLine($"Convert pixels: {sw.ElapsedMilliseconds}ms");
//
//         return new AVFrame
//         {
//             data = data,
//             linesize = linesize,
//             width = _destinationSize.Width,
//             height = _destinationSize.Height
//         };
//     }
//     
//     public nint Convert(AVFrame* sourceFrame)
//     {
//         ffmpeg.sws_scale(_pConvertContext,
//             sourceFrame->data,
//             sourceFrame->linesize,
//             0,
//             sourceFrame->height,
//             _dstData,
//             _dstLinesize);
//         
//         var data = new byte_ptr8();
//         data.UpdateFrom(_dstData);
//         var linesize = new int8();
//         linesize.UpdateFrom(_dstLinesize);
//
//         return (nint)_convertedFrameBufferPtr;
//     }
// }


public unsafe class FrameConverter : IDisposable
{
    private readonly SwsContext* _swsContext;
    private readonly int _width;
    private readonly int _height;
    private AVFrame* _destinationFrame;

    public FrameConverter(
        int width,
        int height,
        AVPixelFormat sourcePixelFormat,
        AVPixelFormat destinationPixelFormat)
    {
        _width = width;
        _height = height;

        _swsContext = ffmpeg.sws_getContext(
            width,
            height,
            sourcePixelFormat,
            width,
            height,
            destinationPixelFormat,
            ffmpeg.SWS_FAST_BILINEAR,
            null,
            null,
            null);

        if (_swsContext == null)
            throw new ApplicationException("Could not initialize the conversion context.");

        // Создаем и инициализируем выходной фрейм один раз
        _destinationFrame = ffmpeg.av_frame_alloc();
        _destinationFrame->width = width;
        _destinationFrame->height = height;
        _destinationFrame->format = (int)destinationPixelFormat;

        if (ffmpeg.av_frame_get_buffer(_destinationFrame, 1) < 0)
            throw new ApplicationException("Could not allocate frame buffer.");
    }

    public nint ConvertFrame(AVFrame sourceFrame, out int bufferSize)
    {
        ffmpeg.sws_scale(_swsContext,
            sourceFrame.data,
            sourceFrame.linesize,
            0,
            _height,
            _destinationFrame->data,
            _destinationFrame->linesize);

        bufferSize = _destinationFrame->linesize[0] * _height;
        return (nint)_destinationFrame->data[0];
    }

    public void Dispose()
    {
        fixed (AVFrame** ptr = &_destinationFrame)
        {
            ffmpeg.av_frame_free(ptr);
        }
        ffmpeg.sws_freeContext(_swsContext);
    }
}