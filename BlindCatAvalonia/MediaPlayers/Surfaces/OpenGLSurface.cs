using System;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FFMpegDll.Core;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public class OpenGLSurface : OpenGlControlBase, IVideoSurface
{
    public Matrix Matrix { get; set; }

    public void SetupSource(IReusableContext source)
    {
        throw new NotImplementedException();
    }

    public void OnFrameReady()
    {
        throw new NotImplementedException();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fbo)
    {
        throw new NotImplementedException();
    }
}