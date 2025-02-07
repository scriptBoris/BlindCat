using System.Drawing;

namespace FFMpegDll.Core;

public interface IReusableContext : IDisposable
{
    /// <summary>
    /// Количество фреймов готовых к отрисовке
    /// </summary>
    int QueuedFrames { get; }
    
    /// <summary>
    /// Размер фрейма
    /// </summary>
    Size FrameSize { get; }

    /// <summary>
    /// Получает готовый фрейм для отрисовке на Surface 
    /// </summary>
    IReusableBitmap? GetFrame();

    /// <summary>
    /// Запускает новый фрейм в конвейр
    /// </summary>
    void PushFrame(nint bitmapArray);
    
    /// <summary>
    /// Перезапускает фрейм для использования вновь  
    /// </summary>
    void RecycleFrame(IReusableBitmap data);
}