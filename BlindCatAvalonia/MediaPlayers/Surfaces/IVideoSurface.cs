using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public interface IVideoSurface
{
    Matrix Matrix { get; set; }
    void OnFrameReady();
    void SetupSource(IReusableContext source);
}