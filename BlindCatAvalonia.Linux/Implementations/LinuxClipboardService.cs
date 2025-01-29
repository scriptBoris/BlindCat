using System;
using System.Threading.Tasks;
using BlindCatCore.Services;

namespace BlindCatAvalonia.Linux.Implementations;

public class LinuxClipboardService : IClipboard
{
    public Task SetImage(object imageBitmap)
    {
        throw new NotImplementedException();
    }

    public void SetText(string text)
    {
        TextCopy.ClipboardService.SetText(text);
    }
}