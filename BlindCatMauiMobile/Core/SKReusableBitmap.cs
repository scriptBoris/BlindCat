using FFMpegDll.Core;
using SkiaSharp;

namespace BlindCatMauiMobile.Core;

public class SKReusableBitmap : SKBitmap, IReusableBitmap
{
    public SKReusableBitmap()
    {
    }
    
    public SKReusableBitmap(int width, int height, SKColorType colorType, SKAlphaType alphaType) 
        : base(width, height, colorType, alphaType)
    {
    }

    public unsafe void Populate(nint bitmapSrc)
    {
        void* src = (void*)bitmapSrc;
        void* dst = (void*)this.GetPixels();
        int len = Width * Height * 4;
        
        Buffer.MemoryCopy(src, dst, len, len);
    }
}