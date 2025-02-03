using System.Runtime.InteropServices;
using Android.Graphics;
using FFmpeg.AutoGen.Abstractions;
using FFMpegDll;
using FFMpegDll.Models;
using Java.Nio;
using Microsoft.Maui.Graphics.Platform;

namespace BlindCatMauiMobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        var pickRes = await FilePicker.PickAsync();
        if (pickRes == null)
        {
            return;
        }

        ProcessorTypes processorType;
        var abi = Android.OS.Build.SupportedAbis?.FirstOrDefault();
        switch (abi)
        {
            case "x86_64":
                processorType = ProcessorTypes.x86_64;
                break;
            case "armeabi-v7a":
                processorType = ProcessorTypes.ARM32;
                break;
            case "arm64-v8a":
                processorType = ProcessorTypes.ARM64;
                break;
            default:
                throw new NotSupportedException();
        }

        FFMpegDll.Init.InitializeFFMpeg(processorType);
        
        string path = pickRes.FullPath;
        var hwacc = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
        var pix = AVPixelFormat.AV_PIX_FMT_ARGB;
        using var decoder = new VideoFileDecoder(path, hwacc, pix);
        
        decoder.SeekTo(TimeSpan.FromSeconds(10));
        var dec = decoder.TryDecodeNextFrame();
        if (dec.IsSuccessed)
        {
            int w = decoder.FrameSize.Width;
            int h = decoder.FrameSize.Height;
            var r = CreateBitmapFromPointer(dec, w, h);
            canvas.Drawable = new PlatformImage(r);
        }
    }
    
    public static unsafe Bitmap CreateBitmapFromPointer(FrameDecodeResult frameDecode, int width, int height)
    {
        var config = Bitmap.Config.Argb8888!;
        var bitmap = Bitmap.CreateBitmap(width, height, config);

        // Определяем размер буфера в байтах
        int bytesPerPixel = config == Bitmap.Config.Argb8888 ? 4 :
            config == Bitmap.Config.Rgb565 ? 2 :
            throw new NotSupportedException("Формат пикселей не поддерживается");

        int bufferSize = width * height * bytesPerPixel;

        var array = new byte[bufferSize];
        nint src = (nint)frameDecode.FrameBitmapRGBA8888;
        Marshal.Copy(src, array, 0, bufferSize);
                
        var buffer = ByteBuffer.Wrap(array);

        bitmap.CopyPixelsFromBuffer(buffer);

        return bitmap;
    }

}