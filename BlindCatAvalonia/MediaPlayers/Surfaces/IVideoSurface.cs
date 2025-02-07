using Avalonia;
using FFMpegDll.Core;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public interface IVideoSurface
{
    Matrix Matrix { get; set; }
    void OnFrameReady();
    void SetupSource(IReusableContext source);
}