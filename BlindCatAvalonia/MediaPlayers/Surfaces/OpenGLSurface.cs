using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;

namespace BlindCatAvalonia.MediaPlayers.Surfaces;

public class OpenGLSurface : OpenGlControlBase, IVideoSurface
{
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_RGBA = 0x1908;
    private const int GL_UNSIGNED_BYTE = 0x1401;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_TRIANGLE_STRIP = 0x0005;
    private const int GL_TEXTURE_MIN_FILTER = 0x2801;
    private const int GL_LINEAR = 0x2601;
    private const int GL_CLAMP_TO_EDGE = 0x812F;
    private const int GL_TEXTURE_WRAP_S = 0x2802;
    private const int GL_TEXTURE_WRAP_T = 0x2803;
    private const int GL_TEXTURE_MAG_FILTER = 0x2800;
    private IReusableContext? _reuseContext; 

    public Matrix Matrix { get; set; }

    public void SetupSource(IReusableContext source)
    {
        _reuseContext?.Dispose();
        _reuseContext = source;
    }

    public void OnFrameReady()
    {
        Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
    }


    protected override void OnOpenGlRender(GlInterface gl, int fbo)
    {
        if (_reuseContext == null)
            return;

        var frameData = _reuseContext.GetFrame();
        if (frameData == null)
            return;

        // ������ �������� �������� � OpenGL
        //int texture = gl.GenTexture();
        //gl.BindTexture(GL_TEXTURE_2D, texture);
        //gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, frameData.Width, frameData.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, frameData.Pointer);
        //gl.BindTexture(GL_TEXTURE_2D, 0);

        //// ����������� ��������
        //gl.BindFramebuffer(GL_FRAMEBUFFER, fbo);
        //gl.DrawArrays(GL_TRIANGLE_STRIP, 0, 4);

        //_reuseContext.RecycleFrame(frameData);
        //gl.DeleteTexture(texture);


        int texture = gl.GenTexture();
        gl.BindTexture(GL_TEXTURE_2D, texture);

        // ��������� ��������
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

        // �������� ��������
        gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, frameData.Width, frameData.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, frameData.Pointer);

        // ���������
        gl.BindFramebuffer(GL_FRAMEBUFFER, fbo);
        gl.BindTexture(GL_TEXTURE_2D, texture);
        gl.DrawArrays(GL_TRIANGLE_STRIP, 0, 4);

        // �������
        gl.BindTexture(GL_TEXTURE_2D, 0);
        gl.DeleteTexture(texture);

        _reuseContext.RecycleFrame(frameData);
    }
}