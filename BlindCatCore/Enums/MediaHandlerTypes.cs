namespace BlindCatCore.Enums;

[Obsolete("Лучше использовать media format")]
public enum MediaHandlerTypes
{
    /// <summary>
    /// Еще не определился с обработчиком, нужно узнать тип данных из bin
    /// </summary>
    Undefined,

    /// <summary>
    /// Тип не поддерживается
    /// </summary>
    Unsupported,

    Skia,
    MauiImage,

    [Obsolete("Use SkiaAndFFMpegCustom")]
    CommunityToolkit,
    SkiaAndFFMpegCustom,
}
