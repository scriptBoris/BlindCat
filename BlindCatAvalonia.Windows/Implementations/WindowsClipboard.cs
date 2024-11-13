using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using BlindCatCore.Services;
using Clowd.Clipboard;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Windows.Implementations;

[SupportedOSPlatform("windows")]
internal class WindowsClipboard : IClipboard
{
    public async Task SetImage(object imgBitmap)
    {
        using var image = SKImage.FromBitmap((SKBitmap)imgBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Seek(0, SeekOrigin.Begin);
        var img = new System.Drawing.Bitmap(stream);
        await ClipboardGdi.SetImageAsync(img);
    }

    public void SetText(string text)
    {
        ClipboardGdi.SetText(text);
    }
}
