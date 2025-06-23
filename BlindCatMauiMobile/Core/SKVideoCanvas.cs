using FFMpegDll.Core;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace BlindCatMauiMobile.Core;

public class SKVideoCanvas : SKCanvasView
{
    private IReusableContext? _context;
    
    public void SetupContext(IReusableContext? context)
    {
        _context = context;
    }
    
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (_context == null)
            return;

        var ibmp = _context.GetFrame();
        if (ibmp == null)
            return;
        
        var bmp = (SKBitmap)ibmp;
        var vpSize = e.Info.Rect;
        var imgSize = new SKRect(0, 0, bmp.Width, bmp.Height);
        var destRect = GetAspectFitRect(vpSize, imgSize);
        canvas.DrawBitmap(bmp, destRect);
        _context.RecycleFrame(ibmp);
    }
    
    private SKRect GetAspectFitRect(SKRect viewPortSize, SKRect imgSize)
    {
        float scale = Math.Min(viewPortSize.Width / imgSize.Width, viewPortSize.Height / imgSize.Height);
        float newWidth = imgSize.Width * scale;
        float newHeight = imgSize.Height * scale;
        float x = (viewPortSize.Width - newWidth) / 2;
        float y = (viewPortSize.Height - newHeight) / 2;

        return new SKRect(x, y, x + newWidth, y + newHeight);
    }
}