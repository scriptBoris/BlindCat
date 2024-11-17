using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public interface IVideoSurface
{
    Matrix Matrix { get; set; }
    void OnFrameReady();
    void SetupSource(IReusableContext source);
}