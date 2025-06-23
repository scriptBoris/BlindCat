using BlindCatCore.Models;
using System.Drawing;

namespace BlindCatCore.Core;

public interface IMediaBase
{
    event EventHandler<double>? ZoomChanged;
    event EventHandler<string?>? ErrorReceived;
    event EventHandler<FileMetaData[]?>? MetaReceived;

    /// <summary>
    /// Зум
    /// По умолчанию: 1.0 - 100%
    /// Минимальный: 0.2
    /// Максимальный: 5.0
    /// </summary>
    double Zoom { get; set; }

    /// <summary>
    /// 0 - Контент смещен влево, отображается его половина <br/>
    /// 0.5 - Контент отображен ровно по середине экрана <br/>
    /// 1.0 - Контент смещен вправо, отображается его половина
    /// </summary>
    double PositionXPercent { get; }

    /// <summary>
    /// 0 - Контент смещен вверх, отображается его половина <br/>
    /// 0.5 - Контент отображен ровно по середине экрана <br/>
    /// 1.0 - Контент смещен вниз, отображается его половина
    /// </summary>
    double PositionYPercent { get; }

    PointF PositionOffset { get; set; }

    void InvalidateSurface();
    void SetPercentPosition(double imagePosPercentX, double imagePosPercentY);
    Task SetSourceLocal(string filePath, CancellationToken cancel);
    Task SetSourceRemote(string url, CancellationToken cancel);
    Task SetSourceStorage(StorageFile file, CancellationToken cancel);
    
    /// <summary>
    /// Останавливает воспроизведение и освобождает затраченные ресурсы
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Получение метаданных
    /// </summary>
    FileMetaData[]? GetMeta();
}
